using System.Collections;
using System.IO;
using System.Numerics;
using Game.Application;
using Game.Content.Runtime;
using Game.Core;
using Game.Simulation;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    public sealed class QinglanG23RewardFlowPlayModeTests
    {
        private const ulong Seed = 0x473233504C41594DUL;

        [UnityTest]
        public IEnumerator RelicRewardPausesApplicationChoiceAndResumesAfterCommit()
        {
            var registry = LoadRegistry();
            var modules = SkillModuleRegistry.CreateDefault();
            var skillCatalog = SkillRuntimeCatalog.Build(registry, modules);
            var enemyCatalog = EnemyRuntimeCatalog.Build(registry);
            var buildCatalog = BuildRuntimeCatalog.Build(registry, modules);
            Assert.That(skillCatalog.IsSuccess, Is.True, skillCatalog.Error.ToString());
            Assert.That(enemyCatalog.IsSuccess, Is.True, enemyCatalog.Error.ToString());
            Assert.That(buildCatalog.IsSuccess, Is.True, buildCatalog.Error.ToString());

            var hub = new QinglanRuntimeHub(
                new CharacterMechanicRuntime(4),
                new RewardRuntime(64));
            var world = new SimulationWorld(
                hub,
                Seed,
                128,
                2f,
                SimulationPipeline.CreateQinglanDemo(),
                new RuntimeStatusCatalog(registry),
                null,
                new SkillRuntime(skillCatalog.Value, Seed, 128),
                new EnemyRuntime(enemyCatalog.Value, DifficultySnapshot.Default, 128));
            var player = world.CreateActor(
                SimulationEntityState.Create(Vector2.Zero, Vector2.Zero),
                ActorCombatInitialization.CreateDefault());
            world.SetPlayer(player);
            world.InitializeProgression(buildCatalog.Value, player, Seed);
            var session = new RunSession(world, player, new GameStateMachine());

            Assert.That(world.Qinglan.Rewards.TryQueueDirect(
                Id("qinglan.reward.elite.afflicted_core"),
                new RewardTransactionId(Seed, Id("test.reward.g2_3.playmode"), 0),
                Vector2.Zero), Is.True);
            Assert.That(session.Advance(SimulationClock.TickDurationSeconds), Is.EqualTo(1));
            Assert.That(session.StateMachine.CurrentState, Is.EqualTo(GameState.RewardChoice));
            Assert.That(session.Runner.Clock.IsPaused, Is.True);
            Assert.That(session.CurrentRewardChoice, Is.Not.Null);
            Assert.That(session.CurrentRewardChoice.CandidateIds.Count, Is.EqualTo(3));

            Assert.That(session.SelectRewardAt(0), Is.True);
            Assert.That(session.StateMachine.CurrentState, Is.EqualTo(GameState.InRun));
            Assert.That(session.Runner.Clock.IsPaused, Is.False);
            Assert.That(world.Qinglan.Rewards.HasPendingRelicChoice, Is.False);
            Assert.That(world.Qinglan.Rewards.Relics.Count, Is.EqualTo(1));
            Assert.That(session.Advance(SimulationClock.TickDurationSeconds), Is.EqualTo(1));
            yield return null;
        }

        private static ContentRegistry LoadRegistry()
        {
            var path = Path.Combine(
                UnityEngine.Application.dataPath,
                "GameAssets/Placeholder/QinglanDemo/QinglanDemoContentPack.baked.json");
            var dto = UnityEngine.JsonUtility.FromJson<BakedContentCatalogDto>(File.ReadAllText(path));
            var catalog = dto.ToCatalog();
            Assert.That(catalog.IsSuccess, Is.True, catalog.Error.ToString());
            var registry = new ContentRegistry();
            var loaded = registry.Load(new[] { catalog.Value }, new ContentVersion(0, 1, 0));
            Assert.That(loaded.IsSuccess, Is.True, loaded.Error.ToString());
            return registry;
        }

        private static ContentId Id(string value) => ContentId.Create(value).Value;
    }
}
