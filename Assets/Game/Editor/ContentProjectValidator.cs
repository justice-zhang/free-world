using System;
using System.Collections.Generic;
using System.IO;
using Game.Content.Authoring;
using Game.Content.Runtime;
using Game.Core;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Performs build-time authoring, baked-file, and cross-pack content validation.
    /// </summary>
    internal static class ContentProjectValidator
    {
        private static readonly ContentVersion GameVersion = new ContentVersion(0, 1, 0);

        public static void AppendCurrentProject(ValidationReport report)
        {
            var guids = AssetDatabase.FindAssets("t:ContentPackAuthoring");
            if (guids.Length == 0)
            {
                report.Add("M1-CONTENT-NONE", "No ContentPackAuthoring assets were found.");
                return;
            }

            var paths = new string[guids.Length];
            for (var index = 0; index < guids.Length; index++)
            {
                paths[index] = AssetDatabase.GUIDToAssetPath(guids[index]);
            }

            Array.Sort(paths, StringComparer.Ordinal);
            var catalogs = new List<BakedContentCatalog>(paths.Length);
            for (var index = 0; index < paths.Length; index++)
            {
                var pack = AssetDatabase.LoadAssetAtPath<ContentPackAuthoring>(paths[index]);
                var bakeResult = ContentBakeUtility.Bake(pack);
                if (!bakeResult.IsSuccess)
                {
                    AddError(report, bakeResult.Error);
                    continue;
                }

                var bakedPath = ContentBakeUtility.GetBakedCatalogPath(paths[index]);
                if (!File.Exists(Path.GetFullPath(bakedPath)))
                {
                    report.Add(
                        "M1-BAKED-MISSING",
                        "Baked catalog is missing for '" + paths[index] +
                        "': " + bakedPath + ".");
                    continue;
                }

                BakedContentCatalogDto dto;
                try
                {
                    dto = JsonUtility.FromJson<BakedContentCatalogDto>(
                        File.ReadAllText(Path.GetFullPath(bakedPath)));
                }
                catch (Exception exception)
                {
                    report.Add(
                        "M1-BAKED-JSON",
                        bakedPath + " cannot be parsed: " + exception.Message);
                    continue;
                }

                if (dto == null)
                {
                    report.Add("M1-BAKED-JSON", bakedPath + " contains no catalog.");
                    continue;
                }

                var storedResult = dto.ToCatalog();
                if (!storedResult.IsSuccess)
                {
                    AddError(report, storedResult.Error);
                    continue;
                }

                if (!string.Equals(
                        storedResult.Value.ContentHash,
                        bakeResult.Value.ContentHash,
                        StringComparison.Ordinal))
                {
                    report.Add(
                        "M1-BAKED-STALE",
                        bakedPath + " has hash " + storedResult.Value.ContentHash +
                        " but authoring now bakes to " + bakeResult.Value.ContentHash + ".");
                    continue;
                }

                catalogs.Add(bakeResult.Value);
            }

            if (catalogs.Count != paths.Length)
            {
                return;
            }

            var validation = ContentValidator.ValidateCatalogs(catalogs, GameVersion);
            for (var index = 0; index < validation.Errors.Count; index++)
            {
                AddError(report, validation.Errors[index]);
            }
        }

        private static void AddError(ValidationReport report, Error error)
        {
            report.Add("M1-" + error.Code.ToString().ToUpperInvariant(), error.ToString());
        }
    }
}
