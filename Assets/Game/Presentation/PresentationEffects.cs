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
    }

    /// <summary>Single-owner VFX request pool with no per-effect Update methods.</summary>
    public sealed class VfxRequestPool : IDisposable
    {
        private readonly List<PooledVfx> all = new List<PooledVfx>(16);
        private readonly Stack<PooledVfx> available = new Stack<PooledVfx>(16);
        private readonly List<PooledVfx> active = new List<PooledVfx>(16);
        private readonly Transform root;
        private readonly Sprite sprite;
        private readonly int maximumCapacity;

        /// <summary>Creates an unbounded pool compatible with the original M7 behavior.</summary>
        public VfxRequestPool(Transform owner, Sprite fallbackSprite)
            : this(owner, fallbackSprite, int.MaxValue)
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

        /// <summary>Spawns an effect using the original fire-and-forget contract.</summary>
        public void Spawn(Vector2 position, Color color, float size, float duration)
        {
            TrySpawn(position, color, size, duration);
        }

        /// <summary>Tries to spawn an effect and returns false when the bounded pool is full.</summary>
        public bool TrySpawn(Vector2 position, Color color, float size, float duration)
        {
            PooledVfx effect;
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
                FailedAcquireCount++;
                DroppedRequestCount++;
                return false;
            }

            effect.Object.transform.position = new Vector3(position.x, position.y, -0.1f);
            effect.Object.transform.localScale = Vector3.one * Mathf.Max(0.05f, size);
            effect.Renderer.color = color;
            effect.Remaining = Mathf.Max(0.01f, duration);
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

        public DamageNumberPool(Canvas sharedCanvas)
        {
            if (sharedCanvas == null) throw new ArgumentNullException(nameof(sharedCanvas));
            var rootObject = new GameObject("M7_DamageNumbers", typeof(RectTransform));
            rootObject.transform.SetParent(sharedCanvas.transform, false);
            root = (RectTransform)rootObject.transform;
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        public int ActiveCount => active.Count;

        public void Spawn(Vector2 worldPosition, float value, bool critical)
        {
            var entry = available.Count > 0 ? available.Pop() : Create();
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
    }

    /// <summary>Pooled route for explicit generated test tones only.</summary>
    public sealed class AudioRequestRouter : IDisposable
    {
        private readonly List<RoutedAudioSource> all = new List<RoutedAudioSource>(8);
        private readonly Stack<RoutedAudioSource> available = new Stack<RoutedAudioSource>(8);
        private readonly List<RoutedAudioSource> active = new List<RoutedAudioSource>(8);
        private readonly Transform root;
        private readonly AudioClip hitTone;
        private readonly AudioClip deathTone;

        public AudioRequestRouter(Transform owner)
        {
            root = owner ?? throw new ArgumentNullException(nameof(owner));
            hitTone = CreateTestTone("M7_TestHitTone", 660f, 0.05f);
            deathTone = CreateTestTone("M7_TestDeathTone", 220f, 0.11f);
        }

        public int ActiveCount => active.Count;

        public void Route(PresentationRequestType type, float volume)
        {
            if (volume <= 0f || type == PresentationRequestType.Status) return;
            var item = available.Count > 0 ? available.Pop() : Create();
            item.Source.clip = type == PresentationRequestType.Death ? deathTone : hitTone;
            item.Source.volume = Mathf.Clamp01(volume);
            item.Remaining = item.Source.clip.length;
            item.Source.Play();
            active.Add(item);
        }

        public void Tick(float unscaledDeltaTime)
        {
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
        }

        public void Dispose()
        {
            for (var index = all.Count - 1; index >= 0; index--)
                UnityObjectLifetime.Destroy(all[index].Source.gameObject);
            UnityObjectLifetime.Destroy(hitTone);
            UnityObjectLifetime.Destroy(deathTone);
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
    }
}
