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
    public sealed class QinglanG14ProgressionTests
    {
        private const ulong Seed = 0x47313450524F4752UL;
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

        private static readonly string[] EvolutionIds =
        {
            "qinglan.evolution.qinglan_flowing_shadow_sword",
            "qinglan.evolution.taiyi_spirit_sealing_array",
            "qinglan.evolution.chilu_hundred_craft_wheel",
            "qinglan.evolution.mirror_sea_tide_wheel",
            "qinglan.evolution.mountain_boundary_seal",
            "qinglan.evolution.earth_vein_spring_branch"
        };

        private static readonly string[] ResultSkillIds =
        {
            "qinglan.skill.evolved.qinglan_flowing_shadow_sword",
            "qinglan.skill.evolved.taiyi_spirit_sealing_array",
            "qinglan.skill.evolved.chilu_hundred_craft_wheel",
            "qinglan.skill.evolved.mirror_sea_tide_wheel",
            "qinglan.skill.evolved.mountain_boundary_seal",
            "qinglan.skill.evolved.earth_vein_spring_branch"
        };

        private static readonly string[] SynergyIds =
        {
            "qinglan.synergy.moving_sword_path",
            "qinglan.synergy.talisman_detonation",
            "qinglan.synergy.living_garden"
        };

        private static readonly float[] PreviewDamagePerSecond =
        {
            165.6667f, 467.5f, 128f, 46.66666f, 58.33333f, 215f
        };

        private static readonly long[] PreviewHits = { 179, 340, 84, 28, 25, 480 };
        private static readonly long[] PreviewTriggers = { 15, 89, 9, 8, 28, 24 };

        [Test]
        public void CheckedInPackContainsCompleteG14ProgressionSlice()
        {
            var registry = LoadRegistry(out var baked);

            Assert.That(baked.Manifest.SchemaVersion, Is.EqualTo(6));
            Assert.That(baked.Manifest.Version, Is.EqualTo(new ContentVersion(0, 3, 0)));
            Assert.That(baked.Definitions.Count, Is.EqualTo(68));
            Assert.That(
                baked.ContentHash,
                Is.EqualTo("ab26f20b76412404f914168e75689528faaf48040e4265131f73fb1a97fc6e1a"));

            for (var index = 0; index < PassiveIds.Length; index++)
            {
                Assert.That(registry.TryGet(Id(PassiveIds[index]), out RuntimePassiveDefinition passive), Is.True);
                Assert.That(passive.MaximumLevel, Is.EqualTo(5), PassiveIds[index]);
                AssertEveryLevelHasModifier(passive);
            }
            for (var index = 0; index < EvolutionIds.Length; index++)
            {
                Assert.That(registry.TryGet(Id(EvolutionIds[index]), out RuntimeEvolutionDefinition evolution), Is.True);
                Assert.That(evolution.RequiredSkillId, Is.EqualTo(Id(SourceSkillIds[index])));
                Assert.That(evolution.RequiredSkillLevel, Is.EqualTo(8));
                Assert.That(evolution.RequiredPassiveIds, Is.EqualTo(new[] { Id(PassiveIds[index]) }));
                Assert.That(evolution.ResultSkillId, Is.EqualTo(Id(ResultSkillIds[index])));
                Assert.That(evolution.ConsumePolicy, Is.EqualTo(EvolutionConsumePolicy.RetainRequiredPassives));
                Assert.That(registry.TryGet(evolution.ResultSkillId, out RuntimeSkillDefinition result), Is.True);
                Assert.That(result.IsExecutable, Is.True, ResultSkillIds[index]);
            }
            for (var index = 0; index < SynergyIds.Length; index++)
                Assert.That(registry.TryGet(Id(SynergyIds[index]), out RuntimeSynergyDefinition _), Is.True);
        }

        [Test]
        public void PassiveLevelsUseExplicitOperationsPrioritiesAndUniqueStackingGroups()
        {
            var registry = LoadRegistry(out _);
            for (var passiveIndex = 0; passiveIndex < PassiveIds.Length; passiveIndex++)
            {
                Assert.That(registry.TryGet(Id(PassiveIds[passiveIndex]), out RuntimePassiveDefinition passive), Is.True);
                for (var index = 0; index < passive.LevelModifiers.Count; index++)
                {
                    var item = passive.LevelModifiers[index];
                    Assert.That(item.Level, Is.InRange(1, 5), PassiveIds[passiveIndex]);
                    Assert.That(item.Modifier.StatId.IsValid, Is.True);
                    Assert.That(item.Modifier.Operation, Is.InRange(ModifierOperation.AddFlat, ModifierOperation.Override));
                    Assert.That(item.Modifier.Priority, Is.GreaterThanOrEqualTo(100));
                    Assert.That(item.Modifier.StackingGroup.IsValid, Is.True);
                    for (var previous = 0; previous < index; previous++)
                    {
                        Assert.That(
                            item.Modifier.StackingGroup,
                            Is.Not.EqualTo(passive.LevelModifiers[previous].Modifier.StackingGroup),
                            PassiveIds[passiveIndex] + " repeats a stacking group.");
                    }
                }
            }

            var artifactControl = Passive(registry, PassiveIds[2]);
            AssertModifier(artifactControl, 2, "base.stat.pierce", ModifierOperation.AddFlat, 1f);
            AssertModifier(artifactControl, 4, "base.stat.projectile_count", ModifierOperation.AddFlat, 1f);
            var longBreath = Passive(registry, PassiveIds[4]);
            AssertModifier(longBreath, 3, "base.stat.regeneration", ModifierOperation.AddFlat, 0.25f);
        }

        [Test]
        public void NormalOfferStreamIsDeterministicAndExcludesManifestations()
        {
            var registry = LoadRegistry(out _);
            var first = CreateWorld(registry, Seed, out _);
            var second = CreateWorld(registry, Seed, out _);
            for (var index = 0; index < 50; index++) first.World.Random.NextUInt();

            for (var draw = 0; draw < 40; draw++)
            {
                var firstSet = first.World.Progression.Offers.Generate(first.World.Progression.Build, 3);
                var secondSet = second.World.Progression.Offers.Generate(second.World.Progression.Build, 3);
                Assert.That(Sequence(secondSet), Is.EqualTo(Sequence(firstSet)));
                for (var offerIndex = 0; offerIndex < firstSet.Count; offerIndex++)
                {
                    Assert.That(
                        firstSet.GetAt(offerIndex).TargetKind,
                        Is.Not.EqualTo(UpgradeTargetKind.Evolution),
                        "Manifestations belong to the G1.7 controlled reward context, not level-up offers.");
                }
            }
            Assert.That(first.World.Progression.Offers.RandomCalls, Is.EqualTo(second.World.Progression.Offers.RandomCalls));
            Assert.That(first.World.Progression.Offers.StreamSeed, Is.EqualTo(second.World.Progression.Offers.StreamSeed));
        }

        [Test]
        public void ThreeTargetSynergiesActivateThroughGenericBuildConditions()
        {
            var registry = LoadRegistry(out _);
            var pairs = new[] { 0, 1, 5 };
            for (var index = 0; index < pairs.Length; index++)
            {
                var contentIndex = pairs[index];
                var fixture = CreateWorld(registry, Seed + (ulong)index, out var player);
                var build = fixture.World.Progression.Build;
                Assert.That(build.TryAcquireSkill(Id(SourceSkillIds[contentIndex])), Is.True);
                Assert.That(build.TryAcquirePassive(Id(PassiveIds[contentIndex])), Is.True);
                Assert.That(ContainsSynergy(build, Id(SynergyIds[index])), Is.True, SynergyIds[index]);

                if (index == 0)
                {
                    Assert.That(
                        fixture.World.Actors.TryReadStat(player, BuiltInStatIndices.ProjectileSpeed, out var speed),
                        Is.True);
                    Assert.That(speed, Is.EqualTo(1.12f).Within(0.0001f));
                }
                else if (index == 2)
                {
                    Assert.That(
                        fixture.World.Actors.TryReadStat(player, BuiltInStatIndices.Duration, out var duration),
                        Is.True);
                    Assert.That(duration, Is.EqualTo(1.2f).Within(0.0001f));
                }
            }
        }

        [Test]
        public void SixManifestationsReachEligibilityAndTransformAtomically()
        {
            var registry = LoadRegistry(out _);
            for (var index = 0; index < EvolutionIds.Length; index++)
            {
                var fixture = CreateWorld(registry, Seed + (ulong)index, out _);
                var build = fixture.World.Progression.Build;
                for (var level = 1; level <= 8; level++)
                    Assert.That(build.TryAcquireSkill(Id(SourceSkillIds[index])), Is.True, SourceSkillIds[index] + " L" + level);
                Assert.That(build.IsEvolutionEligible(Id(EvolutionIds[index])), Is.False, "Passive is still missing.");
                Assert.That(build.TryAcquirePassive(Id(PassiveIds[index])), Is.True);
                Assert.That(build.IsEvolutionEligible(Id(EvolutionIds[index])), Is.True, EvolutionIds[index]);

                var offerId = Id("qinglan.offer.evolution." + EvolutionIds[index].Substring("qinglan.evolution.".Length));
                Assert.That(fixture.Builds.TryGetOffer(offerId, out var offer), Is.True);
                Assert.That(offer.Source.InitiallyUnlocked, Is.False);
                Assert.That(build.CanAcceptOffer(offer), Is.False, "The normal offer stream must not accept the locked manifestation offer.");
                Assert.That(build.ApplyOffer(offer), Is.True, "The future controlled reward adapter commits an already eligible choice.");
                Assert.That(build.Skills.TryGet(Id(SourceSkillIds[index]), out _, out _), Is.False);
                Assert.That(build.Skills.TryGet(Id(ResultSkillIds[index]), out var result, out _), Is.True);
                Assert.That(result.Level, Is.EqualTo(1));
                Assert.That(build.Passives.TryGet(Id(PassiveIds[index]), out var retained, out _), Is.True);
                Assert.That(retained.Level, Is.EqualTo(1));
                Assert.That(build.IsEvolutionEligible(Id(EvolutionIds[index])), Is.False);
            }
        }

        [Test]
        public void EvolvedSkillPreviewsAreDeterministicBoundedAndAllocationFree()
        {
            var registry = LoadRegistry(out _);
            for (var index = 0; index < ResultSkillIds.Length; index++)
            {
                var request = new SkillPreviewRequest(Seed, 6f, 20, 1);
                var first = SkillPreviewHarness.RunDetailed(registry, IndexOf(registry, ResultSkillIds[index]), request);
                var second = SkillPreviewHarness.RunDetailed(registry, IndexOf(registry, ResultSkillIds[index]), request);
                Assert.That(first.IsSuccess, Is.True, first.IsSuccess ? string.Empty : first.Error.ToString());
                Assert.That(second.IsSuccess, Is.True, second.IsSuccess ? string.Empty : second.Error.ToString());
                Assert.That(second.Value.Summary, Is.EqualTo(first.Value.Summary));
                Assert.That(first.Value.ManagedAllocationBytes, Is.Zero, ResultSkillIds[index]);
                Assert.That(first.Value.Summary.TriggerCount, Is.GreaterThan(0), ResultSkillIds[index]);
                Assert.That(first.Value.Summary.HitCount, Is.GreaterThan(0), ResultSkillIds[index]);
                Assert.That(first.Value.Summary.DamagePerSecond, Is.GreaterThan(0f), ResultSkillIds[index]);
                Assert.That(first.Value.Summary.HitCount, Is.LessThanOrEqualTo(2_000), ResultSkillIds[index]);
                Assert.That(
                    first.Value.Summary.DamagePerSecond,
                    Is.EqualTo(PreviewDamagePerSecond[index]).Within(0.0001f),
                    ResultSkillIds[index]);
                Assert.That(first.Value.Summary.HitCount, Is.EqualTo(PreviewHits[index]), ResultSkillIds[index]);
                Assert.That(first.Value.Summary.TriggerCount, Is.EqualTo(PreviewTriggers[index]), ResultSkillIds[index]);
            }
        }

        private static RuntimePassiveDefinition Passive(ContentRegistry registry, string id)
        {
            Assert.That(registry.TryGet(Id(id), out RuntimePassiveDefinition passive), Is.True, id);
            return passive;
        }

        private static void AssertEveryLevelHasModifier(RuntimePassiveDefinition passive)
        {
            for (var level = 1; level <= passive.MaximumLevel; level++)
            {
                var found = false;
                for (var index = 0; index < passive.LevelModifiers.Count; index++)
                    found |= passive.LevelModifiers[index].Level == level;
                Assert.That(found, Is.True, passive.Id + " missing level " + level + ".");
            }
        }

        private static void AssertModifier(
            RuntimePassiveDefinition passive,
            int level,
            string stat,
            ModifierOperation operation,
            float value)
        {
            for (var index = 0; index < passive.LevelModifiers.Count; index++)
            {
                var item = passive.LevelModifiers[index];
                if (item.Level != level || item.Modifier.StatId != Stat(stat)) continue;
                Assert.That(item.Modifier.Operation, Is.EqualTo(operation));
                Assert.That(item.Modifier.Value, Is.EqualTo(value));
                return;
            }
            Assert.Fail(passive.Id + " is missing expected modifier at level " + level + ".");
        }

        private static bool ContainsSynergy(BuildState build, ContentId id)
        {
            for (var index = 0; index < build.ActiveSynergyCount; index++)
                if (build.GetActiveSynergyAt(index) == id) return true;
            return false;
        }

        private static string Sequence(UpgradeOfferSet offers)
        {
            var value = string.Empty;
            for (var index = 0; index < offers.Count; index++) value += offers.GetAt(index).Source.Id.Value + "|";
            return value;
        }

        private static WorldFixture CreateWorld(ContentRegistry registry, ulong seed, out EntityHandle player)
        {
            var modules = SkillModuleRegistry.CreateDefault();
            var skills = SkillRuntimeCatalog.Build(registry, modules);
            var builds = BuildRuntimeCatalog.Build(registry, modules);
            Assert.That(skills.IsSuccess, Is.True, skills.IsSuccess ? string.Empty : skills.Error.ToString());
            Assert.That(builds.IsSuccess, Is.True, builds.IsSuccess ? string.Empty : builds.Error.ToString());
            var runtime = new SkillRuntime(skills.Value, seed, 128);
            var world = new SimulationWorld(
                new QinglanRuntimeHub(new CharacterMechanicRuntime(4)),
                seed: seed,
                initialEntityCapacity: 128,
                pipeline: SimulationPipeline.CreateQinglanDemo(),
                statusCatalog: new RuntimeStatusCatalog(registry),
                skillRuntime: runtime);
            var stats = StatBaseValues.CreateDefault(1_000_000f, 6f);
            stats.ProjectileSpeed = 1f;
            stats.Duration = 1f;
            player = world.CreateActor(
                SimulationEntityState.Create(Vector2.Zero, Vector2.Zero),
                new ActorCombatInitialization(stats, stats.Health, 0f, 0f, default));
            world.SetPlayer(player);
            world.InitializeProgression(builds.Value, player, seed, null, 6, 6, Array.Empty<ContentTag>());
            return new WorldFixture(world, builds.Value);
        }

        private static ContentRegistry LoadRegistry(out BakedContentCatalog baked)
        {
            var pack = AssetDatabase.LoadAssetAtPath<ContentPackAuthoring>(QinglanG12ContentSetup.PackPath);
            Assert.That(pack, Is.Not.Null, QinglanG12ContentSetup.PackPath);
            var bake = ContentBakeUtility.Bake(pack);
            Assert.That(bake.IsSuccess, Is.True, bake.IsSuccess ? string.Empty : bake.Error.ToString());
            baked = bake.Value;
            var registry = new ContentRegistry();
            var load = registry.Load(new[] { baked }, GameVersion);
            Assert.That(load.IsSuccess, Is.True, load.IsSuccess ? string.Empty : load.Error.ToString());
            return registry;
        }

        private static RuntimeContentIndex IndexOf(ContentRegistry registry, string id)
        {
            Assert.That(registry.TryGet(Id(id), out ContentRegistryEntry entry), Is.True, id);
            return entry.Index;
        }

        private static ContentId Id(string value) => ContentId.Create(value).Value;
        private static StatId Stat(string value) => StatId.Create(value).Value;

        private readonly struct WorldFixture
        {
            public WorldFixture(SimulationWorld world, BuildRuntimeCatalog builds)
            {
                World = world;
                Builds = builds;
            }

            public SimulationWorld World { get; }
            public BuildRuntimeCatalog Builds { get; }
        }
    }
}
