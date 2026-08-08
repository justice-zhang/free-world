using System;
using System.Numerics;
using Game.Application;
using Game.Content.Runtime;
using Game.Core;
using Game.Simulation;

namespace Game.Infrastructure
{
    /// <summary>Assembles the checked-in Qinglan Demo from stable content definitions.</summary>
    public sealed class QinglanDemoRunFactory : IRunSessionFactory
    {
        public const string CharacterId = "qinglan.character.lu_qingye";
        public const string MapId = "qinglan.map.old_court";
        public const string DifficultyId = "base.difficulty.normal";
        private readonly GameApplication application;

        public QinglanDemoRunFactory(GameApplication gameApplication)
        {
            application = gameApplication ?? throw new ArgumentNullException(nameof(gameApplication));
        }

        /// <summary>Freezes one selected Demo run identity before loading begins.</summary>
        public Result<RunDescriptor> CreateDescriptor(ulong runId, ulong seed)
        {
            var characterId = RequireId(CharacterId);
            var mapId = RequireId(MapId);
            if (!application.ContentRegistry.TryGet(characterId, out RuntimeCharacterDefinition _))
                return DescriptorFailure("Qinglan Demo character is missing.", characterId);
            if (!application.ContentRegistry.TryGet(mapId, out RuntimeMapDefinition map) || !map.HasM5Data)
                return DescriptorFailure("Qinglan Demo map is missing or not executable.", mapId);
            if (!application.ContentRegistry.TryGet(
                    map.EncounterScheduleId,
                    out RuntimeEncounterSchedule schedule))
                return DescriptorFailure("Qinglan Demo encounter is missing.", map.EncounterScheduleId);

            var bossCount = 0;
            var victoryBossId = default(ContentId);
            for (var phaseIndex = 0; phaseIndex < schedule.Phases.Count; phaseIndex++)
            {
                var rules = schedule.Phases[phaseIndex].BossRules;
                for (var ruleIndex = 0; ruleIndex < rules.Count; ruleIndex++)
                {
                    if (!rules[ruleIndex].BossDefinitionId.IsValid) continue;
                    bossCount++;
                    victoryBossId = rules[ruleIndex].BossDefinitionId;
                }
            }
            if (bossCount == 0 || !victoryBossId.IsValid)
                return DescriptorFailure("Qinglan Demo encounter has no executable victory Boss.", schedule.Id);

            var packs = new RunPackSnapshot[application.LoadedRunPacks.Count];
            for (var index = 0; index < packs.Length; index++)
                packs[index] = application.LoadedRunPacks[index];
            try
            {
                return Result<RunDescriptor>.Success(new RunDescriptor(
                    runId,
                    seed,
                    characterId,
                    mapId,
                    RequireId(DifficultyId),
                    bossCount,
                    victoryBossId,
                    packs));
            }
            catch (ArgumentException exception)
            {
                return Result<RunDescriptor>.Failure(new Error(
                    ErrorCode.InvalidCatalog,
                    exception.Message,
                    mapId));
            }
        }

        /// <inheritdoc />
        public Result<IRunSessionHandle> Create(
            RunDescriptor descriptor,
            GameStateMachine stateMachine)
        {
            if (descriptor == null) throw new ArgumentNullException(nameof(descriptor));
            if (stateMachine == null) throw new ArgumentNullException(nameof(stateMachine));
            var content = application.ContentRegistry;
            if (!content.TryGet(descriptor.CharacterId, out RuntimeCharacterDefinition character))
                return Failure("Selected character is unavailable.", descriptor.CharacterId);
            if (!content.TryGet(descriptor.MapId, out RuntimeMapDefinition map) || !map.HasM5Data)
                return Failure("Selected map is unavailable or not executable.", descriptor.MapId);
            if (!content.TryGet(map.EncounterScheduleId, out RuntimeEncounterSchedule schedule))
                return Failure("Selected map encounter is unavailable.", map.EncounterScheduleId);

            var modules = SkillModuleRegistry.CreateDefault();
            var skillsResult = SkillRuntimeCatalog.Build(content, modules);
            if (!skillsResult.IsSuccess) return Result<IRunSessionHandle>.Failure(skillsResult.Error);
            var enemiesResult = EnemyRuntimeCatalog.Build(content);
            if (!enemiesResult.IsSuccess) return Result<IRunSessionHandle>.Failure(enemiesResult.Error);
            var buildsResult = BuildRuntimeCatalog.Build(content, modules);
            if (!buildsResult.IsSuccess) return Result<IRunSessionHandle>.Failure(buildsResult.Error);

            var capacity = Math.Max(256, schedule.MaximumConcurrentEnemies + 64);
            var difficulty = DifficultySnapshot.Default;
            var mapRuntime = MapRuntimeFactory.Create(map, descriptor.Seed);
            var enemies = new EnemyRuntime(enemiesResult.Value, difficulty, capacity);
            var skills = new SkillRuntime(skillsResult.Value, descriptor.Seed, capacity);
            var encounter = new EncounterScheduler(schedule, mapRuntime, difficulty, descriptor.Seed);
            var hub = new QinglanRuntimeHub();
            var mapInitialization = hub.MapObjectives.Initialize(content, map.Id, descriptor.RunId);
            if (!mapInitialization.IsSuccess)
                return Result<IRunSessionHandle>.Failure(mapInitialization.Error);
            var world = new SimulationWorld(
                hub,
                descriptor.Seed,
                capacity,
                2f,
                SimulationPipeline.CreateQinglanDemo(),
                new RuntimeStatusCatalog(content),
                null,
                skills,
                enemies,
                mapRuntime,
                encounter);

            var stats = StatBaseValues.CreateDefault(character.BaseMaxHealth, character.MoveSpeed);
            var player = world.CreateActor(
                SimulationEntityState.Create(Vector2.Zero, Vector2.Zero),
                new ActorCombatInitialization(stats, character.BaseMaxHealth, 0f, 0f, default));
            world.SetPlayer(player);
            var mapTags = new ContentTag[map.Tags.Count];
            for (var index = 0; index < mapTags.Length; index++) mapTags[index] = map.Tags[index];
            var progression = world.InitializeProgression(
                buildsResult.Value,
                player,
                descriptor.Seed,
                descriptor.RunId,
                null,
                6,
                6,
                mapTags);
            for (var index = 0; index < character.StartingSkillIds.Count; index++)
            {
                if (!progression.Build.TryAcquireSkill(character.StartingSkillIds[index]))
                {
                    DisposeWorld(world);
                    return Failure(
                        "A selected character starting skill could not be acquired.",
                        character.StartingSkillIds[index]);
                }
            }
            for (var index = 0; index < character.MechanicIds.Count; index++)
            {
                if (!content.TryGet(character.MechanicIds[index], out ContentRegistryEntry entry) ||
                    !(entry.Definition is RuntimeCharacterMechanicDefinition mechanic) ||
                    !hub.Mechanics.TryAttach(player, entry.Index, mechanic))
                {
                    DisposeWorld(world);
                    return Failure(
                        "A selected character mechanic could not be attached.",
                        character.MechanicIds[index]);
                }
            }

            return Result<IRunSessionHandle>.Success(
                new QinglanDemoRunHandle(
                    world,
                    new RunSession(world, player, stateMachine, descriptor)));
        }

        private static Result<RunDescriptor> DescriptorFailure(string message, ContentId ownerId) =>
            Result<RunDescriptor>.Failure(new Error(ErrorCode.MissingReference, message, ownerId));

        private static Result<IRunSessionHandle> Failure(string message, ContentId ownerId) =>
            Result<IRunSessionHandle>.Failure(new Error(ErrorCode.MissingReference, message, ownerId));

        private static ContentId RequireId(string value) => ContentId.Create(value).Value;

        internal static void DisposeWorld(SimulationWorld world)
        {
            if (world == null) return;
            new CleanupSystem().Execute(world);
            QueueAll(world);
            new CleanupSystem().Execute(world);
            if (world.Actors.Count > 0 || world.Projectiles.Count > 0 ||
                world.Areas.Count > 0 || world.Pickups.Count > 0)
            {
                QueueAll(world);
                new CleanupSystem().Execute(world);
            }
        }

        private static void QueueAll(SimulationWorld world)
        {
            for (var index = 0; index < world.Projectiles.Count; index++)
                world.Commands.Remove(EntityKind.Projectile, world.Projectiles.GetHandleAt(index));
            for (var index = 0; index < world.Areas.Count; index++)
                world.Commands.Remove(EntityKind.Area, world.Areas.GetHandleAt(index));
            for (var index = 0; index < world.Pickups.Count; index++)
                world.Commands.Remove(EntityKind.Pickup, world.Pickups.GetHandleAt(index));
            for (var index = 0; index < world.Actors.Count; index++)
                world.Commands.Remove(EntityKind.Actor, world.Actors.GetHandleAt(index));
        }
    }

    internal sealed class QinglanDemoRunHandle : IRunSessionHandle
    {
        private SimulationWorld world;

        public QinglanDemoRunHandle(SimulationWorld simulationWorld, RunSession session)
        {
            world = simulationWorld ?? throw new ArgumentNullException(nameof(simulationWorld));
            Session = session ?? throw new ArgumentNullException(nameof(session));
        }

        public RunSession Session { get; }
        public bool IsDisposed { get; private set; }
        internal int ActiveEntityCount => world == null ? 0 :
            world.Actors.Count + world.Projectiles.Count + world.Areas.Count + world.Pickups.Count;

        public void Dispose()
        {
            if (IsDisposed) return;
            QinglanDemoRunFactory.DisposeWorld(world);
            world = null;
            IsDisposed = true;
        }
    }
}
