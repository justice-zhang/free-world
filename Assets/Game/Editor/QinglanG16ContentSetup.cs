using System;
using System.Collections.Generic;
using Game.Content.Authoring;
using Game.Content.Runtime;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>Creates the checked-in G1.6 twelve-minute Qinglan encounter.</summary>
    public static class QinglanG16ContentSetup
    {
        public const string EncounterPath =
            QinglanG12ContentSetup.Folder + "/OldCourtTwelveMinuteEncounter.asset";

        [MenuItem("Tools/Free World/Qinglan/G1.6 Configure Twelve Minute Encounter")]
        public static void Configure()
        {
            var pack = Require<ContentPackAuthoring>(QinglanG12ContentSetup.PackPath);
            var grass = Require<EnemyAuthoring>(QinglanG12ContentSetup.Folder + "/GrassSpirit.asset");
            var crane = Require<EnemyAuthoring>(QinglanG12ContentSetup.Folder + "/PaperCraneSpirit.asset");
            var puppet = Require<EnemyAuthoring>(QinglanG12ContentSetup.Folder + "/WoodenSwordPuppet.asset");
            var lantern = Require<EnemyAuthoring>(QinglanG12ContentSetup.Folder + "/StoneLanternGuard.asset");
            var bell = Require<EnemyAuthoring>(QinglanG12ContentSetup.Folder + "/WindBellSpirit.asset");
            var pod = Require<EnemyAuthoring>(QinglanG12ContentSetup.Folder + "/ExplosiveSeedPod.asset");
            var affixes = new[]
            {
                Require<QinglanDefinitionAuthoring>(QinglanG12ContentSetup.Folder + "/EliteAffixRampaging.asset"),
                Require<QinglanDefinitionAuthoring>(QinglanG12ContentSetup.Folder + "/EliteAffixBarrier.asset"),
                Require<QinglanDefinitionAuthoring>(QinglanG12ContentSetup.Folder + "/EliteAffixSplitting.asset"),
                Require<QinglanDefinitionAuthoring>(QinglanG12ContentSetup.Folder + "/EliteAffixQuaking.asset")
            };

            var encounter = LoadOrCreate<EncounterScheduleAuthoring>(EncounterPath);
            encounter.ConfigureIdentity(
                "qinglan.encounter.old_court.demo_12m",
                "content.qinglan.encounter.old_court.demo_12m.name",
                "content.qinglan.encounter.old_court.demo_12m.description",
                new[] { "content.placeholder", "encounter.old_court", "encounter.demo_12m" });
            encounter.Configure(
                720,
                14f,
                24f,
                new[]
                {
                    Phase(0f, 90f, 2f, 3f, 1.10f, 0.90f, 120, SpawnPattern.Ring,
                        Entries(affixes, Entry(grass, 1f, 1f, 2, 5))),
                    Phase(90f, 180f, 3f, 4.5f, 0.95f, 0.75f, 180, SpawnPattern.Edge,
                        Entries(affixes,
                            Entry(grass, 3f, 1f, 2, 5),
                            Entry(crane, 1f, 2f, 1, 2))),
                    Phase(180f, 270f, 4.5f, 6f, 0.80f, 0.65f, 240, SpawnPattern.Ambush,
                        Entries(affixes,
                            Entry(grass, 3f, 1f, 2, 5),
                            Entry(crane, 1.5f, 2f, 1, 2),
                            Entry(puppet, 1f, 4f, 1, 1)),
                        Elite(puppet, 180f, SpawnPattern.Edge, affixes)),
                    Phase(270f, 360f, 6f, 7.5f, 0.70f, 0.55f, 320, SpawnPattern.Line,
                        Entries(affixes,
                            Entry(grass, 3f, 1f, 2, 5),
                            Entry(crane, 1.5f, 2f, 1, 2),
                            Entry(puppet, 1f, 4f, 1, 1),
                            Entry(lantern, 1f, 3f, 1, 2))),
                    Phase(360f, 390f, 1f, 1f, 1.25f, 1.25f, 80, SpawnPattern.Ring,
                        Entries(affixes,
                            Entry(grass, 2f, 1f, 1, 3),
                            Entry(puppet, 1f, 4f, 1, 1))),
                    Phase(390f, 450f, 7f, 9f, 0.60f, 0.50f, 360, SpawnPattern.Cluster,
                        Entries(affixes,
                            Entry(grass, 3f, 1f, 2, 5),
                            Entry(crane, 1.5f, 2f, 1, 2),
                            Entry(puppet, 1f, 4f, 1, 1),
                            Entry(lantern, 1f, 3f, 1, 2),
                            Entry(bell, 0.75f, 3.5f, 1, 1))),
                    Phase(450f, 540f, 9f, 11f, 0.52f, 0.43f, 440, SpawnPattern.Edge,
                        Entries(affixes,
                            Entry(grass, 3f, 1f, 2, 5),
                            Entry(crane, 1.5f, 2f, 1, 2),
                            Entry(puppet, 1f, 4f, 1, 1),
                            Entry(lantern, 1f, 3f, 1, 2),
                            Entry(bell, 0.75f, 3.5f, 1, 1),
                            Entry(pod, 0.75f, 4f, 1, 1)),
                        Elite(lantern, 450f, SpawnPattern.Ring, affixes)),
                    Phase(540f, 630f, 11f, 14f, 0.46f, 0.38f, 520, SpawnPattern.Cluster,
                        Entries(affixes,
                            Entry(grass, 2.5f, 1f, 2, 5),
                            Entry(crane, 1.5f, 2f, 1, 2),
                            Entry(puppet, 1.25f, 4f, 1, 1),
                            Entry(lantern, 1.25f, 3f, 1, 2),
                            Entry(bell, 0.85f, 3.5f, 1, 1),
                            Entry(pod, 0.85f, 4f, 1, 1))),
                    Phase(630f, 720f, 14f, 18f, 0.40f, 0.32f, 600, SpawnPattern.OffscreenRandom,
                        Entries(affixes,
                            Entry(grass, 2f, 1f, 2, 5),
                            Entry(crane, 1.75f, 2f, 1, 2),
                            Entry(puppet, 1.5f, 4f, 1, 1),
                            Entry(lantern, 1.5f, 3f, 1, 2),
                            Entry(bell, 1f, 3.5f, 1, 1),
                            Entry(pod, 1f, 4f, 1, 1)))
                });

            var definitions = new List<ContentAuthoringBase>(pack.Definitions.Count + 1);
            for (var index = 0; index < pack.Definitions.Count; index++)
                definitions.Add(pack.Definitions[index]);
            if (!definitions.Contains(encounter)) definitions.Add(encounter);
            pack.Configure(
                "qinglan.pack.demo",
                "0.5.0",
                ContentPackTopology.QinglanDemoSchemaVersion,
                "0.1.0",
                string.Empty,
                Array.Empty<ContentPackDependencyAuthoring>(),
                "packs/qinglan.demo/catalog",
                "pack.qinglan.demo",
                false,
                definitions.ToArray());

            M9LocalizationUtility.EnsureContentEntries(
                "content.qinglan.encounter.old_court.demo_12m.name",
                "content.qinglan.encounter.old_court.demo_12m.description",
                "Old Court Twelve-Minute Encounter",
                "Nine continuous pressure phases with two deterministic elite milestones.",
                "青岚旧庭十二分钟遭遇",
                "九段连续压力曲线，包含两个固定时点的一次性精英里程碑。");
            EditorUtility.SetDirty(encounter);
            EditorUtility.SetDirty(pack);
            AssetDatabase.SaveAssets();
            var bake = ContentBakeUtility.Bake(pack);
            if (!bake.IsSuccess) throw new UnityException(bake.Error.ToString());
            ContentBakeUtility.WriteCatalog(QinglanG12ContentSetup.PackPath, bake.Value);
            AssetDatabase.SaveAssets();
            Debug.Log("[Qinglan G1.6] Encounter pack baked: entries=" +
                      bake.Value.Definitions.Count + ", hash=" + bake.Value.ContentHash + ".");
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

        private static EncounterPhaseAuthoringData Phase(
            float start,
            float end,
            float budgetStart,
            float budgetEnd,
            float intervalStart,
            float intervalEnd,
            int cap,
            SpawnPattern pattern,
            EncounterEnemyEntryAuthoringData[] enemies,
            params EncounterEliteRuleAuthoringData[] elites)
        {
            return new EncounterPhaseAuthoringData
            {
                startTimeSeconds = start,
                endTimeSeconds = end,
                budgetPerSecondStart = budgetStart,
                budgetPerSecondEnd = budgetEnd,
                spawnIntervalStart = intervalStart,
                spawnIntervalEnd = intervalEnd,
                maximumConcurrentEnemies = cap,
                spawnPattern = pattern,
                enemies = enemies,
                elites = elites ?? Array.Empty<EncounterEliteRuleAuthoringData>(),
                bosses = Array.Empty<EncounterBossRuleAuthoringData>()
            };
        }

        private static EncounterEnemyEntryAuthoringData[] Entries(
            QinglanDefinitionAuthoring[] affixes,
            params EncounterEnemyEntryAuthoringData[] entries)
        {
            for (var index = 0; index < entries.Length; index++)
                entries[index].affixPool = affixes;
            return entries;
        }

        private static EncounterEnemyEntryAuthoringData Entry(
            EnemyAuthoring enemy,
            float weight,
            float cost,
            int minimumGroup,
            int maximumGroup)
        {
            return new EncounterEnemyEntryAuthoringData
            {
                enemy = enemy,
                weight = weight,
                budgetCost = cost,
                minimumGroupSize = minimumGroup,
                maximumGroupSize = maximumGroup,
                elite = false
            };
        }

        private static EncounterEliteRuleAuthoringData Elite(
            EnemyAuthoring enemy,
            float time,
            SpawnPattern pattern,
            QinglanDefinitionAuthoring[] affixes)
        {
            return new EncounterEliteRuleAuthoringData
            {
                enemy = enemy,
                spawnTimeSeconds = time,
                pattern = pattern,
                affixPool = affixes
            };
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            if (AssetDatabase.LoadMainAssetAtPath(path) != null) AssetDatabase.DeleteAsset(path);
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static T Require<T>(string path) where T : UnityEngine.Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) throw new UnityException("Required Qinglan content is missing: " + path);
            return asset;
        }
    }
}
