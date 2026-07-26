using System;
using Game.Content.Authoring;
using Game.Content.Runtime;
using Game.Core;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    public sealed class SkillContentTests
    {
        [Test]
        public void SchemaThreeSkillRoundTripPreservesModulesEffectsPatchesAndHash()
        {
            var patch = new SkillLevelPatch(
                2,
                SkillPatchTarget.EffectValue0,
                0,
                SkillPatchValueType.Float,
                SkillPatchOperation.Add,
                5f,
                0);
            var skill = SkillTestFactory.Skill(
                "test.skill.schema_three",
                SkillModuleIds.TriggerTimer,
                SkillTestFactory.Module(SkillModuleIds.TargetingNearest, 8f, int0: 1),
                SkillTestFactory.Module(
                    SkillModuleIds.DeliveryProjectile,
                    12f,
                    0.25f,
                    1f,
                    int0: 1,
                    presentation: SkillTestFactory.Placeholder("schema_three_projectile")),
                new[] { SkillTestFactory.Damage() },
                patches: new[] { patch });
            var catalog = Catalog(skill);

            var restored = catalog.ToDto().ToCatalog();

            Assert.That(restored.IsSuccess, Is.True, restored.Error.ToString());
            Assert.That(restored.Value.ContentHash, Is.EqualTo(catalog.ContentHash));
            var restoredSkill = (RuntimeSkillDefinition)restored.Value.Definitions[0];
            Assert.That(restoredSkill.IsExecutable, Is.True);
            Assert.That(restoredSkill.Trigger.ModuleId, Is.EqualTo(SkillModuleIds.TriggerTimer));
            Assert.That(restoredSkill.Targeting.ModuleId, Is.EqualTo(SkillModuleIds.TargetingNearest));
            Assert.That(restoredSkill.Delivery.PresentationId.Value,
                Is.EqualTo("placeholder.presentation.schema_three_projectile"));
            Assert.That(restoredSkill.Effects.Count, Is.EqualTo(1));
            Assert.That(restoredSkill.Effects[0].Code, Is.EqualTo(EffectOpCode.Damage));
            Assert.That(restoredSkill.LevelPatches[0].Target,
                Is.EqualTo(SkillPatchTarget.EffectValue0));
        }

        [Test]
        public void MissingModuleIdFailsCatalogValidation()
        {
            var missing = SkillTestFactory.Id("test.trigger.not_registered");
            var skill = SkillTestFactory.Skill(
                "test.skill.missing_module",
                missing,
                SkillTestFactory.Module(SkillModuleIds.TargetingSelf),
                SkillTestFactory.Module(SkillModuleIds.DeliveryInstant),
                new[] { SkillTestFactory.GainResource() });

            var report = ContentValidator.ValidateCatalogs(
                new[] { Catalog(skill) },
                SkillTestFactory.GameVersion);

            Assert.That(report.IsValid, Is.False);
            Assert.That(report.Errors[0].Message, Does.Contain("trigger module ID"));
        }

        [Test]
        public void LevelPatchWhoseCumulativeFloatResultIsNonFiniteFailsCatalogValidation()
        {
            var skill = SkillTestFactory.Skill(
                "test.skill.patch_float_overflow",
                SkillModuleIds.TriggerTimer,
                SkillTestFactory.Module(SkillModuleIds.TargetingSelf),
                SkillTestFactory.Module(SkillModuleIds.DeliveryInstant),
                new[] { SkillTestFactory.Damage(float.MaxValue) },
                patches: new[]
                {
                    new SkillLevelPatch(
                        2,
                        SkillPatchTarget.EffectValue0,
                        0,
                        SkillPatchValueType.Float,
                        SkillPatchOperation.Multiply,
                        float.MaxValue,
                        0)
                });

            var report = ContentValidator.ValidateCatalogs(
                new[] { Catalog(skill) },
                SkillTestFactory.GameVersion);

            Assert.That(report.IsValid, Is.False);
            Assert.That(report.Errors[0].Message, Does.Contain("LevelPatch"));
        }

        [Test]
        public void LevelPatchWhoseCumulativeIntegerResultOverflowsFailsCatalogValidation()
        {
            var skill = SkillTestFactory.Skill(
                "test.skill.patch_integer_overflow",
                SkillModuleIds.TriggerTimer,
                SkillTestFactory.Module(
                    SkillModuleIds.TargetingNearest,
                    10f,
                    int0: int.MaxValue),
                SkillTestFactory.Module(SkillModuleIds.DeliveryInstant),
                new[] { SkillTestFactory.GainResource() },
                patches: new[]
                {
                    new SkillLevelPatch(
                        2,
                        SkillPatchTarget.TargetingInt0,
                        0,
                        SkillPatchValueType.Integer,
                        SkillPatchOperation.Add,
                        0f,
                        1)
                });

            var report = ContentValidator.ValidateCatalogs(
                new[] { Catalog(skill) },
                SkillTestFactory.GameVersion);

            Assert.That(report.IsValid, Is.False);
            Assert.That(report.Errors[0].Message, Does.Contain("LevelPatch"));
        }

        [Test]
        public void SpawnSecondarySkillReferenceMustPointToExecutableSkill()
        {
            var legacy = new RuntimeSkillDefinition(
                SkillTestFactory.Id("test.skill.legacy_secondary"),
                "content.test.skill.legacy_secondary.name",
                "content.test.skill.legacy_secondary.description",
                "Assets/Test/LegacySecondary.asset",
                Array.Empty<ContentTag>(),
                1f);
            var primary = SkillTestFactory.Skill(
                "test.skill.references_legacy_secondary",
                SkillModuleIds.TriggerTimer,
                SkillTestFactory.Module(SkillModuleIds.TargetingSelf),
                SkillTestFactory.Module(SkillModuleIds.DeliveryInstant),
                new[]
                {
                    new EffectOp(
                        EffectOpCode.SpawnSecondarySkill,
                        referenceId0: legacy.Id)
                });

            var report = ContentValidator.ValidateCatalogs(
                new[]
                {
                    Catalog(primary, "test.pack.skill_content_primary", 3),
                    Catalog(legacy, "test.pack.skill_content_legacy", 2)
                },
                SkillTestFactory.GameVersion);

            Assert.That(report.IsValid, Is.False);
            Assert.That(report.Errors[0].Message, Does.Contain("executable Skill"));
        }

        [TestCase("effects[1].value0", SkillPatchValueType.Float)]
        [TestCase("effects[0].int0", SkillPatchValueType.Float)]
        public void BakerRejectsInvalidLevelPatchPathOrType(
            string path,
            SkillPatchValueType valueType)
        {
            var skill = ScriptableObject.CreateInstance<SkillAuthoring>();
            var pack = ScriptableObject.CreateInstance<ContentPackAuthoring>();
            try
            {
                skill.ConfigureIdentity(
                    "test.skill.invalid_patch",
                    "content.test.skill.invalid_patch.name",
                    "content.test.skill.invalid_patch.description",
                    Array.Empty<string>());
                skill.ConfigureRuntime(
                    1f,
                    0f,
                    Module("base.trigger.timer"),
                    Module("base.condition.always"),
                    Module("base.targeting.self"),
                    Module("base.delivery.instant"),
                    new[]
                    {
                        new SkillEffectAuthoringData
                        {
                            moduleId = "base.effect.damage",
                            value0 = 10f,
                            value1 = 1f,
                            int0 = (int)DamageType.Physical
                        }
                    },
                    new[]
                    {
                        new SkillLevelPatchAuthoringData
                        {
                            level = 2,
                            path = path,
                            valueType = valueType,
                            operation = SkillPatchOperation.Add,
                            floatValue = 1f
                        }
                    });
                pack.Configure(
                    "test.pack.invalid_patch",
                    "0.1.0",
                    3,
                    "0.1.0",
                    string.Empty,
                    Array.Empty<ContentPackDependencyAuthoring>(),
                    "packs/test/invalid_patch",
                    "pack.test.invalid_patch",
                    false,
                    new ContentAuthoringBase[] { skill });

                var result = ContentBaker.Bake(pack, new TestPathResolver(pack, skill));

                Assert.That(result.IsSuccess, Is.False);
                Assert.That(result.Error.Message, Does.Contain("LevelPatch"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(skill);
                UnityEngine.Object.DestroyImmediate(pack);
            }
        }

        [Test]
        public void RuntimeSkillDefinitionContainsNoUnityObjectReferences()
        {
            var fields = typeof(RuntimeSkillDefinition).GetFields(
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic);

            for (var index = 0; index < fields.Length; index++)
            {
                Assert.That(
                    typeof(UnityEngine.Object).IsAssignableFrom(fields[index].FieldType),
                    Is.False,
                    fields[index].Name);
            }
        }

        private static SkillModuleAuthoringData Module(string id)
        {
            return new SkillModuleAuthoringData { moduleId = id };
        }

        private static BakedContentCatalog Catalog(RuntimeContentDefinition definition)
        {
            return Catalog(definition, "test.pack.skill_content", 3);
        }

        private static BakedContentCatalog Catalog(
            RuntimeContentDefinition definition,
            string packIdValue,
            int schemaVersion)
        {
            var packId = SkillTestFactory.Id(packIdValue);
            var manifest = new ContentPackManifest(
                packId,
                SkillTestFactory.GameVersion,
                schemaVersion,
                SkillTestFactory.GameVersion,
                null,
                Array.Empty<ContentPackDependency>(),
                "packs/test/skill_content",
                "pack.test.skill_content",
                false,
                "Assets/Test/SkillContentPack.asset");
            return BakedContentCatalog.Create(manifest, new[] { definition });
        }

        private sealed class TestPathResolver : IAuthoringPathResolver
        {
            private readonly UnityEngine.Object pack;
            private readonly UnityEngine.Object skill;

            public TestPathResolver(UnityEngine.Object pack, UnityEngine.Object skill)
            {
                this.pack = pack;
                this.skill = skill;
            }

            public string GetPath(UnityEngine.Object authoringAsset)
            {
                if (ReferenceEquals(authoringAsset, pack)) return "Assets/Test/InvalidPatchPack.asset";
                if (ReferenceEquals(authoringAsset, skill)) return "Assets/Test/InvalidPatchSkill.asset";
                return string.Empty;
            }
        }
    }
}
