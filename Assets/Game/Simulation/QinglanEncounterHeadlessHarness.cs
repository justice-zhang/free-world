using System;
using System.Numerics;
using Game.Content.Runtime;
using Game.Core;

namespace Game.Simulation
{
    internal readonly struct QinglanEncounterMinuteSummary
    {
        public QinglanEncounterMinuteSummary(
            int minute,
            long spawnedEnemies,
            long deaths,
            int eliteSpawns,
            int peakEnemies)
        {
            Minute = minute;
            SpawnedEnemies = spawnedEnemies;
            Deaths = deaths;
            EliteSpawns = eliteSpawns;
            PeakEnemies = peakEnemies;
        }

        public int Minute { get; }
        public long SpawnedEnemies { get; }
        public long Deaths { get; }
        public int EliteSpawns { get; }
        public int PeakEnemies { get; }
    }

    internal sealed class QinglanEncounterHeadlessSummary
    {
        public QinglanEncounterHeadlessSummary(
            ulong seed,
            long tickCount,
            long spawnedEnemies,
            long deaths,
            int eliteSpawns,
            int affixedSpawns,
            int bossSpawns,
            int peakEnemies,
            ulong spawnChecksum,
            ulong deathChecksum,
            ulong combinedChecksum,
            bool positionsFinite,
            bool concurrencyRespected,
            bool stoppedAfterDuration,
            bool budgetCleared,
            bool entityLeakFree,
            long invalidHandleAccesses,
            QinglanEncounterMinuteSummary[] minutes)
        {
            Seed = seed;
            TickCount = tickCount;
            SpawnedEnemies = spawnedEnemies;
            Deaths = deaths;
            EliteSpawns = eliteSpawns;
            AffixedSpawns = affixedSpawns;
            BossSpawns = bossSpawns;
            PeakEnemies = peakEnemies;
            SpawnChecksum = spawnChecksum;
            DeathChecksum = deathChecksum;
            CombinedChecksum = combinedChecksum;
            PositionsFinite = positionsFinite;
            ConcurrencyRespected = concurrencyRespected;
            StoppedAfterDuration = stoppedAfterDuration;
            BudgetCleared = budgetCleared;
            EntityLeakFree = entityLeakFree;
            InvalidHandleAccesses = invalidHandleAccesses;
            Minutes = minutes ?? Array.Empty<QinglanEncounterMinuteSummary>();
        }

        public ulong Seed { get; }
        public long TickCount { get; }
        public long SpawnedEnemies { get; }
        public long Deaths { get; }
        public int EliteSpawns { get; }
        public int AffixedSpawns { get; }
        public int BossSpawns { get; }
        public int PeakEnemies { get; }
        public ulong SpawnChecksum { get; }
        public ulong DeathChecksum { get; }
        public ulong CombinedChecksum { get; }
        public bool PositionsFinite { get; }
        public bool ConcurrencyRespected { get; }
        public bool StoppedAfterDuration { get; }
        public bool BudgetCleared { get; }
        public bool EntityLeakFree { get; }
        public long InvalidHandleAccesses { get; }
        public QinglanEncounterMinuteSummary[] Minutes { get; }
    }

    /// <summary>Runs the checked-in Qinglan timeline without Scene or presentation state.</summary>
    internal static class QinglanEncounterHeadlessHarness
    {
        public const int TwelveMinuteTickCount = 12 * 60 * SimulationClock.TickRate;
        private const int ClearCadenceTicks = 2 * SimulationClock.TickRate;
        private const int EphemeralClearCadenceTicks = SimulationClock.TickRate;
        private const int PostStopVerificationTicks = SimulationClock.TickRate;
        private static readonly ContentId ClearSourceId = Id("test.encounter.g1_6.clear");

        public static Result<QinglanEncounterHeadlessSummary> Run(
            ContentRegistry content,
            ContentId encounterId,
            ulong seed)
        {
            if (content == null) throw new ArgumentNullException(nameof(content));
            if (!content.TryGet(encounterId, out RuntimeEncounterSchedule schedule))
            {
                return Result<QinglanEncounterHeadlessSummary>.Failure(
                    new Error(ErrorCode.MissingReference, "Qinglan encounter is missing.", encounterId));
            }

            var skillCatalog = SkillRuntimeCatalog.Build(content, SkillModuleRegistry.CreateDefault());
            if (!skillCatalog.IsSuccess)
                return Result<QinglanEncounterHeadlessSummary>.Failure(skillCatalog.Error);
            var enemyCatalog = EnemyRuntimeCatalog.Build(content);
            if (!enemyCatalog.IsSuccess)
                return Result<QinglanEncounterHeadlessSummary>.Failure(enemyCatalog.Error);

            var capacity = Math.Max(128, schedule.MaximumConcurrentEnemies + 32);
            var mapDefinition = CreateMap(schedule);
            var map = MapRuntimeFactory.Create(mapDefinition, seed);
            var enemies = new EnemyRuntime(enemyCatalog.Value, DifficultySnapshot.Default, capacity);
            var skills = new SkillRuntime(skillCatalog.Value, seed, capacity);
            var scheduler = new EncounterScheduler(schedule, map, DifficultySnapshot.Default, seed);
            var world = new SimulationWorld(
                new QinglanRuntimeHub(),
                seed,
                capacity,
                4f,
                CreateTimelinePipeline(),
                new RuntimeStatusCatalog(content),
                null,
                skills,
                enemies,
                map,
                scheduler);
            var player = world.CreateActor(
                SimulationEntityState.Create(Vector2.Zero, Vector2.Zero),
                ActorCombatInitialization.CreateDefault(1_000_000_000f, 0f));
            world.SetPlayer(player);
            var runner = new FixedTickRunner(world);
            var minutes = new QinglanEncounterMinuteSummary[12];
            var deaths = 0L;
            var peakEnemies = 0;
            var positionsFinite = true;
            var concurrencyRespected = true;
            var deathChecksum = 1469598103934665603UL;

            for (var tick = 0; tick < TwelveMinuteTickCount; tick++)
            {
                if (tick > 0 && tick % ClearCadenceTicks == 0)
                    QueueEnemyDeaths(world, player);
                if (tick > 0 && tick % EphemeralClearCadenceTicks == 0)
                    QueueEphemeralCleanup(world);

                runner.Advance(SimulationClock.TickDurationSeconds);
                peakEnemies = Math.Max(peakEnemies, enemies.Count);
                concurrencyRespected &= enemies.Count + enemies.PendingSpawns.Count <=
                                        schedule.MaximumConcurrentEnemies;
                positionsFinite &= ArePositionsFiniteAndWalkable(world, map);
                for (var deathIndex = 0; deathIndex < world.CombatEvents.EntityDiedCount; deathIndex++)
                {
                    var died = world.CombatEvents.GetEntityDiedAt(deathIndex);
                    if (died.Target.Kind != EntityKind.Actor || died.Target.Handle == player)
                        continue;
                    deaths++;
                    AppendDeathChecksum(ref deathChecksum, died);
                }

                if ((tick + 1) % (60 * SimulationClock.TickRate) == 0)
                {
                    var minute = (tick + 1) / (60 * SimulationClock.TickRate);
                    minutes[minute - 1] = new QinglanEncounterMinuteSummary(
                        minute,
                        enemies.SpawnedCount,
                        deaths,
                        enemies.EliteSpawnedCount,
                        peakEnemies);
                }
            }

            var measuredSpawned = enemies.SpawnedCount;
            var measuredElites = enemies.EliteSpawnedCount;
            var measuredAffixed = enemies.AffixedSpawnedCount;
            var measuredBosses = enemies.BossSpawnedCount;
            var measuredSpawnChecksum = enemies.SpawnChecksum;
            for (var tick = 0; tick < PostStopVerificationTicks; tick++)
                scheduler.Tick(world);
            var stopped = enemies.SpawnedCount == measuredSpawned && enemies.PendingSpawns.Count == 0;
            var budgetCleared = scheduler.AccumulatedBudget == 0f;
            var combinedChecksum = Combine(
                measuredSpawnChecksum,
                deathChecksum,
                measuredSpawned,
                deaths,
                measuredElites);

            QueueAllNonPlayerCleanup(world, player);
            new CleanupSystem().Execute(world);
            var leakFree = world.Actors.Count == 1 && world.Actors.Contains(player) &&
                           world.Projectiles.Count == 0 && world.Areas.Count == 0 &&
                           world.Pickups.Count == 0 && enemies.Count == 0 &&
                           skills.InstanceCount == 0 && world.Diagnostics.ActiveEntities == 1;

            return Result<QinglanEncounterHeadlessSummary>.Success(
                new QinglanEncounterHeadlessSummary(
                    seed,
                    world.Tick,
                    measuredSpawned,
                    deaths,
                    measuredElites,
                    measuredAffixed,
                    measuredBosses,
                    peakEnemies,
                    measuredSpawnChecksum,
                    deathChecksum,
                    combinedChecksum,
                    positionsFinite,
                    concurrencyRespected,
                    stopped,
                    budgetCleared,
                    leakFree,
                    world.Diagnostics.InvalidHandleAccesses,
                    minutes));
        }

        private static SimulationPipeline CreateTimelinePipeline()
        {
            return new SimulationPipeline(
                new SpawnSchedulerSystem(),
                new EnemyDecisionSystem(),
                new MovementSystem(),
                new DamageResolutionSystem(),
                new DeathSystem(),
                new LifetimeSystem(),
                new CleanupSystem(),
                new EventFlushSystem(),
                new SnapshotBuildSystem());
        }

        private static RuntimeMapDefinition CreateMap(RuntimeEncounterSchedule schedule)
        {
            var bossRuleCount = 0;
            for (var phaseIndex = 0; phaseIndex < schedule.Phases.Count; phaseIndex++)
                bossRuleCount += schedule.Phases[phaseIndex].BossRules.Count;
            var anchors = new RuntimeMapAnchor[bossRuleCount];
            var anchorCount = 0;
            for (var phaseIndex = 0; phaseIndex < schedule.Phases.Count; phaseIndex++)
            {
                var rules = schedule.Phases[phaseIndex].BossRules;
                for (var ruleIndex = 0; ruleIndex < rules.Count; ruleIndex++)
                {
                    var id = rules[ruleIndex].AnchorId;
                    var duplicate = false;
                    for (var existing = 0; existing < anchorCount; existing++)
                    {
                        if (anchors[existing].Id != id) continue;
                        duplicate = true;
                        break;
                    }
                    if (duplicate || !id.IsValid) continue;
                    var angle = anchorCount == 0
                        ? 0d
                        : (anchorCount - 1) * Math.PI * 2d / Math.Max(1, bossRuleCount - 1);
                    var position = anchorCount == 0
                        ? Vector2.Zero
                        : new Vector2((float)Math.Cos(angle) * 27f, (float)Math.Sin(angle) * 27f);
                    anchors[anchorCount++] = new RuntimeMapAnchor(id, position);
                }
            }
            if (anchorCount != anchors.Length) Array.Resize(ref anchors, anchorCount);
            return new RuntimeMapDefinition(
                Id("test.encounter.map.g1_6_harness"),
                "content.test.encounter.map.g1_6_harness.name",
                "content.test.encounter.map.g1_6_harness.description",
                "Assets/Test/QinglanG16HarnessMap.asset",
                Array.Empty<ContentTag>(),
                "base.map.finite_arena",
                "maps/test/encounter_g1_6_harness",
                MapBoundsMode.Finite,
                new Vector2(-64f, -48f),
                new Vector2(64f, 48f),
                32f,
                2,
                schedule.Id,
                Id("placeholder.presentation.encounter.map.g1_6_harness"),
                Array.Empty<RuntimeMapObstacle>(),
                anchors);
        }

        private static void QueueEnemyDeaths(SimulationWorld world, EntityHandle player)
        {
            for (var dense = 0; dense < world.Actors.Count; dense++)
            {
                var handle = world.Actors.GetHandleAt(dense);
                if (!world.Enemies.IsEnemy(handle)) continue;
                world.QueueDamage(
                    new DamagePacket(
                        new SpatialEntity(EntityKind.Actor, player),
                        new SpatialEntity(EntityKind.Actor, handle),
                        ClearSourceId,
                        DamageType.True,
                        DamageTags.Direct,
                        1_000_000_000f,
                        false,
                        0f,
                        Vector2.Zero,
                        Vector2.Zero,
                        0));
            }
        }

        private static void QueueEphemeralCleanup(SimulationWorld world)
        {
            for (var index = 0; index < world.Projectiles.Count; index++)
                world.Commands.Remove(EntityKind.Projectile, world.Projectiles.GetHandleAt(index));
            for (var index = 0; index < world.Areas.Count; index++)
                world.Commands.Remove(EntityKind.Area, world.Areas.GetHandleAt(index));
            for (var index = 0; index < world.Pickups.Count; index++)
                world.Commands.Remove(EntityKind.Pickup, world.Pickups.GetHandleAt(index));
        }

        private static void QueueAllNonPlayerCleanup(SimulationWorld world, EntityHandle player)
        {
            for (var index = 0; index < world.Actors.Count; index++)
            {
                var handle = world.Actors.GetHandleAt(index);
                if (handle != player) world.Commands.Remove(EntityKind.Actor, handle);
            }
            QueueEphemeralCleanup(world);
        }

        private static bool ArePositionsFiniteAndWalkable(SimulationWorld world, IMapRuntime map)
        {
            for (var dense = 0; dense < world.Actors.Count; dense++)
            {
                var position = world.Actors.GetStateAt(dense).Position;
                if (!Finite(position) || !map.IsWalkable(position)) return false;
            }
            return true;
        }

        private static void AppendDeathChecksum(ref ulong checksum, in EntityDied died)
        {
            unchecked
            {
                checksum ^= (uint)died.Target.Handle.Index;
                checksum *= 1099511628211UL;
                checksum ^= (uint)died.Target.Handle.Generation;
                checksum *= 1099511628211UL;
                checksum ^= (uint)BitConverter.SingleToInt32Bits(died.Position.X);
                checksum *= 1099511628211UL;
                checksum ^= (uint)BitConverter.SingleToInt32Bits(died.Position.Y);
                checksum *= 1099511628211UL;
                checksum ^= (ulong)died.Tick;
                checksum *= 1099511628211UL;
            }
        }

        private static ulong Combine(
            ulong spawnChecksum,
            ulong deathChecksum,
            long spawned,
            long deaths,
            int elites)
        {
            unchecked
            {
                var value = spawnChecksum;
                value ^= deathChecksum;
                value *= 1099511628211UL;
                value ^= (ulong)spawned;
                value *= 1099511628211UL;
                value ^= (ulong)deaths;
                value *= 1099511628211UL;
                value ^= (uint)elites;
                value *= 1099511628211UL;
                return value;
            }
        }

        private static bool Finite(Vector2 value) =>
            !float.IsNaN(value.X) && !float.IsInfinity(value.X) &&
            !float.IsNaN(value.Y) && !float.IsInfinity(value.Y);

        private static ContentId Id(string value)
        {
            var result = ContentId.Create(value);
            if (!result.IsSuccess) throw new InvalidOperationException(result.Error.ToString());
            return result.Value;
        }
    }
}
