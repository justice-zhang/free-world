using System;
using System.IO;
using Game.Content.Authoring;
using Game.Content.Runtime;
using Game.Core;
using Game.Editor;
using Game.Simulation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using NumericsVector2 = System.Numerics.Vector2;

namespace Game.Tests.EditMode
{
    public sealed class QinglanG22BossTests
    {
        private static readonly ContentVersion GameVersion = new ContentVersion(0, 1, 0);
        private static readonly ContentId ZhezhiId = Id("qinglan.boss.zhezhi");
        private static readonly ContentId TingfengId = Id("qinglan.boss.tingfeng");
        private static readonly ContentId GuideId = Id("qinglan.objective.wind_altar.guide");
        private static readonly ContentId ListenId = Id("qinglan.objective.wind_altar.listen");
        private static readonly ContentId StopId = Id("qinglan.objective.wind_altar.stop_balance");

        [Test]
        public void PackPointSevenContainsTwoThreePhaseBossesAndTwoOneShotRules()
        {
            var first = Bake();
            var second = Bake();
            Assert.That(first.Manifest.Version.CompareTo(new ContentVersion(0, 7, 0)), Is.GreaterThanOrEqualTo(0));
            Assert.That(first.Manifest.SchemaVersion, Is.EqualTo(6));
            Assert.That(first.Definitions.Count, Is.GreaterThanOrEqualTo(121));
            Assert.That(first.ContentHash, Has.Length.EqualTo(64));
            Assert.That(second.ContentHash, Is.EqualTo(first.ContentHash));

            var checkedIn = JsonUtility.FromJson<BakedContentCatalogDto>(
                File.ReadAllText(Path.GetFullPath(QinglanG12ContentSetup.BakedCatalogPath))).ToCatalog();
            Assert.That(checkedIn.IsSuccess, Is.True, checkedIn.Error.ToString());
            Assert.That(checkedIn.Value.ContentHash, Is.EqualTo(first.ContentHash));

            var zhezhi = Definition<RuntimeBossDefinition>(first, ZhezhiId);
            var tingfeng = Definition<RuntimeBossDefinition>(first, TingfengId);
            Assert.That(zhezhi.Phases.Count, Is.EqualTo(3));
            Assert.That(tingfeng.Phases.Count, Is.EqualTo(3));
            Assert.That(zhezhi.EnemyId, Is.EqualTo(Id("qinglan.enemy.boss.zhezhi")));
            Assert.That(tingfeng.EnemyId, Is.EqualTo(Id("qinglan.enemy.boss.tingfeng")));
            Assert.That(zhezhi.ResistanceMultiplier, Is.EqualTo(0.35f));
            Assert.That(tingfeng.ResistanceMultiplier, Is.EqualTo(0.25f));
            Assert.That(Thresholds(zhezhi), Is.EqualTo(new[] { 0.65f, 0.30f, 0f }));
            Assert.That(Thresholds(tingfeng), Is.EqualTo(new[] { 0.70f, 0.35f, 0f }));

            var encounter = Definition<RuntimeEncounterSchedule>(
                first,
                Id("qinglan.encounter.old_court.demo_12m"));
            Assert.That(encounter.Phases[4].BossRules.Count, Is.EqualTo(1));
            Assert.That(encounter.Phases[4].BossRules[0].SpawnTimeSeconds, Is.EqualTo(360f));
            Assert.That(encounter.Phases[4].BossRules[0].BossDefinitionId, Is.EqualTo(ZhezhiId));
            Assert.That(encounter.Phases[8].BossRules.Count, Is.EqualTo(1));
            Assert.That(encounter.Phases[8].BossRules[0].SpawnTimeSeconds, Is.EqualTo(719.9f));
            Assert.That(encounter.Phases[8].BossRules[0].BossDefinitionId, Is.EqualTo(TingfengId));
        }

        [Test]
        public void TingfengEightObjectiveCombinationsHaveGoldenSafeModifiersWithoutPhaseSkipping()
        {
            var registry = LoadRegistry(out _);
            Assert.That(registry.TryGet(TingfengId, out RuntimeBossDefinition definition), Is.True);
            var expectedSpatial = new[] { 1f, 0.70f, 1f, 0.70f, 1f, 0.70f, 1f, 0.70f };
            var expectedDeception = new[] { 1f, 1f, 0.65f, 0.65f, 1f, 1f, 0.65f, 0.65f };
            var expectedCadence = new[] { 1f, 1f, 1f, 1f, 1.25f, 1.25f, 1.25f, 1.25f };

            for (var mask = 0; mask < 8; mask++)
            {
                var runtime = new BossPhaseRuntime(1, 8);
                var owner = new EntityHandle(mask, 1);
                Assert.That(runtime.TryAttach(owner, definition, registry), Is.True);
                Assert.That(runtime.TrySetRuleState(owner, GuideId, (mask & 1) != 0), Is.True);
                Assert.That(runtime.TrySetRuleState(owner, ListenId, (mask & 2) != 0), Is.True);
                Assert.That(runtime.TrySetRuleState(owner, StopId, (mask & 4) != 0), Is.True);
                Assert.That(runtime.TryAdvance(owner, 0.50f, false, out var transition), Is.True);
                Assert.That(transition.FromPhase, Is.Zero);
                Assert.That(transition.ToPhase, Is.EqualTo(1));
                Assert.That(runtime.TryGetModifierSnapshot(owner, out var modifiers), Is.True);
                Assert.That(modifiers.ActiveRuleMask, Is.EqualTo(mask));
                Assert.That(modifiers.SpatialLoadMultiplier, Is.EqualTo(expectedSpatial[mask]));
                Assert.That(modifiers.DeceptionMultiplier, Is.EqualTo(expectedDeception[mask]));
                Assert.That(modifiers.CadenceIntervalMultiplier, Is.EqualTo(expectedCadence[mask]));
                Assert.That(modifiers.BonusOutputEligible, Is.EqualTo(mask == 7));
                Assert.That(runtime.GetCurrentPhaseSkillCount(owner), Is.EqualTo(3));
                Assert.That(runtime.TryGet(owner, out var snapshot), Is.True);
                Assert.That(snapshot.Phase, Is.EqualTo(1), "Map objectives must not skip health phases.");
                Assert.That(modifiers.SpatialLoadMultiplier, Is.GreaterThanOrEqualTo(0.70f));
                Assert.That(modifiers.DeceptionMultiplier, Is.GreaterThanOrEqualTo(0.65f));
            }
        }

        [Test]
        public void MultiThresholdCrossingIsOrderedAndLethalDamageFinalizesOnlyOnce()
        {
            var registry = LoadRegistry(out _);
            Assert.That(registry.TryGet(ZhezhiId, out RuntimeBossDefinition source), Is.True);
            var definition = new RuntimeBossDefinition(
                Id("test.boss.g2_2.reward_once"),
                "content.test.boss.g2_2.reward_once.name",
                "content.test.boss.g2_2.reward_once.description",
                "Assets/Test/G22RewardBoss.asset",
                Array.Empty<ContentTag>(),
                source.EnemyId,
                CopyPhases(source),
                Id("qinglan.reward.map.exploration_token"),
                source.ResistanceMultiplier,
                source.PresentationProfileId);
            var runtime = new BossPhaseRuntime(1, 8);
            var owner = new EntityHandle(4, 1);
            Assert.That(runtime.TryAttach(owner, definition, registry), Is.True);
            Assert.That(runtime.TryAdvance(owner, 0.20f, false, out var crossed), Is.True);
            Assert.That(crossed.FromPhase, Is.Zero);
            Assert.That(crossed.ToPhase, Is.EqualTo(2));
            Assert.That(crossed.CrossedPhaseCount, Is.EqualTo(2));
            Assert.That(runtime.TryFinalizeDeath(owner, 77UL, out var transaction, out var reward), Is.True);
            Assert.That(transaction, Is.EqualTo(new RewardTransactionId(77UL, definition.Id, 0)));
            Assert.That(reward, Is.EqualTo(definition.RewardId));
            Assert.That(runtime.TryFinalizeDeath(owner, 77UL, out _, out _), Is.False);
            Assert.That(runtime.CompletedCount, Is.EqualTo(1));
            Assert.That(runtime.TryGet(owner, out var snapshot), Is.True);
            Assert.That(snapshot.Phase, Is.EqualTo(3));
            Assert.That(snapshot.DeathFinalized, Is.True);
        }

        [Test]
        public void CleanupPoliciesExpireFinishTelegraphAndPersistWithoutInvisibleDamage()
        {
            var registry = LoadRegistry(out _);
            Assert.That(registry.TryGet(ZhezhiId, out RuntimeBossDefinition definition), Is.True);
            var runtime = new BossPhaseRuntime(1, 8);
            var owner = new EntityHandle(8, 1);
            Assert.That(runtime.TryAttach(owner, definition, registry), Is.True);

            Assert.That(runtime.TryTrackOwnedEffect(
                owner, BossOwnedEffectKind.Projectile, true, out var expire), Is.True);
            Assert.That(runtime.TryAdvance(owner, 0.50f, false, out var first), Is.True);
            Assert.That(first.ExpiredEffects, Is.EqualTo(1));
            Assert.That(runtime.TryGetOwnedEffect(expire, out var expired), Is.True);
            Assert.That(expired.State, Is.EqualTo(BossOwnedEffectState.Expired));
            Assert.That(expired.DamageEnabled, Is.False);

            Assert.That(runtime.TryTrackOwnedEffect(
                owner, BossOwnedEffectKind.Area, true, out var finish), Is.True);
            Assert.That(runtime.TryAdvance(owner, 0.20f, false, out var second), Is.True);
            Assert.That(second.TelegraphOnlyEffects, Is.EqualTo(1));
            Assert.That(runtime.TryGetOwnedEffect(finish, out var telegraph), Is.True);
            Assert.That(telegraph.State, Is.EqualTo(BossOwnedEffectState.TelegraphOnly));
            Assert.That(telegraph.DamageEnabled, Is.False);
            Assert.That(runtime.TryFinishTelegraph(finish), Is.True);

            Assert.That(runtime.TryTrackOwnedEffect(
                owner, BossOwnedEffectKind.Skill, false, out var persist), Is.True);
            Assert.That(runtime.TryFinalizeDeath(owner, 1UL, out _, out _), Is.True);
            Assert.That(runtime.TryGetOwnedEffect(persist, out var deadCleanup), Is.True);
            Assert.That(deadCleanup.State, Is.EqualTo(BossOwnedEffectState.Expired));
            Assert.That(deadCleanup.DamageEnabled, Is.False);
        }

        [Test]
        public void BossControlStatusDurationUsesDefinitionResistanceMultiplier()
        {
            var registry = LoadRegistry(out _);
            Assert.That(registry.TryGet(TingfengId, out RuntimeBossDefinition definition), Is.True);
            Assert.That(registry.TryGet(Id("qinglan.status.rooted"), out RuntimeStatusDefinition rooted), Is.True);
            Assert.That(registry.TryGet(Id("qinglan.status.burning"), out RuntimeStatusDefinition burning), Is.True);
            var runtime = new BossPhaseRuntime(1, 4);
            var owner = new EntityHandle(9, 1);
            Assert.That(runtime.TryAttach(owner, definition, registry), Is.True);
            Assert.That(runtime.ResolveStatusDuration(owner, rooted, 1f), Is.EqualTo(0.25f));
            Assert.That(runtime.ResolveStatusDuration(owner, burning, 4f), Is.EqualTo(4f));
        }

        [Test]
        public void WorldBindingPreloadsAllSkillsAndEnablesOnlyTheCurrentPhaseSet()
        {
            var registry = LoadRegistry(out _);
            Assert.That(registry.TryGet(TingfengId, out RuntimeBossDefinition definition), Is.True);
            Assert.That(registry.TryGet(
                Id("qinglan.skill.boss.tingfeng.sword_qi"),
                out ContentRegistryEntry baseSkill), Is.True);
            var catalog = SkillRuntimeCatalog.Build(registry, SkillModuleRegistry.CreateDefault());
            Assert.That(catalog.IsSuccess, Is.True, catalog.Error.ToString());
            var skills = new SkillRuntime(catalog.Value, 1UL, 32);
            var owner = new EntityHandle(12, 1);
            var spatialOwner = new SpatialEntity(EntityKind.Actor, owner);
            var added = skills.AddInstance(spatialOwner, baseSkill.Index);
            Assert.That(added.IsSuccess, Is.True, added.Error.ToString());
            var runtime = new BossPhaseRuntime(1, 8);

            Assert.That(runtime.TryAttachWorld(
                owner, definition, registry, skills, added.Value), Is.True);
            Assert.That(skills.InstanceCount, Is.EqualTo(7));
            Assert.That(skills.CountUnsuppressedOwned(spatialOwner), Is.EqualTo(2));
            Assert.That(runtime.TryAdvance(owner, 0.50f, false, out _), Is.True);
            Assert.That(skills.CountUnsuppressedOwned(spatialOwner), Is.EqualTo(3));
            Assert.That(runtime.TryAdvance(owner, 0.20f, false, out _), Is.True);
            Assert.That(skills.CountUnsuppressedOwned(spatialOwner), Is.EqualTo(3));
            Assert.That(runtime.TryFinalizeDeath(owner, 1UL, out _, out _), Is.True);
            Assert.That(skills.CountUnsuppressedOwned(spatialOwner), Is.Zero);
        }

        [Test]
        public void FinishTelegraphPolicyDetachesRealDeliveriesWithoutRemovingTheirVisualLifetime()
        {
            var registry = LoadRegistry(out _);
            Assert.That(registry.TryGet(TingfengId, out RuntimeBossDefinition definition), Is.True);
            Assert.That(registry.TryGet(
                Id("qinglan.skill.boss.tingfeng.sword_qi"),
                out ContentRegistryEntry baseSkill), Is.True);
            var catalog = SkillRuntimeCatalog.Build(registry, SkillModuleRegistry.CreateDefault());
            Assert.That(catalog.IsSuccess, Is.True, catalog.Error.ToString());
            var skills = new SkillRuntime(catalog.Value, 1UL, 32);
            var world = new SimulationWorld(skillRuntime: skills);
            var stats = StatBaseValues.CreateDefault(100f, 0f);
            var owner = world.CreateActor(
                SimulationEntityState.Create(NumericsVector2.Zero, NumericsVector2.Zero),
                new ActorCombatInitialization(stats, 50f, 0f, 0f, default));
            var target = world.CreateActor(
                SimulationEntityState.Create(new NumericsVector2(2f, 0f), NumericsVector2.Zero),
                ActorCombatInitialization.CreateDefault());
            var spatialOwner = new SpatialEntity(EntityKind.Actor, owner);
            var added = skills.AddInstance(spatialOwner, baseSkill.Index);
            Assert.That(added.IsSuccess, Is.True, added.Error.ToString());
            var runtime = new BossPhaseRuntime(1, 16);
            Assert.That(runtime.TryAttachWorld(
                owner, definition, registry, skills, added.Value), Is.True);

            runtime.Tick(world);
            Assert.That(runtime.TryGet(owner, out var phaseOne), Is.True);
            Assert.That(phaseOne.Phase, Is.EqualTo(1));
            skills.TickTriggers(world);
            skills.ApplyPendingSpawns(world);
            Assert.That(skills.ActiveDeliveryCount, Is.EqualTo(3));
            var visualEntityCount = world.Projectiles.Count + world.Areas.Count;
            Assert.That(visualEntityCount, Is.EqualTo(3));

            Assert.That(world.QueueDamage(new DamagePacket(
                new SpatialEntity(EntityKind.Actor, target),
                spatialOwner,
                Id("test.damage.g2_2.phase_exit"),
                DamageType.True,
                DamageTags.Direct,
                30f,
                false,
                0f,
                NumericsVector2.Zero,
                NumericsVector2.Zero,
                0)), Is.True);
            new DamageResolutionSystem().Execute(world);
            runtime.Tick(world);

            Assert.That(runtime.TryGet(owner, out var phaseTwo), Is.True);
            Assert.That(phaseTwo.Phase, Is.EqualTo(2));
            Assert.That(skills.ActiveDeliveryCount, Is.Zero,
                "Finished telegraph visuals must be detached from all damage records.");
            Assert.That(world.Projectiles.Count + world.Areas.Count, Is.EqualTo(visualEntityCount),
                "The visual entity keeps its bounded lifetime instead of being removed immediately.");
            Assert.That(world.Commands.Count, Is.Zero);
            skills.TickDeliveries(world);
            Assert.That(skills.Commands.Count, Is.Zero);
        }

        [Test]
        public void PhaseResolutionHotPathAllocatesZeroBytes()
        {
            var registry = LoadRegistry(out _);
            Assert.That(registry.TryGet(TingfengId, out RuntimeBossDefinition definition), Is.True);
            var runtime = new BossPhaseRuntime(1, 4);
            runtime.ResolvePhase(definition, 0, 0.5f, false);
            var before = GC.GetAllocatedBytesForCurrentThread();
            var checksum = 0;
            for (var index = 0; index < 54_000; index++)
                checksum += runtime.ResolvePhase(definition, 0, 0.5f, false);
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(checksum, Is.EqualTo(54_000));
            Assert.That(allocated, Is.Zero);
        }

        private static float[] Thresholds(RuntimeBossDefinition definition)
        {
            var result = new float[definition.Phases.Count];
            for (var index = 0; index < result.Length; index++)
                result[index] = definition.Phases[index].HealthThreshold;
            return result;
        }

        private static RuntimeBossPhase[] CopyPhases(RuntimeBossDefinition definition)
        {
            var result = new RuntimeBossPhase[definition.Phases.Count];
            for (var index = 0; index < result.Length; index++)
            {
                var source = definition.Phases[index];
                var rules = new ContentId[source.AcceptedRuleIds.Count];
                for (var rule = 0; rule < rules.Length; rule++) rules[rule] = source.AcceptedRuleIds[rule];
                result[index] = new RuntimeBossPhase(source.HealthThreshold, rules, source.CleanupPolicy);
            }
            return result;
        }

        private static BakedContentCatalog Bake()
        {
            var pack = AssetDatabase.LoadAssetAtPath<ContentPackAuthoring>(QinglanG12ContentSetup.PackPath);
            Assert.That(pack, Is.Not.Null);
            var result = ContentBakeUtility.Bake(pack);
            Assert.That(result.IsSuccess, Is.True, result.Error.ToString());
            return result.Value;
        }

        private static ContentRegistry LoadRegistry(out BakedContentCatalog catalog)
        {
            catalog = Bake();
            var registry = new ContentRegistry();
            var loaded = registry.Load(new[] { catalog }, GameVersion);
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
    }
}
