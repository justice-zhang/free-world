using System;
using System.Collections.Generic;
using System.IO;
using Game.Infrastructure;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Editor
{
    /// <summary>Builds the content-free M10 framework Release verification player.</summary>
    public static class WindowsReleaseBuild
    {
        public const string DefaultOutputPath = "Builds/WindowsRelease/AzureSword.exe";
        private const string TemporaryScenePath = "Assets/__M10ReleaseSmoke.generated.unity";

        /// <summary>Builds the framework Release verification player from the Editor menu.</summary>
        [MenuItem("Tools/Free World/M10/Build Windows Release Verification")]
        public static void BuildFromMenu()
        {
            Build(DefaultOutputPath);
        }

        /// <summary>Builds the framework Release verification player and exits the Editor.</summary>
        public static void BuildFromCommandLine()
        {
            var exitCode = 0;
            try
            {
                var output = Environment.GetEnvironmentVariable("BUILD_OUTPUT");
                Build(string.IsNullOrWhiteSpace(output) ? DefaultOutputPath : output);
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
                    DeleteTemporaryScene();
                    WindowsDevelopmentBuild.RemoveTemporaryAddressablesLinkXml();
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                    exitCode = 1;
                }
            }

            EditorApplication.Exit(exitCode);
        }

        /// <summary>Builds a placeholder-free framework verification player at the requested path.</summary>
        public static BuildReport Build(string outputPath)
        {
            if (string.IsNullOrWhiteSpace(outputPath))
                throw new ArgumentException("Build output path is required.", nameof(outputPath));
            var validation = ProjectGovernanceValidator.ValidateCurrentProject();
            if (!validation.IsValid) throw new BuildFailedException(validation.Issues[0].ToString());
            var sourceState = BuildManifestWriter.CaptureSourceState();
            var absoluteOutput = Path.GetFullPath(outputPath);
            var outputDirectory = Path.GetDirectoryName(absoluteOutput);
            if (string.IsNullOrEmpty(outputDirectory))
                throw new BuildFailedException("Unable to resolve Release output directory.");
            Directory.CreateDirectory(outputDirectory);

            BuildReport report;
            using (var addressables = ReleaseAddressablesScope.ExcludeDevelopmentOnlyGroups())
            {
                var releaseValidation = ProjectGovernanceValidator.ValidateCurrentProject();
                ReleaseBuildGateValidator.AppendCurrentProject(releaseValidation);
                CreateTemporaryScene();
                ReleaseBuildGateValidator.AppendSceneDependencies(
                    releaseValidation,
                    TemporaryScenePath);
                if (!releaseValidation.IsValid)
                    throw new BuildFailedException(releaseValidation.Issues[0].ToString());
                report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { TemporaryScenePath },
                    locationPathName = absoluteOutput,
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.None
                });
                if (report.summary.result != BuildResult.Succeeded)
                    throw new BuildFailedException(
                        "Windows Release Build failed with result " + report.summary.result + ".");
                if (ReleaseBuildGateValidator.CountIncludedPlaceholderEntries() != 0)
                    throw new BuildFailedException("Release output still includes Placeholder Addressables.");
                BuildManifestWriter.Write(
                    outputDirectory,
                    absoluteOutput,
                    report,
                    "WindowsReleaseVerification",
                    false,
                    sourceState);
                Debug.Log("[M10 Release Build] PASS: " + absoluteOutput +
                          "; excludedGroups=" + addressables.ExcludedGroupCount + ".");
            }

            DeleteTemporaryScene();
            return report;
        }

        private static void CreateTemporaryScene()
        {
            DeleteTemporaryScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var owner = new GameObject("M10_ReleaseSmokeRoot");
            owner.AddComponent<M10ReleaseSmokeRunner>();
            if (!EditorSceneManager.SaveScene(scene, TemporaryScenePath))
                throw new BuildFailedException("Unable to save the generated M10 Release scene.");
            AssetDatabase.ImportAsset(TemporaryScenePath, ImportAssetOptions.ForceSynchronousImport);
        }

        private static void DeleteTemporaryScene()
        {
            if (!File.Exists(Path.GetFullPath(TemporaryScenePath))) return;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AssetDatabase.DeleteAsset(TemporaryScenePath);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }
    }

    internal sealed class ReleaseAddressablesScope : IDisposable
    {
        private sealed class GroupState
        {
            public BundledAssetGroupSchema Schema;
            public bool Included;
        }

        private readonly List<GroupState> states = new List<GroupState>();

        private ReleaseAddressablesScope()
        {
        }

        public int ExcludedGroupCount { get; private set; }

        public static ReleaseAddressablesScope ExcludeDevelopmentOnlyGroups()
        {
            var scope = new ReleaseAddressablesScope();
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(false);
            if (settings == null) return scope;
            for (var groupIndex = 0; groupIndex < settings.groups.Count; groupIndex++)
            {
                var group = settings.groups[groupIndex];
                var schema = group?.GetSchema<BundledAssetGroupSchema>();
                if (group == null || schema == null || !ContainsOnlyPlaceholder(group)) continue;
                scope.states.Add(new GroupState { Schema = schema, Included = schema.IncludeInBuild });
                if (!schema.IncludeInBuild) continue;
                schema.IncludeInBuild = false;
                scope.ExcludedGroupCount++;
            }

            return scope;
        }

        public void Dispose()
        {
            for (var index = states.Count - 1; index >= 0; index--)
                states[index].Schema.IncludeInBuild = states[index].Included;
            states.Clear();
        }

        private static bool ContainsOnlyPlaceholder(AddressableAssetGroup group)
        {
            var found = false;
            foreach (var entry in group.entries)
            {
                var path = AssetDatabase.GUIDToAssetPath(entry.guid);
                if (ReleaseBuildPolicy.ValidateEntry(path, entry.labels) == null) return false;
                found = true;
            }

            return found;
        }
    }
}
