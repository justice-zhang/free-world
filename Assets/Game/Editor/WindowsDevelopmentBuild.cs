using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Provides the deterministic Windows x64 Development Build entry point.
    /// </summary>
    public static class WindowsDevelopmentBuild
    {
        /// <summary>
        /// Default project-relative executable path.
        /// </summary>
        public const string DefaultOutputPath = "Builds/WindowsDevelopment/AzureSword.exe";

        /// <summary>
        /// Builds the M0 Windows x64 development player using the default output path.
        /// </summary>
        [MenuItem("Tools/Free World/M0/Build Windows Development")]
        public static void BuildFromMenu()
        {
            Build(DefaultOutputPath);
        }

        /// <summary>
        /// Builds from the command line and exits Unity with an actionable status code.
        /// </summary>
        public static void BuildFromCommandLine()
        {
            try
            {
                var configuredOutput = Environment.GetEnvironmentVariable("BUILD_OUTPUT");
                var output = string.IsNullOrWhiteSpace(configuredOutput)
                    ? DefaultOutputPath
                    : configuredOutput;
                Build(output);
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        /// <summary>
        /// Builds a Windows x64 Development player.
        /// </summary>
        /// <param name="outputPath">Absolute or project-relative executable path.</param>
        /// <returns>The completed Unity build report.</returns>
        public static BuildReport Build(string outputPath)
        {
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                throw new ArgumentException("Build output path is required.", nameof(outputPath));
            }

            if (!File.Exists(M0ProjectSetup.BootstrapScenePath))
            {
                throw new BuildFailedException(
                    "Bootstrap scene is missing. Run Game.Editor.M0ProjectSetup.Configure.");
            }

            var validation = ProjectGovernanceValidator.ValidateCurrentProject();
            if (!validation.IsValid)
            {
                throw new BuildFailedException(validation.Issues[0].ToString());
            }

            var absoluteOutput = Path.GetFullPath(outputPath);
            var outputDirectory = Path.GetDirectoryName(absoluteOutput);
            if (string.IsNullOrEmpty(outputDirectory))
            {
                throw new BuildFailedException("Unable to resolve build output directory.");
            }

            Directory.CreateDirectory(outputDirectory);
            var options = new BuildPlayerOptions
            {
                scenes = new[] { M0ProjectSetup.BootstrapScenePath },
                locationPathName = absoluteOutput,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            };

            var report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new BuildFailedException(
                    "Windows Development Build failed with result " + report.summary.result + ".");
            }

            WriteBuildManifest(outputDirectory, absoluteOutput, report);
            Debug.Log("[M0 Build] PASS: " + absoluteOutput);
            return report;
        }

        private static void WriteBuildManifest(
            string outputDirectory,
            string outputPath,
            BuildReport report)
        {
            var manifest = new DevelopmentBuildManifest
            {
                unityVersion = UnityEngine.Application.unityVersion,
                buildTarget = BuildTarget.StandaloneWindows64.ToString(),
                development = true,
                executable = outputPath.Replace('\\', '/'),
                result = report.summary.result.ToString(),
                generatedAtUtc = DateTime.UtcNow.ToString("O")
            };
            var json = JsonUtility.ToJson(manifest, true);
            File.WriteAllText(Path.Combine(outputDirectory, "BuildManifest.json"), json);
        }

        [Serializable]
        private sealed class DevelopmentBuildManifest
        {
            public string unityVersion;
            public string buildTarget;
            public bool development;
            public string executable;
            public string result;
            public string generatedAtUtc;
        }
    }
}
