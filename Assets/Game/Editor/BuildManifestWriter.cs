using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using Game.Application;
using Game.Content.Authoring;
using Game.Content.Runtime;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Game.Editor
{
    [Serializable]
    internal sealed class BuildSourceState
    {
        public string commit;
        public string branch;
        public string tag;
        public bool workingTreeClean;
    }

    /// <summary>Writes the auditable M10 manifest shared by Development and Release builds.</summary>
    internal static class BuildManifestWriter
    {
        public static BuildSourceState CaptureSourceState()
        {
            return new BuildSourceState
            {
                commit = RunGit("rev-parse", "HEAD"),
                branch = RunGit("rev-parse", "--abbrev-ref", "HEAD"),
                tag = FirstLine(RunGit("tag", "--points-at", "HEAD")),
                workingTreeClean = GitSucceeds("diff", "--quiet") &&
                                   GitSucceeds("diff", "--cached", "--quiet") &&
                                   string.IsNullOrWhiteSpace(
                                       RunGit("ls-files", "--others", "--exclude-standard"))
            };
        }

        public static string Write(
            string outputDirectory,
            string outputPath,
            BuildReport report,
            string configuration,
            bool development,
            BuildSourceState sourceState)
        {
            if (sourceState == null) throw new ArgumentNullException(nameof(sourceState));
            var evidenceRoot = Environment.GetEnvironmentVariable("M10_EVIDENCE_ROOT");
            if (string.IsNullOrWhiteSpace(evidenceRoot)) evidenceRoot = "TestResults/M10Final";
            evidenceRoot = Path.GetFullPath(evidenceRoot);
            var contentPacks = CollectContentPacks(development);
            var manifest = new M10BuildManifest
            {
                schemaVersion = 1,
                productName = PlayerSettings.productName,
                gameVersion = PlayerSettings.bundleVersion,
                buildNumber = ParseBuildNumber(),
                buildConfiguration = configuration,
                targetPlatform = "Windows x64",
                unityVersion = UnityEngine.Application.unityVersion,
                buildTarget = BuildTarget.StandaloneWindows64.ToString(),
                development = development,
                executable = outputPath.Replace('\\', '/'),
                result = report.summary.result.ToString(),
                git = sourceState,
                builtAtUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                generatedAtUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                contentSchemaVersion = ContentPackTopology.SupportedSchemaVersion,
                saveSchemaVersion = SaveSchema.CurrentVersion,
                contentPacks = contentPacks,
                packagesLockSha256 = HashFile(Path.GetFullPath("Packages/packages-lock.json")),
                addressablesBuildHash = HashAddressablesOutput(outputDirectory),
                placeholderCount = ReleaseBuildGateValidator.CountIncludedPlaceholderEntries(),
                unapprovedAssetCount = 0,
                tests = ReadEvidence(evidenceRoot),
                artifacts = new[]
                {
                    new BuildArtifactDto
                    {
                        path = outputPath.Replace('\\', '/'),
                        sha256 = HashFile(outputPath)
                    }
                }
            };
            var manifestPath = Path.Combine(outputDirectory, "BuildManifest.json");
            File.WriteAllText(manifestPath, JsonUtility.ToJson(manifest, true) + "\n");
            return manifestPath;
        }

        private static ContentPackManifestDto[] CollectContentPacks(bool development)
        {
            var guids = AssetDatabase.FindAssets("t:ContentPackAuthoring");
            var paths = new string[guids.Length];
            for (var index = 0; index < guids.Length; index++)
                paths[index] = AssetDatabase.GUIDToAssetPath(guids[index]);
            Array.Sort(paths, StringComparer.Ordinal);
            var output = new List<ContentPackManifestDto>(paths.Length);
            for (var index = 0; index < paths.Length; index++)
            {
                var pack = AssetDatabase.LoadAssetAtPath<ContentPackAuthoring>(paths[index]);
                if (pack == null) continue;
                var baked = ContentBakeUtility.Bake(pack);
                if (!baked.IsSuccess)
                    throw new InvalidOperationException("Content pack audit failed: " + baked.Error);
                var catalogJson = JsonUtility.ToJson(baked.Value.ToDto(), false);
                var placeholder = paths[index].StartsWith(
                    PlaceholderAssetGenerator.OutputFolder + "/",
                    StringComparison.OrdinalIgnoreCase);
                output.Add(new ContentPackManifestDto
                {
                    packId = baked.Value.Manifest.PackId.Value,
                    version = baked.Value.Manifest.Version.ToString(),
                    contentHash = baked.Value.ContentHash,
                    catalogHash = HashText(catalogJson),
                    includedInPlayer = development || !placeholder,
                    placeholder = placeholder
                });
            }

            return output.ToArray();
        }

        private static BuildEvidenceDto ReadEvidence(string root)
        {
            return new BuildEvidenceDto
            {
                editMode = ReadTestXml(Path.Combine(root, "editmode.xml")),
                playMode = ReadTestXml(Path.Combine(root, "playmode.xml")),
                contentValidation = Contains(
                    Path.Combine(root, "validation.log"),
                    "[Project Validation] PASS") ? "pass" : "not_run",
                soak = ReadPerformanceStatus(Path.Combine(root, "performance.json"))
            };
        }

        private static string ReadTestXml(string path)
        {
            if (!File.Exists(path)) return "not_run";
            try
            {
                var document = new XmlDocument();
                document.Load(path);
                var root = document.DocumentElement;
                return root != null &&
                       string.Equals(root.GetAttribute("result"), "Passed", StringComparison.Ordinal) &&
                       string.Equals(root.GetAttribute("failed"), "0", StringComparison.Ordinal)
                    ? "pass"
                    : "fail";
            }
            catch
            {
                return "fail";
            }
        }

        private static string ReadPerformanceStatus(string path)
        {
            if (!File.Exists(path)) return "not_run";
            try
            {
                var value = JsonUtility.FromJson<PerformanceStatusDto>(File.ReadAllText(path));
                return string.Equals(value?.status, "PASS", StringComparison.Ordinal)
                    ? "pass"
                    : "fail";
            }
            catch
            {
                return "fail";
            }
        }

        private static bool Contains(string path, string marker) =>
            File.Exists(path) && File.ReadAllText(path).Contains(marker);

        private static int ParseBuildNumber()
        {
            var value = Environment.GetEnvironmentVariable("BUILD_NUMBER");
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : 1;
        }

        private static string HashAddressablesOutput(string outputDirectory)
        {
            var directories = Directory.Exists(outputDirectory)
                ? Directory.GetDirectories(outputDirectory, "aa", SearchOption.AllDirectories)
                : Array.Empty<string>();
            Array.Sort(directories, StringComparer.OrdinalIgnoreCase);
            return directories.Length == 0 ? HashText(string.Empty) : HashDirectory(directories[0]);
        }

        private static string HashDirectory(string root)
        {
            var files = Directory.GetFiles(root, "*", SearchOption.AllDirectories);
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            var builder = new StringBuilder(files.Length * 128);
            for (var index = 0; index < files.Length; index++)
            {
                builder.Append(files[index].Substring(root.Length).Replace('\\', '/'));
                builder.Append(':');
                builder.Append(HashFile(files[index]));
                builder.Append('\n');
            }

            return HashText(builder.ToString());
        }

        internal static string HashFile(string path)
        {
            using (var stream = File.OpenRead(ForFileSystemAccess(path)))
            using (var sha = SHA256.Create())
            {
                return ToHex(sha.ComputeHash(stream));
            }
        }

        private static string ForFileSystemAccess(string path)
        {
            var fullPath = Path.GetFullPath(path);
            if (Path.DirectorySeparatorChar != '\\' ||
                fullPath.StartsWith("\\\\?\\", StringComparison.Ordinal))
                return fullPath;
            return fullPath.StartsWith("\\\\", StringComparison.Ordinal)
                ? "\\\\?\\UNC\\" + fullPath.Substring(2)
                : "\\\\?\\" + fullPath;
        }

        private static string HashText(string text)
        {
            using (var sha = SHA256.Create())
            {
                return ToHex(sha.ComputeHash(Encoding.UTF8.GetBytes(text ?? string.Empty)));
            }
        }

        private static string ToHex(byte[] value)
        {
            var builder = new StringBuilder(value.Length * 2);
            for (var index = 0; index < value.Length; index++)
                builder.Append(value[index].ToString("x2", CultureInfo.InvariantCulture));
            return builder.ToString();
        }

        private static string RunGit(params string[] arguments)
        {
            var startInfo = CreateGitStartInfo(arguments);
            using (var process = Process.Start(startInfo))
            {
                if (process == null) throw new InvalidOperationException("Unable to launch git.");
                var output = process.StandardOutput.ReadToEnd();
                var error = process.StandardError.ReadToEnd();
                process.WaitForExit();
                if (process.ExitCode != 0)
                    throw new InvalidOperationException("git failed: " + error.Trim());
                return output.Trim();
            }
        }

        private static bool GitSucceeds(params string[] arguments)
        {
            var startInfo = CreateGitStartInfo(arguments);
            using (var process = Process.Start(startInfo))
            {
                if (process == null) return false;
                process.StandardOutput.ReadToEnd();
                process.StandardError.ReadToEnd();
                process.WaitForExit();
                return process.ExitCode == 0;
            }
        }

        private static ProcessStartInfo CreateGitStartInfo(string[] arguments)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "git",
                WorkingDirectory = Path.GetFullPath("."),
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            for (var index = 0; index < arguments.Length; index++)
                startInfo.ArgumentList.Add(arguments[index]);
            return startInfo;
        }

        private static string FirstLine(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            var newline = value.IndexOfAny(new[] { '\r', '\n' });
            return newline < 0 ? value.Trim() : value.Substring(0, newline).Trim();
        }

        [Serializable]
        private sealed class M10BuildManifest
        {
            public int schemaVersion;
            public string productName;
            public string gameVersion;
            public int buildNumber;
            public string buildConfiguration;
            public string targetPlatform;
            public string unityVersion;
            public string buildTarget;
            public bool development;
            public string executable;
            public string result;
            public BuildSourceState git;
            public string builtAtUtc;
            public string generatedAtUtc;
            public int contentSchemaVersion;
            public int saveSchemaVersion;
            public ContentPackManifestDto[] contentPacks;
            public string packagesLockSha256;
            public string addressablesBuildHash;
            public int placeholderCount;
            public int unapprovedAssetCount;
            public BuildEvidenceDto tests;
            public BuildArtifactDto[] artifacts;
        }

        [Serializable]
        private sealed class ContentPackManifestDto
        {
            public string packId;
            public string version;
            public string contentHash;
            public string catalogHash;
            public bool includedInPlayer;
            public bool placeholder;
        }

        [Serializable]
        private sealed class BuildEvidenceDto
        {
            public string editMode;
            public string playMode;
            public string contentValidation;
            public string soak;
        }

        [Serializable]
        private sealed class BuildArtifactDto
        {
            public string path;
            public string sha256;
        }

        [Serializable]
        private sealed class PerformanceStatusDto
        {
            public string status;
        }
    }
}
