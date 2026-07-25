using System;
using System.IO;
using System.Text;
using Game.Content.Authoring;
using Game.Content.Runtime;
using Game.Core;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Editor entry points for deterministic content pack baking.
    /// </summary>
    public static class ContentBakeUtility
    {
        /// <summary>
        /// Bakes every authored pack to a sibling *.baked.json asset.
        /// </summary>
        [MenuItem("Tools/Free World/M1/Bake All Content Packs")]
        public static void BakeAll()
        {
            var guids = AssetDatabase.FindAssets("t:ContentPackAuthoring");
            var paths = new string[guids.Length];
            for (var index = 0; index < guids.Length; index++)
            {
                paths[index] = AssetDatabase.GUIDToAssetPath(guids[index]);
            }

            Array.Sort(paths, StringComparer.Ordinal);
            for (var index = 0; index < paths.Length; index++)
            {
                var pack = AssetDatabase.LoadAssetAtPath<ContentPackAuthoring>(paths[index]);
                var result = Bake(pack);
                if (!result.IsSuccess)
                {
                    throw new UnityException(result.Error.ToString());
                }

                WriteCatalog(paths[index], result.Value);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[M1 Bake] Baked content packs: " + paths.Length + ".");
        }

        /// <summary>
        /// Bakes one authoring pack with AssetDatabase-backed source paths.
        /// </summary>
        public static Result<BakedContentCatalog> Bake(ContentPackAuthoring pack)
        {
            return ContentBaker.Bake(pack, AssetDatabaseAuthoringPathResolver.Instance);
        }

        /// <summary>
        /// Gets the conventional baked JSON asset path for an authoring pack.
        /// </summary>
        public static string GetBakedCatalogPath(string authoringPackPath)
        {
            var directory = Path.GetDirectoryName(authoringPackPath) ?? "Assets";
            var fileName = Path.GetFileNameWithoutExtension(authoringPackPath);
            return (directory + "/" + fileName + ".baked.json").Replace('\\', '/');
        }

        /// <summary>
        /// Writes a deterministic catalog DTO beside its authoring pack.
        /// </summary>
        public static string WriteCatalog(
            string authoringPackPath,
            BakedContentCatalog catalog)
        {
            var bakedPath = GetBakedCatalogPath(authoringPackPath);
            var json = JsonUtility.ToJson(catalog.ToDto(), true);
            File.WriteAllText(
                Path.GetFullPath(bakedPath),
                json + Environment.NewLine,
                new UTF8Encoding(false));
            AssetDatabase.ImportAsset(bakedPath, ImportAssetOptions.ForceUpdate);
            return bakedPath;
        }

        private sealed class AssetDatabaseAuthoringPathResolver : IAuthoringPathResolver
        {
            public static readonly AssetDatabaseAuthoringPathResolver Instance =
                new AssetDatabaseAuthoringPathResolver();

            public string GetPath(UnityEngine.Object authoringAsset)
            {
                return authoringAsset == null
                    ? string.Empty
                    : AssetDatabase.GetAssetPath(authoringAsset);
            }
        }
    }
}
