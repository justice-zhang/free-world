using System;
using System.Numerics;
using Game.Application;
using Game.Content.Runtime;
using Game.Core;
using Game.Simulation;

namespace Game.Infrastructure
{
    internal sealed class M7DemoRunContext
    {
        public RunSession Session;
        public EntityHandle Player;
        public RuntimeMapDefinition Map;
    }

    /// <summary>
    /// Composition-only factory for the checked-in programmatic Placeholder run.
    /// All mechanics remain content/runtime driven.
    /// </summary>
    internal static class M7DemoRunFactory
    {
        private const ulong DemoSeed = 0x4D3750524553454EUL;

        public static Result<M7DemoRunContext> Create(
            ContentRegistry content,
            GameStateMachine stateMachine)
        {
            if (content == null) throw new ArgumentNullException(nameof(content));
            if (stateMachine == null) throw new ArgumentNullException(nameof(stateMachine));
            var mapId = RequireId("test.map.finite_arena");
            var encounterId = RequireId("test.encounter.five_minute");
            var initialSkillId = RequireId("test.skill.single_projectile");
            if (!content.TryGet(mapId, out RuntimeMapDefinition map))
                return Failure("M7 Placeholder map is missing.");
            if (!content.TryGet(encounterId, out RuntimeEncounterSchedule schedule))
                return Failure("M7 Placeholder encounter is missing.");

            var modules = SkillModuleRegistry.CreateDefault();
            var skillsResult = SkillRuntimeCatalog.Build(content, modules);
            if (!skillsResult.IsSuccess) return Result<M7DemoRunContext>.Failure(skillsResult.Error);
            var enemiesResult = EnemyRuntimeCatalog.Build(content);
            if (!enemiesResult.IsSuccess) return Result<M7DemoRunContext>.Failure(enemiesResult.Error);
            var buildsResult = BuildRuntimeCatalog.Build(content, modules);
            if (!buildsResult.IsSuccess) return Result<M7DemoRunContext>.Failure(buildsResult.Error);

            var mapRuntime = MapRuntimeFactory.Create(map, DemoSeed);
            var enemies = new EnemyRuntime(enemiesResult.Value, DifficultySnapshot.Default, 256);
            var skills = new SkillRuntime(skillsResult.Value, DemoSeed, 256);
            var encounter = new EncounterScheduler(
                schedule,
                mapRuntime,
                DifficultySnapshot.Default,
                DemoSeed);
            var world = new SimulationWorld(
                DemoSeed,
                256,
                2f,
                SimulationPipeline.CreateM6Default(),
                null,
                null,
                skills,
                enemies,
                mapRuntime,
                encounter);
            var stats = StatBaseValues.CreateDefault(250f, 6f);
            stats.PickupRange = 5f;
            var player = world.CreateActor(
                SimulationEntityState.Create(Vector2.Zero, Vector2.Zero),
                new ActorCombatInitialization(stats, stats.Health, 0f, 0f, default));
            world.SetPlayer(player);
            var mapTags = new ContentTag[map.Tags.Count];
            for (var index = 0; index < mapTags.Length; index++) mapTags[index] = map.Tags[index];
            var progression = world.InitializeProgression(
                buildsResult.Value,
                player,
                DemoSeed,
                null,
                6,
                6,
                mapTags);
            if (!progression.Build.TryAcquireSkill(initialSkillId))
                return Failure("M7 initial Placeholder skill could not be acquired.");

            return Result<M7DemoRunContext>.Success(
                new M7DemoRunContext
                {
                    Session = new RunSession(world, player, stateMachine),
                    Player = player,
                    Map = map
                });
        }

        private static ContentId RequireId(string value)
        {
            var result = ContentId.Create(value);
            if (!result.IsSuccess) throw new InvalidOperationException(result.Error.ToString());
            return result.Value;
        }

        private static Result<M7DemoRunContext> Failure(string message) =>
            Result<M7DemoRunContext>.Failure(new Error(ErrorCode.MissingReference, message));
    }
}
