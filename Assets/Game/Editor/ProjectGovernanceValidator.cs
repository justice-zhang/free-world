using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Describes one governance validation failure.
    /// </summary>
    public sealed class ValidationIssue
    {
        /// <summary>
        /// Initializes a validation issue.
        /// </summary>
        /// <param name="code">Stable diagnostic code.</param>
        /// <param name="message">Human-readable diagnostic message.</param>
        public ValidationIssue(string code, string message)
        {
            Code = code;
            Message = message;
        }

        /// <summary>
        /// Gets the stable diagnostic code.
        /// </summary>
        public string Code { get; }

        /// <summary>
        /// Gets the diagnostic message.
        /// </summary>
        public string Message { get; }

        /// <inheritdoc />
        public override string ToString()
        {
            return Code + ": " + Message;
        }
    }

    /// <summary>
    /// Contains the immutable outcome of project governance validation.
    /// </summary>
    public sealed class ValidationReport
    {
        private readonly List<ValidationIssue> issues = new List<ValidationIssue>();

        /// <summary>
        /// Gets a value indicating whether validation succeeded.
        /// </summary>
        public bool IsValid => issues.Count == 0;

        /// <summary>
        /// Gets all validation failures.
        /// </summary>
        public IReadOnlyList<ValidationIssue> Issues => issues;

        internal void Add(string code, string message)
        {
            issues.Add(new ValidationIssue(code, message));
        }
    }

    /// <summary>
    /// Enforces the minimum M0 third-party, AI provenance, and release-label rules.
    /// </summary>
    public static class ProjectGovernanceValidator
    {
        /// <summary>
        /// Validates the currently open Unity project.
        /// </summary>
        /// <returns>The validation report.</returns>
        [MenuItem("Tools/Free World/Validate Project")]
        public static ValidationReport ValidateCurrentProject()
        {
            var projectRoot = Directory.GetParent(UnityEngine.Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot))
            {
                var unresolved = new ValidationReport();
                unresolved.Add("M0-ROOT", "Unable to resolve the Unity project root.");
                return unresolved;
            }

            var report = Validate(
                projectRoot,
                AddressableAssetSettingsDefaultObject.GetSettings(false));
            ContentProjectValidator.AppendCurrentProject(report);
            LocalizationProjectValidator.AppendCurrentProject(report);
            CoreApiFreezeValidator.AppendCurrentProject(report);
            return report;
        }

        /// <summary>
        /// Validates a project-shaped directory and optional Addressables settings.
        /// </summary>
        /// <param name="projectRoot">Absolute project root.</param>
        /// <param name="settings">Addressables settings, or null for file-only validation.</param>
        /// <returns>The validation report.</returns>
        public static ValidationReport Validate(
            string projectRoot,
            AddressableAssetSettings settings = null)
        {
            if (string.IsNullOrWhiteSpace(projectRoot))
            {
                throw new ArgumentException("Project root is required.", nameof(projectRoot));
            }

            var report = new ValidationReport();
            var absoluteRoot = Path.GetFullPath(projectRoot);
            ValidateThirdParty(absoluteRoot, report);
            AssetProvenanceValidator.AppendProject(absoluteRoot, report);
            ValidateReleaseLabels(settings, report);
            return report;
        }

        private static void ValidateThirdParty(string projectRoot, ValidationReport report)
        {
            var assetRoot = Path.Combine(projectRoot, "Assets", "ThirdParty");
            if (!Directory.Exists(assetRoot))
            {
                report.Add("M0-THIRDPARTY-DIR", "Assets/ThirdParty is missing.");
                return;
            }

            var noticesPath = Path.Combine(projectRoot, "THIRD_PARTY_NOTICES.md");
            var notices = File.Exists(noticesPath) ? File.ReadAllText(noticesPath) : string.Empty;
            var files = Directory.GetFiles(assetRoot, "*", SearchOption.AllDirectories);

            for (var index = 0; index < files.Length; index++)
            {
                if (ShouldIgnoreGovernanceFile(files[index]))
                {
                    continue;
                }

                var relativePath = NormalizeRelativePath(projectRoot, files[index]);
                if (notices.IndexOf(relativePath, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    report.Add(
                        "M0-THIRDPARTY-UNREGISTERED",
                        relativePath + " is not recorded in THIRD_PARTY_NOTICES.md.");
                }
            }
        }

        private static void ValidateReleaseLabels(
            AddressableAssetSettings settings,
            ValidationReport report)
        {
            if (settings == null)
            {
                return;
            }

            for (var groupIndex = 0; groupIndex < settings.groups.Count; groupIndex++)
            {
                var group = settings.groups[groupIndex];
                if (group == null)
                {
                    continue;
                }

                var entries = group.entries;
                foreach (var entry in entries)
                {
                    if (!entry.labels.Contains("release"))
                    {
                        continue;
                    }

                    var assetPath = AssetDatabase.GUIDToAssetPath(entry.guid);
                    var isPlaceholderPath = assetPath.StartsWith(
                        PlaceholderAssetGenerator.OutputFolder + "/",
                        StringComparison.OrdinalIgnoreCase);
                    var hasDevelopmentLabel =
                        entry.labels.Contains(PlaceholderAssetGenerator.PlaceholderLabel) ||
                        entry.labels.Contains(PlaceholderAssetGenerator.DevelopmentOnlyLabel);

                    if (isPlaceholderPath || hasDevelopmentLabel)
                    {
                        report.Add(
                            "M0-RELEASE-PLACEHOLDER",
                            assetPath + " cannot combine release with placeholder/development-only.");
                    }
                }
            }
        }

        private static bool ShouldIgnoreGovernanceFile(string path)
        {
            var fileName = Path.GetFileName(path);
            return fileName.EndsWith(".meta", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(fileName, ".gitkeep", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(fileName, ".DS_Store", StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeRelativePath(string projectRoot, string path)
        {
            var rootUri = new Uri(AppendDirectorySeparator(projectRoot));
            var fileUri = new Uri(Path.GetFullPath(path));
            return Uri.UnescapeDataString(rootUri.MakeRelativeUri(fileUri).ToString())
                .Replace('\\', '/');
        }

        private static string AppendDirectorySeparator(string path)
        {
            if (path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
            {
                return path;
            }

            return path + Path.DirectorySeparatorChar;
        }
    }

    /// <summary>
    /// Exposes governance validation to the command line.
    /// </summary>
    public static class ProjectValidationCommand
    {
        /// <summary>
        /// Runs validation and exits Unity with zero only when no issues are present.
        /// </summary>
        public static void Run()
        {
            var report = ProjectGovernanceValidator.ValidateCurrentProject();
            for (var index = 0; index < report.Issues.Count; index++)
            {
                Debug.LogError("[Project Validation] " + report.Issues[index]);
            }

            if (report.IsValid)
            {
                Debug.Log("[Project Validation] PASS");
            }

            EditorApplication.Exit(report.IsValid ? 0 : 1);
        }
    }
}
