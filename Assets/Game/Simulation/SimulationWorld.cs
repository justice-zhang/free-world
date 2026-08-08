using System;
using System.Diagnostics;
using System.Numerics;
using Game.Core;

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
        SnapshotBuild = 4,

        /// <summary>Resolves buffered damage packets.</summary>
        DamageResolution = 5,

        /// <summary>Ticks, expires, dispels and applies statuses.</summary>
        StatusTick = 6,

        /// <summary>Finalizes queued actor deaths.</summary>
        Death = 7,

        /// <summary>Flushes per-tick combat events to the public runner batch.</summary>
        EventFlush = 8,

        /// <summary>Advances cooldowns and consumes buffered skill trigger contexts.</summary>
        SkillTrigger = 9,

        /// <summary>Advances projectile, area, aura, and orbit delivery state.</summary>
        SkillDelivery = 10,

        /// <summary>Resolves generic effect execution commands into simulation APIs.</summary>
        SkillEffectResolution = 11,

        /// <summary>Advances encounter budgets and emits buffered spawn requests.</summary>
        SpawnScheduler = 12,

        /// <summary>Advances centralized enemy behavior and steering.</summary>
        EnemyDecision = 13,

        /// <summary>Attracts and collects run progression pickups.</summary>
        Pickup = 14,

        /// <summary>Applies collected experience and queues level gains.</summary>
        Experience = 15,

        /// <summary>Produces a deterministic level-up choice request.</summary>
        LevelUpRequest = 16,
        /// <summary>Consumes one typed player command at the tick boundary.</summary>
        InputCommand = 17,
        /// <summary>Advances map objectives, events, and landmarks from previous-tick events.</summary>
        MapObjectiveAndEvent = 18,
        /// <summary>Advances attached boss phases.</summary>
        BossPhase = 19,
        /// <summary>Accumulates character mechanic resources from resolved command movement.</summary>
        CharacterMechanicAccumulate = 20,
        /// <summary>Resolves run-local reward transactions.</summary>
        RewardResolution = 21,
        /// <summary>Reacts character mechanics to current-tick actual damage.</summary>
        CharacterMechanicReaction = 22,
        /// <summary>Applies deterministic health regeneration.</summary>
        Regeneration = 23,
        /// <summary>Queues deduplicated death loot and reward requests.</summary>
        LootAndReward = 24
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

        /// <summary>Creates the explicit M3 combat and status system order.</summary>
        public static SimulationPipeline CreateM3Default()
        {
            return new SimulationPipeline(
                new MovementSystem(),
                new DamageResolutionSystem(),
                new StatusTickSystem(),
                new DeathSystem(),
                new LifetimeSystem(),
                new CleanupSystem(),
                new EventFlushSystem(),
                new SnapshotBuildSystem());
        }

        /// <summary>Creates the explicit M4 modular-skill system order.</summary>
        public static SimulationPipeline CreateM4Default()
        {
            return new SimulationPipeline(
                new SkillTriggerSystem(),
                new MovementSystem(),
                new SkillDeliverySystem(),
                new SkillEffectResolutionSystem(),
                new DamageResolutionSystem(),
                new StatusTickSystem(),
                new DeathSystem(),
                new LifetimeSystem(),
                new CleanupSystem(),
                new EventFlushSystem(),
                new SnapshotBuildSystem());
        }

        /// <summary>Creates the explicit M5 encounter, enemy, skill, and combat order.</summary>
        public static SimulationPipeline CreateM5Default()
        {
            return new SimulationPipeline(
                new SpawnSchedulerSystem(),
                new EnemyDecisionSystem(),
                new SkillTriggerSystem(),
                new MovementSystem(),
                new SkillDeliverySystem(),
                new SkillEffectResolutionSystem(),
                new DamageResolutionSystem(),
                new StatusTickSystem(),
                new DeathSystem(),
                new LifetimeSystem(),
                new CleanupSystem(),
                new EventFlushSystem(),
                new SnapshotBuildSystem());
        }

        /// <summary>Creates the M6 encounter, combat, pickup, and progression order.</summary>
        public static SimulationPipeline CreateM6Default()
        {
            return new SimulationPipeline(
                new SpawnSchedulerSystem(),
                new EnemyDecisionSystem(),
                new SkillTriggerSystem(),
                new MovementSystem(),
                new SkillDeliverySystem(),
                new SkillEffectResolutionSystem(),
                new DamageResolutionSystem(),
                new StatusTickSystem(),
                new DeathSystem(),
                new PickupSystem(),
                new ExperienceSystem(),
                new LevelUpRequestSystem(),
                new LifetimeSystem(),
                new CleanupSystem(),
                new EventFlushSystem(),
                new SnapshotBuildSystem());
        }

        /// <summary>Creates the approved 24-system Qinglan Demo pipeline.</summary>
        public static SimulationPipeline CreateQinglanDemo()
        {
            return new SimulationPipeline(
                new InputCommandSystem(),
                new SpawnSchedulerSystem(),
                new MapObjectiveAndEventSystem(),
                new BossPhaseSystem(),
                new EnemyDecisionSystem(),
                new SkillTriggerSystem(),
                new MovementSystem(),
                new CharacterMechanicAccumulateSystem(),
                new SkillDeliverySystem(),
                new SkillEffectResolutionSystem(),
                new DamageResolutionSystem(),
                new RewardResolutionSystem(),
                new CharacterMechanicReactionSystem(),
                new StatusTickSystem(),
                new RegenerationSystem(),
                new DeathSystem(),
                new LootAndRewardSystem(),
                new PickupSystem(),
                new ExperienceSystem(),
                new LevelUpRequestSystem(),
                new LifetimeSystem(),
                new CleanupSystem(),
                new EventFlushSystem(),
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
        private RandomStream damageRandom;

        /// <summary>
        /// Initializes an isolated simulation world.
        /// </summary>
        public SimulationWorld(
            ulong seed = 1UL,
            int initialEntityCapacity = 64,
            float spatialCellSize = 2f,
            SimulationPipeline pipeline = null,
            RuntimeStatusCatalog statusCatalog = null,
            CombatRules? combatRules = null,
            SkillRuntime skillRuntime = null,
            EnemyRuntime enemyRuntime = null,
            IMapRuntime mapRuntime = null,
            EncounterScheduler encounterScheduler = null)
            : this(
                null,
                seed,
                initialEntityCapacity,
                spatialCellSize,
                pipeline,
                statusCatalog,
                combatRules,
                skillRuntime,
                enemyRuntime,
                mapRuntime,
                encounterScheduler)
        {
        }

        /// <summary>Initializes a world with explicitly composition-root-owned Qinglan runtimes.</summary>
        public SimulationWorld(
            QinglanRuntimeHub qinglanRuntime,
            ulong seed = 1UL,
            int initialEntityCapacity = 64,
            float spatialCellSize = 2f,
            SimulationPipeline pipeline = null,
            RuntimeStatusCatalog statusCatalog = null,
            CombatRules? combatRules = null,
            SkillRuntime skillRuntime = null,
            EnemyRuntime enemyRuntime = null,
            IMapRuntime mapRuntime = null,
            EncounterScheduler encounterScheduler = null)
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
            DamageRequests = new DamageRequestBuffer(initialEntityCapacity);
            StatusApplications = new StatusApplicationBuffer(initialEntityCapacity);
            StatusDispels = new StatusDispelBuffer(initialEntityCapacity);
            DeathRequests = new DeathRequestBuffer(initialEntityCapacity);
            CombatEvents = new CombatEventBuffer(initialEntityCapacity);
            DamageChannels = new DamageChannelPolicyRuntime(initialEntityCapacity);
            StatusTransactions = new StatusTransactionRuntime();
            MovementSources = new MovementSourceRuntime(initialEntityCapacity);
            ResolvedMovements = new ResolvedMovementBuffer(initialEntityCapacity);
            StatusCatalog = statusCatalog ?? new RuntimeStatusCatalog();
            CombatRules = combatRules ?? Game.Simulation.CombatRules.Default;
            Skills = skillRuntime ?? SkillRuntime.CreateEmpty(initialEntityCapacity);
            Enemies = enemyRuntime ?? EnemyRuntime.CreateEmpty(initialEntityCapacity);
            Map = mapRuntime;
            Encounter = encounterScheduler;
            Pipeline = pipeline ?? SimulationPipeline.CreateM4Default();
            Qinglan = qinglanRuntime;
            random = new RandomStream(seed);
            damageRandom = random.Derive(0x44414D414745UL);
            snapshotBuilder = new RenderSnapshotBuilder(initialEntityCapacity);
        }

        /// <summary>Gets optional Qinglan general-purpose runtime owners.</summary>
        public QinglanRuntimeHub Qinglan { get; }

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

        /// <summary>Gets damage packets waiting for the next damage-resolution stage.</summary>
        public DamageRequestBuffer DamageRequests { get; }

        /// <summary>Gets status applications waiting for the next status stage.</summary>
        public StatusApplicationBuffer StatusApplications { get; }

        /// <summary>Gets status dispels waiting for the next status stage.</summary>
        public StatusDispelBuffer StatusDispels { get; }

        /// <summary>Gets M3 events emitted by the latest completed runner batch.</summary>
        public CombatEventBuffer CombatEvents { get; }

        /// <summary>Gets the fixed-capacity target/channel damage policy owner.</summary>
        public DamageChannelPolicyRuntime DamageChannels { get; }

        /// <summary>Gets the fixed-capacity atomic status query and consume owner.</summary>
        public StatusTransactionRuntime StatusTransactions { get; }

        /// <summary>Gets the actor-sidecar that classifies the next movement integration.</summary>
        public MovementSourceRuntime MovementSources { get; }

        /// <summary>Gets the current tick's map-resolved movement records.</summary>
        public ResolvedMovementBuffer ResolvedMovements { get; }

        /// <summary>Gets pure runtime status definitions available to this run.</summary>
        public RuntimeStatusCatalog StatusCatalog { get; }

        /// <summary>Gets immutable damage and proc boundaries.</summary>
        public CombatRules CombatRules { get; }

        /// <summary>Gets the modular M4 skill runtime.</summary>
        public SkillRuntime Skills { get; }

        /// <summary>Gets the centralized M5 enemy runtime.</summary>
        public EnemyRuntime Enemies { get; }

        /// <summary>Gets the optional pure M5 map runtime.</summary>
        public IMapRuntime Map { get; }

        /// <summary>Gets the optional M5 encounter scheduler.</summary>
        public EncounterScheduler Encounter { get; }

        /// <summary>Gets the optional run-local M6 progression runtime.</summary>
        public ProgressionRuntime Progression { get; private set; }

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

        /// <summary>Creates a combat-ready actor during safe setup outside traversal.</summary>
        public EntityHandle CreateActor(
            in SimulationEntityState initialState,
            in ActorCombatInitialization combatState)
        {
            var handle = Actors.Create(initialState, combatState);
            var spatialEntity = new SpatialEntity(EntityKind.Actor, handle);
            if (!SpatialGrid.Insert(spatialEntity, initialState.Position))
            {
                Actors.Remove(handle);
                throw new InvalidOperationException(
                    "A newly created actor already exists in the grid.");
            }

            return handle;
        }

        /// <summary>Assigns the player actor used by enemy decisions and encounter spawning.</summary>
        public void SetPlayer(EntityHandle player)
        {
            if (!Actors.Contains(player)) throw new ArgumentException("Player must be a live actor.", nameof(player));
            Enemies.SetPlayer(player);
        }

        internal bool TryGetPlayerPosition(out Vector2 position)
        {
            if (Enemies.Player.IsValid && Actors.TryRead(Enemies.Player, out var state))
            {
                position = state.Position;
                return true;
            }

            position = default;
            return false;
        }

        internal bool IsHostileTarget(SpatialEntity owner, SpatialEntity candidate)
        {
            return Enemies.IsHostile(owner, candidate);
        }

        /// <summary>Queues damage for the centralized resolution stage.</summary>
        public bool QueueDamage(in DamagePacket packet)
        {
            if (packet.ProcDepth < 0)
            {
                Diagnostics.RecordRejectedDamage();
                return false;
            }

            if (packet.ProcDepth > CombatRules.MaximumProcDepth)
            {
                Diagnostics.RecordTruncatedProcChain();
                return false;
            }

            DamageRequests.Add(packet);
            return true;
        }

        /// <summary>Queues a generic runtime status application.</summary>
        public bool QueueStatus(in StatusApplicationRequest request)
        {
            if (request.ProcDepth < 0 ||
                float.IsNaN(request.Strength) ||
                float.IsInfinity(request.Strength) ||
                request.Strength < 0f ||
                !request.StatusIndex.IsValid)
            {
                Diagnostics.RecordRejectedStatus();
                return false;
            }

            if (request.ProcDepth > CombatRules.MaximumProcDepth)
            {
                Diagnostics.RecordTruncatedProcChain();
                return false;
            }

            StatusApplications.Add(request);
            return true;
        }

        /// <summary>Queues one tag-based status dispel.</summary>
        public bool QueueStatusDispel(in StatusDispelRequest request)
        {
            if (!request.DispelTag.IsValid)
            {
                Diagnostics.RecordRejectedStatus();
                return false;
            }

            StatusDispels.Add(request);
            return true;
        }

        /// <summary>Queues an external M4 trigger such as pickup collection.</summary>
        public bool QueueSkillTrigger(in SkillTriggerContext context)
        {
            if (context.ProcDepth > CombatRules.MaximumProcDepth)
            {
                Diagnostics.RecordTruncatedProcChain();
                return false;
            }

            return Skills.QueueTrigger(context);
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

        /// <summary>Attaches M6 progression to a live player exactly once.</summary>
        public ProgressionRuntime InitializeProgression(
            BuildRuntimeCatalog catalog,
            EntityHandle player,
            ulong runSeed,
            ExperienceCurve? curve = null,
            int skillSlots = 6,
            int passiveSlots = 6,
            ContentTag[] mapTags = null)
        {
            return InitializeProgression(
                catalog,
                player,
                runSeed,
                runSeed,
                curve,
                skillSlots,
                passiveSlots,
                mapTags);
        }

        /// <summary>Attaches progression while keeping deterministic RNG seed separate from the stable run identity.</summary>
        public ProgressionRuntime InitializeProgression(
            BuildRuntimeCatalog catalog,
            EntityHandle player,
            ulong runSeed,
            ulong rewardRunId,
            ExperienceCurve? curve = null,
            int skillSlots = 6,
            int passiveSlots = 6,
            ContentTag[] mapTags = null)
        {
            if (Progression != null) throw new InvalidOperationException("Progression is already initialized.");
            if (!Actors.Contains(player)) throw new ArgumentException("Player must be a live actor.", nameof(player));
            Progression = new ProgressionRuntime(
                catalog,
                Actors,
                Skills,
                player,
                runSeed,
                curve,
                skillSlots,
                passiveSlots,
                mapTags,
                Math.Max(16, Actors.Count),
                Qinglan?.Rewards);
            Qinglan?.Rewards.Initialize(catalog, Progression, player, rewardRunId);
            return Progression;
        }

        internal long ExecutingTick => Tick + 1;

        internal DeathRequestBuffer DeathRequests { get; }

        internal ref RandomStream DamageRandom
        {
            get
            {
                return ref damageRandom;
            }
        }

        internal void BeginTickBatch()
        {
            Events.BeginBatch();
            CombatEvents.BeginBatch();
            Qinglan?.Mechanics.BeginBatch();
        }

        internal void RunTick()
        {
            var startTimestamp = Stopwatch.GetTimestamp();
            CombatEvents.BeginTick();
            Qinglan?.Mechanics.BeginTick(ExecutingTick);
            snapshotBuilder.CapturePrevious(this);
            Pipeline.Execute(this);
            // Event publication is a world invariant, even for an explicitly supplied
            // pipeline that omits the optional EventFlushSystem marker.
            CombatEvents.FlushTick();
            Qinglan?.Mechanics.FlushTick();
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
            if (kind == EntityKind.Actor) Skills.ExpireOwnedDeliveries(this, handle);
            Skills.OnEntityRemoved(kind, handle);
            if (kind == EntityKind.Actor)
            {
                Enemies.OnEntityRemoved(handle);
                Qinglan?.Mechanics.Detach(handle);
                Qinglan?.Bosses.Detach(handle);
            }
            if (kind == EntityKind.Pickup)
            {
                Progression?.OnPickupRemoved(handle);
                Qinglan?.Rewards.OnPickupRemoved(handle);
            }
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
