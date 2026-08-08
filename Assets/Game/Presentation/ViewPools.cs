using System;
using System.Collections.Generic;
using Game.Application;
using Game.Core;
using Game.Simulation;
using UnityEngine;

namespace Game.Presentation
{
    /// <summary>Reusable pool for one high-frequency entity view type.</summary>
    public sealed class EntityViewPool<T> : IDisposable where T : EntityView
    {
        private readonly Stack<T> available;
        private readonly List<T> all;
        private readonly HashSet<T> owned;
        private readonly Transform root;
        private readonly EntityKind kind;
        private readonly VisualProfileCatalog profiles;
        private readonly ProceduralPresentationCatalog proceduralProfiles;
        private readonly AccessibilitySettings settings;
        private readonly ProceduralVisualLibrary fallback;

        internal EntityViewPool(
            Transform poolRoot,
            EntityKind entityKind,
            VisualProfileCatalog catalog,
            ProceduralPresentationCatalog proceduralCatalog,
            AccessibilitySettings accessibilitySettings,
            ProceduralVisualLibrary proceduralFallback,
            int prewarm)
        {
            root = poolRoot ?? throw new ArgumentNullException(nameof(poolRoot));
            kind = entityKind;
            profiles = catalog ?? throw new ArgumentNullException(nameof(catalog));
            proceduralProfiles = proceduralCatalog ?? throw new ArgumentNullException(nameof(proceduralCatalog));
            settings = accessibilitySettings ?? throw new ArgumentNullException(nameof(accessibilitySettings));
            fallback = proceduralFallback ?? throw new ArgumentNullException(nameof(proceduralFallback));
            available = new Stack<T>(Math.Max(1, prewarm));
            all = new List<T>(Math.Max(1, prewarm));
            owned = new HashSet<T>();
            for (var index = 0; index < prewarm; index++) available.Push(Create());
        }

        public int CreatedCount => all.Count;
        public int AvailableCount => available.Count;
        public int ActiveCount => all.Count - available.Count;

        public T Acquire(
            SpatialEntity entity,
            ContentId visualProfileId,
            bool playerStyle,
            out bool usedFallback)
        {
            if (entity.Kind != kind) throw new ArgumentException("Entity kind does not match this pool.", nameof(entity));
            var view = available.Count > 0 ? available.Pop() : Create();
            if (profiles.TryResolve(visualProfileId, kind, out var profile))
            {
                view.Configure(profile.Sprite != null ? profile.Sprite : fallback.Sprite, profile.Color, profile.Size);
                usedFallback = profile.Sprite == null;
            }
            else
            {
                usedFallback = !proceduralProfiles.TryResolve(
                    visualProfileId,
                    kind,
                    playerStyle,
                    settings.ColorVision,
                    out var style);
                view.Configure(style, fallback);
            }
            view.SetStyleIdentity(visualProfileId, playerStyle);
            view.Bind(entity);
            return view;
        }

        internal void RefreshStyle(T view)
        {
            if (view == null || !view.IsBound) return;
            if (profiles.TryResolve(view.ProfileId, kind, out var profile))
            {
                view.Configure(profile.Sprite != null ? profile.Sprite : fallback.Sprite, profile.Color, profile.Size);
                return;
            }
            proceduralProfiles.TryResolve(
                view.ProfileId,
                kind,
                view.UsesPlayerStyle,
                settings.ColorVision,
                out var style);
            view.Configure(style, fallback);
        }

        internal bool ApplyOverlay(T view, int index, ContentId overlayId)
        {
            if (view == null || !view.IsBound || !overlayId.IsValid) return false;
            if (!proceduralProfiles.TryResolve(
                    overlayId,
                    kind,
                    false,
                    settings.ColorVision,
                    out var style))
                return false;
            view.SetOverlay(index, style, fallback);
            return true;
        }

        public bool Release(T view)
        {
            if (view == null || !view.IsBound || !owned.Contains(view)) return false;
            view.Unbind();
            available.Push(view);
            return true;
        }

        public void Dispose()
        {
            for (var index = all.Count - 1; index >= 0; index--)
                if (all[index] != null) UnityObjectLifetime.Destroy(all[index].gameObject);
            all.Clear();
            owned.Clear();
            available.Clear();
        }

        private T Create()
        {
            var instance = new GameObject(typeof(T).Name + "_Pooled").AddComponent<T>();
            instance.transform.SetParent(root, false);
            instance.Configure(fallback.Sprite, ProceduralVisualLibrary.ColorFor(kind), ProceduralVisualLibrary.SizeFor(kind));
            instance.Unbind();
            all.Add(instance);
            owned.Add(instance);
            return instance;
        }
    }
}
