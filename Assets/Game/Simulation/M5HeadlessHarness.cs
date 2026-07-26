using System;
using System.Numerics;
using Game.Content.Runtime;
using Game.Core;

namespace Game.Simulation
{
    /// <summary>Evidence exported by the five-minute map/encounter harness.</summary>
    public readonly struct M5HeadlessSummary
    {
        internal M5HeadlessSummary(
            ulong seed,
            long tickCount,
            long spawnedEnemies,
            int bossSpawnCount,
            int peakEnemyCount,
            ulong spawnChecksum,
            bool positionsFinite,
            bool concurrencyRespected,
            bool entityLeakFree,
            long invalidHandleAccesses)
        {
            Seed = seed;
            TickCount = tickCount;
            SpawnedEnemies = spawnedEnemies;
            BossSpawnCount = bossSpawnCount;
            PeakEnemyCount = peakEnemyCount;
            SpawnChecksum = spawnChecksum;
            PositionsFinite = positionsFinite;
            ConcurrencyRespected = concurrencyRespected;
            EntityLeakFree = entityLeakFree;
            InvalidHandleAccesses = invalidHandleAccesses;
        }

        public ulong Seed { get; }
        public long TickCount { get; }
        public long SpawnedEnemies { get; }
        public int BossSpawnCount { get; }
        public int PeakEnemyCount { get; }
        public ulong SpawnChecksum { get; }
        public bool PositionsFinite { get; }
        public bool ConcurrencyRespected { get; }
        public bool EntityLeakFree { get; }
        public long InvalidHandleAccesses { get; }
    }

    /// <summary>Runs M5 maps and encounters without scenes, engine objects, or presentation.</summary>
    public static class M5HeadlessHarness
    {
        public const int FiveMinuteTickCount = 5 * 60 * SimulationClock.TickRate;

        public static Result<M5HeadlessSummary> RunFiveMinutes(
            ContentRegistry content,
            ContentId mapId,
            ContentId encounterId,
            ulong seed,
            DifficultySnapshot? difficulty = null)
        {
            return Run(content, mapId, encounterId, FiveMinuteTickCount, seed, difficulty);
        }

        public static Result<M5HeadlessSummary> Run(
            ContentRegistry content,
            ContentId mapId,
            ContentId encounterId,
            int tickCount,
            ulong seed,
            DifficultySnapshot? difficulty = null)
        {
            if (content == null) throw new ArgumentNullException(nameof(content));
            if (tickCount < 0) throw new ArgumentOutOfRangeException(nameof(tickCount));
            if (!content.TryGet(mapId, out RuntimeMapDefinition mapDefinition) ||
                !content.TryGet(encounterId, out RuntimeEncounterSchedule schedule))
            {
                return Result<M5HeadlessSummary>.Failure(
                    new Error(ErrorCode.MissingReference, "Harness map or encounter is missing."));
            }

            var modules = SkillModuleRegistry.CreateDefault();
            var skillCatalog = SkillRuntimeCatalog.Build(content, modules);
            if (!skillCatalog.IsSuccess) return Result<M5HeadlessSummary>.Failure(skillCatalog.Error);
            var enemyCatalog = EnemyRuntimeCatalog.Build(content);
            if (!enemyCatalog.IsSuccess) return Result<M5HeadlessSummary>.Failure(enemyCatalog.Error);

            var difficultySnapshot = difficulty ?? DifficultySnapshot.Default;
            var map = MapRuntimeFactory.Create(mapDefinition, seed);
            var enemies = new EnemyRuntime(enemyCatalog.Value, difficultySnapshot, 128);
            var skills = new SkillRuntime(skillCatalog.Value, seed, 128);
            var encounter = new EncounterScheduler(schedule, map, difficultySnapshot, seed);
            var world = new SimulationWorld(
                seed,
                128,
                2f,
                SimulationPipeline.CreateM5Default(),
                null,
                null,
                skills,
                enemies,
                map,
                encounter);
            var player = world.CreateActor(
                SimulationEntityState.Create(Vector2.Zero, Vector2.Zero),
                ActorCombatInitialization.CreateDefault(1_000_000_000f, 5f));
            world.SetPlayer(player);
            var runner = new FixedTickRunner(world);
            var peakEnemies = 0;
            var positionsFinite = true;
            var concurrencyRespected = true;
            for (var tick = 0; tick < tickCount; tick++)
            {
                runner.Advance(SimulationClock.TickDurationSeconds);
                peakEnemies = Math.Max(peakEnemies, enemies.Count);
                concurrencyRespected &= enemies.Count + enemies.PendingSpawns.Count <=
                                        schedule.MaximumConcurrentEnemies;
                for (var actorIndex = 0; actorIndex < world.Actors.Count; actorIndex++)
                {
                    var position = world.Actors.GetStateAt(actorIndex).Position;
                    positionsFinite &= Finite(position) && map.IsWalkable(position);
                }
            }

            var completedTicks = world.Tick;
            RemoveHarnessEntities(world, player);
            var leakFree = world.Actors.Count == 1 && world.Actors.Contains(player) &&
                           world.Projectiles.Count == 0 && world.Areas.Count == 0 &&
                           world.Pickups.Count == 0 && enemies.Count == 0 &&
                           skills.InstanceCount == 0 &&
                           world.Diagnostics.ActiveEntities == 1;
            return Result<M5HeadlessSummary>.Success(
                new M5HeadlessSummary(
                    seed,
                    completedTicks,
                    enemies.SpawnedCount,
                    enemies.BossSpawnedCount,
                    peakEnemies,
                    enemies.SpawnChecksum,
                    positionsFinite,
                    concurrencyRespected,
                    leakFree,
                    world.Diagnostics.InvalidHandleAccesses));
        }

        private static void RemoveHarnessEntities(SimulationWorld world, EntityHandle player)
        {
            for (var index = 0; index < world.Actors.Count; index++)
            {
                var handle = world.Actors.GetHandleAt(index);
                if (handle != player) world.Commands.Remove(EntityKind.Actor, handle);
            }

            for (var index = 0; index < world.Projectiles.Count; index++)
                world.Commands.Remove(EntityKind.Projectile, world.Projectiles.GetHandleAt(index));
            for (var index = 0; index < world.Areas.Count; index++)
                world.Commands.Remove(EntityKind.Area, world.Areas.GetHandleAt(index));
            for (var index = 0; index < world.Pickups.Count; index++)
                world.Commands.Remove(EntityKind.Pickup, world.Pickups.GetHandleAt(index));
            new CleanupSystem().Execute(world);
        }

        private static bool Finite(Vector2 value) =>
            !float.IsNaN(value.X) && !float.IsInfinity(value.X) &&
            !float.IsNaN(value.Y) && !float.IsInfinity(value.Y);
    }
}
