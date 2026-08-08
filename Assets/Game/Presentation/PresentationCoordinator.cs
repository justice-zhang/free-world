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
        private ProceduralMapPresentation mapPresentation;
        private AccessibilitySettings settings;
        private ProceduralPresentationCatalog proceduralProfiles;
        private ColorVisionMode lastColorVision;
        private PresentationMixState mixState;
        private long consumedTick = -1;
        private int lastMechanicTier = -1;
        private int lastBossPhase = -1;
        private bool lastHadBoss;
        private bool initialized;

        public int ActiveViewCount => views.Count;
        public int InvalidHandleRejections { get; private set; }
        public int LastHitRequestCount { get; private set; }
        public int LastDeathRequestCount { get; private set; }
        public int LastStatusRequestCount { get; private set; }
        public int MissingProfileFallbackCount { get; private set; }
        public int ActiveVfxCount => vfx?.ActiveCount ?? 0;
        public int ActiveDamageNumberCount => damageNumbers?.ActiveCount ?? 0;
        public int ActiveAudioCount => audioRouter?.ActiveCount ?? 0;
        public int CreatedVfxCount => vfx?.CreatedCount ?? 0;
        public int CreatedAudioSourceCount => audioRouter?.CreatedSourceCount ?? 0;
        public long DroppedVfxRequestCount => vfx?.DroppedRequestCount ?? 0;
        public long DroppedAudioRequestCount => audioRouter?.DroppedRequestCount ?? 0;
        public int MapMarkerCount => mapPresentation?.MarkerCount ?? 0;

        public void Initialize(
            Canvas sharedCanvas,
            AccessibilitySettings accessibilitySettings,
            VisualProfileCatalog profileCatalog = null,
            ProceduralPresentationCatalog proceduralCatalog = null)
        {
            if (initialized) throw new InvalidOperationException("PresentationCoordinator is already initialized.");
            settings = accessibilitySettings ?? throw new ArgumentNullException(nameof(accessibilitySettings));
            fallback = new ProceduralVisualLibrary();
            var catalog = profileCatalog ?? new VisualProfileCatalog();
            proceduralProfiles = proceduralCatalog ?? new ProceduralPresentationCatalog();
            actors = new EntityViewPool<ActorView>(transform, EntityKind.Actor, catalog, proceduralProfiles, settings, fallback, 8);
            projectiles = new EntityViewPool<ProjectileView>(transform, EntityKind.Projectile, catalog, proceduralProfiles, settings, fallback, 16);
            areas = new EntityViewPool<AreaView>(transform, EntityKind.Area, catalog, proceduralProfiles, settings, fallback, 8);
            pickups = new EntityViewPool<PickupView>(transform, EntityKind.Pickup, catalog, proceduralProfiles, settings, fallback, 16);
            vfx = new VfxRequestPool(transform, fallback, 200, 32);
            damageNumbers = new DamageNumberPool(sharedCanvas);
            audioRouter = new AudioRequestRouter(transform);
            lastColorVision = settings.ColorVision;
            initialized = true;
        }

        public void Sync(RenderSnapshot snapshot, float interpolationAlpha, RunSession session = null)
        {
            if (!initialized) throw new InvalidOperationException("PresentationCoordinator must be initialized.");
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (lastColorVision != settings.ColorVision)
            {
                lastColorVision = settings.ColorVision;
                RefreshAllStyles(session);
            }
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
                    var playerStyle = session != null && entry.Entity == session.Player;
                    view = Acquire(entry.Entity, visualProfileId, playerStyle, out var usedFallback);
                    if (usedFallback) MissingProfileFallbackCount++;
                    ApplyOverlays(view, entry.Entity, session);
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
            audioRouter.SetMix(
                settings.MasterVolume,
                settings.MusicVolume,
                settings.AmbienceVolume,
                settings.EffectsVolume,
                mixState);
            vfx.Tick(unscaledDeltaTime);
            damageNumbers.Tick(unscaledDeltaTime);
            audioRouter.Tick(unscaledDeltaTime);
        }

        public void SetMixState(PresentationMixState value) => mixState = value;

        public void SetMap(ProceduralMapConfiguration configuration)
        {
            mapPresentation?.Dispose();
            mapPresentation = configuration == null ? null : new ProceduralMapPresentation(
                transform,
                configuration,
                proceduralProfiles,
                fallback,
                settings.ColorVision);
        }

        /// <summary>Converts low-frequency run-state transitions into readable presentation signals.</summary>
        public void SyncRunState(RunUiSnapshot snapshot)
        {
            if (!initialized || snapshot == null) return;
            if (lastMechanicTier < 0) lastMechanicTier = snapshot.MechanicTier;
            else if (snapshot.MechanicTier > lastMechanicTier)
            {
                var position = FindPlayerPosition();
                var style = new ProceduralPresentationStyle(
                    ProceduralShape.Ring,
                    new Color(0.25f, 0.9f, 0.82f, Mathf.Max(0.35f, settings.FlashIntensity)),
                    Color.white,
                    Vector2.one * 1.5f,
                    PresentationPriority.Mechanic,
                    PresentationAudioCue.MechanicRise,
                    false,
                    true);
                vfx.TrySpawn(new ProceduralVfxRequest(position, style, 1.8f, 0.4f));
                audioRouter.Route(style.AudioCue, style.Priority, 0.55f);
            }
            lastMechanicTier = snapshot.MechanicTier;

            if (snapshot.HasBoss && (!lastHadBoss || snapshot.BossPhase != lastBossPhase))
            {
                var position = FindCriticalDangerPosition();
                var style = new ProceduralPresentationStyle(
                    ProceduralShape.Ring,
                    new Color(1f, 0.22f, 0.12f, Mathf.Max(0.45f, settings.FlashIntensity)),
                    Color.black,
                    Vector2.one * 2.5f,
                    PresentationPriority.CriticalDanger,
                    PresentationAudioCue.BossPhase,
                    true,
                    true);
                vfx.TrySpawn(new ProceduralVfxRequest(position, style, 3.2f, 0.55f));
                audioRouter.Route(style.AudioCue, style.Priority, 0.8f);
            }
            lastHadBoss = snapshot.HasBoss;
            lastBossPhase = snapshot.HasBoss ? snapshot.BossPhase : -1;
            mapPresentation?.Sync(snapshot, settings.ColorVision);
        }

        public void Clear()
        {
            if (!initialized) return;
            releaseBuffer.Clear();
            foreach (var pair in views) releaseBuffer.Add(pair.Key);
            for (var index = 0; index < releaseBuffer.Count; index++) Release(releaseBuffer[index]);
            requests.Clear();
            consumedTick = -1;
            lastMechanicTier = -1;
            lastBossPhase = -1;
            lastHadBoss = false;
        }

        private EntityView Acquire(
            SpatialEntity entity,
            Game.Core.ContentId visualProfileId,
            bool playerStyle,
            out bool usedFallback)
        {
            switch (entity.Kind)
            {
                case EntityKind.Actor: return actors.Acquire(entity, visualProfileId, playerStyle, out usedFallback);
                case EntityKind.Projectile: return projectiles.Acquire(entity, visualProfileId, playerStyle, out usedFallback);
                case EntityKind.Area: return areas.Acquire(entity, visualProfileId, playerStyle, out usedFallback);
                case EntityKind.Pickup: return pickups.Acquire(entity, visualProfileId, playerStyle, out usedFallback);
                default: throw new ArgumentOutOfRangeException(nameof(entity));
            }
        }

        private void RefreshAllStyles(RunSession session)
        {
            foreach (var pair in views)
            {
                var view = pair.Value;
                view.ClearOverlays();
                switch (pair.Key.Kind)
                {
                    case EntityKind.Actor: actors.RefreshStyle((ActorView)view); break;
                    case EntityKind.Projectile: projectiles.RefreshStyle((ProjectileView)view); break;
                    case EntityKind.Area: areas.RefreshStyle((AreaView)view); break;
                    case EntityKind.Pickup: pickups.RefreshStyle((PickupView)view); break;
                }
                ApplyOverlays(view, pair.Key, session);
            }
        }

        private void ApplyOverlays(EntityView view, SpatialEntity entity, RunSession session)
        {
            if (view == null || session == null || entity.Kind != EntityKind.Actor) return;
            for (var index = 0; index < 2; index++)
            {
                if (!session.TryGetVisualOverlayId(entity, index, out var overlayId)) break;
                actors.ApplyOverlay((ActorView)view, index, overlayId);
            }
        }

        private void RouteRequests()
        {
            for (var index = 0; index < requests.Count; index++)
            {
                var request = requests.GetAt(index);
                var position = new Vector2(request.Position.X, request.Position.Y);
                proceduralProfiles.TryResolveEffect(request.ContentId, settings.ColorVision, out var style);
                switch (request.Type)
                {
                    case PresentationRequestType.Hit:
                        LastHitRequestCount++;
                        if (settings.FlashIntensity > 0f || style.Priority == PresentationPriority.CriticalDanger)
                        {
                            var color = style.Color;
                            color.a = style.Priority == PresentationPriority.CriticalDanger ?
                                Mathf.Max(0.35f, settings.FlashIntensity) : settings.FlashIntensity;
                            style = style.WithColor(color, style.OutlineColor);
                            vfx.TrySpawn(new ProceduralVfxRequest(position, style, 0.45f, 0.12f));
                        }
                        if (settings.DamageNumbersEnabled)
                            damageNumbers.Spawn(position, request.Magnitude, request.Emphasized);
                        audioRouter.Route(
                            style.AudioCue == PresentationAudioCue.None ? PresentationAudioCue.Hit : style.AudioCue,
                            style.Priority,
                            0.35f);
                        break;
                    case PresentationRequestType.Death:
                        LastDeathRequestCount++;
                        var deathColor = style.Color;
                        deathColor.a = Mathf.Max(0.55f, settings.FlashIntensity);
                        style = style.WithColor(deathColor, style.OutlineColor);
                        vfx.TrySpawn(new ProceduralVfxRequest(position, style, 1.4f, 0.3f));
                        audioRouter.Route(PresentationAudioCue.Death, style.Priority, 0.5f);
                        break;
                    case PresentationRequestType.Status:
                        LastStatusRequestCount++;
                        if (settings.FlashIntensity > 0f)
                        {
                            var statusColor = style.Color;
                            statusColor.a = settings.FlashIntensity * 0.7f;
                            style = style.WithColor(statusColor, style.OutlineColor);
                            vfx.TrySpawn(new ProceduralVfxRequest(position, style, 0.7f, 0.2f));
                        }
                        break;
                }
            }
        }

        private Vector2 FindPlayerPosition()
        {
            foreach (var pair in views)
                if (pair.Value.UsesPlayerStyle)
                    return pair.Value.transform.position;
            return Vector2.zero;
        }

        private Vector2 FindCriticalDangerPosition()
        {
            EntityView selected = null;
            var largest = -1f;
            foreach (var pair in views)
            {
                var view = pair.Value;
                if (view.Priority != PresentationPriority.CriticalDanger) continue;
                var size = view.transform.localScale.sqrMagnitude;
                if (size <= largest) continue;
                largest = size;
                selected = view;
            }
            return selected == null ? Vector2.zero : (Vector2)selected.transform.position;
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
            mapPresentation?.Dispose();
            mapPresentation = null;
            fallback.Dispose();
            initialized = false;
        }
    }
}
