using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>Reusable UI over the same validation pipeline used by CLI and builds.</summary>
    public sealed class ContentValidationWindow : EditorWindow
    {
        private ValidationReport report;
        private Vector2 scroll;

        [MenuItem("Tools/Free World/M9/Validator Window")]
        private static void Open()
        {
            GetWindow<ContentValidationWindow>("Content Validator");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Project Content Validation", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Uses the exact ProjectGovernanceValidator pipeline invoked by command line and build preprocessing.",
                MessageType.Info);
            if (GUILayout.Button("Run Validation")) report = ProjectGovernanceValidator.ValidateCurrentProject();
            if (report == null) return;
            EditorGUILayout.HelpBox(
                report.IsValid ? "PASS" : "FAIL: " + report.Issues.Count + " issue(s)",
                report.IsValid ? MessageType.Info : MessageType.Error);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            for (var index = 0; index < report.Issues.Count; index++)
                EditorGUILayout.SelectableLabel(
                    report.Issues[index].ToString(),
                    EditorStyles.textArea,
                    GUILayout.MinHeight(38f));
            EditorGUILayout.EndScrollView();
        }
    }
}
