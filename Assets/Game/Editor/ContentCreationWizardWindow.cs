using Game.Content.Authoring;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>Guided non-programmer entry point for all M9 content types.</summary>
    public sealed class ContentCreationWizardWindow : EditorWindow
    {
        private ContentCreationKind kind = ContentCreationKind.Pack;
        private string namespaceId = "test";
        private string displayName = "new content";
        private string rootFolder = "Assets/GameAssets/Placeholder/GeneratedContent";
        private ContentPackAuthoring pack;
        private SkillAuthoring skillReference;
        private SkillAuthoring secondarySkillReference;
        private PassiveAuthoring passiveReference;
        private EnemyAuthoring enemyReference;
        private EncounterScheduleAuthoring encounterReference;
        private Vector2 scroll;

        [MenuItem("Tools/Free World/M9/Content Creation Wizard")]
        private static void Open()
        {
            GetWindow<ContentCreationWizardWindow>("Content Wizard");
        }

        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField("M9 Content Creation Wizard", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Creates schema-safe Placeholder authoring assets, localization keys, Addressables labels, " +
                "a test template, and a source-record placeholder. Create dependencies first.",
                MessageType.Info);
            kind = (ContentCreationKind)EditorGUILayout.EnumPopup("Content Type", kind);
            namespaceId = EditorGUILayout.TextField("ID Namespace", namespaceId);
            displayName = EditorGUILayout.TextField("Technical Name", displayName);
            if (kind == ContentCreationKind.Pack)
                rootFolder = EditorGUILayout.TextField("Output Root", rootFolder);
            else
                pack = (ContentPackAuthoring)EditorGUILayout.ObjectField(
                    "Target Pack",
                    pack,
                    typeof(ContentPackAuthoring),
                    false);

            DrawReferences();
            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(displayName)))
            {
                if (GUILayout.Button("Create and Validate")) Create();
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawReferences()
        {
            if (kind == ContentCreationKind.Character || kind == ContentCreationKind.Enemy ||
                kind == ContentCreationKind.Evolution)
            {
                skillReference = (SkillAuthoring)EditorGUILayout.ObjectField(
                    kind == ContentCreationKind.Evolution ? "Source Skill" : "Skill Reference",
                    skillReference,
                    typeof(SkillAuthoring),
                    false);
            }

            if (kind == ContentCreationKind.Evolution)
            {
                secondarySkillReference = (SkillAuthoring)EditorGUILayout.ObjectField(
                    "Result Skill",
                    secondarySkillReference,
                    typeof(SkillAuthoring),
                    false);
                passiveReference = (PassiveAuthoring)EditorGUILayout.ObjectField(
                    "Required Passive (Optional)",
                    passiveReference,
                    typeof(PassiveAuthoring),
                    false);
            }

            if (kind == ContentCreationKind.Encounter)
            {
                enemyReference = (EnemyAuthoring)EditorGUILayout.ObjectField(
                    "Enemy Reference",
                    enemyReference,
                    typeof(EnemyAuthoring),
                    false);
            }

            if (kind == ContentCreationKind.Map)
            {
                encounterReference = (EncounterScheduleAuthoring)EditorGUILayout.ObjectField(
                    "Encounter Reference",
                    encounterReference,
                    typeof(EncounterScheduleAuthoring),
                    false);
            }
        }

        private void Create()
        {
            try
            {
                var result = ContentCreationService.Create(new ContentCreationRequest
                {
                    Kind = kind,
                    NamespaceId = namespaceId,
                    DisplayName = displayName,
                    RootFolder = rootFolder,
                    Pack = pack,
                    SkillReference = skillReference,
                    SecondarySkillReference = secondarySkillReference,
                    PassiveReference = passiveReference,
                    EnemyReference = enemyReference,
                    EncounterReference = encounterReference
                });
                EditorUtility.DisplayDialog(
                    "Content Created",
                    result.GeneratedId + "\n" + result.AssetPath +
                    "\nTest: " + result.TestTemplatePath,
                    "OK");
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("Content Creation Failed", exception.Message, "OK");
            }
        }
    }
}
