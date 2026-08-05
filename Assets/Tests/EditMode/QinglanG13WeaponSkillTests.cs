using System;
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
    public sealed class QinglanG13WeaponSkillTests
    {
        private const double TickSeconds = SimulationClock.TickDurationSeconds;
        private const ulong PreviewSeed = 0x473133574541504FUL;
        private static readonly ContentVersion GameVersion = new ContentVersion(0, 1, 0);
        private static readonly string[] WeaponIds =
        {
            "qinglan.skill.weapon.yufeng_sword",
            "qinglan.skill.weapon.yellow_talisman",
            "qinglan.skill.weapon.lihuo_wheel",
            "qinglan.skill.weapon.tide_orb",
            "qinglan.skill.weapon.zhenyue_seal",
            "qinglan.skill.weapon.spirit_vine_seed"
        };

        private static readonly string[] HiddenSkillIds =
        {
            "qinglan.skill.hidden.yufeng_return",
            "qinglan.skill.hidden.riding_wind_blade",
            "qinglan.skill.hidden.talisman_detonation",
            "qinglan.skill.hidden.lihuo_return_explosion",
            "qinglan.skill.hidden.tide_rising",
            "qinglan.skill.hidden.tide_falling",
            "qinglan.skill.hidden.zhenyue_guard_domain",
            "qinglan.skill.hidden.zhenyue_countershock",
            "qinglan.skill.hidden.vine_growth",
            "qinglan.skill.hidden.vine_propagation"
        };

        private static readonly float[] PreviewDamagePerSecond =
        {
            19.1999989f, 27.9999981f, 47.5999947f,
            10.999999f, 14.7999983f, 59.9999962f,
            11.999999f, 29.9999981f, 43.9999962f,
            31.9999962f, 46.3999939f, 81.99999f,
            43.1999969f, 57.1999931f, 89.59999f,
            165.999985f, 251.499969f, 491.399963f
        };

        private static readonly long[] PreviewHits =
        {
            6, 7, 7, 6, 6, 16, 5, 10, 10,
            16, 16, 20, 12, 13, 16, 248, 310, 432
        };

        private static readonly long[] PreviewTriggers =
        {
            3, 4, 4, 6, 6, 10, 3, 3, 3,
            6, 6, 8, 2, 2, 2, 7, 7, 8
        };

        [Test]
        public void CheckedInPackContainsSixWeaponsTenHiddenSkillsAndStartingWeapon()
        {
            var registry = LoadRegistry(out var catalog);

            Assert.That(catalog.Manifest.SchemaVersion, Is.EqualTo(6));
            Assert.That(
                catalog.Manifest.Version.CompareTo(new ContentVersion(0, 2, 0)),
                Is.GreaterThanOrEqualTo(0));
            Assert.That(catalog.Definitions.Count, Is.GreaterThanOrEqualTo(28));
            Assert.That(catalog.ContentHash, Is.Not.Empty);

            for (var index = 0; index < WeaponIds.Length; index++)
            {
                var skill = Skill(registry, WeaponIds[index]);
                Assert.That(skill.IsExecutable, Is.True, WeaponIds[index]);
                Assert.That(skill.MaximumLevel, Is.EqualTo(8), WeaponIds[index]);
                AssertContinuousLevels(skill, WeaponIds[index]);
            }
            for (var index = 0; index < HiddenSkillIds.Length; index++)
            {
                Assert.That(Skill(registry, HiddenSkillIds[index]).IsExecutable, Is.True, HiddenSkillIds[index]);
            }

            Assert.That(
                registry.TryGet(Id("qinglan.character.lu_qingye"), out RuntimeCharacterDefinition character),
                Is.True);
            Assert.That(character.StartingSkillIds, Is.EqualTo(new[] { Id(WeaponIds[0]) }));
        }

        [Test]
        public void WeaponGraphCompilesAtLevelsOneFourAndEightWithStableGenericReferences()
        {
            var registry = LoadRegistry(out _);
            var catalogResult = SkillRuntimeCatalog.Build(registry, SkillModuleRegistry.CreateDefault());
            Assert.That(catalogResult.IsSuccess, Is.True, catalogResult.Error.ToString());

            for (var index = 0; index < WeaponIds.Length; index++)
            {
                var runtimeIndex = IndexOf(registry, WeaponIds[index]);
                Assert.That(catalogResult.Value.TryGet(runtimeIndex, out var compiled), Is.True, WeaponIds[index]);
                var first = compiled.GetLevel(1);
                var middle = compiled.GetLevel(4);
                var final = compiled.GetLevel(8);
                Assert.That(first.Level, Is.EqualTo(1));
                Assert.That(middle.Level, Is.EqualTo(4));
                Assert.That(final.Level, Is.EqualTo(8));
                Assert.That(LevelDiffers(first, final), Is.True, WeaponIds[index]);
            }

            var yufeng = Skill(registry, WeaponIds[0]);
            Assert.That(yufeng.Delivery.ModuleId, Is.EqualTo(SkillModuleIds.DeliveryOutboundReturn));
            Assert.That(yufeng.Delivery.ReferenceId0, Is.EqualTo(Id(HiddenSkillIds[0])));
            Assert.That(yufeng.Delivery.ReferenceId1, Is.EqualTo(Id("qinglan.trait.lu_qingye.riding_wind")));

            var talisman = Skill(registry, WeaponIds[1]);
            Assert.That(talisman.Effects[2].Code, Is.EqualTo(EffectOpCode.SpawnSecondarySkill));
            Assert.That(talisman.Effects[2].ReferenceId0, Is.EqualTo(Id(HiddenSkillIds[2])));

            var tide = Skill(registry, WeaponIds[3]);
            Assert.That(tide.Effects[0].Code, Is.EqualTo(EffectOpCode.SpawnSecondarySkill));
            Assert.That(tide.Effects[0].ReferenceId0, Is.EqualTo(Id(HiddenSkillIds[4])));
            Assert.That(tide.Effects[0].ReferenceId1, Is.EqualTo(Id(HiddenSkillIds[5])));
            Assert.That(tide.Effects[0].Int0, Is.EqualTo(1));
        }

        [Test]
        public void YufengReturnSecondaryRequiresCurrentTierThreeOutput()
        {
            var registry = LoadRegistry(out _);
            var withoutTier = RunSingleYufeng(registry, false);
            var withTier = RunSingleYufeng(registry, true);

            Assert.That(withoutTier.TriggerCount, Is.GreaterThanOrEqualTo(3));
            Assert.That(withTier.TriggerCount, Is.GreaterThan(withoutTier.TriggerCount));
            Assert.That(withTier.HitCount, Is.GreaterThan(withoutTier.HitCount));
            Assert.That(withoutTier.ActiveDeliveryCount, Is.Zero);
            Assert.That(withTier.ActiveDeliveryCount, Is.Zero);
        }

        [Test]
        public void TalismanThirdMarkDetonatesAgainstOriginalHitTargetAndConsumesStacks()
        {
            var registry = LoadRegistry(out _);
            var fixture = CreateWorld(registry);
            var owner = Actor(fixture.World, Vector2.Zero, 1_000f);
            var target = Actor(fixture.World, Vector2.UnitX, 1_000f);
            var add = fixture.Skills.AddInstance(owner, IndexOf(registry, WeaponIds[1]));
            Assert.That(add.IsSuccess, Is.True, add.Error.ToString());
            Assert.That(fixture.Skills.RemoveInstance(add.Value), Is.True);
            var markedIndex = IndexOf(registry, "qinglan.status.marked");
            for (var stack = 0; stack < 3; stack++)
            {
                Assert.That(fixture.World.QueueStatus(new StatusApplicationRequest(
                    owner,
                    target,
                    Id(WeaponIds[1]),
                    markedIndex,
                    1f,
                    0)), Is.True);
            }
            Advance(fixture.World, 1);
            Assert.That(fixture.World.Actors.TryReadStatus(target.Handle, markedIndex, out var marked), Is.True);
            Assert.That(marked.Stacks, Is.EqualTo(3));

            fixture.Skills.QueueSecondary(
                owner,
                target,
                IndexOf(registry, HiddenSkillIds[2]),
                Vector2.UnitX,
                Vector2.UnitX,
                Id(WeaponIds[1]),
                1);
            Advance(fixture.World, 1);

            Assert.That(fixture.World.Actors.TryReadHealth(target.Handle, out var health), Is.True);
            Assert.That(health.Current, Is.EqualTo(985f));
            Assert.That(
                fixture.World.Actors.TryReadStatus(target.Handle, markedIndex, out _),
                Is.False,
                "The detonation transaction must consume exactly the three qualifying marks atomically.");
        }

        [Test]
        public void TideOrbAlternatesRisingControlAndFallingDamageAtPrimaryLevelEight()
        {
            var registry = LoadRegistry(out _);
            var fixture = CreateWorld(registry);
            var owner = Actor(fixture.World, Vector2.Zero, 1_000f);
            var target = Actor(fixture.World, Vector2.UnitX, 1_000f);
            var add = fixture.Skills.AddInstance(owner, IndexOf(registry, WeaponIds[3]), 8);
            Assert.That(add.IsSuccess, Is.True, add.Error.ToString());

            Advance(fixture.World, 4);

            Assert.That(
                fixture.World.Actors.TryReadStatus(
                    target.Handle,
                    IndexOf(registry, "qinglan.status.slowed"),
                    out _),
                Is.True,
                "The first phase must be Rising Tide.");
            Advance(fixture.World, 82);
            Assert.That(fixture.World.Actors.TryReadHealth(target.Handle, out var health), Is.True);
            Assert.That(health.Current, Is.EqualTo(979.5f).Within(0.0001f),
                "The second phase must use the level-8 Falling Tide damage patch.");
            Assert.That(fixture.Skills.TriggerCount, Is.EqualTo(5));
        }

        [Test]
        public void VineAreaAndSecondaryProcGraphStayBoundedAndCleanUpWithOwnerLifecycle()
        {
            var registry = LoadRegistry(out _);
            var fixture = CreateWorld(registry);
            var owner = Actor(fixture.World, Vector2.Zero, 1_000f);
            var target = Actor(fixture.World, Vector2.UnitX, 1_000f);
            var vine = fixture.Skills.AddInstance(owner, IndexOf(registry, WeaponIds[5]), 8);
            Assert.That(vine.IsSuccess, Is.True, vine.Error.ToString());
            Assert.That(fixture.World.QueueSkillTrigger(new SkillTriggerContext(
                SkillTriggerEventType.OnKill,
                owner,
                target,
                Vector2.UnitX,
                Vector2.UnitX,
                Id(WeaponIds[5]),
                IndexOf(registry, WeaponIds[5]),
                0)), Is.True);

            Advance(fixture.World, 12);
            Assert.That(fixture.Skills.ActiveDeliveryCount, Is.EqualTo(1));
            Assert.That(fixture.World.Areas.Count, Is.EqualTo(1));
            Assert.That(
                fixture.World.Actors.TryReadStatus(
                    target.Handle,
                    IndexOf(registry, "qinglan.status.poisoned"),
                    out _),
                Is.True);

            var yufeng = fixture.Skills.AddInstance(owner, IndexOf(registry, WeaponIds[0]));
            Assert.That(yufeng.IsSuccess, Is.True, yufeng.Error.ToString());
            Assert.That(fixture.Skills.RemoveInstance(yufeng.Value), Is.True);
            fixture.Skills.QueueSecondary(
                owner,
                target,
                IndexOf(registry, HiddenSkillIds[0]),
                Vector2.Zero,
                Vector2.UnitX,
                Id(WeaponIds[0]),
                fixture.World.CombatRules.MaximumProcDepth);
            Advance(fixture.World, 2);
            Assert.That(fixture.World.Diagnostics.TruncatedProcChains, Is.EqualTo(1));

            fixture.World.Commands.Remove(EntityKind.Actor, owner.Handle);
            Advance(fixture.World, 320);
            Assert.That(fixture.Skills.InstanceCount, Is.Zero);
            Assert.That(fixture.Skills.ActiveDeliveryCount, Is.Zero);
            Assert.That(fixture.World.Areas.Count, Is.Zero);
        }

        [Test]
        public void SixWeaponPreviewLevelsOneFourEightAreDeterministicZeroAllocationGoldens()
        {
            var registry = LoadRegistry(out _);
            var levels = new[] { 1, 4, 8 };
            for (var weapon = 0; weapon < WeaponIds.Length; weapon++)
            {
                for (var levelIndex = 0; levelIndex < levels.Length; levelIndex++)
                {
                    var level = levels[levelIndex];
                    var request = new SkillPreviewRequest(PreviewSeed, 5f, 16, level);
                    var first = SkillPreviewHarness.RunDetailed(
                        registry,
                        IndexOf(registry, WeaponIds[weapon]),
                        request);
                    var second = SkillPreviewHarness.RunDetailed(
                        registry,
                        IndexOf(registry, WeaponIds[weapon]),
                        request);
                    Assert.That(first.IsSuccess, Is.True, first.Error.ToString());
                    Assert.That(second.IsSuccess, Is.True, second.Error.ToString());
                    Assert.That(second.Value.Summary, Is.EqualTo(first.Value.Summary));
                    Assert.That(first.Value.ManagedAllocationBytes, Is.Zero, WeaponIds[weapon] + " L" + level);
                    var goldenIndex = (weapon * levels.Length) + levelIndex;
                    Assert.That(
                        first.Value.Summary.DamagePerSecond,
                        Is.EqualTo(PreviewDamagePerSecond[goldenIndex]),
                        WeaponIds[weapon] + " L" + level);
                    Assert.That(first.Value.Summary.HitCount, Is.EqualTo(PreviewHits[goldenIndex]));
                    Assert.That(first.Value.Summary.TriggerCount, Is.EqualTo(PreviewTriggers[goldenIndex]));
                }
            }
        }

        private static SkillRuntime RunSingleYufeng(ContentRegistry registry, bool tierThree)
        {
            var fixture = CreateWorld(registry);
            var owner = Actor(fixture.World, Vector2.Zero, 1_000f);
            Actor(fixture.World, new Vector2(2f, 0f), 1_000f);
            Actor(fixture.World, new Vector2(4f, 0f), 1_000f);
            var add = fixture.Skills.AddInstance(owner, IndexOf(registry, WeaponIds[0]));
            Assert.That(add.IsSuccess, Is.True, add.Error.ToString());
            Assert.That(fixture.Skills.TryGetInstance(add.Value, out var primary), Is.True);
            Assert.That(primary.Definition.GetLevel(1).CooldownSeconds, Is.EqualTo(1.8f));
            Assert.That(
                primary.Definition.GetLevel(1).Delivery.ReferenceId1,
                Is.EqualTo(Id("qinglan.trait.lu_qingye.riding_wind")));
            Assert.That(QinglanCharacterBinding.Attach(
                registry,
                Id("qinglan.character.lu_qingye"),
                owner.Handle,
                fixture.World.Qinglan.Mechanics).IsSuccess, Is.True);
            Assert.That(
                fixture.World.Qinglan.Mechanics.MatchesCurrentOutput(
                    owner.Handle,
                    Id("qinglan.trait.lu_qingye.riding_wind")),
                Is.False);
            if (tierThree)
            {
                fixture.World.Qinglan.Mechanics.Accumulate(
                    new ResolvedMovement(owner, MovementSource.PlayerCommand, 30f));
                Assert.That(
                    fixture.World.Qinglan.Mechanics.MatchesCurrentOutput(
                        owner.Handle,
                        Id("qinglan.trait.lu_qingye.riding_wind")),
                    Is.True);
            }

            Advance(fixture.World, 360);
            Assert.That(fixture.Skills.RemoveInstance(add.Value), Is.True);
            Advance(fixture.World, 150);
            return fixture.Skills;
        }

        private static WorldFixture CreateWorld(ContentRegistry registry)
        {
            var catalog = SkillRuntimeCatalog.Build(registry, SkillModuleRegistry.CreateDefault());
            Assert.That(catalog.IsSuccess, Is.True, catalog.Error.ToString());
            var skills = new SkillRuntime(catalog.Value, PreviewSeed, 64);
            var world = new SimulationWorld(
                new QinglanRuntimeHub(new CharacterMechanicRuntime(4)),
                seed: PreviewSeed,
                initialEntityCapacity: 64,
                pipeline: SimulationPipeline.CreateQinglanDemo(),
                statusCatalog: new RuntimeStatusCatalog(registry),
                skillRuntime: skills);
            return new WorldFixture(world, skills);
        }

        private static SpatialEntity Actor(SimulationWorld world, Vector2 position, float health)
        {
            return new SpatialEntity(
                EntityKind.Actor,
                world.CreateActor(
                    SimulationEntityState.Create(position, Vector2.Zero),
                    ActorCombatInitialization.CreateDefault(health, 0f)));
        }

        private static void Advance(SimulationWorld world, int ticks)
        {
            var runner = new FixedTickRunner(world);
            for (var tick = 0; tick < ticks; tick++)
            {
                Assert.That(runner.Advance(TickSeconds), Is.EqualTo(1));
            }
        }

        private static bool LevelDiffers(RuntimeSkillLevel first, RuntimeSkillLevel final)
        {
            if (first.CooldownSeconds != final.CooldownSeconds ||
                first.Targeting.Value0 != final.Targeting.Value0 ||
                first.Targeting.Int0 != final.Targeting.Int0 ||
                first.Delivery.Value0 != final.Delivery.Value0 ||
                first.Delivery.Value1 != final.Delivery.Value1 ||
                first.Delivery.Value2 != final.Delivery.Value2 ||
                first.Delivery.Value3 != final.Delivery.Value3 ||
                first.Delivery.Int0 != final.Delivery.Int0)
            {
                return true;
            }
            for (var index = 0; index < first.Effects.Count; index++)
            {
                if (first.Effects[index].Value0 != final.Effects[index].Value0 ||
                    first.Effects[index].Int0 != final.Effects[index].Int0)
                {
                    return true;
                }
            }
            return false;
        }

        private static void AssertContinuousLevels(RuntimeSkillDefinition skill, string message)
        {
            for (var level = 2; level <= 8; level++)
            {
                var found = false;
                for (var index = 0; index < skill.LevelPatches.Count; index++)
                {
                    if (skill.LevelPatches[index].Level == level)
                    {
                        found = true;
                        break;
                    }
                }
                Assert.That(found, Is.True, message + " missing level " + level + ".");
            }
        }

        private static ContentRegistry LoadRegistry(out BakedContentCatalog catalog)
        {
            var pack = AssetDatabase.LoadAssetAtPath<ContentPackAuthoring>(QinglanG12ContentSetup.PackPath);
            Assert.That(pack, Is.Not.Null, QinglanG12ContentSetup.PackPath);
            var bake = ContentBakeUtility.Bake(pack);
            Assert.That(bake.IsSuccess, Is.True, bake.IsSuccess ? string.Empty : bake.Error.ToString());
            catalog = bake.Value;
            var registry = new ContentRegistry();
            var load = registry.Load(new[] { catalog }, GameVersion);
            Assert.That(load.IsSuccess, Is.True, load.IsSuccess ? string.Empty : load.Error.ToString());
            return registry;
        }

        private static RuntimeSkillDefinition Skill(ContentRegistry registry, string id)
        {
            Assert.That(registry.TryGet(Id(id), out RuntimeSkillDefinition skill), Is.True, id);
            return skill;
        }

        private static RuntimeContentIndex IndexOf(ContentRegistry registry, string id)
        {
            Assert.That(registry.TryGet(Id(id), out ContentRegistryEntry entry), Is.True, id);
            return entry.Index;
        }

        private static ContentId Id(string value) => ContentId.Create(value).Value;

        private readonly struct WorldFixture
        {
            public WorldFixture(SimulationWorld world, SkillRuntime skills)
            {
                World = world;
                Skills = skills;
            }

            public SimulationWorld World { get; }
            public SkillRuntime Skills { get; }
        }
    }
}
