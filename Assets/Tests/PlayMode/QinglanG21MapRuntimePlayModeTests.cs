using System.Collections;
using System.IO;
using Game.Content.Runtime;
using Game.Core;
using Game.Simulation;
using NUnit.Framework;
using UnityEngine.TestTools;
using NumericsVector2 = System.Numerics.Vector2;

namespace Game.Tests.PlayMode
{
    public sealed class QinglanG21MapRuntimePlayModeTests
    {
        [UnityTest]
        public IEnumerator DefendingObjectiveDoesNotLockPlayerMovementInQinglanPipeline()
        {
            var catalog = LoadCatalog();
            var registry = new ContentRegistry();
            var loaded = registry.Load(
                new[] { catalog },
                new ContentVersion(0, 1, 0));
            Assert.That(loaded.IsSuccess, Is.True, loaded.Error.ToString());

            var mapId = Id("qinglan.map.old_court");
            var objectiveId = Id("qinglan.objective.wind_altar.listen");
            var map = Definition<RuntimeMapDefinition>(catalog, mapId);
            var objective = Definition<RuntimeMapObjectiveDefinition>(catalog, objectiveId);
            var activationPosition = FindAnchor(map, objective.AnchorIds[0]);
            var runtime = new MapObjectiveRuntime();
            var initialized = runtime.Initialize(registry, mapId, 0x473231504C41594DUL);
            Assert.That(initialized.IsSuccess, Is.True, initialized.Error.ToString());

            var hub = new QinglanRuntimeHub(mapObjectives: runtime);
            var world = new SimulationWorld(
                hub,
                pipeline: SimulationPipeline.CreateQinglanDemo());
            var player = world.CreateActor(
                SimulationEntityState.Create(activationPosition, new NumericsVector2(3f, 0f)));
            world.SetPlayer(player);
            var activator = new SpatialEntity(EntityKind.Actor, player);
            Assert.That(runtime.RevealObjective(objectiveId), Is.EqualTo(MapCommandStatus.Applied));
            Assert.That(runtime.MakeObjectiveAvailable(objectiveId), Is.EqualTo(MapCommandStatus.Applied));
            Assert.That(runtime.ActivateObjective(objectiveId, activator, activationPosition, 1f),
                Is.EqualTo(MapCommandStatus.Applied));
            Assert.That(runtime.BeginObjectiveDefense(objectiveId), Is.EqualTo(MapCommandStatus.Applied));

            var runner = new FixedTickRunner(world);
            Assert.That(runner.Advance(SimulationClock.TickDurationSeconds), Is.EqualTo(1));
            Assert.That(world.Actors.TryRead(player, out var state), Is.True);
            Assert.That(state.Position.X, Is.GreaterThan(activationPosition.X));
            Assert.That(runtime.TryGetState(objectiveId, out var objectiveState), Is.True);
            Assert.That(objectiveState, Is.EqualTo(ObjectiveState.Defending));
            yield return null;
        }

        private static BakedContentCatalog LoadCatalog()
        {
            var path = Path.Combine(
                UnityEngine.Application.dataPath,
                "GameAssets/Placeholder/QinglanDemo/QinglanDemoContentPack.baked.json");
            var dto = UnityEngine.JsonUtility.FromJson<BakedContentCatalogDto>(File.ReadAllText(path));
            var catalog = dto.ToCatalog();
            Assert.That(catalog.IsSuccess, Is.True, catalog.Error.ToString());
            return catalog.Value;
        }

        private static T Definition<T>(BakedContentCatalog catalog, ContentId id)
            where T : RuntimeContentDefinition
        {
            for (var index = 0; index < catalog.Definitions.Count; index++)
                if (catalog.Definitions[index].Id == id) return (T)catalog.Definitions[index];
            Assert.Fail("Missing definition: " + id.Value);
            return null;
        }

        private static NumericsVector2 FindAnchor(RuntimeMapDefinition map, ContentId anchorId)
        {
            for (var index = 0; index < map.Anchors.Count; index++)
                if (map.Anchors[index].Id == anchorId) return map.Anchors[index].Position;
            Assert.Fail("Missing anchor: " + anchorId.Value);
            return default;
        }

        private static ContentId Id(string value) => ContentId.Create(value).Value;
    }
}
