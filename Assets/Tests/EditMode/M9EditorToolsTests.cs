using System;
using System.Collections.Generic;
using System.IO;
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
    public sealed class M9EditorToolsTests
    {
        [Test]
        public void WizardCoveragePackContainsEveryRequiredTypeAndBakesAsOneRegistry()
        {
            var pack = RequirePack();
            Assert.That(pack.SchemaVersion, Is.EqualTo(ContentPackTopology.BuildProgressionSchemaVersion));
            Assert.That(pack.Definitions.Count, Is.EqualTo(10));
            AssertType<CharacterAuthoring>(pack);
            AssertType<SkillAuthoring>(pack);
            AssertType<PassiveAuthoring>(pack);
            AssertType<TraitAuthoring>(pack);
            AssertType<EnemyAuthoring>(pack);
            AssertType<StatusEffectAuthoring>(pack);
            AssertType<EvolutionAuthoring>(pack);
            AssertType<SynergyAuthoring>(pack);
            AssertType<MapAuthoring>(pack);
            AssertType<EncounterScheduleAuthoring>(pack);

            var baked = ContentBakeUtility.Bake(pack);
            Assert.That(baked.IsSuccess, Is.True, baked.Error.ToString());
            Assert.That(baked.Value.Definitions.Count, Is.EqualTo(10));
            var registry = ContentEditorCatalog.BuildRegistry();
            Assert.That(registry.IsSuccess, Is.True, registry.Error.ToString());
            AssertRegistryId(registry.Value, "test.character.second");
            AssertRegistryId(registry.Value, "test.skill.second");
            AssertRegistryId(registry.Value, "test.map.second");
            Assert.That(
                Directory.GetFiles(
                    Path.GetFullPath(M9ProjectSetup.PackFolder),
                    "*.cs",
                    SearchOption.AllDirectories),
                Is.Empty,
                "Extensibility fixtures must remain data-only.");
        }

        [Test]
        public void WizardAutomationCreatesLocalizationLabelsTestsAndSourceRecord()
        {
            var pack = RequirePack();
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(false);
            Assert.That(settings, Is.Not.Null);
            var english = LocalizationEditorSettings.GetStringTableCollection("UI")
                ?.GetTable("en") as StringTable;
            var chinese = LocalizationEditorSettings.GetStringTableCollection("UI")
                ?.GetTable("zh-Hans") as StringTable;
            Assert.That(english, Is.Not.Null);
            Assert.That(chinese, Is.Not.Null);
            var sourcePath = M9ProjectSetup.PackFolder + "/provenance.placeholder.json";
            var source = File.ReadAllText(Path.GetFullPath(sourcePath));

            for (var index = 0; index < pack.Definitions.Count; index++)
            {
                var definition = pack.Definitions[index];
                Assert.That(english.GetEntry(definition.LocalizedNameKey)?.Value, Is.Not.Empty);
                Assert.That(english.GetEntry(definition.LocalizedDescriptionKey)?.Value, Is.Not.Empty);
                Assert.That(chinese.GetEntry(definition.LocalizedNameKey)?.Value, Is.Not.Empty);
                Assert.That(chinese.GetEntry(definition.LocalizedDescriptionKey)?.Value, Is.Not.Empty);
                var path = AssetDatabase.GetAssetPath(definition);
                var entry = settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(path));
                Assert.That(entry, Is.Not.Null, path);
                Assert.That(entry.labels, Does.Contain(pack.AssetLabel), path);
                Assert.That(entry.labels, Does.Contain(PlaceholderAssetGenerator.PlaceholderLabel), path);
                Assert.That(entry.labels, Does.Contain(PlaceholderAssetGenerator.DevelopmentOnlyLabel), path);
                var testPath = M9ProjectSetup.PackFolder + "/Tests/" +
                               definition.ContentIdText.Replace('.', '_') + ".content-test.json";
                Assert.That(File.Exists(Path.GetFullPath(testPath)), Is.True, testPath);
                Assert.That(File.ReadAllText(Path.GetFullPath(testPath)), Does.Contain(definition.ContentIdText));
                Assert.That(source, Does.Contain(definition.ContentIdText));
            }

            Assert.That(source, Does.Contain("programmatic-placeholder"));
            Assert.That(source, Does.Contain("development-only"));
        }

        [Test]
        public void PackBuilderProducesSameHashesAndCompleteReportForSameInput()
        {
            var root = Path.Combine(Path.GetTempPath(), "free-world-m9-pack-" + Guid.NewGuid().ToString("N"));
            try
            {
                var first = ContentPackBuilder.Build(RequirePack(), Path.Combine(root, "first"));
                var second = ContentPackBuilder.Build(RequirePack(), Path.Combine(root, "second"));
                Assert.That(second.ContentHash, Is.EqualTo(first.ContentHash));
                Assert.That(second.CatalogHash, Is.EqualTo(first.CatalogHash));
                var report = File.ReadAllText(first.ReportPath);
                Assert.That(report, Does.Contain("test.pack.m9_tools"));
                Assert.That(report, Does.Contain("pack.test.m9_tools"));
                Assert.That(report, Does.Contain(first.ContentHash));
                Assert.That(report, Does.Contain(first.CatalogHash));
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        [Test]
        public void ReleasePolicyRejectsPlaceholderPathOrLabelWithoutBypass()
        {
            var byPath = ReleaseBuildPolicy.ValidateEntry(
                "Assets/GameAssets/Placeholder/test.asset",
                new HashSet<string>());
            var byLabel = ReleaseBuildPolicy.ValidateEntry(
                "Assets/GameAssets/AI/test/final/test.png",
                new HashSet<string> { PlaceholderAssetGenerator.DevelopmentOnlyLabel });
            var valid = ReleaseBuildPolicy.ValidateEntry(
                "Assets/GameAssets/AI/test/final/test.png",
                new HashSet<string> { "release" });
            Assert.That(byPath?.Code, Is.EqualTo("M9-RELEASE-PLACEHOLDER"));
            Assert.That(byLabel?.Code, Is.EqualTo("M9-RELEASE-PLACEHOLDER"));
            Assert.That(valid, Is.Null);
        }

        [Test]
        public void ProvenanceValidatorLocatesMissingAndMismatchedHash()
        {
            var root = Path.Combine(Path.GetTempPath(), "free-world-m9-provenance-" + Guid.NewGuid().ToString("N"));
            try
            {
                var thirdParty = Path.Combine(root, "Assets", "ThirdParty");
                var assetFolder = Path.Combine(root, "Assets", "GameAssets", "AI", "test.asset", "final");
                Directory.CreateDirectory(thirdParty);
                Directory.CreateDirectory(assetFolder);
                File.WriteAllText(Path.Combine(root, "THIRD_PARTY_NOTICES.md"), "# Empty");
                File.WriteAllText(Path.Combine(root, "ASSET_PROVENANCE.csv"),
                    "asset_id,relative_path,sha256,commercial_use_reviewed,status");
                var asset = Path.Combine(assetFolder, "sample.bin");
                File.WriteAllBytes(asset, new byte[] { 1, 2, 3, 4 });

                var missing = ProjectGovernanceValidator.Validate(root);
                Assert.That(ContainsIssue(missing, "M9-PROVENANCE-MISSING"), Is.True);

                var relative = "Assets/GameAssets/AI/test.asset/final/sample.bin";
                var hash = ContentPackBuilder.ComputeFileHash(asset);
                File.WriteAllText(
                    Path.Combine(root, "ASSET_PROVENANCE.csv"),
                    "asset_id,relative_path,sha256,source_category,tool_or_provider,model_or_version," +
                    "reference_rights_confirmed,license_or_terms,commercial_use_reviewed,status\n" +
                    "test.asset," + relative + "," + hash +
                    ",ai-generated,test-tool,test-model,false,terms/test.pdf,true,approved-for-release\n");
                var unclearRights = ProjectGovernanceValidator.Validate(root);
                Assert.That(ContainsIssue(unclearRights, "M9-PROVENANCE-APPROVAL"), Is.True);

                File.WriteAllText(
                    Path.Combine(root, "Assets", "GameAssets", "AI", "test.asset", "provenance.json"),
                    "{\n" +
                    "  \"schemaVersion\": 1,\n" +
                    "  \"assetId\": \"test.asset\",\n" +
                    "  \"relativePaths\": [\"Assets/GameAssets/AI/test.asset/final/sample.bin\"],\n" +
                    "  \"sourceCategory\": \"ai-generated\",\n" +
                    "  \"tool\": \"test-tool\",\n" +
                    "  \"modelVersion\": \"test-model\",\n" +
                    "  \"referenceRightsConfirmed\": true,\n" +
                    "  \"licenseOrTermsSnapshot\": \"terms/test.pdf\",\n" +
                    "  \"commercialUseReviewed\": true,\n" +
                    "  \"outputSha256\": { \"sample.bin\": \"0000000000000000000000000000000000000000000000000000000000000000\" },\n" +
                    "  \"status\": \"approved-for-release\"\n" +
                    "}\n");
                var mismatch = ProjectGovernanceValidator.Validate(root);
                Assert.That(ContainsIssue(mismatch, "M9-PROVENANCE-HASH"), Is.True);
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        [Test]
        public void TriggerChainValidatorReportsStableCyclePath()
        {
            var firstId = SkillTestFactory.Id("test.skill.cycle_a");
            var secondId = SkillTestFactory.Id("test.skill.cycle_b");
            var first = ChainSkill("test.skill.cycle_a", secondId);
            var second = ChainSkill("test.skill.cycle_b", firstId);
            var cycles = TriggerChainValidator.FindCycles(new[] { first, second });
            Assert.That(cycles.Count, Is.EqualTo(1));
            Assert.That(cycles[0], Is.EqualTo(
                "test.skill.cycle_a -> test.skill.cycle_b -> test.skill.cycle_a"));
        }

        [Test]
        public void WaveTimelineUsesTheExactRuntimeCurveSamplerAndReportsOutputs()
        {
            var registry = ContentEditorCatalog.BuildRegistry();
            Assert.That(registry.IsSuccess, Is.True, registry.Error.ToString());
            var id = SkillTestFactory.Id("test.encounter.m9");
            Assert.That(registry.Value.TryGet(id, out RuntimeEncounterSchedule schedule), Is.True);
            var report = WaveTimelineAnalyzer.Analyze(schedule, registry.Value, 1.25f);
            Assert.That(report.IsSuccess, Is.True, report.Error.ToString());
            Assert.That(report.Value.Phases.Count, Is.EqualTo(schedule.Phases.Count));
            Assert.That(report.Value.TotalHealth, Is.GreaterThan(0f));
            Assert.That(report.Value.ExperienceOutput, Is.GreaterThan(0f));
            Assert.That(report.Value.Phases[0].TheoreticalConcurrency, Is.EqualTo(32));
            var time = 30f;
            var editorSample = WaveTimelineAnalyzer.Sample(schedule, time, 1.25f);
            var runtimeSample = EncounterTimelineSampler.Sample(schedule.Phases[0], time, 1.25f);
            Assert.That(editorSample.IsSuccess, Is.True);
            Assert.That(editorSample.Value.BudgetPerSecond, Is.EqualTo(runtimeSample.BudgetPerSecond));
            Assert.That(editorSample.Value.SpawnIntervalSeconds, Is.EqualTo(runtimeSample.SpawnIntervalSeconds));
            Assert.That(editorSample.Value.PhaseFraction, Is.EqualTo(runtimeSample.PhaseFraction));
        }

        [Test]
        public void SkillPreviewEditorMatchesHeadlessHarnessForLevelAttributesAndTargets()
        {
            var skill = AssetDatabase.LoadAssetAtPath<SkillAuthoring>(
                M9ProjectSetup.PackFolder + "/Content/Skills/Second.asset");
            Assert.That(skill, Is.Not.Null);
            var request = new SkillPreviewRequest(0x4D39554CUL, 3f, 12, 2, 1.5f, 0.1f);
            var editor = SkillPreviewEditorService.Run(skill, request);
            Assert.That(editor.IsSuccess, Is.True, editor.Error.ToString());
            var registry = ContentEditorCatalog.BuildRegistry();
            Assert.That(registry.IsSuccess, Is.True, registry.Error.ToString());
            var id = SkillTestFactory.Id(skill.ContentIdText);
            Assert.That(registry.Value.TryGet(id, out var entry), Is.True);
            var headless = SkillPreviewHarness.RunDetailed(registry.Value, entry.Index, request);
            Assert.That(headless.IsSuccess, Is.True, headless.Error.ToString());
            Assert.That(editor.Value.Summary, Is.EqualTo(headless.Value.Summary));
            Assert.That(editor.Value.Geometry.Range, Is.EqualTo(headless.Value.Geometry.Range));
            Assert.That(editor.Value.Geometry.HitboxRadius, Is.EqualTo(headless.Value.Geometry.HitboxRadius));
            Assert.That(editor.Value.Summary.DamagePerSecond, Is.GreaterThan(0f));
            Assert.That(editor.Value.LogLines.Count, Is.GreaterThan(0));
            Assert.That(editor.Value.ManagedAllocationBytes, Is.GreaterThanOrEqualTo(0));
        }

        [Test]
        public void CurrentProjectPassesTheSharedWindowCliAndBuildValidationPipeline()
        {
            var report = ProjectGovernanceValidator.ValidateCurrentProject();
            Assert.That(
                report.IsValid,
                Is.True,
                report.Issues.Count == 0 ? string.Empty : report.Issues[0].ToString());
        }

        private static ContentPackAuthoring RequirePack()
        {
            var pack = AssetDatabase.LoadAssetAtPath<ContentPackAuthoring>(M9ProjectSetup.PackPath);
            Assert.That(pack, Is.Not.Null, "Run M9ProjectSetup.Configure first.");
            return pack;
        }

        private static void AssertType<T>(ContentPackAuthoring pack)
            where T : ContentAuthoringBase
        {
            for (var index = 0; index < pack.Definitions.Count; index++)
                if (pack.Definitions[index] is T) return;
            Assert.Fail("Wizard coverage pack is missing " + typeof(T).Name + ".");
        }

        private static void AssertRegistryId(ContentRegistry registry, string value)
        {
            Assert.That(registry.TryGet(SkillTestFactory.Id(value), out _), Is.True, value);
        }

        private static RuntimeSkillDefinition ChainSkill(string id, ContentId target)
        {
            return SkillTestFactory.Skill(
                id,
                SkillModuleIds.TriggerTimer,
                SkillTestFactory.Module(SkillModuleIds.TargetingSelf),
                SkillTestFactory.Module(SkillModuleIds.DeliveryInstant),
                new[]
                {
                    new EffectOp(
                        EffectOpCode.SpawnSecondarySkill,
                        referenceId0: target)
                });
        }

        private static bool ContainsIssue(ValidationReport report, string code)
        {
            for (var index = 0; index < report.Issues.Count; index++)
                if (string.Equals(report.Issues[index].Code, code, StringComparison.Ordinal)) return true;
            return false;
        }
    }
}
