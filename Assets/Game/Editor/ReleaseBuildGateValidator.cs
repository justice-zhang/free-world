using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>Pure release policy shared by the preprocessor and tests.</summary>
    public static class ReleaseBuildPolicy
    {
        /// <summary>Returns a release-blocking issue for one included asset, or null.</summary>
        public static ValidationIssue ValidateEntry(
            string assetPath,
            ICollection<string> labels)
        {
            var placeholderPath = (assetPath ?? string.Empty).StartsWith(
                PlaceholderAssetGenerator.OutputFolder + "/",
                StringComparison.OrdinalIgnoreCase);
            var placeholderLabel = labels != null &&
                                   (labels.Contains(PlaceholderAssetGenerator.PlaceholderLabel) ||
                                    labels.Contains(PlaceholderAssetGenerator.DevelopmentOnlyLabel));
            return placeholderPath || placeholderLabel
                ? new ValidationIssue(
                    "M9-RELEASE-PLACEHOLDER",
                    (assetPath ?? "<unknown>") +
                    " is placeholder/development-only and cannot enter a Release build.")
                : null;
        }
    }

    /// <summary>Applies non-bypassable Release-only project checks.</summary>
    public static class ReleaseBuildGateValidator
    {
        /// <summary>Appends a representative Release-only issue in the current project.</summary>
        public static void AppendCurrentProject(ValidationReport report)
        {
            if (report == null) throw new ArgumentNullException(nameof(report));
            var placeholderReported = false;
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(false);
            if (settings != null)
            {
                for (var groupIndex = 0; groupIndex < settings.groups.Count; groupIndex++)
                {
                    var group = settings.groups[groupIndex];
                    if (group == null) continue;
                    foreach (var entry in group.entries)
                    {
                        var path = AssetDatabase.GUIDToAssetPath(entry.guid);
                        var issue = ReleaseBuildPolicy.ValidateEntry(path, entry.labels);
                        if (issue == null) continue;
                        report.Add(issue.Code, issue.Message);
                        placeholderReported = true;
                        break;
                    }

                    if (placeholderReported) break;
                }
            }

            var placeholderRoot = Path.GetFullPath(PlaceholderAssetGenerator.OutputFolder);
            if (!placeholderReported && Directory.Exists(placeholderRoot))
            {
                var files = Directory.GetFiles(placeholderRoot, "*", SearchOption.AllDirectories);
                for (var index = 0; index < files.Length; index++)
                {
                    if (files[index].EndsWith(".meta", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(Path.GetFileName(files[index]), ".gitkeep", StringComparison.OrdinalIgnoreCase))
                        continue;
                    report.Add(
                        "M9-RELEASE-PLACEHOLDER",
                        files[index].Replace('\\', '/') + " is inside the Placeholder tree.");
                    break;
                }
            }
        }
    }

    /// <summary>Command-line negative/positive gate used before a Release player build.</summary>
    public static class M9ReleaseGateCommand
    {
        /// <summary>Runs normal validation plus Release-only checks and exits nonzero when blocked.</summary>
        public static void Run()
        {
            var report = ProjectGovernanceValidator.ValidateCurrentProject();
            ReleaseBuildGateValidator.AppendCurrentProject(report);
            for (var index = 0; index < report.Issues.Count; index++)
                Debug.LogError("[M9 Release Gate] " + report.Issues[index]);
            Debug.Log(report.IsValid ? "[M9 Release Gate] PASS" : "[M9 Release Gate] BLOCKED");
            EditorApplication.Exit(report.IsValid ? 0 : 1);
        }
    }

    /// <summary>Invokes a real non-Development player build as an expected-failure gate.</summary>
    public static class M9ReleaseBuildNegativeCommand
    {
        /// <summary>Passes only when the Release build is rejected for Placeholder content.</summary>
        public static void Run()
        {
            var exitCode = 0;
            var projectSettingsPath = Path.GetFullPath("ProjectSettings/ProjectSettings.asset");
            var renderSettingsPath = Path.GetFullPath(
                "Assets/UniversalRenderPipelineGlobalSettings.asset");
            var projectSettings = File.Exists(projectSettingsPath)
                ? File.ReadAllBytes(projectSettingsPath)
                : null;
            var renderSettings = File.Exists(renderSettingsPath)
                ? File.ReadAllBytes(renderSettingsPath)
                : null;
            try
            {
                var validation = ProjectGovernanceValidator.ValidateCurrentProject();
                ReleaseBuildGateValidator.AppendCurrentProject(validation);
                var hasExpectedIssue = false;
                for (var index = 0; index < validation.Issues.Count; index++)
                {
                    if (!string.Equals(
                            validation.Issues[index].Code,
                            "M9-RELEASE-PLACEHOLDER",
                            StringComparison.Ordinal)) continue;
                    hasExpectedIssue = true;
                    break;
                }

                if (!hasExpectedIssue)
                    throw new UnityException("The project does not contain the expected Placeholder blocker.");

                var configuredOutput = Environment.GetEnvironmentVariable("BUILD_OUTPUT");
                var output = string.IsNullOrWhiteSpace(configuredOutput)
                    ? "Builds/M9ReleaseGateNegative/AzureSword.exe"
                    : configuredOutput;
                var observedBuildBlock = false;
                Application.LogCallback capture = (condition, stackTrace, type) =>
                {
                    if ((condition ?? string.Empty).IndexOf(
                            "M9-RELEASE-PLACEHOLDER",
                            StringComparison.Ordinal) >= 0)
                        observedBuildBlock = true;
                };
                BuildReport report;
                Application.logMessageReceived += capture;
                try
                {
                    report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                    {
                        scenes = new[] { M0ProjectSetup.BootstrapScenePath },
                        locationPathName = Path.GetFullPath(output),
                        target = BuildTarget.StandaloneWindows64,
                        options = BuildOptions.None
                    });
                }
                finally
                {
                    Application.logMessageReceived -= capture;
                }

                if (report.summary.result == BuildResult.Succeeded)
                    throw new UnityException("Release build unexpectedly succeeded with Placeholder content.");
                if (!observedBuildBlock)
                    throw new UnityException(
                        "Release build failed without observing the expected Placeholder diagnostic.");
                Debug.Log("[M9 Release Build Gate] PASS: actual Release build result=" +
                          report.summary.result + ".");
            }
            catch (BuildFailedException exception)
            {
                if (exception.Message.IndexOf(
                        "M9-RELEASE-PLACEHOLDER",
                        StringComparison.Ordinal) < 0)
                {
                    Debug.LogException(exception);
                    exitCode = 1;
                }
                else
                {
                    Debug.Log("[M9 Release Build Gate] PASS: " + exception.Message);
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                exitCode = 1;
            }
            finally
            {
                try
                {
                    RestoreFile(projectSettingsPath, projectSettings);
                    RestoreFile(renderSettingsPath, renderSettings);
                    AssetDatabase.DeleteAsset("Assets/AddressableAssetsData/link.xml");
                    AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                    exitCode = 1;
                }
            }

            EditorApplication.Exit(exitCode);
        }

        private static void RestoreFile(string path, byte[] content)
        {
            if (content != null) File.WriteAllBytes(path, content);
        }
    }
}
