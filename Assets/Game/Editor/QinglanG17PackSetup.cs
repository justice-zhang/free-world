using System;
using Game.Content.Authoring;
using Game.Content.Runtime;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>Applies the complete G1 Placeholder pack labels and rebuilds its catalog.</summary>
    public static class QinglanG17PackSetup
    {
        [MenuItem("Tools/Free World/Qinglan/G1.7 Configure Complete Placeholder Pack")]
        public static void Configure()
        {
            var pack = AssetDatabase.LoadAssetAtPath<ContentPackAuthoring>(QinglanG12ContentSetup.PackPath);
            if (pack == null) throw new UnityException("Qinglan content pack is missing.");
            var first = ContentBakeUtility.Bake(pack);
            if (!first.IsSuccess) throw new UnityException(first.Error.ToString());
            ContentBakeUtility.WriteCatalog(QinglanG12ContentSetup.PackPath, first.Value);
            AssetDatabase.ImportAsset(
                QinglanG12ContentSetup.BakedCatalogPath,
                ImportAssetOptions.ForceSynchronousImport);

            for (var index = 0; index < pack.Definitions.Count; index++)
            {
                var definition = pack.Definitions[index];
                if (definition == null) throw new UnityException("Qinglan pack contains a null definition.");
                var path = AssetDatabase.GetAssetPath(definition);
                if (!path.StartsWith(QinglanG12ContentSetup.Folder + "/", StringComparison.Ordinal))
                    throw new UnityException("Qinglan G1 content is outside the Placeholder folder: " + path);
                M9AddressableUtility.Configure(
                    path,
                    "qinglan/demo/content/" + definition.ContentIdText.Replace('.', '/'),
                    pack.AssetLabel);
            }
            M9AddressableUtility.Configure(
                QinglanG12ContentSetup.BakedCatalogPath,
                pack.CatalogAddress,
                pack.AssetLabel);

            AssetDatabase.SaveAssets();
            var second = ContentBakeUtility.Bake(pack);
            if (!second.IsSuccess) throw new UnityException(second.Error.ToString());
            if (!string.Equals(first.Value.ContentHash, second.Value.ContentHash, StringComparison.Ordinal) ||
                first.Value.Definitions.Count != second.Value.Definitions.Count)
                throw new UnityException("Qinglan pack changed while applying delivery labels.");
            Debug.Log("[Qinglan G1.7 Pack Setup] PASS: entries=" + second.Value.Definitions.Count +
                      ", hash=" + second.Value.ContentHash + ".");
        }

        public static void RunFromCommandLine()
        {
            try
            {
                Configure();
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }
    }
}
