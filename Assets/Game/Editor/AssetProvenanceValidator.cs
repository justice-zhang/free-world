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
    /// <summary>Validates release provenance records and recorded output SHA-256 values.</summary>
    public static class AssetProvenanceValidator
    {
        private static readonly Regex HashObjectPattern = new Regex(
            "\\\"outputSha256\\\"\\s*:\\s*\\{(?<body>.*?)\\}",
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline);

        internal static void AppendProject(string projectRoot, ValidationReport report)
        {
            var aiRoot = Path.Combine(projectRoot, "Assets", "GameAssets", "AI");
            if (!Directory.Exists(aiRoot))
            {
                report.Add("M0-AI-DIR", "Assets/GameAssets/AI is missing.");
                return;
            }

            var files = Directory.GetFiles(aiRoot, "*", SearchOption.AllDirectories);
            for (var index = 0; index < files.Length; index++)
            {
                if (ShouldIgnore(files[index])) continue;
                var issues = ValidateFile(projectRoot, files[index]);
                for (var issueIndex = 0; issueIndex < issues.Count; issueIndex++)
                    report.Add(issues[issueIndex].Code, issues[issueIndex].Message);
            }
        }

        /// <summary>Validates one AI asset against an ancestor sidecar or the central CSV.</summary>
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
            var aiRoot = Path.Combine(root, "Assets", "GameAssets", "AI");
            var sidecar = FindSidecar(file, aiRoot);
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

            if (document == null || document.schemaVersion != 1 ||
                 string.IsNullOrWhiteSpace(document.assetId) ||
                 string.IsNullOrWhiteSpace(document.sourceCategory) ||
                 string.IsNullOrWhiteSpace(document.tool) ||
                 string.IsNullOrWhiteSpace(document.modelVersion) ||
                 !document.referenceRightsConfirmed || !document.commercialUseReviewed ||
                !string.Equals(document.status, "approved-for-release", StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(document.licenseOrTermsSnapshot))
            {
                issues.Add(new ValidationIssue(
                    "M9-PROVENANCE-APPROVAL",
                    NormalizeRelativePath(root, sidecar) +
                    " lacks schema, rights, commercial review, terms snapshot, or approved status."));
            }

            if (!ContainsPath(document == null ? null : document.relativePaths, relative))
            {
                issues.Add(new ValidationIssue(
                    "M9-PROVENANCE-PATH",
                    NormalizeRelativePath(root, sidecar) + " does not list " + relative + "."));
            }

            var expected = ExtractHash(json, Path.GetFileName(file));
            if (string.IsNullOrEmpty(expected))
            {
                issues.Add(new ValidationIssue(
                    "M9-PROVENANCE-HASH-MISSING",
                    NormalizeRelativePath(root, sidecar) +
                    " has no outputSha256 entry for " + Path.GetFileName(file) + "."));
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

        private static string FindSidecar(string file, string aiRoot)
        {
            var directory = Directory.GetParent(file);
            while (directory != null &&
                   directory.FullName.StartsWith(aiRoot, StringComparison.OrdinalIgnoreCase))
            {
                var candidate = Path.Combine(directory.FullName, "provenance.json");
                if (File.Exists(candidate)) return candidate;
                if (string.Equals(directory.FullName, aiRoot, StringComparison.OrdinalIgnoreCase)) break;
                directory = directory.Parent;
            }

            return null;
        }

        private static string ExtractHash(string json, string fileName)
        {
            var objectMatch = HashObjectPattern.Match(json ?? string.Empty);
            if (!objectMatch.Success) return string.Empty;
            var pattern = "\\\"" + Regex.Escape(fileName) +
                          "\\\"\\s*:\\s*\\\"(?<hash>[0-9a-fA-F]{64})\\\"";
            var hashMatch = Regex.Match(
                objectMatch.Groups["body"].Value,
                pattern,
                RegexOptions.CultureInvariant);
            return hashMatch.Success ? hashMatch.Groups["hash"].Value : string.Empty;
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
                   string.Equals(name, "provenance.json", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(name, "prompt.txt", StringComparison.OrdinalIgnoreCase);
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
            public string[] relativePaths;
            public string sourceCategory;
            public string tool;
            public string modelVersion;
            public bool referenceRightsConfirmed;
            public string licenseOrTermsSnapshot;
            public bool commercialUseReviewed;
            public string status;
        }
    }
}
