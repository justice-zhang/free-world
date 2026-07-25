using System;
using System.Diagnostics;
using System.Numerics;

namespace Game.Simulation
{
    /// <summary>
    /// Stable identifiers for systems admitted to the M2 fixed pipeline.
    /// </summary>
    public enum SimulationSystemId : byte
    {
        /// <summary>Integrates velocity.</summary>
        Movement = 1,

        /// <summary>Advances finite lifetimes.</summary>
        Lifetime = 2,

        /// <summary>Applies buffered structural commands.</summary>
        Cleanup = 3,

        /// <summary>Builds the render snapshot.</summary>
        SnapshotBuild = 4
    }

    /// <summary>
    /// Contract for one explicitly ordered fixed-tick simulation system.
    /// </summary>
    public interface ISimulationSystem
    {
        /// <summary>Gets the stable system identifier.</summary>
        SimulationSystemId Id { get; }

        /// <summary>Executes one fixed tick.</summary>
        void Execute(SimulationWorld world);
    }

    /// <summary>
    /// Immutable-order system pipeline owned by one simulation world.
    /// </summary>
    public sealed class SimulationPipeline
    {
        private readonly ISimulationSystem[] systems;

        /// <summary>Initializes a pipeline in the supplied explicit order.</summary>
        public SimulationPipeline(params ISimulationSystem[] systems)
        {
            if (systems == null)
            {
                throw new ArgumentNullException(nameof(systems));
            }

            this.systems = new ISimulationSystem[systems.Length];
            for (var index = 0; index < systems.Length; index++)
            {
                this.systems[index] = systems[index] ??
                    throw new ArgumentException("Pipeline systems cannot contain null.", nameof(systems));
            }
        }

        /// <summary>Gets the number of ordered systems.</summary>
        public int Count => systems.Length;

        /// <summary>Gets one system identifier by execution index.</summary>
        public SimulationSystemId GetSystemId(int index)
        {
            if (index < 0 || index >= systems.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return systems[index].Id;
        }

        /// <summary>Creates the only default system order admitted by M2.</summary>
        public static SimulationPipeline CreateM2Default()
        {
            return new SimulationPipeline(
                new MovementSystem(),
                new LifetimeSystem(),
                new CleanupSystem(),
                new SnapshotBuildSystem());
        }

        internal void Execute(SimulationWorld world)
        {
            for (var index = 0; index < systems.Length; index++)
            {
                systems[index].Execute(world);
            }
        }
    }

    /// <summary>
    /// Presentation-independent owner of M2 stores, services, buffers and systems.
    /// </summary>
    public sealed class SimulationWorld
    {
        private readonly RenderSnapshotBuilder snapshotBuilder;
        private RandomStream random;

        /// <summary>
        /// Initializes an isolated simulation world.
        /// </summary>
        public SimulationWorld(
            ulong seed = 1UL,
            int initialEntityCapacity = 64,
            float spatialCellSize = 2f,
            SimulationPipeline pipeline = null)
        {
            if (initialEntityCapacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(initialEntityCapacity));
            }

            Diagnostics = new SimulationDiagnostics();
            Actors = new ActorStore(initialEntityCapacity, Diagnostics);
            Projectiles = new ProjectileStore(initialEntityCapacity, Diagnostics);
            Areas = new AreaStore(initialEntityCapacity, Diagnostics);
            Pickups = new PickupStore(initialEntityCapacity, Diagnostics);
            SpatialGrid = new SpatialGrid(spatialCellSize, initialEntityCapacity);
            Commands = new SimulationCommandBuffer(initialEntityCapacity);
            Events = new SimulationEventBuffer(initialEntityCapacity);
            Pipeline = pipeline ?? SimulationPipeline.CreateM2Default();
            random = new RandomStream(seed);
            snapshotBuilder = new RenderSnapshotBuilder(initialEntityCapacity);
        }

        /// <summary>Gets the fixed M2 delta in seconds.</summary>
        public float DeltaTimeSeconds => (float)SimulationClock.TickDurationSeconds;

        /// <summary>Gets the number of completed world ticks.</summary>
        public long Tick { get; private set; }

        /// <summary>Gets the dedicated actor store.</summary>
        public ActorStore Actors { get; }

        /// <summary>Gets the dedicated projectile store.</summary>
        public ProjectileStore Projectiles { get; }

        /// <summary>Gets the dedicated area store.</summary>
        public AreaStore Areas { get; }

        /// <summary>Gets the dedicated pickup store.</summary>
        public PickupStore Pickups { get; }

        /// <summary>Gets the unified spatial grid.</summary>
        public SpatialGrid SpatialGrid { get; }

        /// <summary>Gets the structural command buffer.</summary>
        public SimulationCommandBuffer Commands { get; }

        /// <summary>Gets events emitted by the latest completed runner batch.</summary>
        public SimulationEventBuffer Events { get; }

        /// <summary>Gets the fixed explicit system pipeline.</summary>
        public SimulationPipeline Pipeline { get; }

        /// <summary>Gets cumulative diagnostics.</summary>
        public SimulationDiagnostics Diagnostics { get; }

        /// <summary>Gets the latest completed render snapshot.</summary>
        public RenderSnapshot RenderSnapshot => snapshotBuilder.Snapshot;

        /// <summary>
        /// Gets the world random stream by reference. Callers must not copy it accidentally.
        /// </summary>
        public ref RandomStream Random
        {
            get
            {
                return ref random;
            }
        }

        /// <summary>Creates an actor during safe setup outside system traversal.</summary>
        public EntityHandle CreateActor(in SimulationEntityState initialState)
        {
            return CreateEntity(EntityKind.Actor, initialState);
        }

        /// <summary>Creates a projectile during safe setup outside system traversal.</summary>
        public EntityHandle CreateProjectile(in SimulationEntityState initialState)
        {
            return CreateEntity(EntityKind.Projectile, initialState);
        }

        /// <summary>Creates an area during safe setup outside system traversal.</summary>
        public EntityHandle CreateArea(in SimulationEntityState initialState)
        {
            return CreateEntity(EntityKind.Area, initialState);
        }

        /// <summary>Creates a pickup during safe setup outside system traversal.</summary>
        public EntityHandle CreatePickup(in SimulationEntityState initialState)
        {
            return CreateEntity(EntityKind.Pickup, initialState);
        }

        internal long ExecutingTick => Tick + 1;

        internal void BeginTickBatch()
        {
            Events.BeginBatch();
        }

        internal void RunTick()
        {
            var startTimestamp = Stopwatch.GetTimestamp();
            snapshotBuilder.CapturePrevious(this);
            Pipeline.Execute(this);
            Tick++;
            var endTimestamp = Stopwatch.GetTimestamp();
            var elapsedMilliseconds =
                (endTimestamp - startTimestamp) * 1000d / Stopwatch.Frequency;
            Diagnostics.RecordTick(elapsedMilliseconds);
        }

        internal EntityHandle CreateEntity(
            EntityKind kind,
            in SimulationEntityState initialState)
        {
            EntityHandle handle;
            switch (kind)
            {
                case EntityKind.Actor:
                    handle = Actors.Create(initialState);
                    break;
                case EntityKind.Projectile:
                    handle = Projectiles.Create(initialState);
                    break;
                case EntityKind.Area:
                    handle = Areas.Create(initialState);
                    break;
                case EntityKind.Pickup:
                    handle = Pickups.Create(initialState);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind));
            }

            var spatialEntity = new SpatialEntity(kind, handle);
            if (!SpatialGrid.Insert(spatialEntity, initialState.Position))
            {
                throw new InvalidOperationException("A newly created entity already exists in the grid.");
            }

            return handle;
        }

        internal bool TryRemoveEntity(
            EntityKind kind,
            EntityHandle handle,
            out Vector2 removedPosition)
        {
            SimulationEntityState state;
            bool found;
            switch (kind)
            {
                case EntityKind.Actor:
                    found = Actors.TryRead(handle, out state);
                    break;
                case EntityKind.Projectile:
                    found = Projectiles.TryRead(handle, out state);
                    break;
                case EntityKind.Area:
                    found = Areas.TryRead(handle, out state);
                    break;
                case EntityKind.Pickup:
                    found = Pickups.TryRead(handle, out state);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind));
            }

            if (!found)
            {
                removedPosition = default;
                return false;
            }

            removedPosition = state.Position;
            SpatialGrid.Remove(new SpatialEntity(kind, handle));
            switch (kind)
            {
                case EntityKind.Actor:
                    return Actors.Remove(handle);
                case EntityKind.Projectile:
                    return Projectiles.Remove(handle);
                case EntityKind.Area:
                    return Areas.Remove(handle);
                case EntityKind.Pickup:
                    return Pickups.Remove(handle);
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }

        internal void EmitEvent(
            SimulationEventType type,
            EntityKind kind,
            EntityHandle handle,
            Vector2 position)
        {
            var simulationEvent =
                new SimulationEvent(type, kind, handle, position, ExecutingTick);
            Events.Add(simulationEvent);
        }

        internal void BuildRenderSnapshot()
        {
            snapshotBuilder.BuildCurrent(this, ExecutingTick);
        }
    }
}
