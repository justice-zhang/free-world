using System;
using System.Numerics;
using Game.Application;
using Game.Content.Authoring;
using Game.Content.Runtime;
using Game.Core;
using Game.Editor;
using Game.Infrastructure;
using Game.Platform.Null;
using Game.Simulation;
using NUnit.Framework;
using UnityEditor;

namespace Game.Tests.EditMode
{
    public sealed class QinglanG24RunFlowTests
    {
        private const ulong RunId = 0x47323452554E3031UL;
        private const ulong Seed = 0x4732345345454431UL;
        private static readonly ContentVersion GameVersion = new ContentVersion(0, 1, 0);

        [Test]
        public void DescriptorAndFactoryAssembleCheckedInDemoFromStableContent()
        {
            var application = CreateApplication();
            var stateMachine = new GameStateMachine();
            var factory = new QinglanDemoRunFactory(application);
            var descriptorResult = factory.CreateDescriptor(RunId, Seed);

            Assert.That(descriptorResult.IsSuccess, Is.True, descriptorResult.Error.ToString());
            var descriptor = descriptorResult.Value;
            Assert.That(descriptor.CharacterId, Is.EqualTo(Id(QinglanDemoRunFactory.CharacterId)));
            Assert.That(descriptor.MapId, Is.EqualTo(Id(QinglanDemoRunFactory.MapId)));
            Assert.That(descriptor.DifficultyId, Is.EqualTo(Id(QinglanDemoRunFactory.DifficultyId)));
            Assert.That(descriptor.RequiredBossDefeats, Is.EqualTo(2));
            Assert.That(descriptor.VictoryBossId, Is.EqualTo(Id("qinglan.boss.tingfeng")));
            Assert.That(descriptor.LoadedPacks.Count, Is.EqualTo(1));
            Assert.That(descriptor.LoadedPacks[0].Version, Is.EqualTo(new ContentVersion(0, 9, 0)));
            Assert.That(descriptor.LoadedPacks[0].ContentHash, Has.Length.EqualTo(64));

            var created = factory.Create(descriptor, stateMachine);
            Assert.That(created.IsSuccess, Is.True, created.Error.ToString());
            var handle = (QinglanDemoRunHandle)created.Value;
            Assert.That(handle.Session.Descriptor, Is.SameAs(descriptor));
            Assert.That(handle.ActiveEntityCount, Is.GreaterThan(0));
            Assert.That(handle.Session.End(RunEndReason.Abandoned), Is.True);
            Assert.That(handle.Session.Result.SkillCount, Is.GreaterThanOrEqualTo(1));
            Assert.That(handle.Session.Result.Descriptor.LoadedPacks[0].ContentHash,
                Is.EqualTo(descriptor.LoadedPacks[0].ContentHash));

            handle.Dispose();
            handle.Dispose();
            Assert.That(handle.IsDisposed, Is.True);
            Assert.That(handle.ActiveEntityCount, Is.Zero);
        }

        [Test]
        public void RunResultFreezesBuildMapStatisticsAndRewardDelta()
        {
            var application = CreateApplication();
            var descriptor = new QinglanDemoRunFactory(application).CreateDescriptor(RunId, Seed).Value;
            var fixture = CreateSession(application.ContentRegistry, descriptor);
            var objectiveId = Id("test.objective.g2_4.result");
            Assert.That(fixture.World.Qinglan.MapObjectives.TryAdd(objectiveId), Is.True);
            Assert.That(TransitionToCompleted(fixture.World.Qinglan.MapObjectives, objectiveId), Is.True);
            Assert.That(fixture.World.Progression.Build.TryAcquireSkill(
                Id("qinglan.skill.weapon.yufeng_sword")), Is.True);
            fixture.World.Progression.RecordEnemyDefeat(0f, Vector2.Zero, true, false);
            fixture.World.Progression.RecordEnemyDefeat(0f, Vector2.Zero, false, true);

            var rewardSource = Id("test.reward.g2_4.result");
            Assert.That(fixture.World.Qinglan.Rewards.TryQueueDirect(
                Id("qinglan.reward.first_clear.tingfeng"),
                new RewardTransactionId(descriptor.RunId, rewardSource, 0),
                Vector2.Zero), Is.True);
            new RewardResolutionSystem().Execute(fixture.World);
            Assert.That(fixture.Session.End(RunEndReason.Completed), Is.True);

            var result = fixture.Session.Result;
            Assert.That(result.Outcome, Is.EqualTo(RunOutcome.Victory));
            Assert.That(result.IsVictory, Is.True);
            Assert.That(result.Descriptor, Is.SameAs(descriptor));
            Assert.That(result.Build.Skills.Count, Is.EqualTo(1));
            Assert.That(result.Build.Skills[0].ContentId,
                Is.EqualTo(Id("qinglan.skill.weapon.yufeng_sword")));
            Assert.That(result.Exploration.CompletedObjectiveIds, Does.Contain(objectiveId));
            Assert.That(result.Statistics.EnemyDefeats, Is.EqualTo(2));
            Assert.That(result.Statistics.EliteDefeats, Is.EqualTo(1));
            Assert.That(result.Statistics.BossDefeats, Is.EqualTo(1));
            Assert.That(result.Delta.TransactionId.Value,
                Is.EqualTo("run.result." + RunId.ToString("x16")));
            Assert.That(result.Delta.CurrencyDeltas.Count, Is.GreaterThan(0));
            Assert.That(result.Delta.CurrencyDeltas[0].Value, Is.GreaterThan(0));
            Assert.That(result.ObjectiveChecksum, Is.Not.Zero);
            Assert.That(result.BossChecksum, Is.Not.Zero);

            fixture.World.Progression.Build.TryAcquireSkill(Id("qinglan.skill.weapon.yellow_talisman"));
            fixture.World.Qinglan.MapObjectives.TryAdd(Id("test.objective.g2_4.late"));
            Assert.That(result.Build.Skills.Count, Is.EqualTo(1), "The frozen build must not alias live state.");
            Assert.That(result.Exploration.CompletedObjectiveIds.Count, Is.EqualTo(1));
        }

        [TestCase(RunEndReason.Completed, RunOutcome.Victory)]
        [TestCase(RunEndReason.PlayerDefeated, RunOutcome.Defeat)]
        [TestCase(RunEndReason.Abandoned, RunOutcome.Abandoned)]
        public void TerminalReasonsMapToStableOutcomes(RunEndReason reason, RunOutcome expected)
        {
            var application = CreateApplication();
            var descriptor = new QinglanDemoRunFactory(application).CreateDescriptor(RunId, Seed).Value;
            var fixture = CreateSession(application.ContentRegistry, descriptor);

            Assert.That(fixture.Session.End(reason), Is.True);
            Assert.That(fixture.Session.Result.Outcome, Is.EqualTo(expected));
            Assert.That(fixture.Session.End(reason), Is.False);
        }

        [Test]
        public void AutomaticVictoryWaitsForBossCountAndFinalRewardTransaction()
        {
            var application = CreateApplication();
            var descriptor = new QinglanDemoRunFactory(application).CreateDescriptor(RunId, Seed).Value;
            var withoutReward = CreateSession(application.ContentRegistry, descriptor);
            withoutReward.World.Progression.RecordEnemyDefeat(0f, Vector2.Zero, false, true);
            withoutReward.World.Progression.RecordEnemyDefeat(0f, Vector2.Zero, false, true);
            Assert.That(withoutReward.Session.Advance(SimulationClock.TickDurationSeconds), Is.EqualTo(1));
            Assert.That(withoutReward.Session.HasEnded, Is.False);

            var committed = CreateSession(application.ContentRegistry, descriptor);
            committed.World.Progression.RecordEnemyDefeat(0f, Vector2.Zero, false, true);
            committed.World.Progression.RecordEnemyDefeat(0f, Vector2.Zero, false, true);
            Assert.That(committed.World.Qinglan.Rewards.TryQueueDirect(
                Id("qinglan.reward.first_clear.tingfeng"),
                new RewardTransactionId(descriptor.RunId, descriptor.VictoryBossId, 0),
                Vector2.Zero), Is.True);
            new RewardResolutionSystem().Execute(committed.World);
            Assert.That(committed.Session.Advance(SimulationClock.TickDurationSeconds), Is.EqualTo(1));
            Assert.That(committed.Session.HasEnded, Is.True);
            Assert.That(committed.Session.Result.Outcome, Is.EqualTo(RunOutcome.Victory));
            Assert.That(committed.Session.End(RunEndReason.RecoveryRejected), Is.False);
        }

        [Test]
        public void DescriptorCopiesPackInputAndRecoveryRejectedUsesEmptyResult()
        {
            var originalPack = new RunPackSnapshot(
                Id("test.pack.g2_4"),
                new ContentVersion(1, 2, 3),
                new string('a', 64));
            var packs = new[] { originalPack };
            var descriptor = new RunDescriptor(
                RunId,
                Seed,
                Id("test.character.g2_4"),
                Id("test.map.g2_4"),
                Id("base.difficulty.normal"),
                1,
                Id("test.boss.g2_4"),
                packs);
            packs[0] = new RunPackSnapshot(
                Id("test.pack.changed"),
                new ContentVersion(9, 9, 9),
                new string('b', 64));
            var coordinator = new DemoRunCoordinator(new GameStateMachine(), new NeverFactory());

            Assert.That(coordinator.BeginRun(descriptor), Is.False);
            Assert.That(coordinator.RejectRecovery(descriptor), Is.True);
            Assert.That(coordinator.Stage, Is.EqualTo(DemoFlowStage.Ending));
            Assert.That(coordinator.Tick(0d), Is.Zero);
            Assert.That(coordinator.Stage, Is.EqualTo(DemoFlowStage.Result));
            Assert.That(coordinator.LatestResult.Outcome, Is.EqualTo(RunOutcome.RecoveryRejected));
            Assert.That(coordinator.LatestResult.Delta.CurrencyDeltas, Is.Empty);
            Assert.That(coordinator.LatestResult.Descriptor.LoadedPacks[0].PackId,
                Is.EqualTo(originalPack.PackId));
            Assert.That(coordinator.HasUncommittedResult, Is.True);
            Assert.That(coordinator.ContinueToHub(), Is.True);
            Assert.That(coordinator.StartAgain(), Is.True);
            Assert.That(coordinator.Stage, Is.EqualTo(DemoFlowStage.CharacterSelect));
            Assert.That(coordinator.HasResult, Is.False);
            Assert.That(coordinator.HasUncommittedResult, Is.False);
        }

        private static SessionFixture CreateSession(ContentRegistry registry, RunDescriptor descriptor)
        {
            var modules = SkillModuleRegistry.CreateDefault();
            var skills = SkillRuntimeCatalog.Build(registry, modules);
            var enemies = EnemyRuntimeCatalog.Build(registry);
            var builds = BuildRuntimeCatalog.Build(registry, modules);
            Assert.That(skills.IsSuccess, Is.True, skills.Error.ToString());
            Assert.That(enemies.IsSuccess, Is.True, enemies.Error.ToString());
            Assert.That(builds.IsSuccess, Is.True, builds.Error.ToString());
            var hub = new QinglanRuntimeHub();
            var world = new SimulationWorld(
                hub,
                descriptor.Seed,
                128,
                2f,
                SimulationPipeline.CreateQinglanDemo(),
                new RuntimeStatusCatalog(registry),
                null,
                new SkillRuntime(skills.Value, descriptor.Seed, 128),
                new EnemyRuntime(enemies.Value, DifficultySnapshot.Default, 128));
            var player = world.CreateActor(
                SimulationEntityState.Create(Vector2.Zero, Vector2.Zero),
                ActorCombatInitialization.CreateDefault());
            world.SetPlayer(player);
            world.InitializeProgression(
                builds.Value,
                player,
                descriptor.Seed,
                descriptor.RunId);
            return new SessionFixture(
                world,
                new RunSession(world, player, new GameStateMachine(), descriptor));
        }

        private static bool TransitionToCompleted(MapObjectiveRuntime runtime, ContentId id)
        {
            return runtime.TryTransition(id, ObjectiveState.Hidden, ObjectiveState.Revealed) &&
                   runtime.TryTransition(id, ObjectiveState.Revealed, ObjectiveState.Available) &&
                   runtime.TryTransition(id, ObjectiveState.Available, ObjectiveState.Activating) &&
                   runtime.TryTransition(id, ObjectiveState.Activating, ObjectiveState.Defending) &&
                   runtime.TryTransition(id, ObjectiveState.Defending, ObjectiveState.Completed);
        }

        private static GameApplication CreateApplication()
        {
            var pack = AssetDatabase.LoadAssetAtPath<ContentPackAuthoring>(QinglanG12ContentSetup.PackPath);
            Assert.That(pack, Is.Not.Null);
            var baked = ContentBakeUtility.Bake(pack);
            Assert.That(baked.IsSuccess, Is.True, baked.Error.ToString());
            var application = new GameApplication(new NullPlatformFacade(), new GameStateMachine());
            var initialized = application.Initialize(new[] { baked.Value }, GameVersion);
            Assert.That(initialized.IsSuccess, Is.True, initialized.Error.ToString());
            return application;
        }

        private static ContentId Id(string value) => ContentId.Create(value).Value;

        private readonly struct SessionFixture
        {
            public SessionFixture(SimulationWorld world, RunSession session)
            {
                World = world;
                Session = session;
            }

            public SimulationWorld World { get; }
            public RunSession Session { get; }
        }

        private sealed class NeverFactory : IRunSessionFactory
        {
            public Result<IRunSessionHandle> Create(
                RunDescriptor descriptor,
                GameStateMachine stateMachine)
            {
                Assert.Fail("Recovery rejection must not assemble a run.");
                return default;
            }
        }
    }
}
