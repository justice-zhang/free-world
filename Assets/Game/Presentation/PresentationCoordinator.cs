using System;
using System.Collections.Generic;
using Game.Application;
using Game.Simulation;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Presentation
{
    /// <summary>
    /// Single presentation owner that reconciles snapshots, translates transient
    /// events, and ticks pooled effects. It never writes simulation state.
    /// </summary>
    public sealed class PresentationCoordinator : MonoBehaviour
    {
        private readonly Dictionary<SpatialEntity, EntityView> views =
            new Dictionary<SpatialEntity, EntityView>(256);
        private readonly HashSet<SpatialEntity> visible = new HashSet<SpatialEntity>();
        private readonly List<SpatialEntity> releaseBuffer = new List<SpatialEntity>(64);
        private readonly PresentationRequestBuffer requests = new PresentationRequestBuffer(64);

        private ProceduralVisualLibrary fallback;
        private EntityViewPool<ActorView> actors;
        private EntityViewPool<ProjectileView> projectiles;
        private EntityViewPool<AreaView> areas;
        private EntityViewPool<PickupView> pickups;
        private VfxRequestPool vfx;
        private DamageNumberPool damageNumbers;
        private AudioRequestRouter audioRouter;
        private AccessibilitySettings settings;
        private long consumedTick = -1;
        private bool initialized;

        public int ActiveViewCount => views.Count;
        public int InvalidHandleRejections { get; private set; }
        public int LastHitRequestCount { get; private set; }
        public int LastDeathRequestCount { get; private set; }
        public int LastStatusRequestCount { get; private set; }
        public int MissingProfileFallbackCount { get; private set; }
        public int ActiveVfxCount => vfx?.ActiveCount ?? 0;
        public int ActiveDamageNumberCount => damageNumbers?.ActiveCount ?? 0;

        public void Initialize(
            Canvas sharedCanvas,
            AccessibilitySettings accessibilitySettings,
            VisualProfileCatalog profileCatalog = null)
        {
            if (initialized) throw new InvalidOperationException("PresentationCoordinator is already initialized.");
            settings = accessibilitySettings ?? throw new ArgumentNullException(nameof(accessibilitySettings));
            fallback = new ProceduralVisualLibrary();
            var catalog = profileCatalog ?? new VisualProfileCatalog();
            actors = new EntityViewPool<ActorView>(transform, EntityKind.Actor, catalog, fallback, 8);
            projectiles = new EntityViewPool<ProjectileView>(transform, EntityKind.Projectile, catalog, fallback, 16);
            areas = new EntityViewPool<AreaView>(transform, EntityKind.Area, catalog, fallback, 8);
            pickups = new EntityViewPool<PickupView>(transform, EntityKind.Pickup, catalog, fallback, 16);
            vfx = new VfxRequestPool(transform, fallback.Sprite);
            damageNumbers = new DamageNumberPool(sharedCanvas);
            audioRouter = new AudioRequestRouter(transform);
            initialized = true;
        }

        public void Sync(RenderSnapshot snapshot, float interpolationAlpha, RunSession session = null)
        {
            if (!initialized) throw new InvalidOperationException("PresentationCoordinator must be initialized.");
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            visible.Clear();
            for (var index = 0; index < snapshot.Count; index++)
            {
                var entry = snapshot.GetAt(index);
                if (!entry.Entity.IsValid)
                {
                    InvalidHandleRejections++;
                    continue;
                }

                visible.Add(entry.Entity);
                if (!views.TryGetValue(entry.Entity, out var view))
                {
                    var visualProfileId = default(Game.Core.ContentId);
                    session?.TryGetVisualProfileId(entry.Entity, out visualProfileId);
                    view = Acquire(entry.Entity, visualProfileId, out var usedFallback);
                    if (usedFallback) MissingProfileFallbackCount++;
                    views.Add(entry.Entity, view);
                }

                if (!view.Apply(entry, interpolationAlpha, snapshot.Tick)) InvalidHandleRejections++;
            }

            releaseBuffer.Clear();
            foreach (var pair in views)
                if (!visible.Contains(pair.Key)) releaseBuffer.Add(pair.Key);
            for (var index = 0; index < releaseBuffer.Count; index++) Release(releaseBuffer[index]);
        }

        public bool TryGetView(SpatialEntity entity, out EntityView view)
        {
            return views.TryGetValue(entity, out view);
        }

        public bool Release(SpatialEntity entity)
        {
            if (!entity.IsValid || !views.TryGetValue(entity, out var view))
            {
                InvalidHandleRejections++;
                return false;
            }

            views.Remove(entity);
            switch (entity.Kind)
            {
                case EntityKind.Actor: return actors.Release((ActorView)view);
                case EntityKind.Projectile: return projectiles.Release((ProjectileView)view);
                case EntityKind.Area: return areas.Release((AreaView)view);
                case EntityKind.Pickup: return pickups.Release((PickupView)view);
                default:
                    InvalidHandleRejections++;
                    return false;
            }
        }

        public void ConsumeLatestEvents(
            long snapshotTick,
            SimulationEventBuffer simulationEvents,
            CombatEventBuffer combatEvents)
        {
            if (snapshotTick == consumedTick) return;
            consumedTick = snapshotTick;
            requests.Clear();
            LastHitRequestCount = 0;
            LastDeathRequestCount = 0;
            LastStatusRequestCount = 0;

            if (simulationEvents != null)
            {
                for (var index = 0; index < simulationEvents.Count; index++)
                {
                    var item = simulationEvents.GetAt(index);
                    var removed = new SpatialEntity(item.EntityKind, item.Handle);
                    if (item.Type == SimulationEventType.Removed && views.ContainsKey(removed))
                        Release(removed);
                }
            }

            if (combatEvents != null)
            {
                for (var index = 0; index < combatEvents.DamageAppliedCount; index++)
                {
                    var item = combatEvents.GetDamageAppliedAt(index).Context;
                    requests.Add(new PresentationRequest(
                        PresentationRequestType.Hit,
                        item.Packet.Target,
                        item.Packet.Position,
                        item.FinalDamage,
                        item.WasCritical,
                        item.Packet.SourceContentId));
                }

                for (var index = 0; index < combatEvents.EntityDiedCount; index++)
                {
                    var item = combatEvents.GetEntityDiedAt(index);
                    requests.Add(new PresentationRequest(
                        PresentationRequestType.Death,
                        item.Target,
                        item.Position,
                        1f,
                        true,
                        item.SourceContentId));
                }

                for (var index = 0; index < combatEvents.StatusAppliedCount; index++)
                {
                    var item = combatEvents.GetStatusAppliedAt(index);
                    var position = System.Numerics.Vector2.Zero;
                    if (views.TryGetValue(item.Target, out var targetView))
                        position = new System.Numerics.Vector2(targetView.transform.position.x, targetView.transform.position.y);
                    requests.Add(new PresentationRequest(
                        PresentationRequestType.Status,
                        item.Target,
                        position,
                        item.Stacks,
                        item.Outcome == StatusApplicationOutcome.Replaced,
                        item.StatusId));
                }
            }

            RouteRequests();
        }

        public void TickEffects(float unscaledDeltaTime)
        {
            if (!initialized) return;
            vfx.Tick(unscaledDeltaTime);
            damageNumbers.Tick(unscaledDeltaTime);
            audioRouter.Tick(unscaledDeltaTime);
        }

        public void Clear()
        {
            if (!initialized) return;
            releaseBuffer.Clear();
            foreach (var pair in views) releaseBuffer.Add(pair.Key);
            for (var index = 0; index < releaseBuffer.Count; index++) Release(releaseBuffer[index]);
            requests.Clear();
            consumedTick = -1;
        }

        private EntityView Acquire(
            SpatialEntity entity,
            Game.Core.ContentId visualProfileId,
            out bool usedFallback)
        {
            switch (entity.Kind)
            {
                case EntityKind.Actor: return actors.Acquire(entity, visualProfileId, out usedFallback);
                case EntityKind.Projectile: return projectiles.Acquire(entity, visualProfileId, out usedFallback);
                case EntityKind.Area: return areas.Acquire(entity, visualProfileId, out usedFallback);
                case EntityKind.Pickup: return pickups.Acquire(entity, visualProfileId, out usedFallback);
                default: throw new ArgumentOutOfRangeException(nameof(entity));
            }
        }

        private void RouteRequests()
        {
            for (var index = 0; index < requests.Count; index++)
            {
                var request = requests.GetAt(index);
                var position = new Vector2(request.Position.X, request.Position.Y);
                switch (request.Type)
                {
                    case PresentationRequestType.Hit:
                        LastHitRequestCount++;
                        if (settings.FlashIntensity > 0f)
                            vfx.Spawn(position, new Color(1f, 0.25f, 0.2f, settings.FlashIntensity), 0.45f, 0.12f);
                        if (settings.DamageNumbersEnabled)
                            damageNumbers.Spawn(position, request.Magnitude, request.Emphasized);
                        audioRouter.Route(request.Type, 0.35f);
                        break;
                    case PresentationRequestType.Death:
                        LastDeathRequestCount++;
                        vfx.Spawn(position, new Color(1f, 0.1f, 0.1f, 0.8f), 1.4f, 0.3f);
                        audioRouter.Route(request.Type, 0.5f);
                        break;
                    case PresentationRequestType.Status:
                        LastStatusRequestCount++;
                        if (settings.FlashIntensity > 0f)
                            vfx.Spawn(position, new Color(0.3f, 0.6f, 1f, settings.FlashIntensity * 0.7f), 0.7f, 0.2f);
                        break;
                }
            }
        }

        private void OnDestroy()
        {
            if (!initialized) return;
            Clear();
            actors.Dispose();
            projectiles.Dispose();
            areas.Dispose();
            pickups.Dispose();
            vfx.Dispose();
            damageNumbers.Dispose();
            audioRouter.Dispose();
            fallback.Dispose();
            initialized = false;
        }
    }
}
