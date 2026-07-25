using System;
using System.Globalization;
using System.Numerics;

namespace Game.Simulation
{
    /// <summary>
    /// Structured result exported by the M2 headless simulation harness.
    /// </summary>
    public readonly struct HeadlessSimulationSummary
    {
        /// <summary>Initializes a headless summary.</summary>
        public HeadlessSimulationSummary(
            ulong seed,
            long tickCount,
            int actorCount,
            Vector2 aggregateActorPosition,
            int snapshotEntityCount,
            long createdEntities,
            long removedEntities,
            long invalidHandleAccesses)
        {
            Seed = seed;
            TickCount = tickCount;
            ActorCount = actorCount;
            AggregateActorPosition = aggregateActorPosition;
            SnapshotEntityCount = snapshotEntityCount;
            CreatedEntities = createdEntities;
            RemovedEntities = removedEntities;
            InvalidHandleAccesses = invalidHandleAccesses;
        }

        /// <summary>Gets the root random seed.</summary>
        public ulong Seed { get; }

        /// <summary>Gets the completed fixed-tick count.</summary>
        public long TickCount { get; }

        /// <summary>Gets the live actor count.</summary>
        public int ActorCount { get; }

        /// <summary>Gets the sum of live actor positions.</summary>
        public Vector2 AggregateActorPosition { get; }

        /// <summary>Gets the latest snapshot entity count.</summary>
        public int SnapshotEntityCount { get; }

        /// <summary>Gets the cumulative entity creation count.</summary>
        public long CreatedEntities { get; }

        /// <summary>Gets the cumulative entity removal count.</summary>
        public long RemovedEntities { get; }

        /// <summary>Gets rejected invalid handle accesses.</summary>
        public long InvalidHandleAccesses { get; }

        /// <summary>Exports a stable invariant diagnostic line.</summary>
        public string ExportInvariant()
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "seed={0};ticks={1};actors={2};position=({3:R},{4:R});snapshot={5};" +
                "created={6};removed={7};invalidHandles={8}",
                Seed,
                TickCount,
                ActorCount,
                AggregateActorPosition.X,
                AggregateActorPosition.Y,
                SnapshotEntityCount,
                CreatedEntities,
                RemovedEntities,
                InvalidHandleAccesses);
        }
    }

    /// <summary>
    /// Runs deterministic simulation fixtures without scenes or presentation objects.
    /// </summary>
    public static class HeadlessSimulationHarness
    {
        /// <summary>
        /// Creates deterministic test actors, advances fixed ticks and exports a summary.
        /// </summary>
        public static HeadlessSimulationSummary Run(
            int tickCount,
            ulong seed,
            int actorCount = 1)
        {
            if (tickCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(tickCount));
            }

            if (actorCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(actorCount));
            }

            var world = new SimulationWorld(seed, Math.Max(1, actorCount));
            var fixtureRandom = new RandomStream(seed).Derive(0x4841524E455353UL);
            for (var actorIndex = 0; actorIndex < actorCount; actorIndex++)
            {
                var position = new Vector2(
                    fixtureRandom.NextFloat(-10f, 10f),
                    fixtureRandom.NextFloat(-10f, 10f));
                var velocity = new Vector2(
                    fixtureRandom.NextFloat(-2f, 2f),
                    fixtureRandom.NextFloat(-2f, 2f));
                var state = SimulationEntityState.Create(position, velocity);
                world.CreateActor(state);
            }

            var runner = new FixedTickRunner(world);
            for (var tickIndex = 0; tickIndex < tickCount; tickIndex++)
            {
                runner.Advance(SimulationClock.TickDurationSeconds);
            }

            var aggregatePosition = Vector2.Zero;
            for (var actorIndex = 0; actorIndex < world.Actors.Count; actorIndex++)
            {
                aggregatePosition += world.Actors.GetStateAt(actorIndex).Position;
            }

            return new HeadlessSimulationSummary(
                seed,
                world.Tick,
                world.Actors.Count,
                aggregatePosition,
                world.RenderSnapshot.Count,
                world.Diagnostics.CreatedEntities,
                world.Diagnostics.RemovedEntities,
                world.Diagnostics.InvalidHandleAccesses);
        }
    }
}
