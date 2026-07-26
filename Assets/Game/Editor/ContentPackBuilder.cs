using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Game.Content.Authoring;
using Game.Content.Runtime;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>Result of one deterministic editor-side content-pack build.</summary>
    public sealed class ContentPackBuildResult
    {
        internal ContentPackBuildResult(
            string packId,
            string version,
            string contentHash,
            string catalogHash,
            string catalogPath,
            string reportPath)
        {
            PackId = packId;
            Version = version;
            ContentHash = contentHash;
            CatalogHash = catalogHash;
            CatalogPath = catalogPath;
            ReportPath = reportPath;
        }

        /// <summary>Gets the stable pack ID.</summary>
        public string PackId { get; }
        /// <summary>Gets the semantic pack version.</summary>
        public string Version { get; }
        /// <summary>Gets the hash produced by the canonical content baker.</summary>
        public string ContentHash { get; }
        /// <summary>Gets the SHA-256 of the emitted catalog file.</summary>
        public string CatalogHash { get; }
        /// <summary>Gets the absolute emitted catalog path.</summary>
        public string CatalogPath { get; }
        /// <summary>Gets the absolute emitted build-report path.</summary>
        public string ReportPath { get; }
    }

    /// <summary>Writes deterministic runtime catalogs and auditable pack reports.</summary>
    public static class ContentPackBuilder
    {
        /// <summary>Builds one pack without mutating its authoring data.</summary>
        public static ContentPackBuildResult Build(
            ContentPackAuthoring pack,
            string outputRoot)
        {
            if (pack == null) throw new ArgumentNullException(nameof(pack));
            if (string.IsNullOrWhiteSpace(outputRoot))
                throw new ArgumentException("Output root is required.", nameof(outputRoot));

            var baked = ContentBakeUtility.Bake(pack);
            if (!baked.IsSuccess) throw new UnityException(baked.Error.ToString());
            var catalog = baked.Value;
            var folder = Path.Combine(
                Path.GetFullPath(outputRoot),
                SafeSegment(catalog.Manifest.PackId.Value),
                SafeSegment(catalog.Manifest.Version.ToString()));
            Directory.CreateDirectory(folder);

            var catalogPath = Path.Combine(folder, "catalog.json");
            var catalogJson = JsonUtility.ToJson(catalog.ToDto(), true) + "\n";
            WriteUtf8(catalogPath, catalogJson);
            var catalogHash = ComputeFileHash(catalogPath);

            var dependencies = new PackBuildDependencyDto[catalog.Manifest.Dependencies.Count];
            for (var index = 0; index < dependencies.Length; index++)
            {
                var dependency = catalog.Manifest.Dependencies[index];
                dependencies[index] = new PackBuildDependencyDto
                {
                    packId = dependency.PackId.Value,
                    minimumVersion = dependency.MinimumVersion.ToString(),
                    maximumVersion = dependency.MaximumVersion.HasValue
                        ? dependency.MaximumVersion.Value.ToString()
                        : string.Empty
                };
            }

            var labels = catalog.Manifest.SourceAssetPath.StartsWith(
                PlaceholderAssetGenerator.OutputFolder + "/",
                StringComparison.OrdinalIgnoreCase)
                ? new[]
                {
                    catalog.Manifest.AssetLabel,
                    PlaceholderAssetGenerator.PlaceholderLabel,
                    PlaceholderAssetGenerator.DevelopmentOnlyLabel
                }
                : new[] { catalog.Manifest.AssetLabel };
            Array.Sort(labels, StringComparer.Ordinal);
            var report = new ContentPackBuildReportDto
            {
                schemaVersion = 1,
                packId = catalog.Manifest.PackId.Value,
                version = catalog.Manifest.Version.ToString(),
                contentSchemaVersion = catalog.Manifest.SchemaVersion,
                minimumGameVersion = catalog.Manifest.MinimumGameVersion.ToString(),
                maximumGameVersion = catalog.Manifest.MaximumGameVersion.HasValue
                    ? catalog.Manifest.MaximumGameVersion.Value.ToString()
                    : string.Empty,
                official = catalog.Manifest.Official,
                dependencies = dependencies,
                catalogAddress = catalog.Manifest.CatalogAddress,
                assetLabels = labels,
                definitionCount = catalog.Definitions.Count,
                contentHash = catalog.ContentHash,
                catalogHash = catalogHash,
                catalogFile = "catalog.json"
            };
            var reportPath = Path.Combine(folder, "pack-build-report.json");
            WriteUtf8(reportPath, JsonUtility.ToJson(report, true) + "\n");
            return new ContentPackBuildResult(
                report.packId,
                report.version,
                report.contentHash,
                report.catalogHash,
                catalogPath,
                reportPath);
        }

        /// <summary>Computes a lowercase SHA-256 file hash.</summary>
        public static string ComputeFileHash(string path)
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

        private static void WriteUtf8(string path, string content)
        {
            File.WriteAllText(path, content, new UTF8Encoding(false));
        }

        private static string SafeSegment(string value)
        {
            var output = new StringBuilder(value == null ? 0 : value.Length);
            var source = value ?? string.Empty;
            for (var index = 0; index < source.Length; index++)
            {
                var character = source[index];
                output.Append(char.IsLetterOrDigit(character) || character == '.' || character == '_'
                    ? character
                    : '_');
            }

            return output.Length == 0 ? "pack" : output.ToString();
        }

        [Serializable]
        private sealed class ContentPackBuildReportDto
        {
            public int schemaVersion;
            public string packId;
            public string version;
            public int contentSchemaVersion;
            public string minimumGameVersion;
            public string maximumGameVersion;
            public bool official;
            public PackBuildDependencyDto[] dependencies;
            public string catalogAddress;
            public string[] assetLabels;
            public int definitionCount;
            public string contentHash;
            public string catalogHash;
            public string catalogFile;
        }

        [Serializable]
        private sealed class PackBuildDependencyDto
        {
            public string packId;
            public string minimumVersion;
            public string maximumVersion;
        }
    }

    /// <summary>Command-line entry point for deterministic builds of every content pack.</summary>
    public static class ContentPackBuildCommand
    {
        /// <summary>Builds all packs and exits nonzero on the first error.</summary>
        public static void Run()
        {
            var exitCode = 0;
            try
            {
                var output = Environment.GetEnvironmentVariable("CONTENT_PACK_OUTPUT");
                if (string.IsNullOrWhiteSpace(output)) output = "Builds/ContentPacks";
                var guids = AssetDatabase.FindAssets("t:ContentPackAuthoring");
                var paths = new string[guids.Length];
                for (var index = 0; index < guids.Length; index++)
                    paths[index] = AssetDatabase.GUIDToAssetPath(guids[index]);
                Array.Sort(paths, StringComparer.Ordinal);
                for (var index = 0; index < paths.Length; index++)
                {
                    var pack = AssetDatabase.LoadAssetAtPath<ContentPackAuthoring>(paths[index]);
                    var result = ContentPackBuilder.Build(pack, output);
                    Debug.Log("[M9 Pack Build] " + result.PackId + " " + result.Version +
                              " content=" + result.ContentHash + " catalog=" + result.CatalogHash);
                }

                Debug.Log("[M9 Pack Build] PASS: packs=" + paths.Length + ".");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                exitCode = 1;
            }

            EditorApplication.Exit(exitCode);
        }
    }
}
