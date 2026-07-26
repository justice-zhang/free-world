using System;
using Game.Content.Authoring;
using Game.Content.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Editor
{
    /// <summary>Creates the schema-4 programmatic Placeholder maps, enemies, boss, and encounter.</summary>
    public static class M5TestContentSetup
    {
        public const string Folder = "Assets/GameAssets/Placeholder/TestM5Content";
        public const string PackPath = Folder + "/TestM5ContentPack.asset";
        public const string BakedCatalogPath = Folder + "/TestM5ContentPack.baked.json";
        public const string FiniteScenePath = Folder + "/M5FiniteArena.unity";
        public const string InfiniteScenePath = Folder + "/M5ChunkedInfinite.unity";

        [MenuItem("Tools/Free World/M5/Configure Test Enemy Map Content")]
        public static void Configure()
        {
            EnsureFolder("Assets/GameAssets", "Placeholder");
            EnsureFolder("Assets/GameAssets/Placeholder", "TestM5Content");
            CreatePlaceholderScene(FiniteScenePath, "M5FiniteArenaPlaceholder");
            CreatePlaceholderScene(InfiniteScenePath, "M5ChunkedInfinitePlaceholder");
            var attackSkill = AssetDatabase.LoadAssetAtPath<SkillAuthoring>(
                M4TestSkillSetup.Folder + "/TestSingleProjectile.asset");
            if (attackSkill == null)
            {
                M4TestSkillSetup.Configure();
                attackSkill = AssetDatabase.LoadAssetAtPath<SkillAuthoring>(
                    M4TestSkillSetup.Folder + "/TestSingleProjectile.asset");
            }

            if (attackSkill == null) throw new UnityException("M4 projectile fixture is missing.");

            var chase = Enemy("TestChaser", "test.enemy.chaser", EnemyMovementMode.Chase, attackSkill,
                30f, 2.8f, 1.2f, 1.2f);
            var keeper = Enemy("TestKeeper", "test.enemy.keep_distance", EnemyMovementMode.KeepDistance,
                attackSkill, 38f, 2.4f, 1.5f, 5f);
            var charger = Enemy("TestCharger", "test.enemy.charger", EnemyMovementMode.Charge, attackSkill,
                55f, 2.2f, 2f, 4f);
            var ranged = Enemy("TestRanged", "test.enemy.ranged", EnemyMovementMode.Ranged, attackSkill,
                28f, 2f, 1f, 7f);
            var boss = Enemy("TestBoss", "test.enemy.boss", EnemyMovementMode.Charge, attackSkill,
                450f, 1.8f, 3f, 6f, true);

            var encounter = LoadOrCreate<EncounterScheduleAuthoring>(Folder + "/TestFiveMinuteEncounter.asset");
            Identity(encounter, "test.encounter.five_minute", new[] { "content.placeholder", "encounter.test" });
            encounter.Configure(
                48,
                8f,
                12f,
                new[]
                {
                    Phase(0f, 100f, 1.5f, 3f, 1.2f, 0.8f, 24, SpawnPattern.Ring,
                        new[] { Entry(chase, 3f, 1f, 1, 3), Entry(keeper, 1f, 2f, 1, 2) }),
                    Phase(100f, 200f, 3f, 5f, 0.8f, 0.5f, 36, SpawnPattern.Cluster,
                        new[] { Entry(charger, 2f, 3f, 1, 2), Entry(ranged, 2f, 2f, 1, 3), Entry(chase, 1f, 1f, 2, 4, true) },
                        new[] { Boss(boss, 150f, SpawnPattern.FixedAnchor, "test.anchor.boss") }),
                    Phase(200f, 300f, 5f, 7f, 0.5f, 0.35f, 48, SpawnPattern.OffscreenRandom,
                        new[] { Entry(chase, 2f, 1f, 2, 5), Entry(keeper, 1f, 2f, 1, 3), Entry(charger, 1f, 3f, 1, 2), Entry(ranged, 2f, 2f, 1, 3) })
                });

            var anchors = new[]
            {
                new MapAnchorAuthoringData { id = "test.anchor.boss", position = new Vector2(10f, 0f) },
                new MapAnchorAuthoringData { id = "test.anchor.portal", position = new Vector2(-10f, 0f) }
            };
            var finite = LoadOrCreate<MapAuthoring>(Folder + "/TestFiniteArena.asset");
            Identity(finite, "test.map.finite_arena", new[] { "content.placeholder", "map.finite" });
            finite.ConfigureM5(
                "base.map.finite_arena",
                "maps/test.m5/finite_arena",
                MapBoundsMode.Finite,
                new Vector2(-24f, -16f),
                new Vector2(24f, 16f),
                16f,
                2,
                encounter,
                "placeholder.visual.map.finite",
                new[]
                {
                    new MapObstacleAuthoringData
                    {
                        minimum = new Vector2(-2f, -5f),
                        maximum = new Vector2(2f, -2f)
                    }
                },
                anchors);
            var infinite = LoadOrCreate<MapAuthoring>(Folder + "/TestChunkedInfinite.asset");
            Identity(infinite, "test.map.chunked_infinite", new[] { "content.placeholder", "map.infinite" });
            infinite.ConfigureM5(
                "base.map.chunked_infinite",
                "maps/test.m5/chunked_infinite",
                MapBoundsMode.ChunkedInfinite,
                new Vector2(-8f, -8f),
                new Vector2(8f, 8f),
                16f,
                2,
                encounter,
                "placeholder.visual.map.infinite",
                Array.Empty<MapObstacleAuthoringData>(),
                anchors);

            var pack = LoadOrCreate<ContentPackAuthoring>(PackPath);
            pack.Configure(
                "test.pack.m5_enemy_map",
                "0.1.0",
                ContentPackTopology.EnemyMapEncounterSchemaVersion,
                "0.1.0",
                string.Empty,
                new[]
                {
                    new ContentPackDependencyAuthoring
                    {
                        packId = "test.pack.m4_skills",
                        minimumVersion = "0.1.0"
                    }
                },
                "packs/test.m5_enemy_map/catalog",
                "pack.test.m5_enemy_map",
                false,
                new ContentAuthoringBase[]
                {
                    chase, keeper, charger, ranged, boss, encounter, finite, infinite
                });

            MarkDirty(chase, keeper, charger, ranged, boss, encounter, finite, infinite, pack);
            AssetDatabase.SaveAssets();
            var bake = ContentBakeUtility.Bake(pack);
            if (!bake.IsSuccess) throw new UnityException(bake.Error.ToString());
            ContentBakeUtility.WriteCatalog(PackPath, bake.Value);
            AssetDatabase.SaveAssets();
            OpenBootstrapIfPresent();
            Debug.Log("[M5 Setup] Test pack baked: entries=" + bake.Value.Definitions.Count +
                      ", hash=" + bake.Value.ContentHash + ".");
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

        private static EnemyAuthoring Enemy(
            string file,
            string id,
            EnemyMovementMode mode,
            SkillAuthoring skill,
            float health,
            float speed,
            float damage,
            float preferredDistance,
            bool boss = false)
        {
            var enemy = LoadOrCreate<EnemyAuthoring>(Folder + "/" + file + ".asset");
            Identity(enemy, id, new[] { "content.placeholder", boss ? "enemy.boss" : "enemy.normal" });
            enemy.ConfigureM5(
                health,
                boss ? 1.1f : 0.5f,
                speed,
                damage,
                Math.Max(2f, preferredDistance + 1f),
                skill,
                boss ? 50f : 3f,
                boss ? 10f : 1f,
                "placeholder.visual." + id,
                mode,
                preferredDistance,
                0.1f,
                0.4f,
                0.6f,
                2.2f,
                0.8f,
                1.25f,
                0.6f,
                1f);
            return enemy;
        }

        private static EncounterEnemyEntryAuthoringData Entry(
            EnemyAuthoring enemy,
            float weight,
            float cost,
            int minimum,
            int maximum,
            bool elite = false)
        {
            return new EncounterEnemyEntryAuthoringData
            {
                enemy = enemy,
                weight = weight,
                budgetCost = cost,
                minimumGroupSize = minimum,
                maximumGroupSize = maximum,
                elite = elite
            };
        }

        private static EncounterBossRuleAuthoringData Boss(
            EnemyAuthoring enemy,
            float time,
            SpawnPattern pattern,
            string anchor)
        {
            return new EncounterBossRuleAuthoringData
            {
                enemy = enemy,
                spawnTimeSeconds = time,
                pattern = pattern,
                anchorId = anchor
            };
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
            EncounterBossRuleAuthoringData[] bosses = null)
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
                bosses = bosses ?? Array.Empty<EncounterBossRuleAuthoringData>()
            };
        }

        private static void Identity(ContentAuthoringBase asset, string id, string[] tags)
        {
            asset.ConfigureIdentity(
                id,
                "content." + id + ".name",
                "content." + id + ".description",
                tags);
        }

        private static void CreatePlaceholderScene(string path, string rootName)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject(rootName);
            EditorSceneManager.SaveScene(scene, path);
        }

        private static void OpenBootstrapIfPresent()
        {
            const string bootstrap = "Assets/Scenes/Bootstrap.unity";
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(bootstrap) != null)
                EditorSceneManager.OpenScene(bootstrap, OpenSceneMode.Single);
        }

        private static void MarkDirty(params UnityEngine.Object[] assets)
        {
            for (var index = 0; index < assets.Length; index++) EditorUtility.SetDirty(assets[index]);
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void EnsureFolder(string parent, string name)
        {
            var path = parent + "/" + name;
            if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, name);
        }
    }
}
