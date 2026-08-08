using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>Validates formal asset provenance, recorded SHA-256 values, and release inputs.</summary>
    public static class AssetProvenanceValidator
    {
        public const string ReleaseLabel = "release";
        public const string QinglanPackLabel = "pack.qinglan_demo";
        public const string VisualReleaseLabel = "visual.release";
        public const string QinglanVisualGroup = "QinglanDemo-Visual";

        private static readonly Regex HashObjectPattern = new Regex(
            "\\\"(?<name>sourceSha256|outputSha256)\\\"\\s*:\\s*\\{(?<body>.*?)\\}",
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline);

        internal static void AppendProject(string projectRoot, ValidationReport report)
        {
            var gameAssetsRoot = Path.Combine(projectRoot, "Assets", "GameAssets");
            var aiRoot = Path.Combine(gameAssetsRoot, "AI");
            if (!Directory.Exists(aiRoot))
            {
                report.Add("M0-AI-DIR", "Assets/GameAssets/AI is missing.");
                return;
            }

            AppendRoot(projectRoot, aiRoot, report);
            var firstPartyRoot = Path.Combine(gameAssetsRoot, "FirstParty");
            if (Directory.Exists(firstPartyRoot)) AppendRoot(projectRoot, firstPartyRoot, report);
        }

        private static void AppendRoot(string projectRoot, string formalRoot, ValidationReport report)
        {
            var files = Directory.GetFiles(formalRoot, "*", SearchOption.AllDirectories);
            Array.Sort(files, StringComparer.Ordinal);
            for (var index = 0; index < files.Length; index++)
            {
                if (ShouldIgnore(files[index])) continue;
                var issues = ValidateFile(projectRoot, files[index]);
                for (var issueIndex = 0; issueIndex < issues.Count; issueIndex++)
                    report.Add(issues[issueIndex].Code, issues[issueIndex].Message);
            }
        }

        /// <summary>Validates one formal file against an ancestor sidecar or the central CSV.</summary>
        public static IReadOnlyList<ValidationIssue> ValidateFile(
            string projectRoot,
            string assetPath)
        {
            if (string.IsNullOrWhiteSpace(projectRoot))
                throw new ArgumentException("Project root is required.", nameof(projectRoot));
            if (string.IsNullOrWhiteSpace(assetPath))
                throw new ArgumentException("Asset path is required.", nameof(assetPath));

            var root = Path.GetFullPath(projectRoot);
            var file = Path.GetFullPath(assetPath);
            var relative = NormalizeRelativePath(root, file);
            var issues = new List<ValidationIssue>();
            if (!File.Exists(file))
            {
                issues.Add(new ValidationIssue(
                    "M9-PROVENANCE-FILE",
                    relative + " does not resolve to a formal file."));
                return issues;
            }

            var assetsRoot = Path.Combine(root, "Assets");
            var sidecar = FindSidecar(file, assetsRoot);
            if (sidecar == null)
            {
                if (!ValidateCsvRecord(root, relative, file, issues))
                {
                    issues.Add(new ValidationIssue(
                        "M9-PROVENANCE-MISSING",
                        relative + " has no provenance.json or complete ASSET_PROVENANCE.csv record."));
                }

                return issues;
            }

            ProvenanceDocument document;
            string json;
            try
            {
                json = File.ReadAllText(sidecar);
                document = JsonUtility.FromJson<ProvenanceDocument>(json);
            }
            catch (Exception exception)
            {
                issues.Add(new ValidationIssue(
                    "M9-PROVENANCE-JSON",
                    NormalizeRelativePath(root, sidecar) + " cannot be parsed: " + exception.Message));
                return issues;
            }

            if (!HasCompleteApproval(document))
            {
                issues.Add(new ValidationIssue(
                    "M9-PROVENANCE-APPROVAL",
                    NormalizeRelativePath(root, sidecar) +
                    " lacks Schema 2 ownership, generation, rights, terms, reviewers, or approved status."));
            }

            if (!ContainsPath(document == null ? null : document.relativePaths, relative))
            {
                issues.Add(new ValidationIssue(
                    "M9-PROVENANCE-PATH",
                    NormalizeRelativePath(root, sidecar) + " does not list " + relative + "."));
            }

            var hashObject = IsSourceRecord(relative) ? "sourceSha256" : "outputSha256";
            var expected = ExtractHash(json, hashObject, Path.GetFileName(file));
            if (string.IsNullOrEmpty(expected))
            {
                issues.Add(new ValidationIssue(
                    "M9-PROVENANCE-HASH-MISSING",
                    NormalizeRelativePath(root, sidecar) +
                    " has no " + hashObject + " entry for " + Path.GetFileName(file) + "."));
            }
            else
            {
                var actual = ComputeHash(file);
                if (!string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
                {
                    issues.Add(new ValidationIssue(
                        "M9-PROVENANCE-HASH",
                        relative + " SHA-256 is " + actual + " but provenance records " + expected + "."));
                }
            }

            return issues;
        }

        /// <summary>Validates one Addressables entry that carries a release-category label.</summary>
        public static IReadOnlyList<ValidationIssue> ValidateReleaseInput(
            string projectRoot,
            string assetPath,
            ICollection<string> labels,
            string groupName)
        {
            var issues = new List<ValidationIssue>();
            var hasRelease = ContainsLabel(labels, ReleaseLabel);
            var hasVisual = ContainsLabel(labels, VisualReleaseLabel);
            if (!hasRelease && !hasVisual) return issues;

            if (!hasRelease)
            {
                issues.Add(new ValidationIssue(
                    "M9-RELEASE-LABELS",
                    assetPath + " carries " + VisualReleaseLabel + " without " + ReleaseLabel + "."));
                return issues;
            }

            var normalized = (assetPath ?? string.Empty).Replace('\\', '/');
            if (normalized.IndexOf("/source/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                normalized.IndexOf("/working/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                normalized.EndsWith("/prompt.txt", StringComparison.OrdinalIgnoreCase) ||
                normalized.EndsWith("/provenance.json", StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(new ValidationIssue(
                    "M9-RELEASE-NONRUNTIME",
                    normalized + " is a source, working, prompt, or provenance file and cannot be Addressable."));
                return issues;
            }

            if (hasVisual &&
                (!ContainsLabel(labels, QinglanPackLabel) ||
                 !string.Equals(groupName, QinglanVisualGroup, StringComparison.Ordinal)))
            {
                issues.Add(new ValidationIssue(
                    "M9-RELEASE-VISUAL-ROUTING",
                    normalized + " must use group " + QinglanVisualGroup + " and labels " +
                    QinglanPackLabel + ", " + ReleaseLabel + ", " + VisualReleaseLabel + "."));
            }

            var absolute = Path.Combine(
                Path.GetFullPath(projectRoot),
                normalized.Replace('/', Path.DirectorySeparatorChar));
            if (Directory.Exists(absolute))
            {
                issues.Add(new ValidationIssue(
                    "M9-RELEASE-DIRECTORY",
                    normalized + " is a directory entry; formal release inputs must be explicit files."));
                return issues;
            }

            var provenanceIssues = ValidateFile(projectRoot, absolute);
            for (var index = 0; index < provenanceIssues.Count; index++)
                issues.Add(provenanceIssues[index]);
            return issues;
        }

        private static bool HasCompleteApproval(ProvenanceDocument document)
        {
            return document != null && document.schemaVersion == 2 &&
                   !string.IsNullOrWhiteSpace(document.assetId) &&
                   !string.IsNullOrWhiteSpace(document.owner) &&
                   document.relativePaths != null && document.relativePaths.Length > 0 &&
                   !string.IsNullOrWhiteSpace(document.sourceCategory) &&
                   !string.IsNullOrWhiteSpace(document.tool) &&
                   !string.IsNullOrWhiteSpace(document.modelVersion) &&
                   !string.IsNullOrWhiteSpace(document.generatedOrAcquiredAt) &&
                   !string.IsNullOrWhiteSpace(document.operatorName) &&
                   !string.IsNullOrWhiteSpace(document.promptFile) &&
                   !string.IsNullOrWhiteSpace(document.seed) &&
                   document.referenceInputs != null && document.referenceRightsConfirmed &&
                   document.humanEdits != null &&
                   !string.IsNullOrWhiteSpace(document.licenseOrTermsUrl) &&
                   !string.IsNullOrWhiteSpace(document.licenseOrTermsSnapshot) &&
                   !string.IsNullOrWhiteSpace(document.termsReviewedAt) &&
                   document.allowedPlatforms != null && document.allowedPlatforms.Length > 0 &&
                   document.allowedUses != null && document.allowedUses.Length > 0 &&
                   document.commercialUseReviewed &&
                   !string.IsNullOrWhiteSpace(document.steamDisclosureCategory) &&
                   !string.IsNullOrWhiteSpace(document.technicalReviewer) &&
                   !string.IsNullOrWhiteSpace(document.creativeReviewer) &&
                   !string.IsNullOrWhiteSpace(document.rightsReviewer) &&
                   !string.IsNullOrWhiteSpace(document.reviewedAt) &&
                   string.Equals(document.status, "approved-for-release", StringComparison.Ordinal);
        }

        private static bool ValidateCsvRecord(
            string root,
            string relative,
            string file,
            List<ValidationIssue> issues)
        {
            var csvPath = Path.Combine(root, "ASSET_PROVENANCE.csv");
            if (!File.Exists(csvPath)) return false;
            var lines = File.ReadAllLines(csvPath);
            if (lines.Length < 2) return false;
            var headers = ParseCsvLine(lines[0]);
            var pathIndex = IndexOf(headers, "relative_path");
            var hashIndex = IndexOf(headers, "sha256");
            var assetIdIndex = IndexOf(headers, "asset_id");
            var sourceIndex = IndexOf(headers, "source_category");
            var toolIndex = IndexOf(headers, "tool_or_provider");
            var modelIndex = IndexOf(headers, "model_or_version");
            var rightsIndex = IndexOf(headers, "reference_rights_confirmed");
            var termsIndex = IndexOf(headers, "license_or_terms");
            var reviewedIndex = IndexOf(headers, "commercial_use_reviewed");
            var statusIndex = IndexOf(headers, "status");
            if (pathIndex < 0 || hashIndex < 0 || assetIdIndex < 0 || sourceIndex < 0 ||
                toolIndex < 0 || modelIndex < 0 || rightsIndex < 0 || termsIndex < 0 ||
                reviewedIndex < 0 || statusIndex < 0)
                return false;

            for (var lineIndex = 1; lineIndex < lines.Length; lineIndex++)
            {
                var fields = ParseCsvLine(lines[lineIndex]);
                if (pathIndex >= fields.Count ||
                    !string.Equals(fields[pathIndex], relative, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (hashIndex >= fields.Count || assetIdIndex >= fields.Count ||
                    sourceIndex >= fields.Count || toolIndex >= fields.Count ||
                    modelIndex >= fields.Count || rightsIndex >= fields.Count ||
                    termsIndex >= fields.Count || reviewedIndex >= fields.Count ||
                    statusIndex >= fields.Count ||
                    string.IsNullOrWhiteSpace(fields[assetIdIndex]) ||
                    string.IsNullOrWhiteSpace(fields[sourceIndex]) ||
                    string.IsNullOrWhiteSpace(fields[toolIndex]) ||
                    string.IsNullOrWhiteSpace(fields[modelIndex]) ||
                    !string.Equals(fields[rightsIndex], "true", StringComparison.OrdinalIgnoreCase) ||
                    string.IsNullOrWhiteSpace(fields[termsIndex]) ||
                    !string.Equals(fields[reviewedIndex], "true", StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(fields[statusIndex], "approved-for-release", StringComparison.Ordinal))
                {
                    issues.Add(new ValidationIssue(
                        "M9-PROVENANCE-APPROVAL",
                        relative + " has an incomplete or unapproved CSV provenance record."));
                    return true;
                }

                var actual = ComputeHash(file);
                if (!string.Equals(fields[hashIndex], actual, StringComparison.OrdinalIgnoreCase))
                {
                    issues.Add(new ValidationIssue(
                        "M9-PROVENANCE-HASH",
                        relative + " SHA-256 is " + actual +
                        " but ASSET_PROVENANCE.csv records " + fields[hashIndex] + "."));
                }

                return true;
            }

            return false;
        }

        private static string FindSidecar(string file, string assetsRoot)
        {
            var directory = Directory.GetParent(file);
            while (directory != null && IsSameOrChild(directory.FullName, assetsRoot))
            {
                var candidate = Path.Combine(directory.FullName, "provenance.json");
                if (File.Exists(candidate)) return candidate;
                if (string.Equals(
                        Path.GetFullPath(directory.FullName).TrimEnd(Path.DirectorySeparatorChar),
                        Path.GetFullPath(assetsRoot).TrimEnd(Path.DirectorySeparatorChar),
                        StringComparison.OrdinalIgnoreCase))
                    break;
                directory = directory.Parent;
            }

            return null;
        }

        private static string ExtractHash(string json, string objectName, string fileName)
        {
            var matches = HashObjectPattern.Matches(json ?? string.Empty);
            for (var index = 0; index < matches.Count; index++)
            {
                var objectMatch = matches[index];
                if (!string.Equals(objectMatch.Groups["name"].Value, objectName, StringComparison.Ordinal))
                    continue;
                var pattern = "\\\"" + Regex.Escape(fileName) +
                              "\\\"\\s*:\\s*\\\"(?<hash>[0-9a-fA-F]{64})\\\"";
                var hashMatch = Regex.Match(
                    objectMatch.Groups["body"].Value,
                    pattern,
                    RegexOptions.CultureInvariant);
                return hashMatch.Success ? hashMatch.Groups["hash"].Value : string.Empty;
            }

            return string.Empty;
        }

        private static bool IsSourceRecord(string relative)
        {
            var normalized = (relative ?? string.Empty).Replace('\\', '/');
            return normalized.IndexOf("/source/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   normalized.EndsWith("/prompt.txt", StringComparison.OrdinalIgnoreCase);
        }

        private static bool ContainsLabel(ICollection<string> labels, string expected)
        {
            if (labels == null) return false;
            foreach (var label in labels)
                if (string.Equals(label, expected, StringComparison.Ordinal)) return true;
            return false;
        }

        private static bool ContainsPath(string[] paths, string relative)
        {
            if (paths == null) return false;
            for (var index = 0; index < paths.Length; index++)
            {
                if (string.Equals(
                        (paths[index] ?? string.Empty).Replace('\\', '/'),
                        relative,
                        StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static string ComputeHash(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(stream);
                var output = new StringBuilder(bytes.Length * 2);
                for (var index = 0; index < bytes.Length; index++)
                    output.Append(bytes[index].ToString("x2", CultureInfo.InvariantCulture));
                return output.ToString();
            }
        }

        private static bool ShouldIgnore(string path)
        {
            var name = Path.GetFileName(path);
            return name.EndsWith(".meta", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(name, ".gitkeep", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(name, ".DS_Store", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(name, "provenance.json", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsSameOrChild(string path, string parent)
        {
            var fullPath = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar) +
                           Path.DirectorySeparatorChar;
            var fullParent = Path.GetFullPath(parent).TrimEnd(Path.DirectorySeparatorChar) +
                             Path.DirectorySeparatorChar;
            return fullPath.StartsWith(fullParent, StringComparison.OrdinalIgnoreCase);
        }

        private static int IndexOf(IReadOnlyList<string> values, string expected)
        {
            for (var index = 0; index < values.Count; index++)
                if (string.Equals(values[index], expected, StringComparison.OrdinalIgnoreCase)) return index;
            return -1;
        }

        private static List<string> ParseCsvLine(string line)
        {
            var fields = new List<string>();
            var value = new StringBuilder();
            var quoted = false;
            for (var index = 0; index < (line ?? string.Empty).Length; index++)
            {
                var character = line[index];
                if (character == '"')
                {
                    if (quoted && index + 1 < line.Length && line[index + 1] == '"')
                    {
                        value.Append('"');
                        index++;
                    }
                    else quoted = !quoted;
                }
                else if (character == ',' && !quoted)
                {
                    fields.Add(value.ToString());
                    value.Length = 0;
                }
                else value.Append(character);
            }

            fields.Add(value.ToString());
            return fields;
        }

        private static string NormalizeRelativePath(string root, string path)
        {
            var rootUri = new Uri(root.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                ? root
                : root + Path.DirectorySeparatorChar);
            var fileUri = new Uri(Path.GetFullPath(path));
            return Uri.UnescapeDataString(rootUri.MakeRelativeUri(fileUri).ToString()).Replace('\\', '/');
        }

        [Serializable]
        private sealed class ProvenanceDocument
        {
            public int schemaVersion;
            public string assetId;
            public string owner;
            public string[] relativePaths;
            public string sourceCategory;
            public string tool;
            public string modelVersion;
            public string generatedOrAcquiredAt;
            public string operatorName;
            public string promptFile;
            public string seed;
            public string[] referenceInputs;
            public bool referenceRightsConfirmed;
            public string[] humanEdits;
            public string licenseOrTermsUrl;
            public string licenseOrTermsSnapshot;
            public string termsReviewedAt;
            public string[] allowedPlatforms;
            public string[] allowedUses;
            public bool commercialUseReviewed;
            public string steamDisclosureCategory;
            public string technicalReviewer;
            public string creativeReviewer;
            public string rightsReviewer;
            public string reviewedAt;
            public string status;
        }
    }
}
