using System;
using System.Numerics;
using Game.Content.Runtime;
using Game.Core;

namespace Game.Simulation
{
    /// <summary>Evidence exported by the deterministic ten-minute M6 run.</summary>
    public readonly struct M6HeadlessSummary
    {
        internal M6HeadlessSummary(
            ulong seed,
            long tickCount,
            int level,
            int skillCount,
            int passiveCount,
            int activeSynergyCount,
            in RunStatisticsSnapshot statistics,
            ulong checksum,
            bool entityLeakFree,
            long invalidHandleAccesses)
        {
            Seed = seed;
            TickCount = tickCount;
            Level = level;
            SkillCount = skillCount;
            PassiveCount = passiveCount;
            ActiveSynergyCount = activeSynergyCount;
            Statistics = statistics;
            Checksum = checksum;
            EntityLeakFree = entityLeakFree;
            InvalidHandleAccesses = invalidHandleAccesses;
        }

        public ulong Seed { get; }
        public long TickCount { get; }
        public int Level { get; }
        public int SkillCount { get; }
        public int PassiveCount { get; }
        public int ActiveSynergyCount { get; }
        public RunStatisticsSnapshot Statistics { get; }
        public ulong Checksum { get; }
        public bool EntityLeakFree { get; }
        public long InvalidHandleAccesses { get; }
    }

    /// <summary>Automatic movement, pickup, and upgrade selection for M6 soak coverage.</summary>
    public static class M6HeadlessHarness
    {
        public const int TenMinuteTickCount = 10 * 60 * SimulationClock.TickRate;

        public static Result<M6HeadlessSummary> RunTenMinutes(
            ContentRegistry content,
            ContentId mapId,
            ContentId encounterId,
            ContentId initialSkillId,
            ulong seed,
            DifficultySnapshot? difficulty = null)
        {
            return Run(content, mapId, encounterId, initialSkillId, TenMinuteTickCount, seed, difficulty);
        }

        public static Result<M6HeadlessSummary> Run(
            ContentRegistry content,
            ContentId mapId,
            ContentId encounterId,
            ContentId initialSkillId,
            int tickCount,
            ulong seed,
            DifficultySnapshot? difficulty = null)
        {
            if (content == null) throw new ArgumentNullException(nameof(content));
            if (tickCount < 0) throw new ArgumentOutOfRangeException(nameof(tickCount));
            if (!content.TryGet(mapId, out RuntimeMapDefinition mapDefinition) ||
                !content.TryGet(encounterId, out RuntimeEncounterSchedule schedule))
                return Failure("Harness map or encounter is missing.");

            var modules = SkillModuleRegistry.CreateDefault();
            var skillCatalog = SkillRuntimeCatalog.Build(content, modules);
            if (!skillCatalog.IsSuccess) return Result<M6HeadlessSummary>.Failure(skillCatalog.Error);
            var enemyCatalog = EnemyRuntimeCatalog.Build(content);
            if (!enemyCatalog.IsSuccess) return Result<M6HeadlessSummary>.Failure(enemyCatalog.Error);
            var buildCatalog = BuildRuntimeCatalog.Build(content, modules);
            if (!buildCatalog.IsSuccess) return Result<M6HeadlessSummary>.Failure(buildCatalog.Error);

            var difficultySnapshot = difficulty ?? DifficultySnapshot.Default;
            var map = MapRuntimeFactory.Create(mapDefinition, seed);
            var enemies = new EnemyRuntime(enemyCatalog.Value, difficultySnapshot, 256);
            var skills = new SkillRuntime(skillCatalog.Value, seed, 256);
            var encounter = new EncounterScheduler(schedule, map, difficultySnapshot, seed);
            var world = new SimulationWorld(
                seed,
                256,
                2f,
                SimulationPipeline.CreateM6Default(),
                null,
                null,
                skills,
                enemies,
                map,
                encounter);
            var stats = StatBaseValues.CreateDefault(1_000_000_000f, 7f);
            stats.PickupRange = 8f;
            var player = world.CreateActor(
                SimulationEntityState.Create(Vector2.Zero, Vector2.Zero),
                new ActorCombatInitialization(stats, stats.Health, 0f, 0f, default));
            world.SetPlayer(player);
            var mapTags = new ContentTag[mapDefinition.Tags.Count];
            for (var index = 0; index < mapTags.Length; index++) mapTags[index] = mapDefinition.Tags[index];
            var progression = world.InitializeProgression(buildCatalog.Value, player, seed, null, 6, 6, mapTags);
            if (!progression.Build.TryAcquireSkill(initialSkillId))
                return Failure("Initial harness skill could not be acquired.");

            var runner = new FixedTickRunner(world);
            while (world.Tick < tickCount)
            {
                if (progression.HasPendingChoice)
                {
                    if (progression.CurrentOffers.Count > 0)
                        progression.SelectOffer(progression.CurrentOffers.GetAt(0).Source.Id);
                    else
                        progression.SkipOffer();
                    runner.Clock.Resume();
                }

                ApplyAutomaticMovement(world, player);
                runner.Advance(SimulationClock.TickDurationSeconds);
            }

            var snapshot = progression.Statistics;
            var checksum = Combine(enemies.SpawnChecksum, snapshot.DecisionChecksum, world.Tick, progression.Experience.Level);
            var level = progression.Experience.Level;
            var skillCount = progression.Build.Skills.Count;
            var passiveCount = progression.Build.Passives.Count;
            var synergyCount = progression.Build.ActiveSynergyCount;
            RemoveTransientEntities(world, player);
            var leakFree = world.Actors.Count == 1 && world.Actors.Contains(player) &&
                           world.Projectiles.Count == 0 && world.Areas.Count == 0 &&
                           world.Pickups.Count == 0 && enemies.Count == 0 &&
                           skills.InstanceCount == skillCount &&
                           world.Diagnostics.ActiveEntities == 1;
            return Result<M6HeadlessSummary>.Success(
                new M6HeadlessSummary(
                    seed,
                    world.Tick,
                    level,
                    skillCount,
                    passiveCount,
                    synergyCount,
                    snapshot,
                    checksum,
                    leakFree,
                    world.Diagnostics.InvalidHandleAccesses));
        }

        private static void ApplyAutomaticMovement(SimulationWorld world, EntityHandle player)
        {
            if (!world.Actors.TryRead(player, out var state)) return;
            var target = Vector2.Zero;
            var found = false;
            var bestDistance = float.PositiveInfinity;
            for (var index = 0; index < world.Pickups.Count; index++)
            {
                var candidate = world.Pickups.GetStateAt(index).Position;
                var distance = Vector2.DistanceSquared(state.Position, candidate);
                if (distance >= bestDistance) continue;
                target = candidate;
                bestDistance = distance;
                found = true;
            }
            if (!found)
            {
                var phase = (world.Tick / (SimulationClock.TickRate * 8)) % 4;
                target = phase == 0 ? new Vector2(8f, 8f) :
                         phase == 1 ? new Vector2(-8f, 8f) :
                         phase == 2 ? new Vector2(-8f, -8f) : new Vector2(8f, -8f);
            }
            var offset = target - state.Position;
            state.Velocity = offset.LengthSquared() > 0.04f ? Vector2.Normalize(offset) * 7f : Vector2.Zero;
            world.Actors.TryWrite(player, state);
        }

        private static void RemoveTransientEntities(SimulationWorld world, EntityHandle player)
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

        private static ulong Combine(ulong first, ulong second, long ticks, int level)
        {
            unchecked
            {
                var value = first ^ second;
                value = (value ^ (ulong)ticks) * 1099511628211UL;
                return (value ^ (uint)level) * 1099511628211UL;
            }
        }

        private static Result<M6HeadlessSummary> Failure(string message) =>
            Result<M6HeadlessSummary>.Failure(new Error(ErrorCode.MissingReference, message));
    }
}
