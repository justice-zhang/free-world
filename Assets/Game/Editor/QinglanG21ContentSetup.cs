using System;
using System.Collections.Generic;
using Game.Content.Authoring;
using Game.Content.Runtime;
using Game.Presentation;
using UnityEditor;
using UnityEditor.Localization;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Editor
{
    /// <summary>Creates the G2.1 old-court map, objectives, events, landmarks, and Placeholder scene.</summary>
    public static class QinglanG21ContentSetup
    {
        public const string MapPath = QinglanG12ContentSetup.Folder + "/OldCourtMap.asset";
        public const string ScenePath = QinglanG12ContentSetup.Folder + "/QinglanOldCourtPlaceholder.unity";
        public const string SceneAddress = "maps/qinglan.demo/old_court";
        public const string MapId = "qinglan.map.old_court";
        public const string RewardId = "qinglan.reward.map.exploration_token";

        private static readonly AnchorSpec[] Anchors =
        {
            new AnchorSpec("qinglan.anchor.old_court.zone.central", 0f, 0f),
            new AnchorSpec("qinglan.anchor.old_court.zone.west", -32f, 4f),
            new AnchorSpec("qinglan.anchor.old_court.zone.east", 32f, 4f),
            new AnchorSpec("qinglan.anchor.old_court.zone.north", 0f, 27f),
            new AnchorSpec("qinglan.anchor.old_court.zone.south", 0f, -27f),
            new AnchorSpec("qinglan.anchor.old_court.objective.listen", 0f, 25f),
            new AnchorSpec("qinglan.anchor.old_court.objective.guide", -30f, 7f),
            new AnchorSpec("qinglan.anchor.old_court.objective.stop_balance", 29f, 5f),
            new AnchorSpec("qinglan.anchor.old_court.landmark.stele", -6f, -25f),
            new AnchorSpec("qinglan.anchor.old_court.landmark.sealed_cache", 35f, 8f),
            new AnchorSpec("qinglan.anchor.old_court.landmark.herb_garden", -34f, 8f),
            new AnchorSpec("qinglan.anchor.old_court.landmark.broken_wall", 23f, 18f),
            new AnchorSpec("qinglan.anchor.old_court.landmark.guest_letter", 6f, -28f)
        };

        [MenuItem("Tools/Free World/Qinglan/G2.1 Configure Old Court Map")]
        public static void Configure()
        {
            var pack = Require<ContentPackAuthoring>(QinglanG12ContentSetup.PackPath);
            var encounter = Require<EncounterScheduleAuthoring>(QinglanG16ContentSetup.EncounterPath);

            var reward = Definition(
                "MapExplorationReward",
                RewardId,
                RuntimeContentKinds.Reward,
                new QinglanRuntimeDefinitionDto
                {
                    enum0 = (int)RewardRepeatPolicy.OncePerTransaction,
                    rewardOperations = new[]
                    {
                        new QinglanRewardOperationDto
                        {
                            code = (int)RewardOperationCode.AddCurrency,
                            integerValue = 25
                        }
                    },
                    presentationProfileId = "placeholder.presentation.qinglan.reward.map_exploration"
                },
                "reward.map", "reward.exploration");

            var objectives = new[]
            {
                StateGraph(
                    "ObjectiveListenToWind",
                    "qinglan.objective.wind_altar.listen",
                    RuntimeContentKinds.MapObjective,
                    new[] { "qinglan.anchor.old_court.objective.listen" },
                    RewardId),
                StateGraph(
                    "ObjectiveGuideWindPulse",
                    "qinglan.objective.wind_altar.guide",
                    RuntimeContentKinds.MapObjective,
                    new[] { "qinglan.anchor.old_court.objective.guide" },
                    RewardId),
                StateGraph(
                    "ObjectiveStopWindBalance",
                    "qinglan.objective.wind_altar.stop_balance",
                    RuntimeContentKinds.MapObjective,
                    new[] { "qinglan.anchor.old_court.objective.stop_balance" },
                    RewardId)
            };

            var events = new[]
            {
                StateGraph(
                    "EventWindVeinRiot",
                    "qinglan.event.wind_vein_riot",
                    RuntimeContentKinds.MapEvent,
                    new[]
                    {
                        "qinglan.anchor.old_court.zone.central",
                        "qinglan.anchor.old_court.zone.north"
                    },
                    objectives[0].ContentIdText,
                    390f,
                    450f),
                StateGraph(
                    "EventHerbGardenRevival",
                    "qinglan.event.herb_garden_revival",
                    RuntimeContentKinds.MapEvent,
                    new[]
                    {
                        "qinglan.anchor.old_court.zone.west",
                        "qinglan.anchor.old_court.landmark.herb_garden"
                    },
                    objectives[1].ContentIdText,
                    120f,
                    600f),
                StateGraph(
                    "EventOldSwordResonance",
                    "qinglan.event.old_sword_resonance",
                    RuntimeContentKinds.MapEvent,
                    new[]
                    {
                        "qinglan.anchor.old_court.zone.east",
                        "qinglan.anchor.old_court.landmark.sealed_cache"
                    },
                    objectives[2].ContentIdText,
                    180f,
                    660f)
            };

            var landmarks = new[]
            {
                Landmark("LandmarkTrialStele", "qinglan.landmark.wind_vein_stele", "qinglan.anchor.old_court.landmark.stele"),
                Landmark("LandmarkSealedCache", "qinglan.landmark.sealed_sword_cache", "qinglan.anchor.old_court.landmark.sealed_cache"),
                Landmark("LandmarkHerbGardenVariant", "qinglan.landmark.herb_garden_variant", "qinglan.anchor.old_court.landmark.herb_garden"),
                Landmark("LandmarkBrokenWall", "qinglan.landmark.broken_wall_sword_mark", "qinglan.anchor.old_court.landmark.broken_wall"),
                Landmark("LandmarkGuestLetter", "qinglan.landmark.guest_pavilion_letter", "qinglan.anchor.old_court.landmark.guest_letter")
            };

            var map = LoadOrCreate<MapAuthoring>(MapPath);
            Identity(map, MapId, "map.old_court", "map.demo");
            map.ConfigureM5(
                "qinglan.runtime.map.finite",
                SceneAddress,
                MapBoundsMode.Finite,
                new Vector2(-48f, -36f),
                new Vector2(48f, 36f),
                24f,
                2,
                encounter,
                "placeholder.presentation.map.old_court",
                CreateObstacles(),
                CreateAnchors());
            map.ConfigureQinglanReferences(objectives, events, landmarks);

            var definitions = new List<ContentAuthoringBase>(pack.Definitions.Count + 13);
            for (var index = 0; index < pack.Definitions.Count; index++) definitions.Add(pack.Definitions[index]);
            AddUnique(definitions, reward);
            for (var index = 0; index < objectives.Length; index++) AddUnique(definitions, objectives[index]);
            for (var index = 0; index < events.Length; index++) AddUnique(definitions, events[index]);
            for (var index = 0; index < landmarks.Length; index++) AddUnique(definitions, landmarks[index]);
            AddUnique(definitions, map);
            pack.Configure(
                "qinglan.pack.demo",
                "0.6.0",
                ContentPackTopology.QinglanDemoSchemaVersion,
                "0.1.0",
                string.Empty,
                Array.Empty<ContentPackDependencyAuthoring>(),
                "packs/qinglan.demo/catalog",
                "pack.qinglan.demo",
                false,
                definitions.ToArray());

            LocalizeAll();
            EditorUtility.SetDirty(reward);
            for (var index = 0; index < objectives.Length; index++) EditorUtility.SetDirty(objectives[index]);
            for (var index = 0; index < events.Length; index++) EditorUtility.SetDirty(events[index]);
            for (var index = 0; index < landmarks.Length; index++) EditorUtility.SetDirty(landmarks[index]);
            EditorUtility.SetDirty(map);
            EditorUtility.SetDirty(pack);
            AssetDatabase.SaveAssets();

            CreatePlaceholderScene();
            M9AddressableUtility.Configure(ScenePath, SceneAddress, pack.AssetLabel);
            QinglanG17PackSetup.Configure();
            AssetDatabase.SaveAssets();

            pack = Require<ContentPackAuthoring>(QinglanG12ContentSetup.PackPath);
            var baked = ContentBakeUtility.Bake(pack);
            if (!baked.IsSuccess) throw new UnityException(baked.Error.ToString());
            if (baked.Value.Definitions.Count != 107)
                throw new UnityException("G2.1 pack must contain exactly 107 definitions.");
            Debug.Log("[Qinglan G2.1] PASS: entries=" + baked.Value.Definitions.Count +
                      ", hash=" + baked.Value.ContentHash + ".");
            OpenBootstrapIfPresent();
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

        private static QinglanDefinitionAuthoring StateGraph(
            string file,
            string id,
            string kind,
            string[] anchorIds,
            string outputId,
            float triggerStart = 0f,
            float triggerEnd = 720f)
        {
            var definition = Definition(
                file,
                id,
                kind,
                new QinglanRuntimeDefinitionDto
                {
                    references0 = anchorIds,
                    referenceId0 = outputId,
                    value0 = triggerStart,
                    value1 = triggerEnd,
                    stateTransitions = FullObjectiveGraph(),
                    presentationProfileId = "placeholder.presentation." + id
                },
                kind == RuntimeContentKinds.MapEvent ? "map.event" : "map.objective");
            return definition;
        }

        private static QinglanDefinitionAuthoring Landmark(string file, string id, string anchorId)
        {
            return Definition(
                file,
                id,
                RuntimeContentKinds.Landmark,
                new QinglanRuntimeDefinitionDto
                {
                    referenceId0 = anchorId,
                    referenceId1 = RewardId,
                    referenceId2 = string.Empty,
                    bool0 = false,
                    presentationProfileId = "placeholder.presentation." + id
                },
                "map.landmark");
        }

        private static QinglanDefinitionAuthoring Definition(
            string file,
            string id,
            string kind,
            QinglanRuntimeDefinitionDto runtime,
            params string[] tags)
        {
            var definition = LoadOrCreate<QinglanDefinitionAuthoring>(
                QinglanG12ContentSetup.Folder + "/" + file + ".asset");
            Identity(definition, id, tags);
            definition.ConfigureRuntime(kind, runtime);
            return definition;
        }

        private static QinglanStateTransitionDto[] FullObjectiveGraph()
        {
            return new[]
            {
                Transition(ObjectiveState.Hidden, ObjectiveState.Revealed),
                Transition(ObjectiveState.Revealed, ObjectiveState.Available),
                Transition(ObjectiveState.Available, ObjectiveState.Activating),
                Transition(ObjectiveState.Activating, ObjectiveState.Defending),
                Transition(ObjectiveState.Activating, ObjectiveState.Available),
                Transition(ObjectiveState.Defending, ObjectiveState.Completed),
                Transition(ObjectiveState.Defending, ObjectiveState.Available)
            };
        }

        private static QinglanStateTransitionDto Transition(ObjectiveState from, ObjectiveState to)
        {
            return new QinglanStateTransitionDto { from = (int)from, to = (int)to };
        }

        private static MapObstacleAuthoringData[] CreateObstacles()
        {
            return new[]
            {
                Obstacle(-21f, -18f, -19f, 12f),
                Obstacle(-21f, 18f, -19f, 34f),
                Obstacle(19f, -34f, 21f, -14f),
                Obstacle(19f, -8f, 21f, 12f),
                Obstacle(-44f, 14f, -12f, 16f),
                Obstacle(10f, 14f, 18f, 16f),
                Obstacle(28f, 14f, 44f, 16f),
                Obstacle(-18f, -17f, -4f, -15f),
                Obstacle(5f, -17f, 18f, -15f)
            };
        }

        private static MapObstacleAuthoringData Obstacle(float minX, float minY, float maxX, float maxY)
        {
            return new MapObstacleAuthoringData
            {
                minimum = new Vector2(minX, minY),
                maximum = new Vector2(maxX, maxY)
            };
        }

        private static MapAnchorAuthoringData[] CreateAnchors()
        {
            var result = new MapAnchorAuthoringData[Anchors.Length];
            for (var index = 0; index < Anchors.Length; index++)
            {
                result[index] = new MapAnchorAuthoringData
                {
                    id = Anchors[index].Id,
                    position = new Vector2(Anchors[index].X, Anchors[index].Y)
                };
            }
            return result;
        }

        private static void CreatePlaceholderScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("QinglanOldCourtPlaceholder");
            var zones = new GameObject("FiveRuntimeZones");
            zones.transform.SetParent(root.transform, false);
            for (var index = 0; index < 5; index++)
            {
                var zone = GameObject.CreatePrimitive(PrimitiveType.Cube);
                zone.name = "Zone_" + Anchors[index].Id.Substring(Anchors[index].Id.LastIndexOf('.') + 1);
                zone.transform.SetParent(zones.transform, false);
                zone.transform.position = new Vector3(Anchors[index].X, -0.2f, Anchors[index].Y);
                zone.transform.localScale = new Vector3(18f, 0.25f, 12f);
                UnityEngine.Object.DestroyImmediate(zone.GetComponent<Collider>());
            }

            var obstacles = new GameObject("ObstacleLayout");
            obstacles.transform.SetParent(root.transform, false);
            var obstacleData = CreateObstacles();
            for (var index = 0; index < obstacleData.Length; index++)
            {
                var source = obstacleData[index];
                var obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
                obstacle.name = "Obstacle_" + index.ToString("00");
                obstacle.transform.SetParent(obstacles.transform, false);
                obstacle.transform.position = new Vector3(
                    (source.minimum.x + source.maximum.x) * 0.5f,
                    0.5f,
                    (source.minimum.y + source.maximum.y) * 0.5f);
                obstacle.transform.localScale = new Vector3(
                    source.maximum.x - source.minimum.x,
                    1f,
                    source.maximum.y - source.minimum.y);
                UnityEngine.Object.DestroyImmediate(obstacle.GetComponent<Collider>());
            }

            var bindings = new GameObject("StableAnchorBindings");
            bindings.transform.SetParent(root.transform, false);
            for (var index = 0; index < Anchors.Length; index++)
            {
                var anchor = new GameObject("Anchor_" + index.ToString("00"));
                anchor.transform.SetParent(bindings.transform, false);
                anchor.transform.position = new Vector3(Anchors[index].X, 0.5f, Anchors[index].Y);
                anchor.AddComponent<MapAnchorBinding>().Configure(Anchors[index].Id);
            }

            var cameraObject = new GameObject("PlaceholderCamera");
            cameraObject.transform.SetParent(root.transform, false);
            cameraObject.transform.position = new Vector3(0f, 80f, 0f);
            cameraObject.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 42f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.07f, 0.11f, 0.09f, 1f);

            var lightObject = new GameObject("PlaceholderLight");
            lightObject.transform.SetParent(root.transform, false);
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            lightObject.AddComponent<Light>().type = LightType.Directional;

            if (!EditorSceneManager.SaveScene(scene, ScenePath, false))
                throw new UnityException("Failed to save " + ScenePath + ".");
            AssetDatabase.ImportAsset(ScenePath, ImportAssetOptions.ForceSynchronousImport);
        }

        private static void OpenBootstrapIfPresent()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(M0ProjectSetup.BootstrapScenePath) != null)
                EditorSceneManager.OpenScene(M0ProjectSetup.BootstrapScenePath, OpenSceneMode.Single);
        }

        private static void LocalizeAll()
        {
            RemoveLegacyLocalizationKeys();
            Localize(MapId, "Old Court", "A five-zone finite arena with stable runtime anchors.", "旧演武场", "包含五个区域与稳定运行时锚点的有限竞技场。");
            Localize(RewardId, "Exploration Token", "A Placeholder reward emitted by map interactions.", "探索令牌", "地图交互产出的占位奖励。");
            Localize("qinglan.objective.wind_altar.listen", "Listen to the Wind Altar", "Activate and defend the listening altar.", "聆听风坛", "激活并守护聆风祭坛。");
            Localize("qinglan.objective.wind_altar.guide", "Guide the Wind Pulse", "Guide a wind pulse through the western court.", "引导风脉", "引导风脉穿过西侧庭院。");
            Localize("qinglan.objective.wind_altar.stop_balance", "Hold the Wind Balance", "Interrupt danger and complete the eastern balance ritual.", "止衡风仪", "化解威胁并完成东侧平衡仪式。");
            Localize("qinglan.event.wind_vein_riot", "Wind Vein Riot", "A deterministic eligible event in the central and northern zones.", "风脉暴动", "在中央与北侧区域触发的确定性候选事件。");
            Localize("qinglan.event.herb_garden_revival", "Herb Garden Revival", "A western-zone event that reveals a map objective.", "药圃复苏", "在西侧区域揭示地图目标的事件。");
            Localize("qinglan.event.old_sword_resonance", "Old Sword Resonance", "An eastern-zone event that reveals a map objective.", "旧剑共鸣", "在东侧区域揭示地图目标的事件。");
            Localize("qinglan.landmark.wind_vein_stele", "Wind Vein Stele", "A discoverable wind-vein marker.", "风脉旧碑", "可发现的风脉旧碑。");
            Localize("qinglan.landmark.sealed_sword_cache", "Sealed Sword Cache", "A one-shot exploration landmark.", "藏剑封存匣", "一次性探索地标。");
            Localize("qinglan.landmark.herb_garden_variant", "Herb Garden", "A one-shot garden landmark.", "药圃", "一次性药圃地标。");
            Localize("qinglan.landmark.broken_wall_sword_mark", "Broken Wall Sword Mark", "A one-shot ruined-wall landmark.", "断墙剑痕", "一次性断墙剑痕地标。");
            Localize("qinglan.landmark.guest_pavilion_letter", "Guest Pavilion Letter", "A one-shot letter landmark.", "迎客亭旧信", "一次性书信地标。");
        }

        private static void RemoveLegacyLocalizationKeys()
        {
            var collection = LocalizationEditorSettings.GetStringTableCollection("UI");
            if (collection == null) return;
            var legacyIds = new[]
            {
                "qinglan.landmark.trial_stele",
                "qinglan.landmark.sealed_cache",
                "qinglan.landmark.broken_wall",
                "qinglan.landmark.guest_letter"
            };
            for (var index = 0; index < legacyIds.Length; index++)
            {
                collection.RemoveEntry("content." + legacyIds[index] + ".name");
                collection.RemoveEntry("content." + legacyIds[index] + ".description");
            }
        }

        private static void Localize(string id, string enName, string enDescription, string zhName, string zhDescription)
        {
            M9LocalizationUtility.EnsureContentEntries(
                "content." + id + ".name",
                "content." + id + ".description",
                enName,
                enDescription,
                zhName,
                zhDescription);
        }

        private static void Identity(ContentAuthoringBase asset, string id, params string[] tags)
        {
            var merged = new string[(tags == null ? 0 : tags.Length) + 1];
            merged[0] = "content.placeholder";
            if (tags != null && tags.Length > 0) Array.Copy(tags, 0, merged, 1, tags.Length);
            asset.ConfigureIdentity(
                id,
                "content." + id + ".name",
                "content." + id + ".description",
                merged);
        }

        private static void AddUnique(List<ContentAuthoringBase> definitions, ContentAuthoringBase value)
        {
            if (!definitions.Contains(value)) definitions.Add(value);
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

        private readonly struct AnchorSpec
        {
            public AnchorSpec(string id, float x, float y)
            {
                Id = id;
                X = x;
                Y = y;
            }

            public string Id { get; }
            public float X { get; }
            public float Y { get; }
        }
    }
}
