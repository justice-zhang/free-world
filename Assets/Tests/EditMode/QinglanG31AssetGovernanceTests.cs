using System;
using System.Collections.Generic;
using System.IO;
using Game.Editor;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public sealed class QinglanG31AssetGovernanceTests
    {
        [Test]
        public void FirstPartyFormalFileWithoutProvenanceIsRejected()
        {
            var root = CreateProjectRoot();
            try
            {
                var file = Path.Combine(
                    root,
                    "Assets",
                    "GameAssets",
                    "FirstParty",
                    "QinglanDemo",
                    "ART-UI-002",
                    "final",
                    "ui-atlas.png");
                Directory.CreateDirectory(Path.GetDirectoryName(file));
                File.WriteAllBytes(file, new byte[] { 1, 2, 3 });

                var report = ProjectGovernanceValidator.Validate(root);

                Assert.That(ContainsIssue(report.Issues, "M9-PROVENANCE-MISSING"), Is.True);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [Test]
        public void SchemaTwoValidatesSourcePromptFinalAndReleaseRouting()
        {
            var root = CreateProjectRoot();
            try
            {
                var batch = WriteApprovedBatch(root, true, true);

                var report = ProjectGovernanceValidator.Validate(root);
                var accepted = AssetProvenanceValidator.ValidateReleaseInput(
                    root,
                    batch.FinalRelativePath,
                    new HashSet<string>
                    {
                        AssetProvenanceValidator.QinglanPackLabel,
                        AssetProvenanceValidator.ReleaseLabel,
                        AssetProvenanceValidator.VisualReleaseLabel
                    },
                    AssetProvenanceValidator.QinglanVisualGroup);

                Assert.That(report.IsValid, Is.True, JoinIssues(report.Issues));
                Assert.That(accepted, Is.Empty, JoinIssues(accepted));
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [Test]
        public void ReleaseRoutingRejectsSourceAndWrongVisualGroup()
        {
            var root = CreateProjectRoot();
            try
            {
                var batch = WriteApprovedBatch(root, true, true);
                var labels = new HashSet<string>
                {
                    AssetProvenanceValidator.QinglanPackLabel,
                    AssetProvenanceValidator.ReleaseLabel,
                    AssetProvenanceValidator.VisualReleaseLabel
                };

                var sourceIssues = AssetProvenanceValidator.ValidateReleaseInput(
                    root,
                    batch.SourceRelativePath,
                    labels,
                    AssetProvenanceValidator.QinglanVisualGroup);
                var routingIssues = AssetProvenanceValidator.ValidateReleaseInput(
                    root,
                    batch.FinalRelativePath,
                    labels,
                    "Default Local Group");

                Assert.That(ContainsIssue(sourceIssues, "M9-RELEASE-NONRUNTIME"), Is.True);
                Assert.That(ContainsIssue(routingIssues, "M9-RELEASE-VISUAL-ROUTING"), Is.True);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [Test]
        public void ApprovalAndHashChangesInvalidateFormalBatch()
        {
            var root = CreateProjectRoot();
            try
            {
                var unapproved = WriteApprovedBatch(root, false, true);
                var approvalIssues = AssetProvenanceValidator.ValidateFile(root, unapproved.FinalPath);
                Assert.That(ContainsIssue(approvalIssues, "M9-PROVENANCE-APPROVAL"), Is.True);

                Directory.Delete(unapproved.BatchRoot, true);
                var mismatched = WriteApprovedBatch(root, true, false);
                var hashIssues = AssetProvenanceValidator.ValidateFile(root, mismatched.FinalPath);
                Assert.That(ContainsIssue(hashIssues, "M9-PROVENANCE-HASH"), Is.True);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        private static string CreateProjectRoot()
        {
            var root = Path.Combine(
                Path.GetTempPath(),
                "free-world-g31-governance-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(root, "Assets", "GameAssets", "AI"));
            Directory.CreateDirectory(Path.Combine(root, "Assets", "ThirdParty"));
            File.WriteAllText(Path.Combine(root, "THIRD_PARTY_NOTICES.md"), "# Test notices\n");
            return root;
        }

        private static BatchPaths WriteApprovedBatch(
            string root,
            bool commercialUseReviewed,
            bool correctFinalHash)
        {
            var batchRoot = Path.Combine(
                root,
                "Assets",
                "GameAssets",
                "FirstParty",
                "QinglanDemo",
                "ART-UI-002");
            var sourcePath = Path.Combine(batchRoot, "source", "generator-spec.json");
            var promptPath = Path.Combine(batchRoot, "prompt.txt");
            var finalPath = Path.Combine(batchRoot, "final", "ui-atlas.png");
            Directory.CreateDirectory(Path.GetDirectoryName(sourcePath));
            Directory.CreateDirectory(Path.GetDirectoryName(finalPath));
            File.WriteAllText(sourcePath, "{\"seed\":31002}\n");
            File.WriteAllText(promptPath, "First-party vector construction specification.\n");
            File.WriteAllBytes(finalPath, new byte[] { 4, 8, 15, 16, 23, 42 });

            var sourceRelative = Relative(root, sourcePath);
            var promptRelative = Relative(root, promptPath);
            var finalRelative = Relative(root, finalPath);
            var sourceHash = ContentPackBuilder.ComputeFileHash(sourcePath);
            var promptHash = ContentPackBuilder.ComputeFileHash(promptPath);
            var finalHash = correctFinalHash
                ? ContentPackBuilder.ComputeFileHash(finalPath)
                : new string('0', 64);
            File.WriteAllText(
                Path.Combine(batchRoot, "provenance.json"),
                "{\n" +
                "  \"schemaVersion\": 2,\n" +
                "  \"assetId\": \"ART-UI-002\",\n" +
                "  \"owner\": \"Qinglan Demo Visual Owner\",\n" +
                "  \"relativePaths\": [\"" + sourceRelative + "\", \"" + promptRelative +
                "\", \"" + finalRelative + "\"],\n" +
                "  \"sourceCategory\": \"first-party-vector\",\n" +
                "  \"tool\": \"repository-generator\",\n" +
                "  \"modelVersion\": \"git-test-sha\",\n" +
                "  \"generatedOrAcquiredAt\": \"2026-08-09T00:00:00+08:00\",\n" +
                "  \"operatorName\": \"Codex\",\n" +
                "  \"promptFile\": \"" + promptRelative + "\",\n" +
                "  \"seed\": \"31002\",\n" +
                "  \"referenceInputs\": [],\n" +
                "  \"referenceRightsConfirmed\": true,\n" +
                "  \"humanEdits\": [\"none\"],\n" +
                "  \"sourceSha256\": {\"generator-spec.json\": \"" + sourceHash +
                "\", \"prompt.txt\": \"" + promptHash + "\"},\n" +
                "  \"outputSha256\": {\"ui-atlas.png\": \"" + finalHash + "\"},\n" +
                "  \"licenseOrTermsUrl\": \"repository://AGENTS.md\",\n" +
                "  \"licenseOrTermsSnapshot\": \"Docs/AssetTerms/first-party-2026-08-09.md\",\n" +
                "  \"termsReviewedAt\": \"2026-08-09\",\n" +
                "  \"allowedPlatforms\": [\"Windows x64\", \"Steam\"],\n" +
                "  \"allowedUses\": [\"commercial game runtime\"],\n" +
                "  \"commercialUseReviewed\": " +
                commercialUseReviewed.ToString().ToLowerInvariant() + ",\n" +
                "  \"steamDisclosureCategory\": \"first-party-procedural\",\n" +
                "  \"technicalReviewer\": \"Codex\",\n" +
                "  \"creativeReviewer\": \"Codex\",\n" +
                "  \"rightsReviewer\": \"Codex\",\n" +
                "  \"reviewedAt\": \"2026-08-09T00:00:00+08:00\",\n" +
                "  \"status\": \"approved-for-release\"\n" +
                "}\n");

            return new BatchPaths(batchRoot, sourcePath, sourceRelative, finalPath, finalRelative);
        }

        private static string Relative(string root, string path)
        {
            var rootUri = new Uri(root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar);
            return Uri.UnescapeDataString(rootUri.MakeRelativeUri(new Uri(path)).ToString()).Replace('\\', '/');
        }

        private static bool ContainsIssue(IReadOnlyList<ValidationIssue> issues, string code)
        {
            for (var index = 0; index < issues.Count; index++)
                if (string.Equals(issues[index].Code, code, StringComparison.Ordinal)) return true;
            return false;
        }

        private static string JoinIssues(IReadOnlyList<ValidationIssue> issues)
        {
            var value = string.Empty;
            for (var index = 0; index < issues.Count; index++)
                value += (index == 0 ? string.Empty : "\n") + issues[index];
            return value;
        }

        private sealed class BatchPaths
        {
            public BatchPaths(
                string batchRoot,
                string sourcePath,
                string sourceRelativePath,
                string finalPath,
                string finalRelativePath)
            {
                BatchRoot = batchRoot;
                SourcePath = sourcePath;
                SourceRelativePath = sourceRelativePath;
                FinalPath = finalPath;
                FinalRelativePath = finalRelativePath;
            }

            public string BatchRoot { get; }
            public string SourcePath { get; }
            public string SourceRelativePath { get; }
            public string FinalPath { get; }
            public string FinalRelativePath { get; }
        }
    }
}
