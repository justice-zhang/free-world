using System;
using System.Collections.Generic;
using System.Globalization;
using Game.Application;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Presentation
{
    internal sealed class PooledVfx
    {
        public GameObject Object;
        public SpriteRenderer Renderer;
        public float Remaining;
        public PresentationPriority Priority;
        public ProceduralShape Shape;
    }

    /// <summary>Single-owner VFX request pool with no per-effect Update methods.</summary>
    public sealed class VfxRequestPool : IDisposable
    {
        private readonly List<PooledVfx> all = new List<PooledVfx>(16);
        private readonly Stack<PooledVfx> available = new Stack<PooledVfx>(16);
        private readonly List<PooledVfx> active = new List<PooledVfx>(16);
        private readonly Transform root;
        private readonly Sprite sprite;
        private readonly ProceduralVisualLibrary library;
        private readonly int maximumCapacity;
        private readonly long[] droppedByPriority = new long[4];

        /// <summary>Creates the bounded production default while preserving the original constructor.</summary>
        public VfxRequestPool(Transform owner, Sprite fallbackSprite)
            : this(owner, fallbackSprite, 200)
        {
        }

        /// <summary>Creates a pool with an explicit simultaneous-effect capacity.</summary>
        public VfxRequestPool(Transform owner, Sprite fallbackSprite, int maximumCapacity)
        {
            root = owner ?? throw new ArgumentNullException(nameof(owner));
            sprite = fallbackSprite ?? throw new ArgumentNullException(nameof(fallbackSprite));
            if (maximumCapacity <= 0) throw new ArgumentOutOfRangeException(nameof(maximumCapacity));
            this.maximumCapacity = maximumCapacity;
        }

        internal VfxRequestPool(
            Transform owner,
            ProceduralVisualLibrary proceduralLibrary,
            int maximumCapacity,
            int prewarm)
        {
            root = owner ?? throw new ArgumentNullException(nameof(owner));
            library = proceduralLibrary ?? throw new ArgumentNullException(nameof(proceduralLibrary));
            sprite = library.Sprite;
            if (maximumCapacity <= 0) throw new ArgumentOutOfRangeException(nameof(maximumCapacity));
            if (prewarm < 0 || prewarm > maximumCapacity) throw new ArgumentOutOfRangeException(nameof(prewarm));
            this.maximumCapacity = maximumCapacity;
            for (var index = 0; index < prewarm; index++) available.Push(Create());
        }

        /// <summary>Gets the number of effects currently leased from the pool.</summary>
        public int ActiveCount => active.Count;
        /// <summary>Gets the number of effect objects created by this pool.</summary>
        public int CreatedCount => all.Count;
        /// <summary>Gets the highest simultaneous active count observed.</summary>
        public int PeakActiveCount { get; private set; }
        /// <summary>Gets the number of acquisitions served by an available object.</summary>
        public long HitCount { get; private set; }
        /// <summary>Gets the number of objects created to expand the pool.</summary>
        public long ExpansionCount { get; private set; }
        /// <summary>Gets the number of acquisitions rejected at capacity.</summary>
        public long FailedAcquireCount { get; private set; }
        /// <summary>Gets the number of VFX requests dropped at capacity.</summary>
        public long DroppedRequestCount { get; private set; }
        public long EvictedLowerPriorityCount { get; private set; }
        public long MergedCriticalCount { get; private set; }

        public long GetDroppedCount(PresentationPriority priority) => droppedByPriority[(int)priority];

        /// <summary>Spawns an effect using the original fire-and-forget contract.</summary>
        public void Spawn(Vector2 position, Color color, float size, float duration)
        {
            TrySpawn(position, color, size, duration);
        }

        /// <summary>Tries to spawn an effect and returns false when the bounded pool is full.</summary>
        public bool TrySpawn(Vector2 position, Color color, float size, float duration)
        {
            var style = new ProceduralPresentationStyle(
                ProceduralShape.Circle,
                color,
                Color.clear,
                Vector2.one,
                PresentationPriority.Combat,
                PresentationAudioCue.None,
                false);
            return TrySpawn(new ProceduralVfxRequest(position, style, size, duration));
        }

        public bool TrySpawn(in ProceduralVfxRequest request)
        {
            PooledVfx effect = null;
            if (available.Count > 0)
            {
                effect = available.Pop();
                HitCount++;
            }
            else if (all.Count < maximumCapacity)
            {
                effect = Create();
                ExpansionCount++;
            }
            else
            {
                var candidate = FindEvictionCandidate(request.Style.Priority);
                if (candidate >= 0)
                {
                    effect = active[candidate];
                    active.RemoveAt(candidate);
                    EvictedLowerPriorityCount++;
                }
                else if (request.Style.Priority == PresentationPriority.CriticalDanger && active.Count > 0)
                {
                    var merge = FindCriticalMerge(request.Style.Shape);
                    merge.Remaining = Mathf.Max(merge.Remaining, request.Duration);
                    merge.Object.transform.localScale = Vector3.Max(
                        merge.Object.transform.localScale,
                        Vector3.one * request.Size);
                    MergedCriticalCount++;
                    return true;
                }
                else
                {
                    FailedAcquireCount++;
                    DroppedRequestCount++;
                    droppedByPriority[(int)request.Style.Priority]++;
                    return false;
                }
            }

            effect.Object.transform.SetPositionAndRotation(
                new Vector3(request.Position.x, request.Position.y, -0.1f),
                Quaternion.Euler(0f, 0f, request.RotationDegrees));
            effect.Object.transform.localScale = Vector3.one * request.Size;
            effect.Renderer.sprite = library == null ? sprite : library.GetSprite(request.Style.Shape);
            effect.Renderer.color = request.Style.Color;
            effect.Remaining = request.Duration;
            effect.Priority = request.Style.Priority;
            effect.Shape = request.Style.Shape;
            effect.Object.SetActive(true);
            active.Add(effect);
            if (active.Count > PeakActiveCount) PeakActiveCount = active.Count;
            return true;
        }

        public void Tick(float unscaledDeltaTime)
        {
            for (var index = active.Count - 1; index >= 0; index--)
            {
                var effect = active[index];
                effect.Remaining -= unscaledDeltaTime;
                if (effect.Remaining > 0f) continue;
                effect.Object.SetActive(false);
                active.RemoveAt(index);
                available.Push(effect);
            }
        }

        public void Dispose()
        {
            for (var index = all.Count - 1; index >= 0; index--)
                UnityObjectLifetime.Destroy(all[index].Object);
            all.Clear();
            active.Clear();
            available.Clear();
        }

        private PooledVfx Create()
        {
            var value = new PooledVfx();
            value.Object = new GameObject("M7_PooledVfx");
            value.Object.transform.SetParent(root, false);
            value.Renderer = value.Object.AddComponent<SpriteRenderer>();
            value.Renderer.sprite = sprite;
            value.Object.SetActive(false);
            all.Add(value);
            return value;
        }

        private int FindEvictionCandidate(PresentationPriority incoming)
        {
            var candidate = -1;
            var lowest = incoming;
            for (var index = 0; index < active.Count; index++)
            {
                if (active[index].Priority <= incoming || active[index].Priority <= lowest) continue;
                lowest = active[index].Priority;
                candidate = index;
            }
            return candidate;
        }

        private PooledVfx FindCriticalMerge(ProceduralShape shape)
        {
            for (var index = 0; index < active.Count; index++)
                if (active[index].Priority == PresentationPriority.CriticalDanger && active[index].Shape == shape)
                    return active[index];
            return active[0];
        }
    }

    internal sealed class DamageNumberEntry
    {
        public Text Text;
        public float Remaining;
    }

    /// <summary>Damage-number pool sharing the existing UI Canvas.</summary>
    public sealed class DamageNumberPool : IDisposable
    {
        private readonly List<DamageNumberEntry> all = new List<DamageNumberEntry>(16);
        private readonly Stack<DamageNumberEntry> available = new Stack<DamageNumberEntry>(16);
        private readonly List<DamageNumberEntry> active = new List<DamageNumberEntry>(16);
        private readonly RectTransform root;
        private readonly Font font;
        private readonly int maximumCapacity;

        public DamageNumberPool(Canvas sharedCanvas)
            : this(sharedCanvas, 96, 16)
        {
        }

        public DamageNumberPool(Canvas sharedCanvas, int maximumCapacity, int prewarm)
        {
            if (sharedCanvas == null) throw new ArgumentNullException(nameof(sharedCanvas));
            if (maximumCapacity <= 0) throw new ArgumentOutOfRangeException(nameof(maximumCapacity));
            if (prewarm < 0 || prewarm > maximumCapacity) throw new ArgumentOutOfRangeException(nameof(prewarm));
            this.maximumCapacity = maximumCapacity;
            var rootObject = new GameObject("M7_DamageNumbers", typeof(RectTransform));
            rootObject.transform.SetParent(sharedCanvas.transform, false);
            root = (RectTransform)rootObject.transform;
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            for (var index = 0; index < prewarm; index++) available.Push(Create());
        }

        public int ActiveCount => active.Count;
        public int CreatedCount => all.Count;
        public long AggregatedCount { get; private set; }

        public void Spawn(Vector2 worldPosition, float value, bool critical)
        {
            DamageNumberEntry entry;
            if (available.Count > 0) entry = available.Pop();
            else if (all.Count < maximumCapacity) entry = Create();
            else
            {
                AggregatedCount++;
                if (active.Count > 0 && critical)
                {
                    entry = active[active.Count - 1];
                    entry.Text.color = Color.yellow;
                    entry.Remaining = Mathf.Max(entry.Remaining, 0.65f);
                }
                return;
            }
            entry.Text.rectTransform.anchoredPosition = worldPosition * 18f;
            entry.Text.text = value.ToString("0", CultureInfo.InvariantCulture);
            entry.Text.color = critical ? Color.yellow : Color.white;
            entry.Remaining = 0.65f;
            entry.Text.gameObject.SetActive(true);
            active.Add(entry);
        }

        public void Tick(float unscaledDeltaTime)
        {
            for (var index = active.Count - 1; index >= 0; index--)
            {
                var entry = active[index];
                entry.Remaining -= unscaledDeltaTime;
                entry.Text.rectTransform.anchoredPosition += Vector2.up * (24f * unscaledDeltaTime);
                if (entry.Remaining > 0f) continue;
                entry.Text.gameObject.SetActive(false);
                active.RemoveAt(index);
                available.Push(entry);
            }
        }

        public void Dispose()
        {
            UnityObjectLifetime.Destroy(root.gameObject);
            all.Clear();
            active.Clear();
            available.Clear();
        }

        private DamageNumberEntry Create()
        {
            var objectValue = new GameObject("M7_DamageNumber", typeof(RectTransform), typeof(Text));
            objectValue.transform.SetParent(root, false);
            var text = objectValue.GetComponent<Text>();
            text.font = font;
            text.fontSize = 18;
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
            text.rectTransform.sizeDelta = new Vector2(120f, 32f);
            objectValue.SetActive(false);
            var entry = new DamageNumberEntry { Text = text };
            all.Add(entry);
            return entry;
        }
    }

    internal sealed class RoutedAudioSource
    {
        public AudioSource Source;
        public float Remaining;
        public float BaseVolume;
        public PresentationPriority Priority;
        public PresentationAudioCue Cue;
    }

    /// <summary>Presentation-only mix state used by the generated Placeholder audio bed.</summary>
    public enum PresentationMixState : byte
    {
        Gameplay = 0,
        Paused = 1,
        Story = 2,
        Boss = 3
    }

    /// <summary>Bounded priority route for explicit generated test tones only.</summary>
    public sealed class AudioRequestRouter : IDisposable
    {
        private readonly List<RoutedAudioSource> all;
        private readonly Stack<RoutedAudioSource> available;
        private readonly List<RoutedAudioSource> active;
        private readonly Transform root;
        private readonly AudioClip[] cueClips;
        private readonly float[] cooldowns;
        private readonly int maximumCapacity;
        private readonly int reservedCriticalCapacity;
        private readonly AudioSource musicSource;
        private readonly AudioSource ambienceSource;
        private float masterVolume = 1f;
        private float musicVolume = 1f;
        private float ambienceVolume = 1f;
        private float effectsVolume = 1f;
        private float effectsMix = 1f;
        private float duckRemaining;

        public AudioRequestRouter(Transform owner)
            : this(owner, 32, 8, 8)
        {
        }

        public AudioRequestRouter(Transform owner, int maximumCapacity, int reservedCriticalCapacity, int prewarm)
        {
            root = owner ?? throw new ArgumentNullException(nameof(owner));
            if (maximumCapacity <= 2) throw new ArgumentOutOfRangeException(nameof(maximumCapacity));
            var transientCapacity = maximumCapacity - 2;
            if (reservedCriticalCapacity < 0 || reservedCriticalCapacity >= transientCapacity)
                throw new ArgumentOutOfRangeException(nameof(reservedCriticalCapacity));
            if (prewarm < 0 || prewarm > transientCapacity) throw new ArgumentOutOfRangeException(nameof(prewarm));
            this.maximumCapacity = transientCapacity;
            this.reservedCriticalCapacity = reservedCriticalCapacity;
            all = new List<RoutedAudioSource>(transientCapacity);
            available = new Stack<RoutedAudioSource>(transientCapacity);
            active = new List<RoutedAudioSource>(transientCapacity);
            cueClips = new AudioClip[Enum.GetValues(typeof(PresentationAudioCue)).Length];
            cooldowns = new float[cueClips.Length];
            cueClips[(int)PresentationAudioCue.Hit] = CreateTestTone("G2_7_HitTone", 660f, 0.05f);
            cueClips[(int)PresentationAudioCue.Death] = CreateTestTone("G2_7_DeathTone", 220f, 0.11f);
            cueClips[(int)PresentationAudioCue.Pickup] = CreateTestTone("G2_7_PickupTone", 880f, 0.08f);
            cueClips[(int)PresentationAudioCue.MechanicRise] = CreateTestTone("G2_7_MechanicTone", 520f, 0.16f);
            cueClips[(int)PresentationAudioCue.Objective] = CreateTestTone("G2_7_ObjectiveTone", 740f, 0.14f);
            cueClips[(int)PresentationAudioCue.Danger] = CreateTestTone("G2_7_DangerTone", 150f, 0.18f);
            cueClips[(int)PresentationAudioCue.BossPhase] = CreateTestTone("G2_7_BossTone", 110f, 0.28f);
            cueClips[(int)PresentationAudioCue.Confirm] = CreateTestTone("G2_7_ConfirmTone", 780f, 0.07f);
            musicSource = CreateLoopSource("G2_7_GeneratedMusic", CreateLoop("G2_7_MusicLoop", 110f, 165f));
            ambienceSource = CreateLoopSource("G2_7_GeneratedAmbience", CreateLoop("G2_7_AmbienceLoop", 55f, 82.5f));
            for (var index = 0; index < prewarm; index++) available.Push(Create());
            SetMix(1f, 1f, 1f, 1f, PresentationMixState.Gameplay);
        }

        public int ActiveCount => active.Count;
        public int CreatedSourceCount => all.Count + 2;
        public int PeakActiveCount { get; private set; }
        public long DroppedRequestCount { get; private set; }
        public long SuppressedCooldownCount { get; private set; }
        public long EvictedLowerPriorityCount { get; private set; }
        public long MergedCriticalCount { get; private set; }

        public void Route(PresentationRequestType type, float volume)
        {
            var cue = type == PresentationRequestType.Death ? PresentationAudioCue.Death :
                type == PresentationRequestType.Hit ? PresentationAudioCue.Hit : PresentationAudioCue.None;
            Route(cue, PresentationPriority.Combat, volume);
        }

        public bool Route(PresentationAudioCue cue, PresentationPriority priority, float volume)
        {
            if (cue == PresentationAudioCue.None || volume <= 0f) return false;
            var cueIndex = (int)cue;
            if (cueIndex < 0 || cueIndex >= cueClips.Length || cueClips[cueIndex] == null) return false;
            if (priority != PresentationPriority.CriticalDanger && cooldowns[cueIndex] > 0f)
            {
                SuppressedCooldownCount++;
                return false;
            }

            var ordinaryLimit = maximumCapacity - reservedCriticalCapacity;
            RoutedAudioSource item = null;
            if (priority != PresentationPriority.CriticalDanger && active.Count >= ordinaryLimit)
            {
                DroppedRequestCount++;
                return false;
            }
            if (available.Count > 0) item = available.Pop();
            else if (all.Count < maximumCapacity) item = Create();
            else
            {
                var candidate = FindEvictionCandidate(priority);
                if (candidate >= 0)
                {
                    item = active[candidate];
                    item.Source.Stop();
                    active.RemoveAt(candidate);
                    EvictedLowerPriorityCount++;
                }
                else if (priority == PresentationPriority.CriticalDanger && active.Count > 0)
                {
                    var merge = FindCriticalMerge(cue);
                    merge.Remaining = Mathf.Max(merge.Remaining, cueClips[cueIndex].length);
                    merge.BaseVolume = Mathf.Max(merge.BaseVolume, Mathf.Clamp01(volume));
                    MergedCriticalCount++;
                    duckRemaining = Mathf.Max(duckRemaining, 0.35f);
                    ApplyEffectVolumes();
                    return true;
                }
                else
                {
                    DroppedRequestCount++;
                    return false;
                }
            }

            item.Source.clip = cueClips[cueIndex];
            item.BaseVolume = Mathf.Clamp01(volume);
            item.Priority = priority;
            item.Cue = cue;
            item.Remaining = item.Source.clip.length;
            item.Source.volume = ResolveEffectVolume(item);
            if (UnityEngine.Application.isPlaying) item.Source.Play();
            active.Add(item);
            if (active.Count > PeakActiveCount) PeakActiveCount = active.Count;
            cooldowns[cueIndex] = CooldownFor(cue);
            if (priority == PresentationPriority.CriticalDanger)
                duckRemaining = Mathf.Max(duckRemaining, 0.35f);
            ApplyEffectVolumes();
            return true;
        }

        public void SetMix(
            float master,
            float music,
            float ambience,
            float effects,
            PresentationMixState state)
        {
            masterVolume = Mathf.Clamp01(master);
            musicVolume = Mathf.Clamp01(music);
            ambienceVolume = Mathf.Clamp01(ambience);
            effectsVolume = Mathf.Clamp01(effects);
            float musicMix;
            float ambienceMix;
            switch (state)
            {
                case PresentationMixState.Paused:
                    musicMix = 0.12f;
                    ambienceMix = 0.1f;
                    effectsMix = 0.6f;
                    break;
                case PresentationMixState.Story:
                    musicMix = 0.2f;
                    ambienceMix = 0.1f;
                    effectsMix = 0.65f;
                    break;
                case PresentationMixState.Boss:
                    musicMix = 0.34f;
                    ambienceMix = 0.16f;
                    effectsMix = 1f;
                    break;
                default:
                    musicMix = 0.28f;
                    ambienceMix = 0.2f;
                    effectsMix = 1f;
                    break;
            }
            musicSource.volume = masterVolume * musicVolume * musicMix;
            ambienceSource.volume = masterVolume * ambienceVolume * ambienceMix;
            ApplyEffectVolumes();
        }

        public void Tick(float unscaledDeltaTime)
        {
            for (var index = 0; index < cooldowns.Length; index++)
                cooldowns[index] = Mathf.Max(0f, cooldowns[index] - unscaledDeltaTime);
            duckRemaining = Mathf.Max(0f, duckRemaining - unscaledDeltaTime);
            for (var index = active.Count - 1; index >= 0; index--)
            {
                var item = active[index];
                item.Remaining -= unscaledDeltaTime;
                if (item.Remaining > 0f) continue;
                item.Source.Stop();
                item.Source.clip = null;
                active.RemoveAt(index);
                available.Push(item);
            }
            ApplyEffectVolumes();
        }

        public void Dispose()
        {
            for (var index = all.Count - 1; index >= 0; index--)
                UnityObjectLifetime.Destroy(all[index].Source.gameObject);
            for (var index = 0; index < cueClips.Length; index++)
                if (cueClips[index] != null) UnityObjectLifetime.Destroy(cueClips[index]);
            var musicClip = musicSource.clip;
            var ambienceClip = ambienceSource.clip;
            UnityObjectLifetime.Destroy(musicSource.gameObject);
            UnityObjectLifetime.Destroy(ambienceSource.gameObject);
            UnityObjectLifetime.Destroy(musicClip);
            UnityObjectLifetime.Destroy(ambienceClip);
            all.Clear();
            active.Clear();
            available.Clear();
        }

        private RoutedAudioSource Create()
        {
            var objectValue = new GameObject("M7_PooledAudio");
            objectValue.transform.SetParent(root, false);
            var value = new RoutedAudioSource { Source = objectValue.AddComponent<AudioSource>() };
            value.Source.playOnAwake = false;
            all.Add(value);
            return value;
        }

        private AudioSource CreateLoopSource(string name, AudioClip clip)
        {
            var objectValue = new GameObject(name);
            objectValue.transform.SetParent(root, false);
            var source = objectValue.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.clip = clip;
            if (UnityEngine.Application.isPlaying) source.Play();
            return source;
        }

        private int FindEvictionCandidate(PresentationPriority incoming)
        {
            var candidate = -1;
            var leastImportant = incoming;
            for (var index = 0; index < active.Count; index++)
            {
                if (active[index].Priority <= incoming || active[index].Priority <= leastImportant) continue;
                leastImportant = active[index].Priority;
                candidate = index;
            }
            return candidate;
        }

        private RoutedAudioSource FindCriticalMerge(PresentationAudioCue cue)
        {
            for (var index = 0; index < active.Count; index++)
                if (active[index].Priority == PresentationPriority.CriticalDanger && active[index].Cue == cue)
                    return active[index];
            return active[0];
        }

        private void ApplyEffectVolumes()
        {
            for (var index = 0; index < active.Count; index++)
                active[index].Source.volume = ResolveEffectVolume(active[index]);
        }

        private float ResolveEffectVolume(RoutedAudioSource item)
        {
            var duck = duckRemaining > 0f && item.Priority != PresentationPriority.CriticalDanger ? 0.5f : 1f;
            return item.BaseVolume * masterVolume * effectsVolume * effectsMix * duck;
        }

        private static float CooldownFor(PresentationAudioCue cue)
        {
            switch (cue)
            {
                case PresentationAudioCue.Hit: return 0.035f;
                case PresentationAudioCue.Pickup: return 0.08f;
                case PresentationAudioCue.Danger: return 0.12f;
                default: return 0.05f;
            }
        }

        private static AudioClip CreateTestTone(string name, float frequency, float seconds)
        {
            const int sampleRate = 22050;
            var count = Mathf.CeilToInt(sampleRate * seconds);
            var samples = new float[count];
            for (var index = 0; index < count; index++)
            {
                var envelope = 1f - ((float)index / count);
                samples[index] = Mathf.Sin(2f * Mathf.PI * frequency * index / sampleRate) * envelope * 0.08f;
            }
            var clip = AudioClip.Create(name, count, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip CreateLoop(string name, float firstFrequency, float secondFrequency)
        {
            const int sampleRate = 22050;
            const int count = sampleRate;
            var samples = new float[count];
            for (var index = 0; index < count; index++)
            {
                var first = Mathf.Sin(2f * Mathf.PI * firstFrequency * index / sampleRate);
                var second = Mathf.Sin(2f * Mathf.PI * secondFrequency * index / sampleRate);
                samples[index] = ((first * 0.7f) + (second * 0.3f)) * 0.025f;
            }
            var clip = AudioClip.Create(name, count, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
