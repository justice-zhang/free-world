using System;
using Game.Core;
using Game.Simulation;
using UnityEngine;

namespace Game.Presentation
{
    /// <summary>Optional presentation-only profile resolved outside simulation truth.</summary>
    [CreateAssetMenu(menuName = "Free World/Presentation/Visual Profile")]
    public sealed class VisualProfile : ScriptableObject
    {
        [SerializeField] private EntityKind entityKind = EntityKind.Actor;
        [SerializeField] private string stableId;
        [SerializeField] private Sprite sprite;
        [SerializeField] private Color color = Color.white;
        [SerializeField] private Vector2 size = Vector2.one;

        public EntityKind EntityKind => entityKind;
        public string StableId => stableId ?? string.Empty;
        public Sprite Sprite => sprite;
        public Color Color => color;
        public Vector2 Size => size;
    }

    /// <summary>Presentation catalog whose miss path always returns a procedural fallback.</summary>
    public sealed class VisualProfileCatalog
    {
        private readonly VisualProfile[] profiles;

        public VisualProfileCatalog(VisualProfile[] source = null)
        {
            profiles = source == null ? Array.Empty<VisualProfile>() : (VisualProfile[])source.Clone();
        }

        public bool TryResolve(ContentId id, EntityKind kind, out VisualProfile profile)
        {
            for (var index = 0; index < profiles.Length; index++)
            {
                if (profiles[index] != null &&
                    profiles[index].EntityKind == kind &&
                    id.IsValid &&
                    string.Equals(profiles[index].StableId, id.Value, StringComparison.Ordinal))
                {
                    profile = profiles[index];
                    return true;
                }
            }

            profile = null;
            return false;
        }
    }

    /// <summary>Base binding that consumes snapshots and never owns gameplay truth.</summary>
    public abstract class EntityView : MonoBehaviour
    {
        private SpriteRenderer spriteRenderer;
        private SpriteRenderer outlineRenderer;
        private SpriteRenderer[] overlayRenderers;

        public SpatialEntity Binding { get; private set; }
        public bool IsBound => Binding.IsValid;
        public long LastSnapshotTick { get; private set; }
        public PresentationPriority Priority { get; private set; } = PresentationPriority.Decoration;
        public ProceduralShape Shape { get; private set; } = ProceduralShape.Square;
        public Color DisplayColor => spriteRenderer == null ? Color.clear : spriteRenderer.color;
        public int ActiveOverlayCount { get; private set; }
        internal ContentId ProfileId { get; private set; }
        internal bool UsesPlayerStyle { get; private set; }

        internal void Configure(Sprite sprite, Color color, Vector2 size)
        {
            if (spriteRenderer == null) spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = sprite;
            spriteRenderer.color = color;
            spriteRenderer.sortingOrder = SortingOrderFor(PresentationPriority.Decoration);
            transform.localScale = new Vector3(size.x, size.y, 1f);
            if (outlineRenderer != null) outlineRenderer.gameObject.SetActive(false);
            ClearOverlays();
        }

        internal void Configure(
            in ProceduralPresentationStyle style,
            ProceduralVisualLibrary library)
        {
            if (library == null) throw new ArgumentNullException(nameof(library));
            if (spriteRenderer == null) spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = library.GetSprite(style.Shape);
            spriteRenderer.color = style.Color;
            spriteRenderer.sortingOrder = SortingOrderFor(style.Priority);
            transform.localScale = new Vector3(style.Size.x, style.Size.y, 1f);
            var outline = EnsureOutline();
            outline.sprite = library.GetSprite(style.Shape);
            outline.color = style.OutlineColor;
            outline.sortingOrder = spriteRenderer.sortingOrder - 1;
            outline.transform.localScale = Vector3.one * 1.18f;
            outline.gameObject.SetActive(true);
            Priority = style.Priority;
            Shape = style.Shape;
        }

        internal void SetOverlay(
            int index,
            in ProceduralPresentationStyle style,
            ProceduralVisualLibrary library)
        {
            if (index < 0 || index >= 2) throw new ArgumentOutOfRangeException(nameof(index));
            if (library == null) throw new ArgumentNullException(nameof(library));
            EnsureOverlays();
            var renderer = overlayRenderers[index];
            renderer.sprite = library.GetSprite(style.Shape);
            renderer.color = style.Color;
            renderer.sortingOrder = Math.Max(
                spriteRenderer.sortingOrder + 1 + index,
                SortingOrderFor(style.Priority) + index);
            renderer.transform.localScale = Vector3.one * (1.28f + (index * 0.18f));
            renderer.gameObject.SetActive(true);
            if (index + 1 > ActiveOverlayCount) ActiveOverlayCount = index + 1;
        }

        internal void ClearOverlays()
        {
            if (overlayRenderers != null)
                for (var index = 0; index < overlayRenderers.Length; index++)
                    if (overlayRenderers[index] != null) overlayRenderers[index].gameObject.SetActive(false);
            ActiveOverlayCount = 0;
        }

        internal void SetStyleIdentity(ContentId profileId, bool playerStyle)
        {
            ProfileId = profileId;
            UsesPlayerStyle = playerStyle;
        }

        public void Bind(SpatialEntity entity)
        {
            if (!entity.IsValid) throw new ArgumentException("A view requires a valid entity.", nameof(entity));
            Binding = entity;
            LastSnapshotTick = -1;
            gameObject.SetActive(true);
        }

        public bool Apply(in RenderEntitySnapshot snapshot, float alpha, long snapshotTick)
        {
            if (!IsBound || snapshot.Entity != Binding) return false;
            var position = snapshot.InterpolatePosition(alpha);
            transform.SetPositionAndRotation(
                new Vector3(position.X, position.Y, 0f),
                Quaternion.Euler(0f, 0f, snapshot.InterpolateFacing(alpha) * Mathf.Rad2Deg));
            gameObject.SetActive((snapshot.CurrentStateFlags & SimulationStateFlags.Hidden) == 0);
            LastSnapshotTick = snapshotTick;
            return true;
        }

        public void Unbind()
        {
            Binding = default;
            LastSnapshotTick = -1;
            ProfileId = default;
            UsesPlayerStyle = false;
            ClearOverlays();
            gameObject.SetActive(false);
        }

        private SpriteRenderer EnsureOutline()
        {
            if (outlineRenderer != null) return outlineRenderer;
            var child = new GameObject("ProceduralOutline");
            child.transform.SetParent(transform, false);
            outlineRenderer = child.AddComponent<SpriteRenderer>();
            return outlineRenderer;
        }

        private static int SortingOrderFor(PresentationPriority priority)
        {
            switch (priority)
            {
                case PresentationPriority.CriticalDanger: return 40;
                case PresentationPriority.Mechanic: return 30;
                case PresentationPriority.Combat: return 20;
                default: return 10;
            }
        }

        private void EnsureOverlays()
        {
            if (overlayRenderers != null) return;
            overlayRenderers = new SpriteRenderer[2];
            for (var index = 0; index < overlayRenderers.Length; index++)
            {
                var child = new GameObject("ProceduralOverlay_" + index);
                child.transform.SetParent(transform, false);
                overlayRenderers[index] = child.AddComponent<SpriteRenderer>();
                child.SetActive(false);
            }
        }
    }

    public sealed class ActorView : EntityView { }
    public sealed class ProjectileView : EntityView { }
    public sealed class AreaView : EntityView { }
    public sealed class PickupView : EntityView { }

    internal sealed class ProceduralVisualLibrary : IDisposable
    {
        private readonly Texture2D[] textures;
        private readonly Sprite[] sprites;

        public ProceduralVisualLibrary()
        {
            var count = Enum.GetValues(typeof(ProceduralShape)).Length;
            textures = new Texture2D[count];
            sprites = new Sprite[count];
            for (var index = 0; index < count; index++) Create((ProceduralShape)index);
        }

        public Sprite Sprite => GetSprite(ProceduralShape.Square);

        public Sprite GetSprite(ProceduralShape shape)
        {
            var index = (int)shape;
            return index >= 0 && index < sprites.Length ? sprites[index] : sprites[0];
        }

        public void Dispose()
        {
            for (var index = 0; index < sprites.Length; index++)
            {
                UnityObjectLifetime.Destroy(sprites[index]);
                UnityObjectLifetime.Destroy(textures[index]);
            }
        }

        public static Color ColorFor(EntityKind kind)
        {
            switch (kind)
            {
                case EntityKind.Actor: return new Color(0.2f, 0.8f, 1f, 1f);
                case EntityKind.Projectile: return new Color(1f, 0.85f, 0.2f, 1f);
                case EntityKind.Area: return new Color(0.7f, 0.25f, 1f, 0.45f);
                case EntityKind.Pickup: return new Color(0.25f, 1f, 0.4f, 1f);
                default: return Color.magenta;
            }
        }

        public static Vector2 SizeFor(EntityKind kind)
        {
            switch (kind)
            {
                case EntityKind.Actor: return Vector2.one;
                case EntityKind.Projectile: return Vector2.one * 0.35f;
                case EntityKind.Area: return Vector2.one * 2f;
                case EntityKind.Pickup: return Vector2.one * 0.45f;
                default: return Vector2.one;
            }
        }

        private void Create(ProceduralShape shape)
        {
            const int size = 32;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "G27_Procedural_" + shape,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };
            var pixels = new Color32[size * size];
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var nx = ((x + 0.5f) / size * 2f) - 1f;
                var ny = ((y + 0.5f) / size * 2f) - 1f;
                pixels[(y * size) + x] = Contains(shape, nx, ny) ?
                    new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            var sprite = UnityEngine.Sprite.Create(
                texture,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                size);
            sprite.name = texture.name + "_Sprite";
            sprite.hideFlags = HideFlags.DontSave;
            textures[(int)shape] = texture;
            sprites[(int)shape] = sprite;
        }

        private static bool Contains(ProceduralShape shape, float x, float y)
        {
            var ax = Mathf.Abs(x);
            var ay = Mathf.Abs(y);
            switch (shape)
            {
                case ProceduralShape.Circle: return (x * x) + (y * y) <= 0.72f;
                case ProceduralShape.Diamond: return ax + ay <= 0.92f;
                case ProceduralShape.Triangle: return y >= -0.78f && y <= 0.78f && ax <= (0.82f - (y * 0.52f));
                case ProceduralShape.Ring:
                    var radius = (x * x) + (y * y);
                    return radius >= 0.34f && radius <= 0.76f;
                case ProceduralShape.Cross: return (ax <= 0.2f && ay <= 0.78f) || (ay <= 0.2f && ax <= 0.78f);
                case ProceduralShape.Chevron:
                    return ay <= 0.8f && Mathf.Abs(ax - ((y + 0.8f) * 0.48f)) <= 0.13f;
                case ProceduralShape.Hexagon: return ax <= 0.78f && ay <= 0.68f && (ax + (ay * 0.58f)) <= 0.92f;
                case ProceduralShape.Line: return ax <= 0.88f && ay <= 0.13f;
                default: return ax <= 0.72f && ay <= 0.72f;
            }
        }
    }

    internal static class UnityObjectLifetime
    {
        public static void Destroy(UnityEngine.Object value)
        {
            if (value == null) return;
            if (UnityEngine.Application.isPlaying) UnityEngine.Object.Destroy(value);
            else UnityEngine.Object.DestroyImmediate(value);
        }
    }
}
