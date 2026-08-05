using System;
using System.IO;
using System.Numerics;
using Game.Application;
using Game.Content.Authoring;
using Game.Content.Runtime;
using Game.Core;
using Game.Editor;
using Game.Simulation;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.Localization;
using UnityEngine.Localization.Tables;

namespace Game.Tests.EditMode
{
    public sealed class QinglanG17PackRewardTests
    {
        private const ulong Seed = 0x4731375041434B52UL;
        private static readonly ContentVersion GameVersion = new ContentVersion(0, 1, 0);
        private static readonly string[] SourceSkillIds =
        {
            "qinglan.skill.weapon.yufeng_sword",
            "qinglan.skill.weapon.yellow_talisman",
            "qinglan.skill.weapon.lihuo_wheel",
            "qinglan.skill.weapon.tide_orb",
            "qinglan.skill.weapon.zhenyue_seal",
            "qinglan.skill.weapon.spirit_vine_seed"
        };

        private static readonly string[] PassiveIds =
        {
            "qinglan.passive.treading_wind",
            "qinglan.passive.clear_mind",
            "qinglan.passive.artifact_control",
            "qinglan.passive.domain_expansion",
            "qinglan.passive.long_breath",
            "qinglan.passive.spirit_gathering"
        };

        [Test]
        public void CompletePackBakesDeterministicallyWithBilingualPlaceholderDeliveryMetadata()
        {
            var pack = AssetDatabase.LoadAssetAtPath<ContentPackAuthoring>(QinglanG12ContentSetup.PackPath);
            Assert.That(pack, Is.Not.Null);
            var first = ContentBakeUtility.Bake(pack);
            var second = ContentBakeUtility.Bake(pack);
            Assert.That(first.IsSuccess, Is.True, first.Error.ToString());
            Assert.That(second.IsSuccess, Is.True, second.Error.ToString());
            Assert.That(first.Value.Manifest.Version, Is.EqualTo(new ContentVersion(0, 5, 0)));
            Assert.That(first.Value.Manifest.SchemaVersion, Is.EqualTo(6));
            Assert.That(first.Value.Definitions.Count, Is.EqualTo(94));
            Assert.That(first.Value.ContentHash,
                Is.EqualTo("798dbb302dda57b9f0158e83010ee89392ffdc291cc629715ba357b691ebd5ad"));
            Assert.That(second.Value.ContentHash, Is.EqualTo(first.Value.ContentHash));

            var checkedIn = UnityEngine.JsonUtility.FromJson<BakedContentCatalogDto>(
                File.ReadAllText(Path.GetFullPath(QinglanG12ContentSetup.BakedCatalogPath))).ToCatalog();
            Assert.That(checkedIn.IsSuccess, Is.True, checkedIn.Error.ToString());
            Assert.That(checkedIn.Value.ContentHash, Is.EqualTo(first.Value.ContentHash));
            Assert.That(checkedIn.Value.Definitions.Count, Is.EqualTo(first.Value.Definitions.Count));

            var settings = AddressableAssetSettingsDefaultObject.GetSettings(false);
            var collection = LocalizationEditorSettings.GetStringTableCollection("UI");
            var english = collection?.GetTable("en") as StringTable;
            var chinese = collection?.GetTable("zh-Hans") as StringTable;
            Assert.That(settings, Is.Not.Null);
            Assert.That(english, Is.Not.Null);
            Assert.That(chinese, Is.Not.Null);
            for (var index = 0; index < pack.Definitions.Count; index++)
            {
                var definition = pack.Definitions[index];
                var path = AssetDatabase.GetAssetPath(definition);
                Assert.That(path, Does.StartWith(QinglanG12ContentSetup.Folder + "/"));
                Assert.That(english.GetEntry(definition.LocalizedNameKey)?.Value, Is.Not.Empty, path);
                Assert.That(english.GetEntry(definition.LocalizedDescriptionKey)?.Value, Is.Not.Empty, path);
                Assert.That(chinese.GetEntry(definition.LocalizedNameKey)?.Value, Is.Not.Empty, path);
                Assert.That(chinese.GetEntry(definition.LocalizedDescriptionKey)?.Value, Is.Not.Empty, path);
                var entry = settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(path));
                Assert.That(entry, Is.Not.Null, path);
                Assert.That(entry.labels, Does.Contain(pack.AssetLabel), path);
                Assert.That(entry.labels, Does.Contain(PlaceholderAssetGenerator.PlaceholderLabel), path);
                Assert.That(entry.labels, Does.Contain(PlaceholderAssetGenerator.DevelopmentOnlyLabel), path);
            }

            var catalogEntry = settings.FindAssetEntry(
                AssetDatabase.AssetPathToGUID(QinglanG12ContentSetup.BakedCatalogPath));
            Assert.That(catalogEntry, Is.Not.Null);
            Assert.That(catalogEntry.address, Is.EqualTo(pack.CatalogAddress));
            Assert.That(catalogEntry.labels, Does.Contain(pack.AssetLabel));
            Assert.That(catalogEntry.labels, Does.Contain(PlaceholderAssetGenerator.PlaceholderLabel));
            Assert.That(catalogEntry.labels, Does.Contain(PlaceholderAssetGenerator.DevelopmentOnlyLabel));
        }

        [Test]
        public void RewardStreamSelectsEligibleLockedManifestationsIndependentlyFromNormalOffers()
        {
            var registry = LoadRegistry();
            var first = CreateWorld(registry, Seed);
            var second = CreateWorld(registry, Seed);
            for (var draw = 0; draw < 5; draw++)
                first.World.Progression.Offers.Generate(first.World.Progression.Build);
            MakeAllEligible(first.World.Progression.Build);
            MakeAllEligible(second.World.Progression.Build);
            var source = Id("test.reward.g1_7.manifestation");
            var fallback = Id("qinglan.reward.elite.afflicted_core");
            var transaction = new RewardTransactionId(Seed, source, 0);

            Assert.That(
                first.World.Progression.RewardChoices.RequestEvolutionChoice(transaction, fallback),
                Is.EqualTo(RewardChoiceRequestStatus.ChoiceRequested));
            Assert.That(
                second.World.Progression.RewardChoices.RequestEvolutionChoice(transaction, fallback),
                Is.EqualTo(RewardChoiceRequestStatus.ChoiceRequested));
            Assert.That(ChoiceSequence(second.World.Progression.RewardChoices.CurrentChoice),
                Is.EqualTo(ChoiceSequence(first.World.Progression.RewardChoices.CurrentChoice)));
            Assert.That(first.World.Progression.RewardChoices.CurrentChoice.CandidateCount, Is.EqualTo(3));
            Assert.That(first.World.Progression.RewardChoices.RandomCalls,
                Is.EqualTo(second.World.Progression.RewardChoices.RandomCalls));
            Assert.That(first.World.Progression.Offers.RandomCalls,
                Is.GreaterThan(second.World.Progression.Offers.RandomCalls));

            var selected = first.World.Progression.RewardChoices.CurrentChoice.GetCandidateAt(0);
            Assert.That(first.Builds.TryGetOffer(selected, out var offer), Is.True);
            Assert.That(offer.TargetKind, Is.EqualTo(UpgradeTargetKind.Evolution));
            Assert.That(offer.Source.InitiallyUnlocked, Is.False);
            Assert.That(
                first.World.Progression.RewardChoices.Select(Id("test.offer.invalid")),
                Is.EqualTo(RewardChoiceResolutionStatus.InvalidSelection));
            Assert.That(first.World.Qinglan.Rewards.CommittedCount, Is.Zero);
            Assert.That(
                first.World.Progression.RewardChoices.Select(selected),
                Is.EqualTo(RewardChoiceResolutionStatus.Committed));
            Assert.That(first.World.Qinglan.Rewards.CommittedCount, Is.EqualTo(1));
            Assert.That(first.World.Progression.Build.IsEvolutionEligible(offer.Source.TargetContentId), Is.False);
            Assert.That(first.World.Progression.RewardChoices.LastResolvedId,
                Is.EqualTo(offer.Source.TargetContentId));
            Assert.That(
                first.World.Progression.RewardChoices.RequestEvolutionChoice(transaction, fallback),
                Is.EqualTo(RewardChoiceRequestStatus.AlreadyCommitted));
            Assert.That(first.World.Qinglan.Rewards.CommittedCount, Is.EqualTo(1));
        }

        [Test]
        public void EmptyEligibilityCommitsDeterministicFallbackWithoutPausingOrRolling()
        {
            var fixture = CreateWorld(LoadRegistry(), Seed);
            var fallback = Id("qinglan.reward.elite.afflicted_core");
            var transaction = new RewardTransactionId(Seed, Id("test.reward.g1_7.empty"), 0);
            var before = fixture.World.Progression.RewardChoices.RandomCalls;

            Assert.That(
                fixture.World.Progression.RewardChoices.RequestEvolutionChoice(transaction, fallback),
                Is.EqualTo(RewardChoiceRequestStatus.FallbackCommitted));
            Assert.That(fixture.World.Progression.RewardChoices.HasPendingChoice, Is.False);
            Assert.That(fixture.World.Progression.RewardChoices.PauseRequested, Is.False);
            Assert.That(fixture.World.Progression.RewardChoices.LastResolvedId, Is.EqualTo(fallback));
            Assert.That(fixture.World.Progression.RewardChoices.RandomCalls, Is.EqualTo(before));
            Assert.That(fixture.World.Qinglan.Rewards.CommittedCount, Is.EqualTo(1));
            Assert.That(
                fixture.World.Progression.RewardChoices.RequestEvolutionChoice(transaction, fallback),
                Is.EqualTo(RewardChoiceRequestStatus.AlreadyCommitted));
            Assert.That(fixture.World.Qinglan.Rewards.CommittedCount, Is.EqualTo(1));
        }

        [Test]
        public void RunSessionPausesForRewardSelectionThenPreservesNormalLevelUpFlow()
        {
            var fixture = CreateWorld(LoadRegistry(), Seed);
            MakeEligible(fixture.World.Progression.Build, 0);
            var fallback = Id("qinglan.reward.elite.afflicted_core");
            var transaction = new RewardTransactionId(Seed, Id("test.reward.g1_7.session"), 0);
            Assert.That(
                fixture.World.Progression.RewardChoices.RequestEvolutionChoice(transaction, fallback),
                Is.EqualTo(RewardChoiceRequestStatus.ChoiceRequested));
            var session = new RunSession(fixture.World, fixture.Player, new GameStateMachine());

            Assert.That(session.Advance(SimulationClock.TickDurationSeconds), Is.EqualTo(1));
            Assert.That(session.StateMachine.CurrentState, Is.EqualTo(GameState.RewardChoice));
            Assert.That(session.Runner.Clock.IsPaused, Is.True);
            Assert.That(session.CurrentRewardChoice, Is.Not.Null);
            Assert.That(session.CurrentRewardChoice.HasReplayKey, Is.True);
            Assert.That(session.CurrentRewardChoice.RunId, Is.EqualTo(Seed));
            Assert.That(session.Advance(1d), Is.Zero);
            Assert.That(session.SelectRewardAt(0), Is.True);
            Assert.That(session.StateMachine.CurrentState, Is.EqualTo(GameState.InRun));
            Assert.That(session.Runner.Clock.IsPaused, Is.False);
            Assert.That(session.CurrentRewardChoice, Is.Null);

            Assert.That(session.GrantDebugExperience(1_000f), Is.True);
            Assert.That(session.Advance(SimulationClock.TickDurationSeconds), Is.EqualTo(1));
            Assert.That(session.StateMachine.CurrentState, Is.EqualTo(GameState.LevelUpChoice));
            Assert.That(session.CurrentOffers, Is.Not.Null);
            Assert.That(session.SelectAt(0), Is.True);
            Assert.That(session.StateMachine.CurrentState, Is.EqualTo(GameState.InRun));
        }

        private static void MakeAllEligible(BuildState build)
        {
            for (var index = 0; index < SourceSkillIds.Length; index++) MakeEligible(build, index);
        }

        private static void MakeEligible(BuildState build, int index)
        {
            for (var level = 0; level < 8; level++)
                Assert.That(build.TryAcquireSkill(Id(SourceSkillIds[index])), Is.True);
            Assert.That(build.TryAcquirePassive(Id(PassiveIds[index])), Is.True);
        }

        private static string ChoiceSequence(RewardChoiceSnapshot choice)
        {
            var value = string.Empty;
            for (var index = 0; index < choice.CandidateCount; index++)
                value += choice.GetCandidateAt(index).Value + "|";
            return value;
        }

        private static WorldFixture CreateWorld(ContentRegistry registry, ulong seed)
        {
            var modules = SkillModuleRegistry.CreateDefault();
            var skills = SkillRuntimeCatalog.Build(registry, modules);
            var builds = BuildRuntimeCatalog.Build(registry, modules);
            Assert.That(skills.IsSuccess, Is.True, skills.Error.ToString());
            Assert.That(builds.IsSuccess, Is.True, builds.Error.ToString());
            var hub = new QinglanRuntimeHub(
                new CharacterMechanicRuntime(4),
                new RewardRuntime(16));
            var world = new SimulationWorld(
                hub,
                seed: seed,
                initialEntityCapacity: 128,
                pipeline: SimulationPipeline.CreateQinglanDemo(),
                statusCatalog: new RuntimeStatusCatalog(registry),
                skillRuntime: new SkillRuntime(skills.Value, seed, 128));
            var stats = StatBaseValues.CreateDefault(1_000_000f, 6f);
            var player = world.CreateActor(
                SimulationEntityState.Create(Vector2.Zero, Vector2.Zero),
                new ActorCombatInitialization(stats, stats.Health, 0f, 0f, default));
            world.SetPlayer(player);
            world.InitializeProgression(builds.Value, player, seed, null, 6, 6, Array.Empty<ContentTag>());
            return new WorldFixture(world, builds.Value, player);
        }

        private static ContentRegistry LoadRegistry()
        {
            var pack = AssetDatabase.LoadAssetAtPath<ContentPackAuthoring>(QinglanG12ContentSetup.PackPath);
            Assert.That(pack, Is.Not.Null);
            var baked = ContentBakeUtility.Bake(pack);
            Assert.That(baked.IsSuccess, Is.True, baked.Error.ToString());
            var registry = new ContentRegistry();
            var load = registry.Load(new[] { baked.Value }, GameVersion);
            Assert.That(load.IsSuccess, Is.True, load.Error.ToString());
            return registry;
        }

        private static ContentId Id(string value) => ContentId.Create(value).Value;

        private readonly struct WorldFixture
        {
            public WorldFixture(SimulationWorld world, BuildRuntimeCatalog builds, EntityHandle player)
            {
                World = world;
                Builds = builds;
                Player = player;
            }

            public SimulationWorld World { get; }
            public BuildRuntimeCatalog Builds { get; }
            public EntityHandle Player { get; }
        }
    }
}
