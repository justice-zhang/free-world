using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using Game.Content.Authoring;
using Game.Content.Runtime;
using Game.Core;
using Game.Editor;
using Game.Simulation;
using NUnit.Framework;
using UnityEditor;

namespace Game.Tests.EditMode
{
    public sealed class QinglanG23RewardTests
    {
        private const ulong Seed = 0x4732335245574152UL;
        private static readonly ContentVersion GameVersion = new ContentVersion(0, 1, 0);
        private static readonly string[] PickupIds =
        {
            "qinglan.pickup.greenwood_dew",
            "qinglan.pickup.boundary_talisman",
            "qinglan.pickup.thunder_jade",
            "qinglan.pickup.spirit_gourd",
            "qinglan.pickup.heart_guard_jade",
            "qinglan.pickup.riding_wind_feather"
        };
        private static readonly string[] RelicIds =
        {
            "qinglan.relic.broken_sword_tassel",
            "qinglan.relic.wind_vein_copper",
            "qinglan.relic.herb_garden_seed_pod",
            "qinglan.relic.listening_wind_core",
            "qinglan.relic.old_court_bell",
            "qinglan.relic.blank_sword_trial_token"
        };

        [Test]
        public void PackPointNineRetainsSixPickupsSixRelicsAndFixedBossRewards()
        {
            var first = Bake();
            var second = Bake();
            Assert.That(first.Manifest.Version, Is.EqualTo(new ContentVersion(0, 9, 0)));
            Assert.That(first.Manifest.SchemaVersion, Is.EqualTo(6));
            Assert.That(first.Definitions.Count, Is.EqualTo(193));
            Assert.That(second.ContentHash, Is.EqualTo(first.ContentHash));
            Assert.That(first.ContentHash, Has.Length.EqualTo(64));

            for (var index = 0; index < PickupIds.Length; index++)
            {
                var pickup = Definition<RuntimePickupDefinition>(first, Id(PickupIds[index]));
                Assert.That(pickup.RewardId.IsValid, Is.True);
                Assert.That(pickup.Radius, Is.GreaterThan(0f));
                Assert.That(pickup.LifetimeSeconds, Is.EqualTo(90f));
                Assert.That(Definition<RuntimeRewardDefinition>(first, pickup.RewardId).Operations.Count,
                    Is.GreaterThan(0));
            }
            for (var index = 0; index < RelicIds.Length; index++)
            {
                var relic = Definition<RuntimeRelicDefinition>(first, Id(RelicIds[index]));
                Assert.That(relic.MaximumLevel, Is.EqualTo(1),
                    "G2.3 locks the no-duplicate relic rule instead of permitting unbounded levels.");
                Assert.That(relic.OutputIds.Count, Is.GreaterThan(0));
            }

            var manifestation = Definition<RuntimeRewardDefinition>(
                first,
                Id("qinglan.reward.manifestation_chest"));
            Assert.That(CountOperations(manifestation, RewardOperationCode.GrantEvolutionChoice), Is.EqualTo(6));
            Assert.That(manifestation.FallbackRewardId, Is.EqualTo(Id("qinglan.reward.fallback.spirit_sand")));
            var zhezhi = Definition<RuntimeBossDefinition>(first, Id("qinglan.boss.zhezhi"));
            var tingfeng = Definition<RuntimeBossDefinition>(first, Id("qinglan.boss.tingfeng"));
            Assert.That(zhezhi.RewardId, Is.EqualTo(manifestation.Id));
            Assert.That(tingfeng.RewardId, Is.EqualTo(Id("qinglan.reward.first_clear.tingfeng")));

            var checkedIn = UnityEngine.JsonUtility.FromJson<BakedContentCatalogDto>(
                File.ReadAllText(Path.GetFullPath(QinglanG12ContentSetup.BakedCatalogPath))).ToCatalog();
            Assert.That(checkedIn.IsSuccess, Is.True, checkedIn.Error.ToString());
            Assert.That(checkedIn.Value.ContentHash, Is.EqualTo(first.ContentHash));
        }

        [Test]
        public void SixInstantPickupOperationsRespectHealingStatusDamageAndMovementBoundaries()
        {
            var registry = LoadRegistry();
            var fixture = CreateWorld(registry, Seed, 128, 50f);
            var rewards = fixture.World.Qinglan.Rewards;
            var pickupTransaction = Transaction("test.reward.g2_3.greenwood", 0);
            Assert.That(rewards.TryQueuePickup(Id("qinglan.pickup.greenwood_dew"), pickupTransaction, Vector2.Zero), Is.True);
            new CleanupSystem().Execute(fixture.World);
            new PickupSystem().Execute(fixture.World);
            new RewardResolutionSystem().Execute(fixture.World);
            Assert.That(fixture.World.Actors.TryReadHealth(fixture.Player, out var healed), Is.True);
            Assert.That(healed.Current, Is.EqualTo(95f).Within(0.001f));
            new CleanupSystem().Execute(fixture.World);
            Assert.That(rewards.ActivePickupCount, Is.Zero);

            var full = CreateWorld(registry, Seed + 1, 64, 100f);
            Assert.That(full.World.Qinglan.Rewards.TryQueuePickup(
                Id("qinglan.pickup.greenwood_dew"), Transaction("test.reward.g2_3.full_health", 0), Vector2.Zero), Is.True);
            new CleanupSystem().Execute(full.World);
            new PickupSystem().Execute(full.World);
            Assert.That(full.World.Qinglan.Rewards.ActivePickupCount, Is.EqualTo(1),
                "A heal-only pickup is not consumed at full health.");

            var enemy = SpawnEnemy(fixture, "qinglan.enemy.grass_spirit", new Vector2(1f, 0f));
            Assert.That(fixture.World.Actors.TryReadHealth(enemy, out var enemyBefore), Is.True);
            Assert.That(rewards.TryQueueDirect(
                Id("qinglan.reward.pickup.boundary_talisman"),
                Transaction("test.reward.g2_3.boundary", 0),
                Vector2.Zero,
                fixture.PlayerEntity), Is.True);
            new RewardResolutionSystem().Execute(fixture.World);
            new StatusTickSystem().Execute(fixture.World);
            Assert.That(fixture.Builds.TryGetIndex(Id("qinglan.status.rooted"), out var rooted), Is.True);
            Assert.That(fixture.World.Actors.TryReadStatus(enemy, rooted, out _), Is.True);

            Assert.That(rewards.TryQueueDirect(
                Id("qinglan.reward.pickup.thunder_jade"),
                Transaction("test.reward.g2_3.thunder", 0),
                Vector2.Zero,
                fixture.PlayerEntity), Is.True);
            Assert.That(rewards.TryQueueDirect(
                Id("qinglan.reward.pickup.heart_guard_jade"),
                Transaction("test.reward.g2_3.heart", 0),
                Vector2.Zero,
                fixture.PlayerEntity), Is.True);
            Assert.That(rewards.TryQueueDirect(
                Id("qinglan.reward.pickup.riding_wind_feather"),
                Transaction("test.reward.g2_3.feather", 0),
                Vector2.Zero,
                fixture.PlayerEntity), Is.True);
            new RewardResolutionSystem().Execute(fixture.World);
            new DamageResolutionSystem().Execute(fixture.World);
            new StatusTickSystem().Execute(fixture.World);

            Assert.That(fixture.Builds.TryGetIndex(Id("qinglan.status.damage_immunity"), out var immune), Is.True);
            Assert.That(fixture.Builds.TryGetIndex(Id("qinglan.status.pickup.riding_wind_feather"), out var feather), Is.True);
            Assert.That(fixture.World.Actors.TryReadStatus(fixture.Player, immune, out _), Is.True);
            Assert.That(fixture.World.Actors.TryReadStatus(fixture.Player, feather, out _), Is.True);
            Assert.That(fixture.World.Actors.TryReadHealth(enemy, out var enemyAfter), Is.True);
            Assert.That(enemyAfter.Current, Is.LessThan(enemyBefore.Current));
            Assert.That(fixture.World.Actors.TryReadStat(
                fixture.Player,
                BuiltInStatIndices.MoveSpeed,
                out var moveSpeed), Is.True);
            Assert.That(moveSpeed, Is.GreaterThan(6f));
        }

        [Test]
        public void SpiritGourdCollectsOrdinaryPickupsButExcludesChoiceAndUniqueSources()
        {
            var fixture = CreateWorld(LoadRegistry(), Seed, 128, 100f);
            var rewards = fixture.World.Qinglan.Rewards;
            Assert.That(rewards.TryQueuePickup(
                Id("qinglan.pickup.thunder_jade"),
                Transaction("test.reward.g2_3.ordinary", 0),
                new Vector2(20f, 0f)), Is.True);
            var choiceTransaction =
                Transaction("test.reward.g2_3.choice", 0);
            Assert.That(rewards.TryQueueGroundReward(
                Id("qinglan.reward.manifestation_chest"),
                choiceTransaction,
                new Vector2(22f, 0f)), Is.True);
            new RewardResolutionSystem().Execute(fixture.World);
            new CleanupSystem().Execute(fixture.World);
            Assert.That(rewards.ActivePickupCount, Is.EqualTo(2));
            Assert.That(rewards.TryQueueGroundReward(
                Id("qinglan.reward.manifestation_chest"),
                choiceTransaction,
                new Vector2(22f, 0f)), Is.False,
                "An active ground reward keeps its transaction pending and rejects replay.");

            Assert.That(rewards.TryQueueDirect(
                Id("qinglan.reward.pickup.spirit_gourd"),
                Transaction("test.reward.g2_3.gourd", 0),
                Vector2.Zero), Is.True);
            new RewardResolutionSystem().Execute(fixture.World);
            new PickupSystem().Execute(fixture.World);
            new CleanupSystem().Execute(fixture.World);

            Assert.That(rewards.ActivePickupCount, Is.EqualTo(1));
            var remainingChoice = false;
            for (var index = 0; index < fixture.World.Pickups.Count; index++)
            {
                var handle = fixture.World.Pickups.GetHandleAt(index);
                if (rewards.TryGetPickup(handle, out var pickup) && pickup.IsChoice) remainingChoice = true;
            }
            Assert.That(remainingChoice, Is.True);
        }

        [Test]
        public void EliteRelicChoicesAreDeterministicIndependentAndFallbackAtThreeFullSlots()
        {
            var registry = LoadRegistry();
            var first = CreateWorld(registry, Seed, 128, 100f);
            var second = CreateWorld(registry, Seed, 128, 100f);
            second.World.Progression.Offers.Generate(second.World.Progression.Build);
            second.World.Progression.Offers.Generate(second.World.Progression.Build);
            var source = Id("qinglan.reward.elite.afflicted_core");
            Assert.That(first.World.Qinglan.Rewards.TryQueueDirect(source, Transaction("test.reward.g2_3.relic", 0), Vector2.Zero), Is.True);
            Assert.That(second.World.Qinglan.Rewards.TryQueueDirect(source, Transaction("test.reward.g2_3.relic", 0), Vector2.Zero), Is.True);
            new RewardResolutionSystem().Execute(first.World);
            new RewardResolutionSystem().Execute(second.World);
            Assert.That(Choice(first.World.Qinglan.Rewards.CurrentRelicChoice),
                Is.EqualTo(Choice(second.World.Qinglan.Rewards.CurrentRelicChoice)));
            Assert.That(first.World.Qinglan.Rewards.CurrentRelicChoice.CandidateCount, Is.EqualTo(3));
            Assert.That(first.World.Qinglan.Rewards.RandomCalls, Is.EqualTo(second.World.Qinglan.Rewards.RandomCalls));
            Assert.That(first.World.Progression.Offers.RandomCalls, Is.Not.EqualTo(second.World.Progression.Offers.RandomCalls));

            for (var slot = 0; slot < 3; slot++)
            {
                var choice = first.World.Qinglan.Rewards.CurrentRelicChoice;
                Assert.That(choice, Is.Not.Null);
                Assert.That(first.World.Qinglan.Rewards.SelectRelic(first.World, choice.GetCandidateAt(0)),
                    Is.EqualTo(RelicChoiceResolutionStatus.Committed));
                if (slot < 2)
                {
                    Assert.That(first.World.Qinglan.Rewards.TryQueueDirect(
                        source,
                        Transaction("test.reward.g2_3.relic", slot + 1),
                        Vector2.Zero), Is.True);
                    new RewardResolutionSystem().Execute(first.World);
                }
            }
            Assert.That(first.World.Qinglan.Rewards.Relics.Count, Is.EqualTo(3));
            Assert.That(first.World.Qinglan.Rewards.TryQueueDirect(
                source,
                Transaction("test.reward.g2_3.relic", 3),
                Vector2.Zero), Is.True);
            new RewardResolutionSystem().Execute(first.World);
            Assert.That(first.World.Qinglan.Rewards.HasPendingRelicChoice, Is.False);
            Assert.That(first.World.Qinglan.Rewards.ResultEntryCount, Is.GreaterThan(0));
            Assert.That(first.World.Qinglan.Rewards.GetResultEntryAt(
                first.World.Qinglan.Rewards.ResultEntryCount - 1).Kind,
                Is.EqualTo(RewardDeltaKind.Currency));
        }

        [Test]
        public void RelicOutputsInstallBoundedSkillsOverhealBarrierAndBossOnlyRiskRule()
        {
            var registry = LoadRegistry();
            var tokenFixture = AcquireSpecificRelic(registry, Id("qinglan.relic.blank_sword_trial_token"));
            Assert.That(tokenFixture.World.Qinglan.Rewards.ResolveDamageMultiplier(
                tokenFixture.PlayerEntity,
                new SpatialEntity(EntityKind.Actor, new EntityHandle(100, 1)),
                true), Is.EqualTo(1.25f));
            Assert.That(tokenFixture.World.Qinglan.Rewards.ResolveDamageMultiplier(
                tokenFixture.PlayerEntity,
                new SpatialEntity(EntityKind.Actor, new EntityHandle(100, 1)),
                false), Is.EqualTo(1f), "The damage bonus must not affect non-Boss targets.");
            Assert.That(tokenFixture.World.Qinglan.Rewards.ResolveDamageMultiplier(
                new SpatialEntity(EntityKind.Actor, new EntityHandle(100, 1)),
                tokenFixture.PlayerEntity,
                false), Is.EqualTo(1.15f));
            Assert.That(tokenFixture.World.Actors.TryReadStat(
                tokenFixture.Player,
                BuiltInStatIndices.Armor,
                out var armor), Is.True);
            Assert.That(armor, Is.LessThan(1f));

            var herbFixture = AcquireSpecificRelic(registry, Id("qinglan.relic.herb_garden_seed_pod"));
            Assert.That(herbFixture.World.Qinglan.Rewards.TryQueueDirect(
                Id("qinglan.reward.pickup.greenwood_dew"),
                Transaction("test.reward.g2_3.overheal", 0),
                Vector2.Zero), Is.True);
            new RewardResolutionSystem().Execute(herbFixture.World);
            Assert.That(herbFixture.World.Actors.TryReadShield(herbFixture.Player, out var shield), Is.True);
            Assert.That(shield.Current, Is.GreaterThan(0f));
            Assert.That(shield.Current, Is.LessThanOrEqualTo(shield.Maximum));

            var brokenFixture = AcquireSpecificRelic(registry, Id("qinglan.relic.broken_sword_tassel"));
            Assert.That(brokenFixture.World.Skills.InstanceCount, Is.EqualTo(1));
            Assert.That(brokenFixture.World.Qinglan.Rewards.Relics.TryGet(
                Id("qinglan.relic.broken_sword_tassel"), out var broken), Is.True);
            Assert.That(broken.IsMaximumLevel, Is.True);
        }

        [Test]
        public void ManifestationAndFirstClearUseDeterministicFallbackAndUniqueProfileSnapshot()
        {
            var fixture = CreateWorld(LoadRegistry(), Seed, 128, 100f);
            var manifestation = Transaction("test.reward.g2_3.manifestation", 0);
            var before = fixture.World.Qinglan.Rewards.RandomCalls;
            Assert.That(fixture.World.Qinglan.Rewards.TryQueueDirect(
                Id("qinglan.reward.manifestation_chest"), manifestation, Vector2.Zero), Is.True);
            new RewardResolutionSystem().Execute(fixture.World);
            Assert.That(fixture.World.Progression.RewardChoices.HasPendingChoice, Is.False);
            Assert.That(fixture.World.Qinglan.Rewards.RandomCalls, Is.EqualTo(before));
            Assert.That(fixture.World.Qinglan.Rewards.GetResultEntryAt(0).Kind,
                Is.EqualTo(RewardDeltaKind.Currency));

            var marker = Id("qinglan.progress.region_mark.qinglan");
            fixture.World.Qinglan.Rewards.SetOwnedUniqueRewards(new[] { marker });
            var firstClear = Transaction("test.reward.g2_3.first_clear", 0);
            Assert.That(fixture.World.Qinglan.Rewards.TryQueueDirect(
                Id("qinglan.reward.first_clear.tingfeng"), firstClear, Vector2.Zero), Is.True);
            new RewardResolutionSystem().Execute(fixture.World);
            var uniqueCount = 0;
            var currencyCount = 0;
            for (var index = 0; index < fixture.World.Qinglan.Rewards.ResultEntryCount; index++)
            {
                var entry = fixture.World.Qinglan.Rewards.GetResultEntryAt(index);
                if (entry.Kind == RewardDeltaKind.Unique) uniqueCount++;
                if (entry.Kind == RewardDeltaKind.Currency) currencyCount++;
            }
            Assert.That(uniqueCount, Is.Zero);
            Assert.That(currencyCount, Is.EqualTo(3),
                "Manifest fallback plus owned-unique fallback and fixed first-clear currency are deterministic.");
            Assert.That(fixture.World.Qinglan.Rewards.TryQueueDirect(
                Id("qinglan.reward.first_clear.tingfeng"), firstClear, Vector2.Zero), Is.False);
            new RewardResolutionSystem().Execute(fixture.World);
            Assert.That(fixture.World.Qinglan.Rewards.ResultEntryCount, Is.EqualTo(3));
        }

        [Test]
        public void FiveThousandRewardPickupScanAllocatesZeroBytesAfterSetup()
        {
            const int pickupCount = 5_000;
            var fixture = CreateWorld(LoadRegistry(), Seed, 6_000, 100f, 6_000);
            var rewards = fixture.World.Qinglan.Rewards;
            for (var index = 0; index < pickupCount; index++)
            {
                Assert.That(rewards.TryQueuePickup(
                    Id(PickupIds[index % PickupIds.Length]),
                    new RewardTransactionId(Seed, Id("test.reward.g2_3.performance"), index),
                    new Vector2(100f + index * 0.01f, 100f)), Is.True);
            }
            new CleanupSystem().Execute(fixture.World);
            Assert.That(rewards.ActivePickupCount, Is.EqualTo(pickupCount));
            rewards.TickPickups(fixture.World);
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var iteration = 0; iteration < 120; iteration++) rewards.TickPickups(fixture.World);
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(allocated, Is.Zero);
            Assert.That(rewards.PickupCapacityGrowthCount, Is.Zero);
            Assert.That(rewards.RejectedCapacity, Is.Zero);
        }

        [Test]
        public void DefaultDemoHubCommitsBeyondLegacySkeletonCapacityWithoutGrowth()
        {
            var fixture = CreateWorld(LoadRegistry(), Seed, 512, 100f, 256, true);
            var source = Id("test.reward.g2_3.default_capacity");
            for (var sequence = 0; sequence < 256; sequence++)
            {
                Assert.That(fixture.World.Qinglan.Rewards.TryQueueDirect(
                    Id("qinglan.reward.pickup.greenwood_dew"),
                    new RewardTransactionId(Seed, source, sequence),
                    Vector2.Zero), Is.True);
            }
            new RewardResolutionSystem().Execute(fixture.World);
            Assert.That(fixture.World.Qinglan.Rewards.CommittedCount, Is.EqualTo(256));
            Assert.That(fixture.World.Qinglan.Rewards.RejectedCapacity, Is.Zero);
        }

        private static WorldFixture AcquireSpecificRelic(ContentRegistry registry, ContentId relicId)
        {
            for (ulong seed = 1; seed <= 64; seed++)
            {
                var fixture = CreateWorld(registry, seed, 128, 100f);
                Assert.That(fixture.World.Qinglan.Rewards.TryQueueDirect(
                    Id("qinglan.reward.elite.afflicted_core"),
                    new RewardTransactionId(seed, Id("test.reward.g2_3.acquire_specific"), 0),
                    Vector2.Zero), Is.True);
                new RewardResolutionSystem().Execute(fixture.World);
                var choice = fixture.World.Qinglan.Rewards.CurrentRelicChoice;
                for (var index = 0; index < choice.CandidateCount; index++)
                {
                    if (choice.GetCandidateAt(index) != relicId) continue;
                    Assert.That(fixture.World.Qinglan.Rewards.SelectRelic(fixture.World, relicId),
                        Is.EqualTo(RelicChoiceResolutionStatus.Committed));
                    return fixture;
                }
            }
            Assert.Fail("Unable to deterministically offer relic " + relicId.Value);
            return default;
        }

        private static EntityHandle SpawnEnemy(WorldFixture fixture, string enemyId, Vector2 position)
        {
            Assert.That(fixture.World.Enemies.Catalog.TryGet(Id(enemyId), out var enemy), Is.True);
            fixture.World.Enemies.PendingSpawns.Add(new SpawnRequest(
                enemy.Index,
                position,
                false,
                false,
                1));
            new CleanupSystem().Execute(fixture.World);
            for (var index = 0; index < fixture.World.Actors.Count; index++)
            {
                var handle = fixture.World.Actors.GetHandleAt(index);
                if (fixture.World.Enemies.IsEnemy(handle)) return handle;
            }
            Assert.Fail("Enemy did not spawn.");
            return default;
        }

        private static WorldFixture CreateWorld(
            ContentRegistry registry,
            ulong seed,
            int capacity,
            float currentHealth,
            int rewardCapacity = 256,
            bool useDefaultRewardRuntime = false)
        {
            var modules = SkillModuleRegistry.CreateDefault();
            var skills = SkillRuntimeCatalog.Build(registry, modules);
            var enemies = EnemyRuntimeCatalog.Build(registry);
            var builds = BuildRuntimeCatalog.Build(registry, modules);
            Assert.That(skills.IsSuccess, Is.True, skills.Error.ToString());
            Assert.That(enemies.IsSuccess, Is.True, enemies.Error.ToString());
            Assert.That(builds.IsSuccess, Is.True, builds.Error.ToString());
            var hub = useDefaultRewardRuntime
                ? new QinglanRuntimeHub()
                : new QinglanRuntimeHub(
                    new CharacterMechanicRuntime(4),
                    new RewardRuntime(rewardCapacity, rewardCapacity));
            var world = new SimulationWorld(
                hub,
                seed,
                capacity,
                2f,
                SimulationPipeline.CreateQinglanDemo(),
                new RuntimeStatusCatalog(registry),
                null,
                new SkillRuntime(skills.Value, seed, capacity),
                new EnemyRuntime(enemies.Value, DifficultySnapshot.Default, capacity));
            var stats = StatBaseValues.CreateDefault(100f, 6f);
            var player = world.CreateActor(
                SimulationEntityState.Create(Vector2.Zero, Vector2.Zero),
                new ActorCombatInitialization(stats, currentHealth, 0f, 0f, default));
            world.SetPlayer(player);
            world.InitializeProgression(builds.Value, player, seed);
            return new WorldFixture(world, builds.Value, player);
        }

        private static string Choice(RelicChoiceSnapshot choice)
        {
            var output = string.Empty;
            for (var index = 0; index < choice.CandidateCount; index++)
                output += choice.GetCandidateAt(index).Value + "|";
            return output;
        }

        private static int CountOperations(RuntimeRewardDefinition reward, RewardOperationCode code)
        {
            var count = 0;
            for (var index = 0; index < reward.Operations.Count; index++)
                if (reward.Operations[index].Code == code) count++;
            return count;
        }

        private static RewardTransactionId Transaction(string sourceId, int sequence) =>
            new RewardTransactionId(Seed, Id(sourceId), sequence);

        private static BakedContentCatalog Bake()
        {
            var pack = AssetDatabase.LoadAssetAtPath<ContentPackAuthoring>(QinglanG12ContentSetup.PackPath);
            Assert.That(pack, Is.Not.Null);
            var result = ContentBakeUtility.Bake(pack);
            Assert.That(result.IsSuccess, Is.True, result.Error.ToString());
            return result.Value;
        }

        private static ContentRegistry LoadRegistry()
        {
            var registry = new ContentRegistry();
            var loaded = registry.Load(new[] { Bake() }, GameVersion);
            Assert.That(loaded.IsSuccess, Is.True, loaded.Error.ToString());
            return registry;
        }

        private static T Definition<T>(BakedContentCatalog catalog, ContentId id)
            where T : RuntimeContentDefinition
        {
            for (var index = 0; index < catalog.Definitions.Count; index++)
                if (catalog.Definitions[index].Id == id) return (T)catalog.Definitions[index];
            Assert.Fail("Missing definition: " + id.Value);
            return null;
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
            public SpatialEntity PlayerEntity => new SpatialEntity(EntityKind.Actor, Player);
        }
    }
}
