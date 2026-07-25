using System;
using System.Collections.Generic;
using System.Numerics;

namespace Game.Simulation
{
    /// <summary>
    /// Previous and current fixed-tick render state for one live entity.
    /// </summary>
    public readonly struct RenderEntitySnapshot
    {
        /// <summary>Initializes one render snapshot entry.</summary>
        public RenderEntitySnapshot(
            SpatialEntity entity,
            Vector2 previousPosition,
            Vector2 currentPosition,
            float previousFacingRadians,
            float currentFacingRadians,
            SimulationStateFlags previousStateFlags,
            SimulationStateFlags currentStateFlags)
        {
            Entity = entity;
            PreviousPosition = previousPosition;
            CurrentPosition = currentPosition;
            PreviousFacingRadians = previousFacingRadians;
            CurrentFacingRadians = currentFacingRadians;
            PreviousStateFlags = previousStateFlags;
            CurrentStateFlags = currentStateFlags;
        }

        /// <summary>Gets the store-qualified entity identifier.</summary>
        public SpatialEntity Entity { get; }

        /// <summary>Gets position captured before the current fixed tick.</summary>
        public Vector2 PreviousPosition { get; }

        /// <summary>Gets position after the current fixed tick.</summary>
        public Vector2 CurrentPosition { get; }

        /// <summary>Gets facing captured before the current fixed tick.</summary>
        public float PreviousFacingRadians { get; }

        /// <summary>Gets facing after the current fixed tick.</summary>
        public float CurrentFacingRadians { get; }

        /// <summary>Gets state flags captured before the current fixed tick.</summary>
        public SimulationStateFlags PreviousStateFlags { get; }

        /// <summary>Gets state flags after the current fixed tick.</summary>
        public SimulationStateFlags CurrentStateFlags { get; }

        /// <summary>Linearly interpolates position with alpha clamped to [0, 1].</summary>
        public Vector2 InterpolatePosition(float alpha)
        {
            var clamped = Clamp01(alpha);
            return PreviousPosition + ((CurrentPosition - PreviousPosition) * clamped);
        }

        /// <summary>Interpolates facing along the shortest angular path.</summary>
        public float InterpolateFacing(float alpha)
        {
            var clamped = Clamp01(alpha);
            var difference = CurrentFacingRadians - PreviousFacingRadians;
            while (difference > Math.PI)
            {
                difference -= (float)(Math.PI * 2d);
            }

            while (difference < -Math.PI)
            {
                difference += (float)(Math.PI * 2d);
            }

            return PreviousFacingRadians + (difference * clamped);
        }

        private static float Clamp01(float value)
        {
            if (value < 0f)
            {
                return 0f;
            }

            return value > 1f ? 1f : value;
        }
    }

    /// <summary>
    /// Reusable presentation snapshot produced after each completed fixed tick.
    /// </summary>
    public sealed class RenderSnapshot
    {
        private readonly Dictionary<SpatialEntity, int> indexByEntity;
        private RenderEntitySnapshot[] entries;

        internal RenderSnapshot(int initialCapacity)
        {
            entries = new RenderEntitySnapshot[Math.Max(1, initialCapacity)];
            indexByEntity = new Dictionary<SpatialEntity, int>(Math.Max(1, initialCapacity));
        }

        /// <summary>Gets the completed fixed tick represented by this snapshot.</summary>
        public long Tick { get; private set; }

        /// <summary>Gets the number of live entity entries.</summary>
        public int Count { get; private set; }

        /// <summary>Gets an entry by dense snapshot index.</summary>
        public RenderEntitySnapshot GetAt(int index)
        {
            if (index < 0 || index >= Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return entries[index];
        }

        /// <summary>Finds an entry by its store-qualified handle.</summary>
        public bool TryGet(SpatialEntity entity, out RenderEntitySnapshot entry)
        {
            if (!indexByEntity.TryGetValue(entity, out var index))
            {
                entry = default;
                return false;
            }

            entry = entries[index];
            return true;
        }

        internal void Reset(long tick)
        {
            Array.Clear(entries, 0, Count);
            indexByEntity.Clear();
            Count = 0;
            Tick = tick;
        }

        internal void Add(in RenderEntitySnapshot entry)
        {
            if (Count == entries.Length)
            {
                Array.Resize(ref entries, entries.Length * 2);
            }

            entries[Count] = entry;
            indexByEntity.Add(entry.Entity, Count);
            Count++;
        }
    }

    internal sealed class RenderSnapshotBuilder
    {
        private readonly struct PreviousRenderState
        {
            public PreviousRenderState(
                Vector2 position,
                float facingRadians,
                SimulationStateFlags stateFlags)
            {
                Position = position;
                FacingRadians = facingRadians;
                StateFlags = stateFlags;
            }

            public Vector2 Position { get; }
            public float FacingRadians { get; }
            public SimulationStateFlags StateFlags { get; }
        }

        private readonly Dictionary<SpatialEntity, PreviousRenderState> previousByEntity;

        public RenderSnapshotBuilder(int initialCapacity)
        {
            previousByEntity =
                new Dictionary<SpatialEntity, PreviousRenderState>(Math.Max(1, initialCapacity));
            Snapshot = new RenderSnapshot(initialCapacity);
        }

        public RenderSnapshot Snapshot { get; }

        public void CapturePrevious(SimulationWorld world)
        {
            previousByEntity.Clear();
            CaptureActors(world.Actors);
            CaptureProjectiles(world.Projectiles);
            CaptureAreas(world.Areas);
            CapturePickups(world.Pickups);
        }

        public void BuildCurrent(SimulationWorld world, long tick)
        {
            Snapshot.Reset(tick);
            BuildActors(world.Actors);
            BuildProjectiles(world.Projectiles);
            BuildAreas(world.Areas);
            BuildPickups(world.Pickups);
        }

        private void CaptureActors(ActorStore store)
        {
            for (var index = 0; index < store.Count; index++)
            {
                Capture(
                    new SpatialEntity(EntityKind.Actor, store.GetHandleAt(index)),
                    store.GetStateAt(index));
            }
        }

        private void CaptureProjectiles(ProjectileStore store)
        {
            for (var index = 0; index < store.Count; index++)
            {
                Capture(
                    new SpatialEntity(EntityKind.Projectile, store.GetHandleAt(index)),
                    store.GetStateAt(index));
            }
        }

        private void CaptureAreas(AreaStore store)
        {
            for (var index = 0; index < store.Count; index++)
            {
                Capture(
                    new SpatialEntity(EntityKind.Area, store.GetHandleAt(index)),
                    store.GetStateAt(index));
            }
        }

        private void CapturePickups(PickupStore store)
        {
            for (var index = 0; index < store.Count; index++)
            {
                Capture(
                    new SpatialEntity(EntityKind.Pickup, store.GetHandleAt(index)),
                    store.GetStateAt(index));
            }
        }

        private void Capture(SpatialEntity entity, in SimulationEntityState state)
        {
            previousByEntity.Add(
                entity,
                new PreviousRenderState(
                    state.Position,
                    state.FacingRadians,
                    state.StateFlags));
        }

        private void BuildActors(ActorStore store)
        {
            for (var index = 0; index < store.Count; index++)
            {
                AddCurrent(
                    new SpatialEntity(EntityKind.Actor, store.GetHandleAt(index)),
                    store.GetStateAt(index));
            }
        }

        private void BuildProjectiles(ProjectileStore store)
        {
            for (var index = 0; index < store.Count; index++)
            {
                AddCurrent(
                    new SpatialEntity(EntityKind.Projectile, store.GetHandleAt(index)),
                    store.GetStateAt(index));
            }
        }

        private void BuildAreas(AreaStore store)
        {
            for (var index = 0; index < store.Count; index++)
            {
                AddCurrent(
                    new SpatialEntity(EntityKind.Area, store.GetHandleAt(index)),
                    store.GetStateAt(index));
            }
        }

        private void BuildPickups(PickupStore store)
        {
            for (var index = 0; index < store.Count; index++)
            {
                AddCurrent(
                    new SpatialEntity(EntityKind.Pickup, store.GetHandleAt(index)),
                    store.GetStateAt(index));
            }
        }

        private void AddCurrent(SpatialEntity entity, in SimulationEntityState current)
        {
            if (!previousByEntity.TryGetValue(entity, out var previous))
            {
                previous = new PreviousRenderState(
                    current.Position,
                    current.FacingRadians,
                    current.StateFlags);
            }

            var entry = new RenderEntitySnapshot(
                entity,
                previous.Position,
                current.Position,
                previous.FacingRadians,
                current.FacingRadians,
                previous.StateFlags,
                current.StateFlags);
            Snapshot.Add(entry);
        }
    }
}
