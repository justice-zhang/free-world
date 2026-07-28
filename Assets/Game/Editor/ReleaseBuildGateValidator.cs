using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine.SceneManagement;
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
        /// <summary>Appends issues for Addressables groups that will enter the current build.</summary>
        public static void AppendCurrentProject(ValidationReport report)
        {
            if (report == null) throw new ArgumentNullException(nameof(report));
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(false);
            if (settings != null)
            {
                for (var groupIndex = 0; groupIndex < settings.groups.Count; groupIndex++)
                {
                    var group = settings.groups[groupIndex];
                    if (group == null || !IsIncludedInBuild(group)) continue;
                    foreach (var entry in group.entries)
                    {
                        var path = AssetDatabase.GUIDToAssetPath(entry.guid);
                        var issue = ReleaseBuildPolicy.ValidateEntry(path, entry.labels);
                        if (issue == null) continue;
                        report.Add(issue.Code, issue.Message);
                        return;
                    }
                }
            }
        }

        /// <summary>Appends an issue when a built scene or one of its dependencies is Placeholder.</summary>
        public static void AppendSceneDependencies(ValidationReport report, string scenePath)
        {
            if (report == null) throw new ArgumentNullException(nameof(report));
            if (string.IsNullOrWhiteSpace(scenePath)) return;
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(false);
            var dependencies = AssetDatabase.GetDependencies(scenePath, true);
            Array.Sort(dependencies, StringComparer.Ordinal);
            for (var index = 0; index < dependencies.Length; index++)
            {
                var path = dependencies[index];
                var entry = settings?.FindAssetEntry(AssetDatabase.AssetPathToGUID(path));
                var issue = ReleaseBuildPolicy.ValidateEntry(path, entry?.labels);
                if (issue == null) continue;
                report.Add(issue.Code, issue.Message);
                return;
            }
        }

        internal static bool IsIncludedInBuild(AddressableAssetGroup group)
        {
            var schema = group?.GetSchema<BundledAssetGroupSchema>();
            return schema == null || schema.IncludeInBuild;
        }

        internal static int CountIncludedPlaceholderEntries()
        {
            var count = 0;
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(false);
            if (settings == null) return 0;
            for (var groupIndex = 0; groupIndex < settings.groups.Count; groupIndex++)
            {
                var group = settings.groups[groupIndex];
                if (group == null || !IsIncludedInBuild(group)) continue;
                foreach (var entry in group.entries)
                {
                    var path = AssetDatabase.GUIDToAssetPath(entry.guid);
                    if (ReleaseBuildPolicy.ValidateEntry(path, entry.labels) != null) count++;
                }
            }

            return count;
        }
    }

    /// <summary>Validates the dependencies of every scene actually processed for Release.</summary>
    internal sealed class ReleaseSceneBuildProcessor : IProcessSceneWithReport
    {
        public int callbackOrder => -1000;

        public void OnProcessScene(Scene scene, BuildReport report)
        {
            // The Test Runner also invokes scene processors while loading PlayMode scenes,
            // but there is no player BuildReport in that path.
            if (report == null) return;
            if ((report.summary.options & BuildOptions.Development) != 0) return;
            var validation = new ValidationReport();
            ReleaseBuildGateValidator.AppendSceneDependencies(validation, scene.path);
            if (!validation.IsValid) throw new BuildFailedException(validation.Issues[0].ToString());
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
                UnityEngine.Application.LogCallback capture = (condition, stackTrace, type) =>
                {
                    if ((condition ?? string.Empty).IndexOf(
                            "M9-RELEASE-PLACEHOLDER",
                            StringComparison.Ordinal) >= 0)
                        observedBuildBlock = true;
                };
                BuildReport report;
                UnityEngine.Application.logMessageReceived += capture;
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
                    UnityEngine.Application.logMessageReceived -= capture;
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
