using Game.Content.Authoring;
using Game.Content.Runtime;
using Game.Core;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>Editor UI for Scene-free encounter curve and output previews.</summary>
    public sealed class WaveTimelineWindow : EditorWindow
    {
        private EncounterScheduleAuthoring encounter;
        private float spawnRateMultiplier = 1f;
        private float sampleTime;
        private WaveTimelineReport report;
        private RuntimeEncounterSchedule runtimeSchedule;
        private Vector2 scroll;

        [MenuItem("Tools/Free World/M9/Wave Timeline Editor")]
        private static void Open()
        {
            GetWindow<WaveTimelineWindow>("Wave Timeline");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Wave Timeline Editor", EditorStyles.boldLabel);
            encounter = (EncounterScheduleAuthoring)EditorGUILayout.ObjectField(
                "Encounter",
                encounter,
                typeof(EncounterScheduleAuthoring),
                false);
            spawnRateMultiplier = EditorGUILayout.FloatField("Spawn Rate Multiplier", spawnRateMultiplier);
            using (new EditorGUI.DisabledScope(encounter == null || spawnRateMultiplier <= 0f))
            {
                if (GUILayout.Button("Analyze")) Analyze();
            }

            if (report == null || runtimeSchedule == null) return;
            var endTime = runtimeSchedule.Phases[runtimeSchedule.Phases.Count - 1].EndTimeSeconds;
            sampleTime = EditorGUILayout.Slider("Runtime Sample Time", sampleTime, 0f, endTime - 0.001f);
            var sample = WaveTimelineAnalyzer.Sample(runtimeSchedule, sampleTime, spawnRateMultiplier);
            if (sample.IsSuccess)
            {
                EditorGUILayout.LabelField(
                    "Runtime Curves",
                    "budget/s " + sample.Value.BudgetPerSecond.ToString("0.###") +
                    ", interval " + sample.Value.SpawnIntervalSeconds.ToString("0.###") + "s");
            }

            EditorGUILayout.LabelField("Total Theoretical Health", report.TotalHealth.ToString("0.##"));
            EditorGUILayout.LabelField("Total Theoretical XP", report.ExperienceOutput.ToString("0.##"));
            scroll = EditorGUILayout.BeginScrollView(scroll);
            for (var index = 0; index < report.Phases.Count; index++)
            {
                var phaseReport = report.Phases[index];
                var phase = runtimeSchedule.Phases[index];
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Phase " + index, EditorStyles.boldLabel);
                EditorGUILayout.LabelField(
                    "Time",
                    phaseReport.StartTimeSeconds.ToString("0.##") + "–" +
                    phaseReport.EndTimeSeconds.ToString("0.##") + " s");
                EditorGUILayout.LabelField(
                    "Budget",
                    phaseReport.BudgetPerSecondStart.ToString("0.##") + "→" +
                    phaseReport.BudgetPerSecondEnd.ToString("0.##") +
                    " /s, integrated " + phaseReport.IntegratedBudget.ToString("0.##"));
                EditorGUILayout.LabelField(
                    "Interval",
                    phaseReport.SpawnIntervalStart.ToString("0.##") + "→" +
                    phaseReport.SpawnIntervalEnd.ToString("0.##") + " s");
                EditorGUILayout.LabelField("Expected Enemies", phaseReport.ExpectedEnemyCount.ToString("0.##"));
                EditorGUILayout.LabelField("Theoretical Concurrency", phaseReport.TheoreticalConcurrency.ToString());
                EditorGUILayout.LabelField(
                    "Health / XP",
                    phaseReport.TotalHealth.ToString("0.##") + " / " +
                    phaseReport.ExperienceOutput.ToString("0.##"));
                for (var entryIndex = 0; entryIndex < phase.EnemyEntries.Count; entryIndex++)
                {
                    var entry = phase.EnemyEntries[entryIndex];
                    EditorGUILayout.LabelField(
                        "Enemy Weight",
                        entry.EnemyId + " = " + entry.Weight.ToString("0.###"));
                }

                for (var bossIndex = 0; bossIndex < phaseReport.BossTimes.Count; bossIndex++)
                    EditorGUILayout.LabelField(
                        "Boss " + bossIndex,
                        phaseReport.BossTimes[bossIndex].ToString("0.##") + " s");
            }
            EditorGUILayout.EndScrollView();
        }

        private void Analyze()
        {
            report = null;
            runtimeSchedule = null;
            var registry = ContentEditorCatalog.BuildRegistry();
            if (!registry.IsSuccess)
            {
                EditorUtility.DisplayDialog("Timeline Failed", registry.Error.ToString(), "OK");
                return;
            }

            var id = ContentId.Create(encounter.ContentIdText);
            if (!id.IsSuccess ||
                !registry.Value.TryGet(id.Value, out RuntimeEncounterSchedule schedule))
            {
                EditorUtility.DisplayDialog("Timeline Failed", "Encounter is not in a valid baked registry.", "OK");
                return;
            }

            var analysis = WaveTimelineAnalyzer.Analyze(schedule, registry.Value, spawnRateMultiplier);
            if (!analysis.IsSuccess)
            {
                EditorUtility.DisplayDialog("Timeline Failed", analysis.Error.ToString(), "OK");
                return;
            }

            runtimeSchedule = schedule;
            report = analysis.Value;
            sampleTime = schedule.Phases[0].StartTimeSeconds;
        }
    }
}
