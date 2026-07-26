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

        public SpatialEntity Binding { get; private set; }
        public bool IsBound => Binding.IsValid;
        public long LastSnapshotTick { get; private set; }

        internal void Configure(Sprite sprite, Color color, Vector2 size)
        {
            if (spriteRenderer == null) spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = sprite;
            spriteRenderer.color = color;
            transform.localScale = new Vector3(size.x, size.y, 1f);
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
            gameObject.SetActive(false);
        }
    }

    public sealed class ActorView : EntityView { }
    public sealed class ProjectileView : EntityView { }
    public sealed class AreaView : EntityView { }
    public sealed class PickupView : EntityView { }

    internal sealed class ProceduralVisualLibrary : IDisposable
    {
        private readonly Texture2D texture;

        public ProceduralVisualLibrary()
        {
            texture = new Texture2D(8, 8, TextureFormat.RGBA32, false)
            {
                name = "M7_ProceduralFallbackTexture",
                filterMode = FilterMode.Point,
                hideFlags = HideFlags.DontSave
            };
            var pixels = new Color32[64];
            for (var index = 0; index < pixels.Length; index++) pixels[index] = Color.white;
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            Sprite = Sprite.Create(texture, new Rect(0f, 0f, 8f, 8f), new Vector2(0.5f, 0.5f), 8f);
            Sprite.name = "M7_ProceduralFallbackSprite";
            Sprite.hideFlags = HideFlags.DontSave;
        }

        public Sprite Sprite { get; }

        public void Dispose()
        {
            UnityObjectLifetime.Destroy(Sprite);
            UnityObjectLifetime.Destroy(texture);
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
