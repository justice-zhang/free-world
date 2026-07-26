using System;
using System.Numerics;
using Game.Content.Runtime;
using Game.Core;
using Game.Simulation;

namespace Game.Tests.EditMode
{
    internal sealed class M6Fixture
    {
        public M5Fixture M5;
        public ContentRegistry Registry;
        public BakedContentCatalog Catalog;
        public RuntimeSkillDefinition SourceSkill;
        public RuntimeSkillDefinition EvolvedSkill;
        public RuntimeSkillDefinition AuxiliarySkill;
        public RuntimeSkillDefinition AuxiliaryResultSkill;
        public RuntimePassiveDefinition ForcePassive;
        public RuntimePassiveDefinition ReachPassive;
        public RuntimeTraitDefinition Trait;
        public RuntimeEvolutionDefinition Evolution;
        public RuntimeUpgradeOfferDefinition SourceOffer;
        public RuntimeUpgradeOfferDefinition ForceOffer;
        public RuntimeUpgradeOfferDefinition ReachOffer;
        public RuntimeUpgradeOfferDefinition AuxiliaryOffer;
        public RuntimeUpgradeOfferDefinition EvolutionOffer;
        public RuntimeSynergyDefinition FirstSynergy;
        public RuntimeSynergyDefinition SecondSynergy;
    }

    internal static class M6TestFactory
    {
        public static M6Fixture Create(float duration = 600f)
        {
            var m5 = M5TestFactory.Create(duration, duration * 0.5f, 16);
            var source = Skill(
                "test.skill.m6_source",
                40f,
                new[]
                {
                    new SkillLevelPatch(2, SkillPatchTarget.EffectValue0, 0, SkillPatchValueType.Float, SkillPatchOperation.Add, 20f, 0),
                    new SkillLevelPatch(3, SkillPatchTarget.EffectValue0, 0, SkillPatchValueType.Float, SkillPatchOperation.Add, 20f, 0)
                });
            var evolved = Skill("test.skill.m6_evolved", 100f, null);
            var auxiliary = Skill("test.skill.m6_auxiliary", 20f, null);
            var auxiliaryResult = Skill("test.skill.m6_auxiliary_result", 30f, null);
            var force = new RuntimePassiveDefinition(
                Id("test.passive.m6_force"), Name("force"), Description("force"), Path("Force"),
                new[] { Tag("build.force") }, 3,
                new[]
                {
                    new RuntimePassiveLevelModifier(1, Modifier(BuiltInStatIds.Damage, ModifierOperation.AddPercent, 0.1f)),
                    new RuntimePassiveLevelModifier(2, Modifier(BuiltInStatIds.Damage, ModifierOperation.AddPercent, 0.1f)),
                    new RuntimePassiveLevelModifier(3, Modifier(BuiltInStatIds.Damage, ModifierOperation.AddPercent, 0.1f))
                });
            var reach = new RuntimePassiveDefinition(
                Id("test.passive.m6_reach"), Name("reach"), Description("reach"), Path("Reach"),
                new[] { Tag("build.reach") }, 2,
                new[]
                {
                    new RuntimePassiveLevelModifier(1, Modifier(BuiltInStatIds.PickupRange, ModifierOperation.AddFlat, 1f)),
                    new RuntimePassiveLevelModifier(2, Modifier(BuiltInStatIds.PickupRange, ModifierOperation.AddFlat, 1f))
                });
            var trait = new RuntimeTraitDefinition(
                Id("test.trait.m6_prepared"), Name("trait"), Description("trait"), Path("Trait"),
                new[] { Tag("build.prepared") },
                new[] { Modifier(BuiltInStatIds.Luck, ModifierOperation.AddFlat, 1f) });
            var evolution = new RuntimeEvolutionDefinition(
                Id("test.evolution.m6_source"), Name("evolution"), Description("evolution"), Path("Evolution"),
                new[] { Tag("build.evolution") },
                source.Id, 2, new[] { force.Id }, Array.Empty<BuildCondition>(), evolved.Id,
                EvolutionConsumePolicy.ConsumeRequiredPassives);

            var sourceOffer = Offer("source", source.Id, 1f, true);
            var forceOffer = Offer("force", force.Id, 20f, true);
            var reachOffer = new RuntimeUpgradeOfferDefinition(
                Id("test.offer.m6_reach"), Name("reach_offer"), Description("reach_offer"), Path("ReachOffer"),
                new[] { Tag("build.offer") }, reach.Id, 2f, true,
                new[] { Condition(BuildConditionType.OwnsContent, content: source.Id) },
                Array.Empty<ContentId>());
            var auxiliaryOffer = new RuntimeUpgradeOfferDefinition(
                Id("test.offer.m6_auxiliary"), Name("auxiliary_offer"), Description("auxiliary_offer"), Path("AuxiliaryOffer"),
                new[] { Tag("build.offer") }, auxiliary.Id, 1f, true,
                Array.Empty<BuildCondition>(), new[] { force.Id });
            var evolutionOffer = Offer("evolution", evolution.Id, 5f, false);

            var firstSynergy = new RuntimeSynergyDefinition(
                Id("test.synergy.m6_first"), Name("first_synergy"), Description("first_synergy"), Path("FirstSynergy"),
                new[] { Tag("build.synergy.first") },
                new[]
                {
                    Condition(BuildConditionType.HasTagCount, tag: Tag("build.force"), integer: 1),
                    Condition(BuildConditionType.SkillLevelAtLeast, content: source.Id, integer: 2),
                    Condition(BuildConditionType.StatAtLeast, stat: BuiltInStatIds.Damage, value: 1f),
                    Condition(BuildConditionType.MapHasTag, tag: Tag("map.finite"))
                },
                new[]
                {
                    new RuntimeSynergyOutput(
                        SynergyOutputType.AddModifier,
                        Modifier(BuiltInStatIds.Range, ModifierOperation.AddPercent, 0.25f),
                        default,
                        default,
                        default),
                    new RuntimeSynergyOutput(SynergyOutputType.UnlockOffer, default, default, evolutionOffer.Id, default),
                    new RuntimeSynergyOutput(SynergyOutputType.AddEffectOp, default, source.Id, default, SkillTestFactory.Damage(2f))
                });
            var secondSynergy = new RuntimeSynergyDefinition(
                Id("test.synergy.m6_second"), Name("second_synergy"), Description("second_synergy"), Path("SecondSynergy"),
                new[] { Tag("build.synergy.second") },
                new[]
                {
                    Condition(BuildConditionType.OwnsContent, content: reach.Id),
                    Condition(BuildConditionType.OwnsContent, content: auxiliary.Id),
                    Condition(BuildConditionType.MapHasTag, tag: Tag("map.finite"))
                },
                new[]
                {
                    new RuntimeSynergyOutput(SynergyOutputType.TransformSkill, default, auxiliary.Id, auxiliaryResult.Id, default),
                    new RuntimeSynergyOutput(SynergyOutputType.GrantTrait, default, default, trait.Id, default)
                });

            var definitions = new RuntimeContentDefinition[]
            {
                source, evolved, auxiliary, auxiliaryResult,
                force, reach, trait, evolution,
                sourceOffer, forceOffer, reachOffer, auxiliaryOffer, evolutionOffer,
                firstSynergy, secondSynergy
            };
            var manifest = new ContentPackManifest(
                Id("test.pack.m6_runtime"),
                SkillTestFactory.GameVersion,
                ContentPackTopology.BuildProgressionSchemaVersion,
                SkillTestFactory.GameVersion,
                null,
                new[]
                {
                    new ContentPackDependency(Id("test.pack.m5_runtime"), SkillTestFactory.GameVersion, SkillTestFactory.GameVersion)
                },
                "packs/test/m6_runtime",
                "pack.test.m6_runtime",
                false,
                "Assets/Test/M6RuntimePack.asset");
            var m6Catalog = BakedContentCatalog.Create(manifest, definitions);
            var registry = new ContentRegistry();
            var load = registry.Load(new[] { m5.Catalog, m6Catalog }, SkillTestFactory.GameVersion);
            if (!load.IsSuccess) throw new InvalidOperationException(load.Error.ToString());
            return new M6Fixture
            {
                M5 = m5,
                Registry = registry,
                Catalog = m6Catalog,
                SourceSkill = source,
                EvolvedSkill = evolved,
                AuxiliarySkill = auxiliary,
                AuxiliaryResultSkill = auxiliaryResult,
                ForcePassive = force,
                ReachPassive = reach,
                Trait = trait,
                Evolution = evolution,
                SourceOffer = sourceOffer,
                ForceOffer = forceOffer,
                ReachOffer = reachOffer,
                AuxiliaryOffer = auxiliaryOffer,
                EvolutionOffer = evolutionOffer,
                FirstSynergy = firstSynergy,
                SecondSynergy = secondSynergy
            };
        }

        public static SimulationWorld World(
            M6Fixture fixture,
            ulong seed,
            out EntityHandle player,
            bool schedule = false,
            int skillSlots = 6,
            int passiveSlots = 6)
        {
            var modules = SkillModuleRegistry.CreateDefault();
            var skills = SkillRuntimeCatalog.Build(fixture.Registry, modules);
            var enemies = EnemyRuntimeCatalog.Build(fixture.Registry);
            var builds = BuildRuntimeCatalog.Build(fixture.Registry, modules);
            if (!skills.IsSuccess || !enemies.IsSuccess || !builds.IsSuccess) throw new InvalidOperationException("M6 fixture compilation failed.");
            var map = MapRuntimeFactory.Create(fixture.M5.FiniteMap, seed);
            var enemyRuntime = new EnemyRuntime(enemies.Value, DifficultySnapshot.Default, 64);
            var skillRuntime = new SkillRuntime(skills.Value, seed, 64);
            var encounter = schedule
                ? new EncounterScheduler(fixture.M5.Encounter, map, DifficultySnapshot.Default, seed)
                : null;
            var world = new SimulationWorld(
                seed, 64, 2f, SimulationPipeline.CreateM6Default(), null, null,
                skillRuntime, enemyRuntime, map, encounter);
            var stats = StatBaseValues.CreateDefault(1_000_000_000f, 6f);
            stats.PickupRange = 8f;
            player = world.CreateActor(
                SimulationEntityState.Create(Vector2.Zero, Vector2.Zero),
                new ActorCombatInitialization(stats, stats.Health, 0f, 0f, default));
            world.SetPlayer(player);
            world.InitializeProgression(
                builds.Value,
                player,
                seed,
                null,
                skillSlots,
                passiveSlots,
                new[] { Tag("map.finite") });
            return world;
        }

        private static RuntimeSkillDefinition Skill(string id, float damage, SkillLevelPatch[] patches) =>
            SkillTestFactory.Skill(
                id,
                SkillModuleIds.TriggerTimer,
                SkillTestFactory.Module(SkillModuleIds.TargetingNearest, 16f, int0: 1),
                SkillTestFactory.Module(SkillModuleIds.DeliveryInstant),
                new[] { SkillTestFactory.Damage(damage) },
                0.2f,
                0f,
                patches);

        private static RuntimeUpgradeOfferDefinition Offer(string suffix, ContentId target, float weight, bool unlocked) =>
            new RuntimeUpgradeOfferDefinition(
                Id("test.offer.m6_" + suffix), Name(suffix + "_offer"), Description(suffix + "_offer"), Path(suffix + "Offer"),
                new[] { Tag("build.offer") }, target, weight, unlocked,
                Array.Empty<BuildCondition>(), Array.Empty<ContentId>());

        private static BuildCondition Condition(
            BuildConditionType type,
            ContentId content = default,
            ContentTag tag = default,
            int integer = 0,
            StatId stat = default,
            float value = 0f) => new BuildCondition(type, content, tag, integer, stat, value);

        private static RuntimeBuildModifier Modifier(StatId stat, ModifierOperation operation, float value) =>
            new RuntimeBuildModifier(stat, operation, value, 0, default);

        private static ContentId Id(string value) => SkillTestFactory.Id(value);
        private static ContentTag Tag(string value)
        {
            var result = ContentTag.Create(value);
            if (!result.IsSuccess) throw new InvalidOperationException(result.Error.ToString());
            return result.Value;
        }
        private static string Name(string value) => "content.test.m6." + value + ".name";
        private static string Description(string value) => "content.test.m6." + value + ".description";
        private static string Path(string value) => "Assets/Test/M6" + value + ".asset";
    }
}
