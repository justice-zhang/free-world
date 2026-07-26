using System;
using System.Numerics;
using Game.Application;
using Game.Content.Runtime;
using Game.Core;
using Game.Simulation;
using NUnit.Framework;
using JsonUtility = UnityEngine.JsonUtility;

namespace Game.Tests.EditMode
{
    public sealed class M6ProgressionTests
    {
        [Test]
        public void ExperienceCurveCarriesOverflowAcrossMultipleLevels()
        {
            var experience = new ExperienceProgression(new ExperienceCurve(5f, 2f, 0f));

            var gained = experience.Gain(34f);

            Assert.That(gained, Is.EqualTo(4));
            Assert.That(experience.Level, Is.EqualTo(5));
            Assert.That(experience.PendingLevelUps, Is.EqualTo(4));
            Assert.That(experience.CurrentExperience, Is.EqualTo(2f).Within(0.0001f));
            Assert.That(experience.TotalExperience, Is.EqualTo(34d));
        }

        [Test]
        public void InventoriesEnforceSlotsDuplicateLevelsMaximumAndReplacement()
        {
            var first = SkillTestFactory.Id("test.inventory.first");
            var second = SkillTestFactory.Id("test.inventory.second");
            var inventory = new SkillInventory(1);

            Assert.That(inventory.TryAcquire(first, new RuntimeContentIndex(1), 2, InventoryReplacementPolicy.Reject, -1, out _, out _),
                Is.EqualTo(InventoryAcquireResult.Added));
            Assert.That(inventory.TryAcquire(first, new RuntimeContentIndex(1), 2, InventoryReplacementPolicy.Reject, -1, out _, out _),
                Is.EqualTo(InventoryAcquireResult.Leveled));
            Assert.That(inventory.TryAcquire(first, new RuntimeContentIndex(1), 2, InventoryReplacementPolicy.Reject, -1, out _, out _),
                Is.EqualTo(InventoryAcquireResult.Rejected));
            Assert.That(inventory.TryAcquire(second, new RuntimeContentIndex(2), 1, InventoryReplacementPolicy.Reject, -1, out _, out _),
                Is.EqualTo(InventoryAcquireResult.Rejected));
            Assert.That(inventory.TryAcquire(second, new RuntimeContentIndex(2), 1, InventoryReplacementPolicy.ReplaceSpecifiedSlot, 0, out var slot, out var previous),
                Is.EqualTo(InventoryAcquireResult.Replaced));
            Assert.That(slot, Is.EqualTo(0));
            Assert.That(previous.ContentId, Is.EqualTo(first));
            Assert.That(inventory.GetAt(0).ContentId, Is.EqualTo(second));
        }

        [Test]
        public void OfferStreamIsWeightedReproducibleRerollableAndBanishable()
        {
            var fixture = M6TestFactory.Create(30f);
            var firstWorld = M6TestFactory.World(fixture, 7123UL, out _);
            var secondWorld = M6TestFactory.World(fixture, 7123UL, out _);
            var first = firstWorld.Progression.Offers;
            var second = secondWorld.Progression.Offers;
            for (var index = 0; index < 20; index++) firstWorld.Random.NextUInt();
            var initialFirst = Sequence(first.Generate(firstWorld.Progression.Build));
            var initialSecond = Sequence(second.Generate(secondWorld.Progression.Build));
            Assert.That(initialSecond, Is.EqualTo(initialFirst));

            var sawDifferentReroll = false;
            for (var index = 0; index < 5; index++)
            {
                var rerolledFirst = Sequence(first.Reroll(firstWorld.Progression.Build));
                var rerolledSecond = Sequence(second.Reroll(secondWorld.Progression.Build));
                Assert.That(rerolledSecond, Is.EqualTo(rerolledFirst));
                sawDifferentReroll |= rerolledFirst != initialFirst;
            }
            Assert.That(sawDifferentReroll, Is.True);
            Assert.That(first.RandomCalls, Is.EqualTo(second.RandomCalls));
            Assert.That(first.StreamSeed, Is.EqualTo(second.StreamSeed));

            var current = first.Generate(firstWorld.Progression.Build);
            var banished = current.GetAt(0).Source.Id;
            var afterBanish = first.Banish(firstWorld.Progression.Build, banished);
            Assert.That(Contains(afterBanish, banished), Is.False);

            var weightedWorld = M6TestFactory.World(fixture, 99UL, out _);
            var forceCount = 0;
            for (var index = 0; index < 300; index++)
            {
                var offer = weightedWorld.Progression.Offers.Generate(weightedWorld.Progression.Build, 1);
                if (offer.GetAt(0).Source.TargetContentId == fixture.ForcePassive.Id) forceCount++;
            }
            Assert.That(forceCount, Is.GreaterThan(200));
        }

        [Test]
        public void OfferFilteringHonorsMaximumSlotsAndMutualExclusion()
        {
            var fixture = M6TestFactory.Create(30f);
            var world = M6TestFactory.World(fixture, 17UL, out _, false, 1, 1);
            var build = world.Progression.Build;
            Assert.That(CanGenerateTarget(world, fixture.ReachPassive.Id), Is.False, "offer prerequisite");
            Assert.That(build.TryAcquireSkill(fixture.SourceSkill.Id), Is.True);
            Assert.That(CanGenerateTarget(world, fixture.AuxiliarySkill.Id), Is.False, "full skill slot");
            Assert.That(build.TryAcquireSkill(fixture.SourceSkill.Id), Is.True);
            Assert.That(build.TryAcquireSkill(fixture.SourceSkill.Id), Is.True);
            Assert.That(build.TryAcquireSkill(fixture.SourceSkill.Id), Is.False, "maximum level");
            Assert.That(CanGenerateTarget(world, fixture.SourceSkill.Id), Is.False, "maximum skill level");

            Assert.That(build.TryAcquirePassive(fixture.ForcePassive.Id), Is.True);
            Assert.That(CanGenerateTarget(world, fixture.AuxiliarySkill.Id), Is.False, "mutually exclusive passive");
            Assert.That(CanGenerateTarget(world, fixture.ReachPassive.Id), Is.False, "full passive slot");
            Assert.That(build.TryAcquirePassive(fixture.ForcePassive.Id), Is.True);
            Assert.That(build.TryAcquirePassive(fixture.ForcePassive.Id), Is.True);
            Assert.That(build.TryAcquirePassive(fixture.ForcePassive.Id), Is.False, "maximum passive level");
            Assert.That(CanGenerateTarget(world, fixture.ForcePassive.Id), Is.False);
        }

        [Test]
        public void SynergyConditionsOutputsAndTagsRemainCentralizedInBuildState()
        {
            var fixture = M6TestFactory.Create(30f);
            var world = M6TestFactory.World(fixture, 33UL, out var player);
            var build = world.Progression.Build;
            Assert.That(build.TryAcquireSkill(fixture.SourceSkill.Id), Is.True);
            Assert.That(build.TryAcquireSkill(fixture.SourceSkill.Id), Is.True);
            Assert.That(build.TryAcquirePassive(fixture.ForcePassive.Id), Is.True);

            Assert.That(build.GetTagCount(Tag("build.force")), Is.EqualTo(1));
            Assert.That(build.ActiveSynergyCount, Is.EqualTo(1));
            Assert.That(build.GetActiveSynergyAt(0), Is.EqualTo(fixture.FirstSynergy.Id));
            Assert.That(build.IsOfferUnlocked(fixture.EvolutionOffer.Id), Is.True);
            Assert.That(world.Actors.TryReadStat(player, BuiltInStatIndices.Range, out var range), Is.True);
            Assert.That(range, Is.EqualTo(1.25f).Within(0.0001f));

            var targetStats = StatBaseValues.CreateDefault(1_000f, 0f);
            world.CreateActor(
                SimulationEntityState.Create(Vector2.UnitX, Vector2.Zero),
                new ActorCombatInitialization(targetStats, targetStats.Health, 0f, 0f, default));
            new FixedTickRunner(world).Advance(SimulationClock.TickDurationSeconds);
            Assert.That(
                world.CombatEvents.DamageAppliedCount,
                Is.EqualTo(2),
                "the base skill effect and synergy AddEffectOp must both resolve");

            var other = M6TestFactory.World(fixture, 34UL, out _).Progression.Build;
            Assert.That(other.TryAcquireSkill(fixture.AuxiliarySkill.Id), Is.True);
            Assert.That(other.TryAcquirePassive(fixture.ReachPassive.Id), Is.True);
            Assert.That(other.ActiveSynergyCount, Is.EqualTo(1));
            Assert.That(other.Skills.TryGet(fixture.AuxiliarySkill.Id, out _, out _), Is.False);
            Assert.That(other.Skills.TryGet(fixture.AuxiliaryResultSkill.Id, out _, out _), Is.True);
            Assert.That(other.TraitCount, Is.EqualTo(1));
            Assert.That(other.GetTraitAt(0), Is.EqualTo(fixture.Trait.Id));
            Assert.That(other.GetTagCount(Tag("build.prepared")), Is.EqualTo(1));
        }

        [Test]
        public void EvolutionTransformsSkillAndConsumesConfiguredPassive()
        {
            var fixture = M6TestFactory.Create(30f);
            var world = M6TestFactory.World(fixture, 44UL, out _);
            var build = world.Progression.Build;
            Assert.That(build.TryAcquireSkill(fixture.SourceSkill.Id), Is.True);
            Assert.That(build.TryAcquireSkill(fixture.SourceSkill.Id), Is.True);
            Assert.That(build.TryAcquirePassive(fixture.ForcePassive.Id), Is.True);
            Assert.That(build.IsEvolutionEligible(fixture.Evolution.Id), Is.True);
            Assert.That(world.Progression.Build.IsOfferUnlocked(fixture.EvolutionOffer.Id), Is.True);
            Assert.That(BuildCatalog(fixture).TryGetOffer(fixture.EvolutionOffer.Id, out var offer), Is.True);

            Assert.That(build.ApplyOffer(offer), Is.True);

            Assert.That(build.Skills.TryGet(fixture.SourceSkill.Id, out _, out _), Is.False);
            Assert.That(build.Skills.TryGet(fixture.EvolvedSkill.Id, out _, out _), Is.True);
            Assert.That(build.Passives.TryGet(fixture.ForcePassive.Id, out _, out _), Is.False);
            Assert.That(build.GetTagCount(Tag("build.force")), Is.EqualTo(0));
            Assert.That(build.IsEvolutionEligible(fixture.Evolution.Id), Is.False);
        }

        [Test]
        public void EnemyDeathCreatesCollectibleExperiencePickupInFixedPipeline()
        {
            var fixture = M6TestFactory.Create(30f);
            var world = M6TestFactory.World(fixture, 55UL, out var player);
            var enemy = M5TestFactory.Spawn(world, fixture.Registry, fixture.M5.Chase, Vector2.Zero);
            world.QueueDamage(new DamagePacket(
                new SpatialEntity(EntityKind.Actor, player),
                new SpatialEntity(EntityKind.Actor, enemy),
                fixture.SourceSkill.Id,
                DamageType.Physical,
                DamageTags.Direct,
                10_000f,
                false,
                1f,
                Vector2.Zero,
                Vector2.Zero,
                0));
            var runner = new FixedTickRunner(world);

            runner.Advance(SimulationClock.TickDurationSeconds);
            Assert.That(world.Pickups.Count, Is.EqualTo(1));
            Assert.That(world.Progression.Statistics.EnemyDefeats, Is.EqualTo(1));

            runner.Advance(SimulationClock.TickDurationSeconds);
            Assert.That(world.Pickups.Count, Is.EqualTo(0));
            Assert.That(world.Progression.Statistics.PickupsCollected, Is.EqualTo(1));
            Assert.That(world.Progression.Experience.TotalExperience, Is.EqualTo(2d));
        }

        [Test]
        public void RunSessionPausesForCommandSelectionAndProducesResult()
        {
            var fixture = M6TestFactory.Create(30f);
            var world = M6TestFactory.World(fixture, 66UL, out var player);
            world.Progression.Experience.Gain(6f);
            var stateMachine = new GameStateMachine();
            var session = new RunSession(world, player, stateMachine);

            Assert.That(session.Advance(SimulationClock.TickDurationSeconds), Is.EqualTo(1));
            Assert.That(stateMachine.CurrentState, Is.EqualTo(GameState.LevelUpChoice));
            Assert.That(session.Runner.Clock.IsPaused, Is.True);
            Assert.That(session.Advance(1d), Is.EqualTo(0));
            Assert.That(session.SelectAt(0), Is.True);
            Assert.That(session.Runner.Clock.IsPaused, Is.False);
            Assert.That(stateMachine.CurrentState, Is.EqualTo(GameState.InRun));
            world.Progression.Experience.Gain(6f);
            Assert.That(session.Advance(SimulationClock.TickDurationSeconds), Is.EqualTo(1));
            Assert.That(session.Reroll(), Is.True);
            Assert.That(session.Banish(session.CurrentOffers.GetAt(0).Source.Id), Is.True);
            Assert.That(session.Skip(), Is.True);
            Assert.That(session.End(RunEndReason.Completed), Is.True);
            Assert.That(session.Result.Level, Is.EqualTo(3));
            Assert.That(session.Result.Statistics.OffersSelected, Is.EqualTo(1));
            Assert.That(session.Result.Statistics.OffersRerolled, Is.EqualTo(1));
            Assert.That(session.Result.Statistics.OffersBanished, Is.EqualTo(1));
            Assert.That(session.Result.Statistics.OffersSkipped, Is.EqualTo(1));
            Assert.That(stateMachine.CurrentState, Is.EqualTo(GameState.RunResult));
        }

        [Test]
        public void SchemaFiveRoundTripPreservesBuildDefinitionsAndHash()
        {
            var fixture = M6TestFactory.Create(30f);

            var json = JsonUtility.ToJson(fixture.Catalog.ToDto());
            var restored = JsonUtility.FromJson<BakedContentCatalogDto>(json).ToCatalog();

            Assert.That(restored.IsSuccess, Is.True, restored.Error.ToString());
            Assert.That(restored.Value.ContentHash, Is.EqualTo(fixture.Catalog.ContentHash));
            Assert.That(restored.Value.Definitions.Count, Is.EqualTo(15));
            Assert.That(restored.Value.Definitions[4], Is.TypeOf<RuntimePassiveDefinition>());
            Assert.That(restored.Value.Definitions[7], Is.TypeOf<RuntimeEvolutionDefinition>());
            Assert.That(restored.Value.Definitions[13], Is.TypeOf<RuntimeSynergyDefinition>());
        }

        private static bool CanGenerateTarget(SimulationWorld world, ContentId target)
        {
            for (var attempt = 0; attempt < 20; attempt++)
            {
                var offers = world.Progression.Offers.Generate(world.Progression.Build);
                for (var index = 0; index < offers.Count; index++)
                    if (offers.GetAt(index).Source.TargetContentId == target) return true;
            }
            return false;
        }

        private static BuildRuntimeCatalog BuildCatalog(M6Fixture fixture)
        {
            var result = BuildRuntimeCatalog.Build(fixture.Registry, SkillModuleRegistry.CreateDefault());
            if (!result.IsSuccess) throw new InvalidOperationException(result.Error.ToString());
            return result.Value;
        }

        private static string Sequence(UpgradeOfferSet offers)
        {
            var value = string.Empty;
            for (var index = 0; index < offers.Count; index++) value += offers.GetAt(index).Source.Id.Value + "|";
            return value;
        }

        private static bool Contains(UpgradeOfferSet offers, ContentId id)
        {
            for (var index = 0; index < offers.Count; index++) if (offers.GetAt(index).Source.Id == id) return true;
            return false;
        }

        private static ContentTag Tag(string value)
        {
            var result = ContentTag.Create(value);
            if (!result.IsSuccess) throw new InvalidOperationException(result.Error.ToString());
            return result.Value;
        }
    }
}
