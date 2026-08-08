using System;
using System.Globalization;
using System.IO;
using Game.Application;
using Game.Content.Runtime;
using Game.Core;
using Game.Infrastructure;
using Game.Presentation;
using Game.Simulation;
using UnityEditor;
using UnityEngine;
using Vector2 = System.Numerics.Vector2;

namespace Game.Editor
{
    /// <summary>
    /// Renders the production procedural presentation under a deliberately dense 600-enemy
    /// placeholder load. The images are review evidence, never gameplay or balance truth.
    /// </summary>
    public static class QinglanG28ReadabilityCommand
    {
        private const int EnemyCount = 600;
        private const int HazardCount = 18;
        private const int Width = 1920;
        private const int Height = 1080;
        private const ulong Seed = 0x4732385245414441UL;
        private static readonly ContentVersion GameVersion = new ContentVersion(0, 1, 0);
        private static readonly string[] NormalEnemyIds =
        {
            "qinglan.enemy.grass_spirit",
            "qinglan.enemy.paper_crane_spirit",
            "qinglan.enemy.wooden_sword_puppet",
            "qinglan.enemy.stone_lantern_guard",
            "qinglan.enemy.wind_bell_spirit",
            "qinglan.enemy.explosive_seed_pod"
        };

        public static void Run()
        {
            var exitCode = 1;
            QinglanDemoRunHandle handle = null;
            GameObject root = null;
            GameObject cameraObject = null;
            GameObject canvasObject = null;
            RenderTexture target = null;
            Texture2D pixels = null;
            try
            {
                var outputDirectory = ResolveOutputDirectory();
                Directory.CreateDirectory(outputDirectory);
                var standardPath = Path.Combine(outputDirectory, "readability-standard.png");
                var highContrastPath = Path.Combine(outputDirectory, "readability-high-contrast.png");
                var resultPath = Path.Combine(outputDirectory, "readability.json");

                var catalogs = ContentEditorCatalog.BakeAll();
                if (!catalogs.IsSuccess) throw new InvalidOperationException(catalogs.Error.ToString());
                var application = QinglanDemoRunFactory.CreateInitializedApplicationForDiagnostics(
                    catalogs.Value, GameVersion);
                var factory = new QinglanDemoRunFactory(application);
                var descriptor = factory.CreateDescriptor(Seed ^ 0x52554E4944473238UL, Seed);
                if (!descriptor.IsSuccess) throw new InvalidOperationException(descriptor.Error.ToString());
                var created = factory.Create(descriptor.Value, application.StateMachine);
                if (!created.IsSuccess) throw new InvalidOperationException(created.Error.ToString());
                handle = created.Value as QinglanDemoRunHandle;
                if (handle == null) throw new InvalidOperationException("Qinglan factory returned an unexpected handle.");

                var world = handle.World;
                var session = handle.Session;
                var boss = PopulateDenseBattle(application.ContentRegistry, world);
                var hazards = new SpatialEntity[HazardCount];
                for (var index = 0; index < hazards.Length; index++)
                {
                    var angle = index * (Math.PI * 2d / hazards.Length);
                    var radius = 3.2f + ((index % 3) * 2.6f);
                    var position = new Vector2(
                        (float)Math.Cos(angle) * radius,
                        (float)Math.Sin(angle) * radius);
                    hazards[index] = new SpatialEntity(
                        EntityKind.Area,
                        world.CreateArea(SimulationEntityState.Create(position, Vector2.Zero)));
                }
                if (session.Advance(SimulationClock.TickDurationSeconds) != 1)
                    throw new InvalidOperationException("Readability probe did not advance exactly one production tick.");

                canvasObject = new GameObject("G2_8_Readability_Canvas");
                var canvas = canvasObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                root = new GameObject("G2_8_Readability_Presentation");
                var coordinator = root.AddComponent<PresentationCoordinator>();
                var settings = new AccessibilitySettings();
                coordinator.Initialize(
                    canvas,
                    settings,
                    null,
                    QinglanProceduralPresentationFactory.Build(application.ContentRegistry));
                coordinator.SetMap(QinglanProceduralMapFactory.Build(
                    application.ContentRegistry, session.Descriptor.MapId));
                coordinator.Sync(session.RenderSnapshot, session.InterpolationAlpha, session);

                cameraObject = new GameObject("G2_8_Readability_Camera");
                var camera = cameraObject.AddComponent<Camera>();
                camera.orthographic = true;
                camera.orthographicSize = 11.5f;
                camera.transform.position = new UnityEngine.Vector3(0f, 0f, -10f);
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.055f, 0.065f, 0.075f, 1f);
                target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
                target.Create();
                camera.targetTexture = target;
                pixels = new Texture2D(Width, Height, TextureFormat.RGB24, false);

                Render(camera, target, pixels, standardPath);
                settings.SetColorVision(ColorVisionMode.HighContrast);
                coordinator.Sync(session.RenderSnapshot, session.InterpolationAlpha, session);
                Render(camera, target, pixels, highContrastPath);

                if (!coordinator.TryGetView(session.Player, out var playerView) ||
                    !coordinator.TryGetView(boss, out var bossView))
                    throw new InvalidOperationException("Player or Boss view was absent from the dense snapshot.");
                var criticalCount = 0;
                var lowestCriticalOrder = int.MaxValue;
                var highestCombatOrder = int.MinValue;
                for (var index = 0; index < session.RenderSnapshot.Count; index++)
                {
                    var entity = session.RenderSnapshot.GetAt(index).Entity;
                    if (!coordinator.TryGetView(entity, out var view)) continue;
                    var renderer = view.GetComponent<SpriteRenderer>();
                    if (view.Priority == PresentationPriority.CriticalDanger)
                    {
                        criticalCount++;
                        lowestCriticalOrder = Math.Min(lowestCriticalOrder, renderer.sortingOrder);
                    }
                    else if (view.Priority == PresentationPriority.Combat)
                    {
                        highestCombatOrder = Math.Max(highestCombatOrder, renderer.sortingOrder);
                    }
                }
                var allHazardsVisible = true;
                for (var index = 0; index < hazards.Length; index++)
                    allHazardsVisible &= coordinator.TryGetView(hazards[index], out var view) &&
                                         view.Priority == PresentationPriority.CriticalDanger &&
                                         view.Shape == ProceduralShape.Ring;

                var passed = world.Enemies.Count >= EnemyCount &&
                             coordinator.ActiveViewCount >= EnemyCount + HazardCount + 1 &&
                             playerView.Shape == ProceduralShape.Triangle &&
                             bossView.Shape == ProceduralShape.Hexagon &&
                             bossView.Priority == PresentationPriority.CriticalDanger &&
                             allHazardsVisible && criticalCount >= HazardCount + 1 &&
                             lowestCriticalOrder > highestCombatOrder &&
                             File.Exists(standardPath) && File.Exists(highContrastPath);
                var result = new QinglanG28ReadabilityResult
                {
                    schemaVersion = 1,
                    status = passed ? "PASS" : "FAIL",
                    generatedAtUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                    unityVersion = UnityEngine.Application.unityVersion,
                    width = Width,
                    height = Height,
                    enemyCount = world.Enemies.Count,
                    activeViews = coordinator.ActiveViewCount,
                    criticalDangerViews = criticalCount,
                    hazardViews = HazardCount,
                    playerShape = playerView.Shape.ToString(),
                    bossShape = bossView.Shape.ToString(),
                    lowestCriticalSortingOrder = lowestCriticalOrder,
                    highestCombatSortingOrder = highestCombatOrder,
                    criticalSortsAboveCombat = lowestCriticalOrder > highestCombatOrder,
                    standardImage = standardPath,
                    highContrastImage = highContrastPath
                };
                File.WriteAllText(resultPath, JsonUtility.ToJson(result, true) + "\n");
                if (!passed) throw new InvalidOperationException("Dense readability assertions failed: " + resultPath);
                Debug.Log("[Qinglan G2.8 Readability] PASS: " + resultPath);
                exitCode = 0;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
            finally
            {
                if (target != null)
                {
                    target.Release();
                    UnityEngine.Object.DestroyImmediate(target);
                }
                if (pixels != null) UnityEngine.Object.DestroyImmediate(pixels);
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
                if (cameraObject != null) UnityEngine.Object.DestroyImmediate(cameraObject);
                if (canvasObject != null) UnityEngine.Object.DestroyImmediate(canvasObject);
                handle?.Dispose();
            }
            EditorApplication.Exit(exitCode);
        }

        private static SpatialEntity PopulateDenseBattle(ContentRegistry content, SimulationWorld world)
        {
            var sequence = 1L;
            for (var index = 0; index < EnemyCount - 1; index++)
            {
                var id = ContentId.Create(NormalEnemyIds[index % NormalEnemyIds.Length]).Value;
                if (!content.TryGet(id, out ContentRegistryEntry entry))
                    throw new InvalidOperationException("Readability enemy is missing: " + id.Value);
                var column = index % 30;
                var row = index / 30;
                var position = new Vector2(
                    (column - 14.5f) * 0.94f + ((row & 1) * 0.22f),
                    (row - 9.5f) * 0.94f);
                world.Enemies.PendingSpawns.Add(
                    new SpawnRequest(entry.Index, position, false, false, sequence++));
            }

            var bossId = ContentId.Create("qinglan.enemy.boss.tingfeng").Value;
            if (!content.TryGet(bossId, out ContentRegistryEntry bossEntry))
                throw new InvalidOperationException("Readability Boss is missing.");
            world.Enemies.PendingSpawns.Add(
                new SpawnRequest(bossEntry.Index, new Vector2(0f, 4.2f), false, true, sequence));
            world.Enemies.ApplyPendingSpawns(world);
            for (var dense = 0; dense < world.Actors.Count; dense++)
            {
                var handle = world.Actors.GetHandleAt(dense);
                if (world.Enemies.TryGetSnapshot(handle, out var enemy) && enemy.Boss)
                    return new SpatialEntity(EntityKind.Actor, handle);
            }
            throw new InvalidOperationException("Dense readability Boss did not spawn.");
        }

        private static void Render(Camera camera, RenderTexture target, Texture2D pixels, string path)
        {
            camera.Render();
            var previous = RenderTexture.active;
            RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0f, 0f, Width, Height), 0, 0, false);
            pixels.Apply(false, false);
            File.WriteAllBytes(path, pixels.EncodeToPNG());
            RenderTexture.active = previous;
        }

        private static string ResolveOutputDirectory()
        {
            var value = Environment.GetEnvironmentVariable("QINGLAN_G28_READABILITY_DIR");
            if (string.IsNullOrWhiteSpace(value))
                value = Path.Combine("TestResults", "QinglanDemo", "G2.8", "readability");
            return Path.GetFullPath(value);
        }

        [Serializable]
        private sealed class QinglanG28ReadabilityResult
        {
            public int schemaVersion;
            public string status;
            public string generatedAtUtc;
            public string unityVersion;
            public int width;
            public int height;
            public int enemyCount;
            public int activeViews;
            public int criticalDangerViews;
            public int hazardViews;
            public string playerShape;
            public string bossShape;
            public int lowestCriticalSortingOrder;
            public int highestCombatSortingOrder;
            public bool criticalSortsAboveCombat;
            public string standardImage;
            public string highContrastImage;
        }
    }
}
