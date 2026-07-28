using System;
using System.Numerics;
using System.Diagnostics;
using Game.Content.Runtime;
using Game.Core;

namespace Game.Simulation
{
    /// <summary>Immutable configuration for the fixed-seed M10 target-scale scenario.</summary>
    internal readonly struct M10StressConfiguration
    {
        public const int TargetEnemyCount = 1500;
        public const int TargetProjectileCount = 3000;
        public const int TargetPickupCount = 5000;
        public const int TargetVfxRequestCount = 200;
        public const int ThirtyMinuteTickCount = 30 * 60 * SimulationClock.TickRate;

        public M10StressConfiguration(
            ulong seed,
            int tickCount,
            int enemyCount,
            int projectileCount,
            int pickupCount,
            int vfxRequestCount,
            int warmupTickCount = 300)
        {
            if (tickCount <= 0) throw new ArgumentOutOfRangeException(nameof(tickCount));
            if (enemyCount <= 0) throw new ArgumentOutOfRangeException(nameof(enemyCount));
            if (projectileCount <= 0) throw new ArgumentOutOfRangeException(nameof(projectileCount));
            if (pickupCount <= 0) throw new ArgumentOutOfRangeException(nameof(pickupCount));
            if (vfxRequestCount <= 0) throw new ArgumentOutOfRangeException(nameof(vfxRequestCount));
            if (warmupTickCount < 0) throw new ArgumentOutOfRangeException(nameof(warmupTickCount));
            Seed = seed;
            TickCount = tickCount;
            EnemyCount = enemyCount;
            ProjectileCount = projectileCount;
            PickupCount = pickupCount;
            VfxRequestCount = vfxRequestCount;
            WarmupTickCount = warmupTickCount;
        }

        public ulong Seed { get; }
        public int TickCount { get; }
        public int EnemyCount { get; }
        public int ProjectileCount { get; }
        public int PickupCount { get; }
        public int VfxRequestCount { get; }
        public int WarmupTickCount { get; }
        public int ExpectedEntityCount => EnemyCount + ProjectileCount + PickupCount + 1;

        public static M10StressConfiguration Target(ulong seed = 0x4D3130465245455AUL) =>
            new M10StressConfiguration(
                seed,
                ThirtyMinuteTickCount,
                TargetEnemyCount,
                TargetProjectileCount,
                TargetPickupCount,
                TargetVfxRequestCount);
    }

    /// <summary>
    /// Owns the fixed M10 pressure scenario. It reuses the production stores, enemy sidecar,
    /// spatial grid, movement system and render-snapshot builder without engine objects.
    /// </summary>
    internal sealed class M10StressScenario
    {
        private const float SpawnRadius = 96f;
        private readonly FixedTickRunner runner;
        private readonly M10SystemTimingCounter[] systemTimings;

        private M10StressScenario(
            in M10StressConfiguration configuration,
            SimulationWorld world,
            M10SystemTimingCounter[] timings)
        {
            Configuration = configuration;
            World = world;
            systemTimings = timings;
            runner = new FixedTickRunner(world);
        }

        public M10StressConfiguration Configuration { get; }
        public SimulationWorld World { get; }
        public int EnemyCount => World.Enemies.Count;
        public int ProjectileCount => World.Projectiles.Count;
        public int PickupCount => World.Pickups.Count;
        public int TotalEntityCount => World.Diagnostics.ActiveEntities;
        public RenderSnapshot Snapshot => World.RenderSnapshot;

        public static Result<M10StressScenario> Create(
            ContentRegistry content,
            ContentId enemyId,
            in M10StressConfiguration configuration)
        {
            if (content == null) throw new ArgumentNullException(nameof(content));
            if (!enemyId.IsValid)
            {
                return Failure("The stress enemy ContentId is invalid.");
            }

            if (!content.TryGet(enemyId, out var enemyEntry) ||
                !(enemyEntry.Definition is RuntimeEnemyDefinition enemyDefinition) ||
                !enemyDefinition.HasM5Data)
            {
                return Failure("The stress enemy is missing or has no M5 runtime data.");
            }

            var modules = SkillModuleRegistry.CreateDefault();
            var skillCatalog = SkillRuntimeCatalog.Build(content, modules);
            if (!skillCatalog.IsSuccess) return Result<M10StressScenario>.Failure(skillCatalog.Error);
            var enemyCatalog = EnemyRuntimeCatalog.Build(content);
            if (!enemyCatalog.IsSuccess) return Result<M10StressScenario>.Failure(enemyCatalog.Error);
            if (!enemyCatalog.Value.TryGet(enemyEntry.Index, out _))
            {
                return Failure("The stress enemy did not compile into the runtime catalog.");
            }

            var capacity = configuration.ExpectedEntityCount;
            var skills = new SkillRuntime(skillCatalog.Value, configuration.Seed, capacity);
            var enemies = new EnemyRuntime(
                enemyCatalog.Value,
                DifficultySnapshot.Default,
                configuration.EnemyCount + 1);
            var timings = new[]
            {
                new M10SystemTimingCounter(SimulationSystemId.EnemyDecision),
                new M10SystemTimingCounter(SimulationSystemId.Movement),
                new M10SystemTimingCounter(SimulationSystemId.Lifetime),
                new M10SystemTimingCounter(SimulationSystemId.Cleanup),
                new M10SystemTimingCounter(SimulationSystemId.SnapshotBuild)
            };
            var pipeline = new SimulationPipeline(
                new M10TimedSystem(new EnemyDecisionSystem(), timings[0]),
                new M10TimedSystem(new MovementSystem(), timings[1]),
                new M10TimedSystem(new LifetimeSystem(), timings[2]),
                new M10TimedSystem(new CleanupSystem(), timings[3]),
                new M10TimedSystem(new SnapshotBuildSystem(), timings[4]));
            var world = new SimulationWorld(
                configuration.Seed,
                capacity,
                2f,
                pipeline,
                null,
                null,
                skills,
                enemies);
            var player = world.CreateActor(
                SimulationEntityState.Create(Vector2.Zero, Vector2.Zero),
                ActorCombatInitialization.CreateDefault(1_000_000_000f, 0f));
            world.SetPlayer(player);

            var random = new RandomStream(configuration.Seed).Derive(0x4D31305354524553UL);
            for (var index = 0; index < configuration.EnemyCount; index++)
            {
                var position = DistributedPosition(index, configuration.EnemyCount, SpawnRadius, ref random);
                enemies.PendingSpawns.Add(
                    new SpawnRequest(enemyEntry.Index, position, false, false, index));
            }

            new CleanupSystem().Execute(world);
            for (var index = 0; index < configuration.ProjectileCount; index++)
            {
                var position = DistributedPosition(
                    index,
                    configuration.ProjectileCount,
                    SpawnRadius * 1.75f,
                    ref random);
                var angle = random.NextFloat() * (float)(Math.PI * 2d);
                var speed = random.NextFloat(2f, 7f);
                var velocity = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * speed;
                world.CreateProjectile(SimulationEntityState.Create(position, velocity));
            }

            for (var index = 0; index < configuration.PickupCount; index++)
            {
                var position = DistributedPosition(
                    index,
                    configuration.PickupCount,
                    SpawnRadius * 2.25f,
                    ref random);
                var angle = random.NextFloat() * (float)(Math.PI * 2d);
                var velocity = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * 0.15f;
                world.CreatePickup(SimulationEntityState.Create(position, velocity));
            }

            new SnapshotBuildSystem().Execute(world);
            var scenario = new M10StressScenario(configuration, world, timings);
            if (!scenario.HasExactConfiguredCounts())
            {
                return Failure("The stress scenario did not reach its configured entity counts.");
            }

            return Result<M10StressScenario>.Success(scenario);
        }

        public void AdvanceOneTick()
        {
            var advanced = runner.Advance(SimulationClock.TickDurationSeconds);
            if (advanced != 1)
            {
                throw new InvalidOperationException("The M10 stress runner must advance exactly one fixed tick.");
            }
        }

        public bool HasExactConfiguredCounts()
        {
            return EnemyCount == Configuration.EnemyCount &&
                   ProjectileCount == Configuration.ProjectileCount &&
                   PickupCount == Configuration.PickupCount &&
                   TotalEntityCount == Configuration.ExpectedEntityCount &&
                   Snapshot.Count == Configuration.ExpectedEntityCount;
        }

        public ulong CalculateChecksum()
        {
            unchecked
            {
                var hash = 1469598103934665603UL;
                hash = Combine(hash, (ulong)World.Tick);
                hash = Combine(hash, (ulong)EnemyCount);
                hash = Combine(hash, (ulong)ProjectileCount);
                hash = Combine(hash, (ulong)PickupCount);
                for (var index = 0; index < Snapshot.Count; index++)
                {
                    var item = Snapshot.GetAt(index);
                    hash = Combine(hash, (ulong)item.Entity.Kind);
                    hash = Combine(hash, (uint)item.Entity.Handle.Index);
                    hash = Combine(hash, item.Entity.Handle.Generation);
                    hash = Combine(hash, (uint)BitConverter.SingleToInt32Bits(item.CurrentPosition.X));
                    hash = Combine(hash, (uint)BitConverter.SingleToInt32Bits(item.CurrentPosition.Y));
                    hash = Combine(hash, (uint)item.CurrentStateFlags);
                }

                return hash;
            }
        }

        public M10SystemTimingSnapshot[] CaptureSystemTimings()
        {
            var output = new M10SystemTimingSnapshot[systemTimings.Length];
            for (var index = 0; index < output.Length; index++)
                output[index] = systemTimings[index].Capture();
            return output;
        }

        private static Vector2 DistributedPosition(
            int index,
            int count,
            float radius,
            ref RandomStream random)
        {
            const float GoldenAngle = 2.39996323f;
            var fraction = (index + 0.5f) / Math.Max(1, count);
            var distance = radius * (float)Math.Sqrt(fraction);
            var angle = index * GoldenAngle + random.NextFloat(-0.01f, 0.01f);
            return new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * distance;
        }

        private static ulong Combine(ulong hash, ulong value)
        {
            unchecked
            {
                hash ^= value;
                return hash * 1099511628211UL;
            }
        }

        private static Result<M10StressScenario> Failure(string message) =>
            Result<M10StressScenario>.Failure(
                new Error(ErrorCode.InvalidAuthoringData, message));
    }

    internal readonly struct M10SystemTimingSnapshot
    {
        public M10SystemTimingSnapshot(
            SimulationSystemId systemId,
            long calls,
            double totalMilliseconds,
            double maximumMilliseconds)
        {
            SystemId = systemId;
            Calls = calls;
            TotalMilliseconds = totalMilliseconds;
            MaximumMilliseconds = maximumMilliseconds;
        }

        public SimulationSystemId SystemId { get; }
        public long Calls { get; }
        public double TotalMilliseconds { get; }
        public double AverageMilliseconds => Calls == 0 ? 0d : TotalMilliseconds / Calls;
        public double MaximumMilliseconds { get; }
    }

    internal sealed class M10SystemTimingCounter
    {
        private long calls;
        private double totalMilliseconds;
        private double maximumMilliseconds;

        public M10SystemTimingCounter(SimulationSystemId systemId)
        {
            SystemId = systemId;
        }

        public SimulationSystemId SystemId { get; }

        public void Record(long startTimestamp)
        {
            var elapsed = (Stopwatch.GetTimestamp() - startTimestamp) *
                          1000d / Stopwatch.Frequency;
            calls++;
            totalMilliseconds += elapsed;
            if (elapsed > maximumMilliseconds) maximumMilliseconds = elapsed;
        }

        public M10SystemTimingSnapshot Capture() =>
            new M10SystemTimingSnapshot(
                SystemId,
                calls,
                totalMilliseconds,
                maximumMilliseconds);
    }

    internal sealed class M10TimedSystem : ISimulationSystem
    {
        private readonly ISimulationSystem inner;
        private readonly M10SystemTimingCounter counter;

        public M10TimedSystem(ISimulationSystem system, M10SystemTimingCounter timingCounter)
        {
            inner = system ?? throw new ArgumentNullException(nameof(system));
            counter = timingCounter ?? throw new ArgumentNullException(nameof(timingCounter));
            if (inner.Id != counter.SystemId)
                throw new ArgumentException("The system timing counter ID does not match the system.");
        }

        public SimulationSystemId Id => inner.Id;

        public void Execute(SimulationWorld world)
        {
            var start = Stopwatch.GetTimestamp();
            inner.Execute(world);
            counter.Record(start);
        }
    }
}
