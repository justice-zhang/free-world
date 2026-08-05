using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using Game.Content.Authoring;
using Game.Content.Runtime;
using Game.Core;
using Game.Editor;
using Game.Presentation;
using Game.Simulation;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace Game.Tests.EditMode
{
    public sealed class QinglanG21MapRuntimeTests
    {
        private const ulong RunId = 0x4732314F4C444354UL;
        private static readonly ContentVersion GameVersion = new ContentVersion(0, 1, 0);
        private static readonly string[] ObjectiveIds =
        {
            "qinglan.objective.wind_altar.guide",
            "qinglan.objective.wind_altar.listen",
            "qinglan.objective.wind_altar.stop_balance"
        };

        private static readonly string[] EventIds =
        {
            "qinglan.event.herb_garden_revival",
            "qinglan.event.old_sword_resonance",
            "qinglan.event.wind_vein_riot"
        };

        [Test]
        public void PackPointSixBakesDeterministicallyAndSceneBindsEveryStableAnchor()
        {
            var first = Bake();
            var second = Bake();
            Assert.That(first.Manifest.Version, Is.EqualTo(new ContentVersion(0, 6, 0)));
            Assert.That(first.Definitions.Count, Is.EqualTo(107));
            Assert.That(first.ContentHash,
                Is.EqualTo("fbb58777702837b2730be64e515ef4b2386254089bb109e4c8c6e926ab2ca67c"));
            Assert.That(second.ContentHash, Is.EqualTo(first.ContentHash));

            var checkedIn = UnityEngine.JsonUtility.FromJson<BakedContentCatalogDto>(
                File.ReadAllText(Path.GetFullPath(QinglanG12ContentSetup.BakedCatalogPath))).ToCatalog();
            Assert.That(checkedIn.IsSuccess, Is.True, checkedIn.Error.ToString());
            Assert.That(checkedIn.Value.ContentHash, Is.EqualTo(first.ContentHash));

            var map = Definition<RuntimeMapDefinition>(first, QinglanG21ContentSetup.MapId);
            Assert.That(map.ObjectiveIds.Count, Is.EqualTo(3));
            Assert.That(map.EventIds.Count, Is.EqualTo(3));
            Assert.That(map.LandmarkIds.Count, Is.EqualTo(5));
            Assert.That(map.Anchors.Count, Is.EqualTo(13));
            Assert.That(map.BoundsMode, Is.EqualTo(MapBoundsMode.Finite));
            for (var index = 0; index < map.Anchors.Count; index++)
                Assert.That(IsWalkable(map, map.Anchors[index].Position), Is.True, map.Anchors[index].Id.Value);

            var settings = AddressableAssetSettingsDefaultObject.GetSettings(false);
            var sceneEntry = settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(QinglanG21ContentSetup.ScenePath));
            Assert.That(sceneEntry, Is.Not.Null);
            Assert.That(sceneEntry.address, Is.EqualTo(QinglanG21ContentSetup.SceneAddress));
            Assert.That(sceneEntry.labels, Does.Contain("pack.qinglan.demo"));

            var scene = EditorSceneManager.OpenScene(QinglanG21ContentSetup.ScenePath, OpenSceneMode.Additive);
            try
            {
                var seen = new HashSet<ContentId>();
                var roots = scene.GetRootGameObjects();
                for (var rootIndex = 0; rootIndex < roots.Length; rootIndex++)
                {
                    var bindings = roots[rootIndex].GetComponentsInChildren<MapAnchorBinding>(true);
                    for (var bindingIndex = 0; bindingIndex < bindings.Length; bindingIndex++)
                    {
                        Assert.That(bindings[bindingIndex].TryGetAnchorId(out var id), Is.True);
                        Assert.That(seen.Add(id), Is.True, id.Value);
                    }
                }
                Assert.That(seen.Count, Is.EqualTo(map.Anchors.Count));
                for (var index = 0; index < map.Anchors.Count; index++)
                    Assert.That(seen.Contains(map.Anchors[index].Id), Is.True, map.Anchors[index].Id.Value);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void ObjectivesValidateDistanceInterruptResumeAndEmitExactlyOnce()
        {
            var registry = LoadRegistry(out var catalog);
            var map = Definition<RuntimeMapDefinition>(catalog, QinglanG21ContentSetup.MapId);
            var runtime = CreateRuntime(registry);
            var activator = new SpatialEntity(EntityKind.Actor, new EntityHandle(1, 1));

            for (var index = 0; index < ObjectiveIds.Length; index++)
            {
                var id = Id(ObjectiveIds[index]);
                var objective = Definition<RuntimeMapObjectiveDefinition>(catalog, ObjectiveIds[index]);
                var position = AnchorPosition(map, objective.AnchorIds[0]);
                Assert.That(runtime.RevealObjective(id), Is.EqualTo(MapCommandStatus.Applied));
                Assert.That(runtime.MakeObjectiveAvailable(id), Is.EqualTo(MapCommandStatus.Applied));
                Assert.That(runtime.ActivateObjective(id, activator, position + new Vector2(20f, 0f), 2f),
                    Is.EqualTo(MapCommandStatus.OutOfRange));
                Assert.That(runtime.ActivateObjective(id, activator, position, 2f),
                    Is.EqualTo(MapCommandStatus.Applied));
                Assert.That(runtime.BeginObjectiveDefense(id), Is.EqualTo(MapCommandStatus.Applied));
                Assert.That(runtime.ReportObjectiveProgress(id, 0.4f), Is.EqualTo(MapCommandStatus.Applied));
                Assert.That(runtime.InterruptObjective(id), Is.EqualTo(MapCommandStatus.Applied));
                Assert.That(runtime.ActivateObjective(id, activator, position, 2f),
                    Is.EqualTo(MapCommandStatus.Applied));
                Assert.That(runtime.BeginObjectiveDefense(id), Is.EqualTo(MapCommandStatus.Applied));
                Assert.That(runtime.ReportObjectiveProgress(id, 1f), Is.EqualTo(MapCommandStatus.Applied));
                Assert.That(runtime.ReportObjectiveProgress(id, 1f), Is.EqualTo(MapCommandStatus.AlreadyApplied));
                Assert.That(runtime.IsObjectiveCompleted(id), Is.True);
            }

            Assert.That(runtime.OutputCount, Is.EqualTo(3));
            for (var index = 0; index < runtime.OutputCount; index++)
            {
                var output = runtime.GetOutputAt(index);
                Assert.That(output.OutputId, Is.EqualTo(Id(QinglanG21ContentSetup.RewardId)));
                Assert.That(output.Transaction.RunId, Is.EqualTo(RunId));
                Assert.That(output.Transaction.Sequence, Is.Zero);
            }
        }

        [Test]
        public void EventSelectionUsesIndependentDeterministicStreamAndUnlocksItsObjective()
        {
            var registry = LoadRegistry(out _);
            var first = CreateRuntime(registry);
            var second = CreateRuntime(registry);
            var unrelated = new RandomStream(RunId);
            for (var index = 0; index < 17; index++) unrelated.NextFloat();

            for (var index = 0; index < EventIds.Length; index++)
            {
                Assert.That(first.ArmEvent(Id(EventIds[index])), Is.EqualTo(MapCommandStatus.Applied));
                Assert.That(second.ArmEvent(Id(EventIds[index])), Is.EqualTo(MapCommandStatus.Applied));
            }
            first.AdvanceEvents(390f);
            second.AdvanceEvents(390f);
            var firstActive = ActiveEvent(first);
            var secondActive = ActiveEvent(second);
            Assert.That(firstActive.Id, Is.EqualTo(secondActive.Id));
            Assert.That(firstActive.ActiveAnchorId, Is.EqualTo(secondActive.ActiveAnchorId));
            Assert.That(first.EventRandomCalls, Is.EqualTo(2));
            Assert.That(second.EventRandomCalls, Is.EqualTo(2));

            Assert.That(first.ReportEventProgress(firstActive.Id, 1f), Is.EqualTo(MapCommandStatus.Applied));
            Assert.That(first.OutputCount, Is.Zero, "event-to-objective output is internal unlock state");
            Assert.That(first.TryGetState(firstActive.OutputId, out var state), Is.True);
            Assert.That(state, Is.EqualTo(ObjectiveState.Available));
        }

        [Test]
        public void LandmarksDiscoverByDistanceAndRejectDuplicateOneShotClaims()
        {
            var registry = LoadRegistry(out var catalog);
            var map = Definition<RuntimeMapDefinition>(catalog, QinglanG21ContentSetup.MapId);
            var runtime = CreateRuntime(registry);
            var landmark = Definition<RuntimeLandmarkDefinition>(catalog, "qinglan.landmark.wind_vein_stele");
            var position = AnchorPosition(map, landmark.AnchorId);

            Assert.That(runtime.UpdateLandmarkDiscovery(position + new Vector2(4f, 0f), 2.5f), Is.Zero);
            Assert.That(runtime.UpdateLandmarkDiscovery(position, 2.5f), Is.EqualTo(1));
            Assert.That(runtime.ClaimLandmark(landmark.Id), Is.EqualTo(MapCommandStatus.Applied));
            Assert.That(runtime.ClaimLandmark(landmark.Id), Is.EqualTo(MapCommandStatus.AlreadyApplied));
            Assert.That(runtime.OutputCount, Is.EqualTo(1));
            var output = runtime.GetOutputAt(0);
            Assert.That(output.SourceKind, Is.EqualTo(MapRuntimeEntryKind.LandmarkReward));
            Assert.That(output.SourceId, Is.EqualTo(landmark.Id));
            Assert.That(output.Transaction, Is.EqualTo(new RewardTransactionId(RunId, landmark.Id, 0)));
            var claimed = default(LandmarkSnapshot);
            for (var index = 0; index < runtime.LandmarkCount; index++)
                if (runtime.GetLandmarkAt(index).Id == landmark.Id) claimed = runtime.GetLandmarkAt(index);
            Assert.That(claimed.ClaimCount, Is.EqualTo(1));
        }

        [Test]
        public void EveryObjectiveCompletionSubsetProducesExactStableMask()
        {
            var registry = LoadRegistry(out var catalog);
            var map = Definition<RuntimeMapDefinition>(catalog, QinglanG21ContentSetup.MapId);
            var activator = new SpatialEntity(EntityKind.Actor, new EntityHandle(2, 1));
            for (var subset = 0; subset < 8; subset++)
            {
                var runtime = CreateRuntime(registry);
                for (var index = 0; index < runtime.ObjectiveCount; index++)
                {
                    if ((subset & (1 << index)) == 0) continue;
                    var snapshot = runtime.GetObjectiveAt(index);
                    var objective = Definition<RuntimeMapObjectiveDefinition>(catalog, snapshot.Id.Value);
                    var position = AnchorPosition(map, objective.AnchorIds[0]);
                    Assert.That(runtime.RevealObjective(snapshot.Id), Is.EqualTo(MapCommandStatus.Applied));
                    Assert.That(runtime.MakeObjectiveAvailable(snapshot.Id), Is.EqualTo(MapCommandStatus.Applied));
                    Assert.That(runtime.ActivateObjective(snapshot.Id, activator, position, 1f), Is.EqualTo(MapCommandStatus.Applied));
                    Assert.That(runtime.BeginObjectiveDefense(snapshot.Id), Is.EqualTo(MapCommandStatus.Applied));
                    Assert.That(runtime.ReportObjectiveProgress(snapshot.Id, 1f), Is.EqualTo(MapCommandStatus.Applied));
                }
                Assert.That(runtime.CompletedObjectiveMask, Is.EqualTo((ulong)subset), "subset=" + subset);
            }
        }

        [Test]
        public void ValidatorRejectsMissingAndNonWalkableOwnedAnchors()
        {
            var baked = Bake();
            var sourceMap = Definition<RuntimeMapDefinition>(baked, QinglanG21ContentSetup.MapId);
            var missing = CloneMap(sourceMap, Array.Empty<RuntimeMapAnchor>());
            var missingReport = ContentValidator.ValidateCatalogs(
                new[] { ReplaceMap(baked, missing) }, GameVersion);
            Assert.That(missingReport.IsValid, Is.False);
            Assert.That(Errors(missingReport), Does.Contain("missing or non-walkable anchor"));

            var anchors = CopyAnchors(sourceMap);
            var objective = Definition<RuntimeMapObjectiveDefinition>(baked, ObjectiveIds[0]);
            for (var index = 0; index < anchors.Length; index++)
            {
                if (anchors[index].Id != objective.AnchorIds[0]) continue;
                anchors[index] = new RuntimeMapAnchor(anchors[index].Id, sourceMap.Obstacles[0].Minimum);
            }
            var blockedReport = ContentValidator.ValidateCatalogs(
                new[] { ReplaceMap(baked, CloneMap(sourceMap, anchors)) }, GameVersion);
            Assert.That(blockedReport.IsValid, Is.False);
            Assert.That(Errors(blockedReport), Does.Contain("outside obstacles"));

            var foreignOutputReport = ContentValidator.ValidateCatalogs(
                new[]
                {
                    ReplaceMap(
                        baked,
                        CloneMap(sourceMap, CopyAnchors(sourceMap), Array.Empty<ContentId>()))
                },
                GameVersion);
            Assert.That(foreignOutputReport.IsValid, Is.False);
            Assert.That(Errors(foreignOutputReport), Does.Contain("not owned by the same map"));
        }

        private static MapObjectiveRuntime CreateRuntime(ContentRegistry registry)
        {
            var runtime = new MapObjectiveRuntime();
            var initialized = runtime.Initialize(registry, Id(QinglanG21ContentSetup.MapId), RunId);
            Assert.That(initialized.IsSuccess, Is.True, initialized.Error.ToString());
            return runtime;
        }

        private static MapEventSnapshot ActiveEvent(MapObjectiveRuntime runtime)
        {
            for (var index = 0; index < runtime.EventCount; index++)
            {
                var snapshot = runtime.GetEventAt(index);
                if (snapshot.State == ObjectiveState.Defending) return snapshot;
            }
            Assert.Fail("No active event was selected.");
            return default;
        }

        private static BakedContentCatalog Bake()
        {
            var pack = AssetDatabase.LoadAssetAtPath<ContentPackAuthoring>(QinglanG12ContentSetup.PackPath);
            Assert.That(pack, Is.Not.Null);
            var baked = ContentBakeUtility.Bake(pack);
            Assert.That(baked.IsSuccess, Is.True, baked.Error.ToString());
            return baked.Value;
        }

        private static ContentRegistry LoadRegistry(out BakedContentCatalog catalog)
        {
            catalog = Bake();
            var registry = new ContentRegistry();
            var loaded = registry.Load(new[] { catalog }, GameVersion);
            Assert.That(loaded.IsSuccess, Is.True, loaded.Error.ToString());
            return registry;
        }

        private static T Definition<T>(BakedContentCatalog catalog, string id) where T : RuntimeContentDefinition
        {
            var expected = Id(id);
            for (var index = 0; index < catalog.Definitions.Count; index++)
                if (catalog.Definitions[index].Id == expected)
                    return (T)catalog.Definitions[index];
            Assert.Fail("Missing definition: " + id);
            return null;
        }

        private static RuntimeMapDefinition CloneMap(
            RuntimeMapDefinition source,
            RuntimeMapAnchor[] anchors,
            ContentId[] objectiveIds = null)
        {
            return new RuntimeMapDefinition(
                source.Id,
                source.LocalizedNameKey,
                source.LocalizedDescriptionKey,
                source.SourceAssetPath,
                CopyTags(source),
                source.RuntimeProviderId,
                source.SceneAddress,
                source.BoundsMode,
                source.Minimum,
                source.Maximum,
                source.ChunkSize,
                source.ActiveChunkRadius,
                source.EncounterScheduleId,
                source.VisualProfileId,
                CopyObstacles(source),
                anchors,
                objectiveIds ?? CopyIds(source.ObjectiveIds),
                CopyIds(source.EventIds),
                CopyIds(source.LandmarkIds));
        }

        private static BakedContentCatalog ReplaceMap(BakedContentCatalog source, RuntimeMapDefinition replacement)
        {
            var definitions = new RuntimeContentDefinition[source.Definitions.Count];
            for (var index = 0; index < definitions.Length; index++)
                definitions[index] = source.Definitions[index].Id == replacement.Id
                    ? replacement
                    : source.Definitions[index];
            return BakedContentCatalog.Create(source.Manifest, definitions);
        }

        private static RuntimeMapObstacle[] CopyObstacles(RuntimeMapDefinition map)
        {
            var result = new RuntimeMapObstacle[map.Obstacles.Count];
            for (var index = 0; index < result.Length; index++) result[index] = map.Obstacles[index];
            return result;
        }

        private static RuntimeMapAnchor[] CopyAnchors(RuntimeMapDefinition map)
        {
            var result = new RuntimeMapAnchor[map.Anchors.Count];
            for (var index = 0; index < result.Length; index++) result[index] = map.Anchors[index];
            return result;
        }

        private static ContentId[] CopyIds(IReadOnlyList<ContentId> source)
        {
            var result = new ContentId[source.Count];
            for (var index = 0; index < result.Length; index++) result[index] = source[index];
            return result;
        }

        private static ContentTag[] CopyTags(RuntimeContentDefinition source)
        {
            var result = new ContentTag[source.Tags.Count];
            for (var index = 0; index < result.Length; index++) result[index] = source.Tags[index];
            return result;
        }

        private static Vector2 AnchorPosition(RuntimeMapDefinition map, ContentId id)
        {
            for (var index = 0; index < map.Anchors.Count; index++)
                if (map.Anchors[index].Id == id) return map.Anchors[index].Position;
            Assert.Fail("Missing anchor: " + id.Value);
            return default;
        }

        private static bool IsWalkable(RuntimeMapDefinition map, Vector2 position)
        {
            if (position.X < map.Minimum.X || position.X > map.Maximum.X ||
                position.Y < map.Minimum.Y || position.Y > map.Maximum.Y)
                return false;
            for (var index = 0; index < map.Obstacles.Count; index++)
            {
                var obstacle = map.Obstacles[index];
                if (position.X >= obstacle.Minimum.X && position.X <= obstacle.Maximum.X &&
                    position.Y >= obstacle.Minimum.Y && position.Y <= obstacle.Maximum.Y)
                    return false;
            }
            return true;
        }

        private static string Errors(ContentValidationReport report)
        {
            var text = string.Empty;
            for (var index = 0; index < report.Errors.Count; index++)
                text += report.Errors[index] + Environment.NewLine;
            return text;
        }

        private static ContentId Id(string value) => ContentId.Create(value).Value;
    }
}
