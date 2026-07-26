using Game.Content.Authoring;
using Game.Core;
using Game.Simulation;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>Bridges authored skills to the exact headless M4 preview harness.</summary>
    public static class SkillPreviewEditorService
    {
        /// <summary>Bakes all packs, resolves the skill, and runs a detailed headless preview.</summary>
        public static Result<SkillPreviewReport> Run(
            SkillAuthoring skill,
            in SkillPreviewRequest request)
        {
            if (skill == null) throw new System.ArgumentNullException(nameof(skill));
            var registry = ContentEditorCatalog.BuildRegistry();
            if (!registry.IsSuccess) return Result<SkillPreviewReport>.Failure(registry.Error);
            var id = ContentId.Create(skill.ContentIdText);
            if (!id.IsSuccess) return Result<SkillPreviewReport>.Failure(id.Error);
            if (!registry.Value.TryGet(id.Value, out var entry))
            {
                return Result<SkillPreviewReport>.Failure(
                    new Error(
                        ErrorCode.MissingReference,
                        "Preview skill is not present in the baked registry.",
                        id.Value,
                        default,
                        AssetDatabase.GetAssetPath(skill)));
            }

            return SkillPreviewHarness.RunDetailed(registry.Value, entry.Index, request);
        }
    }

    /// <summary>Level-, attribute-, and target-aware UI for the headless skill preview.</summary>
    public sealed class SkillPreviewWindow : EditorWindow
    {
        private SkillAuthoring skill;
        private int level = 1;
        private int enemyCount = 16;
        private float durationSeconds = 5f;
        private float damageMultiplier = 1f;
        private float criticalChance;
        private string seedText = "1295271244";
        private SkillPreviewReport report;
        private Vector2 scroll;

        [MenuItem("Tools/Free World/M9/Skill Preview Harness")]
        private static void Open()
        {
            GetWindow<SkillPreviewWindow>("Skill Preview");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Skill Preview Harness", EditorStyles.boldLabel);
            skill = (SkillAuthoring)EditorGUILayout.ObjectField(
                "Skill",
                skill,
                typeof(SkillAuthoring),
                false);
            level = EditorGUILayout.IntSlider("Level", level, 1, 20);
            enemyCount = EditorGUILayout.IntSlider("Enemy Count", enemyCount, 1, 256);
            durationSeconds = EditorGUILayout.FloatField("Duration (seconds)", durationSeconds);
            damageMultiplier = EditorGUILayout.FloatField("Damage Attribute", damageMultiplier);
            criticalChance = EditorGUILayout.Slider("Critical Chance", criticalChance, 0f, 1f);
            seedText = EditorGUILayout.TextField("Seed", seedText);
            using (new EditorGUI.DisabledScope(
                       skill == null || durationSeconds <= 0f || damageMultiplier < 0f))
            {
                if (GUILayout.Button("Run Headless Preview")) Run();
            }

            if (report == null) return;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Targeting", report.Geometry.TargetingId.Value);
            EditorGUILayout.LabelField("Delivery", report.Geometry.DeliveryId.Value);
            EditorGUILayout.LabelField("Range", report.Geometry.Range.ToString("0.###"));
            EditorGUILayout.LabelField("Hitbox Radius", report.Geometry.HitboxRadius.ToString("0.###"));
            EditorGUILayout.LabelField("DPS", report.Summary.DamagePerSecond.ToString("0.###"));
            EditorGUILayout.LabelField("Hits", report.Summary.HitCount.ToString());
            EditorGUILayout.LabelField("Triggers", report.Summary.TriggerCount.ToString());
            EditorGUILayout.LabelField("Tick Allocations", report.ManagedAllocationBytes + " B");
            DrawGeometry(report.Geometry);
            EditorGUILayout.LabelField("Simulation Log", EditorStyles.boldLabel);
            scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.MinHeight(100f));
            for (var index = 0; index < report.LogLines.Count; index++)
                EditorGUILayout.SelectableLabel(report.LogLines[index], EditorStyles.textField);
            EditorGUILayout.EndScrollView();
        }

        private void Run()
        {
            if (!ulong.TryParse(seedText, out var seed))
            {
                EditorUtility.DisplayDialog("Preview Failed", "Seed must be an unsigned integer.", "OK");
                return;
            }

            var result = SkillPreviewEditorService.Run(
                skill,
                new SkillPreviewRequest(
                    seed,
                    durationSeconds,
                    enemyCount,
                    level,
                    damageMultiplier,
                    criticalChance));
            if (!result.IsSuccess)
            {
                report = null;
                EditorUtility.DisplayDialog("Preview Failed", result.Error.ToString(), "OK");
                return;
            }

            report = result.Value;
        }

        private static void DrawGeometry(SkillPreviewGeometry geometry)
        {
            var rect = GUILayoutUtility.GetRect(180f, 180f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, new Color(0.08f, 0.08f, 0.1f, 1f));
            var center = rect.center;
            var maximum = Mathf.Max(1f, geometry.Range, geometry.HitboxRadius);
            var scale = 75f / maximum;
            Handles.BeginGUI();
            Handles.color = Color.cyan;
            Handles.DrawWireDisc(center, Vector3.forward, geometry.Range * scale);
            Handles.color = Color.yellow;
            Handles.DrawWireDisc(center, Vector3.forward, geometry.HitboxRadius * scale);
            Handles.color = Color.white;
            Handles.DrawSolidDisc(center, Vector3.forward, 3f);
            Handles.EndGUI();
        }
    }
}
