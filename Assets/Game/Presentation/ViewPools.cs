using System;
using System.Collections.Generic;
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
        private readonly ProceduralVisualLibrary fallback;

        internal EntityViewPool(
            Transform poolRoot,
            EntityKind entityKind,
            VisualProfileCatalog catalog,
            ProceduralVisualLibrary proceduralFallback,
            int prewarm)
        {
            root = poolRoot ?? throw new ArgumentNullException(nameof(poolRoot));
            kind = entityKind;
            profiles = catalog ?? throw new ArgumentNullException(nameof(catalog));
            fallback = proceduralFallback ?? throw new ArgumentNullException(nameof(proceduralFallback));
            available = new Stack<T>(Math.Max(1, prewarm));
            all = new List<T>(Math.Max(1, prewarm));
            owned = new HashSet<T>();
            for (var index = 0; index < prewarm; index++) available.Push(Create());
        }

        public int CreatedCount => all.Count;
        public int AvailableCount => available.Count;
        public int ActiveCount => all.Count - available.Count;

        public T Acquire(SpatialEntity entity, ContentId visualProfileId, out bool usedFallback)
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
                view.Configure(fallback.Sprite, ProceduralVisualLibrary.ColorFor(kind), ProceduralVisualLibrary.SizeFor(kind));
                usedFallback = true;
            }
            view.Bind(entity);
            return view;
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
