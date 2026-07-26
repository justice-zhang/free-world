using System;
using Game.Content.Authoring;
using Game.Content.Runtime;
using Game.Core;
using UnityEditor;

namespace Game.Editor
{
    /// <summary>Builds validated, in-memory catalogs for editor previews and tools.</summary>
    public static class ContentEditorCatalog
    {
        private static readonly ContentVersion GameVersion = new ContentVersion(0, 1, 0);

        /// <summary>Bakes every authored pack in stable asset-path order.</summary>
        public static Result<BakedContentCatalog[]> BakeAll()
        {
            var guids = AssetDatabase.FindAssets("t:ContentPackAuthoring");
            var paths = new string[guids.Length];
            for (var index = 0; index < guids.Length; index++)
            {
                paths[index] = AssetDatabase.GUIDToAssetPath(guids[index]);
            }

            Array.Sort(paths, StringComparer.Ordinal);
            var catalogs = new BakedContentCatalog[paths.Length];
            for (var index = 0; index < paths.Length; index++)
            {
                var pack = AssetDatabase.LoadAssetAtPath<ContentPackAuthoring>(paths[index]);
                var baked = ContentBakeUtility.Bake(pack);
                if (!baked.IsSuccess)
                {
                    return Result<BakedContentCatalog[]>.Failure(baked.Error);
                }

                catalogs[index] = baked.Value;
            }

            return Result<BakedContentCatalog[]>.Success(catalogs);
        }

        /// <summary>Builds the same stable-ID registry consumed by headless runtime tools.</summary>
        public static Result<ContentRegistry> BuildRegistry()
        {
            var catalogs = BakeAll();
            if (!catalogs.IsSuccess)
            {
                return Result<ContentRegistry>.Failure(catalogs.Error);
            }

            var registry = new ContentRegistry();
            var loaded = registry.Load(catalogs.Value, GameVersion);
            return loaded.IsSuccess
                ? Result<ContentRegistry>.Success(registry)
                : Result<ContentRegistry>.Failure(loaded.Error);
        }

        /// <summary>Finds the pack that owns an authored definition.</summary>
        public static ContentPackAuthoring FindOwningPack(ContentAuthoringBase definition)
        {
            if (definition == null)
            {
                return null;
            }

            var guids = AssetDatabase.FindAssets("t:ContentPackAuthoring");
            for (var guidIndex = 0; guidIndex < guids.Length; guidIndex++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[guidIndex]);
                var pack = AssetDatabase.LoadAssetAtPath<ContentPackAuthoring>(path);
                if (pack == null)
                {
                    continue;
                }

                for (var definitionIndex = 0;
                     definitionIndex < pack.Definitions.Count;
                     definitionIndex++)
                {
                    if (pack.Definitions[definitionIndex] == definition)
                    {
                        return pack;
                    }
                }
            }

            return null;
        }
    }
}
