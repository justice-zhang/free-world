using Game.Content.Authoring;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>Non-programmer UI for deterministic pack catalog/report generation.</summary>
    public sealed class ContentPackBuilderWindow : EditorWindow
    {
        private ContentPackAuthoring pack;
        private string outputRoot = "Builds/ContentPacks";
        private ContentPackBuildResult result;

        [MenuItem("Tools/Free World/M9/Content Pack Builder")]
        private static void Open()
        {
            GetWindow<ContentPackBuilderWindow>("Pack Builder");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Content Pack Builder", EditorStyles.boldLabel);
            pack = (ContentPackAuthoring)EditorGUILayout.ObjectField(
                "Pack",
                pack,
                typeof(ContentPackAuthoring),
                false);
            outputRoot = EditorGUILayout.TextField("Output Root", outputRoot);
            using (new EditorGUI.DisabledScope(pack == null || string.IsNullOrWhiteSpace(outputRoot)))
            {
                if (GUILayout.Button("Build Catalog and Report")) Build();
            }

            if (result == null) return;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Pack", result.PackId + " @ " + result.Version);
            EditorGUILayout.LabelField("Content Hash", result.ContentHash);
            EditorGUILayout.LabelField("Catalog Hash", result.CatalogHash);
            EditorGUILayout.SelectableLabel(result.ReportPath, EditorStyles.textField);
        }

        private void Build()
        {
            try
            {
                result = ContentPackBuilder.Build(pack, outputRoot);
                Debug.Log("[M9 Pack Build] PASS: " + result.ReportPath);
            }
            catch (System.Exception exception)
            {
                result = null;
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("Pack Build Failed", exception.Message, "OK");
            }
        }
    }
}
