using System;
using System.IO;
using Game.Application;
using Game.Content.Runtime;
using Game.Core;
using Game.Infrastructure;
using Game.Platform.Null;
using Game.Presentation;
using Game.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Game.Tests.EditMode
{
    public sealed class QinglanG27PresentationPolishTests
    {
        private GameObject root;

        [TearDown]
        public void TearDown()
        {
            if (root != null) Object.DestroyImmediate(root);
        }

        [Test]
        public void RegistryDrivenProfilesGivePlayerBossAndBossSkillDistinctNonColorChannels()
        {
            var registry = LoadRegistry();
            var profiles = QinglanProceduralPresentationFactory.Build(registry);
            Assert.That(profiles.Count, Is.GreaterThan(registry.Count));

            Assert.That(registry.TryGet(Id("qinglan.character.lu_qingye"), out RuntimeCharacterDefinition player), Is.True);
            Assert.That(profiles.TryResolve(player.Id, EntityKind.Actor, true, ColorVisionMode.Standard,
                out var playerStyle), Is.True);
            Assert.That(playerStyle.Shape, Is.EqualTo(ProceduralShape.Triangle));
            Assert.That(playerStyle.Directional, Is.True);

            Assert.That(registry.TryGet(Id("qinglan.enemy.boss.tingfeng"), out RuntimeEnemyDefinition boss), Is.True);
            Assert.That(profiles.TryResolve(boss.VisualProfileId, EntityKind.Actor, false, ColorVisionMode.Standard,
                out var bossStyle), Is.True);
            Assert.That(bossStyle.Shape, Is.EqualTo(ProceduralShape.Hexagon));
            Assert.That(bossStyle.Priority, Is.EqualTo(PresentationPriority.CriticalDanger));

            Assert.That(registry.TryGet(Id("qinglan.skill.boss.tingfeng.obscuring_windfield"),
                out RuntimeSkillDefinition skill), Is.True);
            Assert.That(profiles.TryResolve(skill.Delivery.PresentationId, EntityKind.Area, false,
                ColorVisionMode.HighContrast, out var dangerStyle), Is.True);
            Assert.That(dangerStyle.Shape, Is.EqualTo(ProceduralShape.Ring));
            Assert.That(dangerStyle.Priority, Is.EqualTo(PresentationPriority.CriticalDanger));
            Assert.That(dangerStyle.OutlineColor, Is.EqualTo(Color.black));
        }

        [Test]
        public void ProceduralMapCopiesBoundsObstaclesAndAllStateMarkers()
        {
            var registry = LoadRegistry();
            var configuration = QinglanProceduralMapFactory.Build(registry, Id(QinglanDemoRunFactory.MapId));

            Assert.That(configuration, Is.Not.Null);
            Assert.That(configuration.Minimum, Is.EqualTo(new Vector2(-48f, -36f)));
            Assert.That(configuration.Maximum, Is.EqualTo(new Vector2(48f, 36f)));
            Assert.That(configuration.Obstacles.Count, Is.EqualTo(9));
            Assert.That(configuration.Zones.Count, Is.EqualTo(5));
            Assert.That(configuration.Markers.Count, Is.EqualTo(11));

            root = new GameObject("G27MapCoordinator");
            var canvas = new GameObject("Canvas").AddComponent<Canvas>();
            canvas.transform.SetParent(root.transform, false);
            var coordinator = root.AddComponent<PresentationCoordinator>();
            coordinator.Initialize(canvas, new AccessibilitySettings(), null,
                QinglanProceduralPresentationFactory.Build(registry));
            coordinator.SetMap(configuration);
            Assert.That(coordinator.MapMarkerCount, Is.EqualTo(11));
        }

        [Test]
        public void RealSessionExposesStableDeliveryPresentationIdentityWithoutViewReadingStores()
        {
            var catalog = LoadCatalog();
            var state = new GameStateMachine();
            var application = new GameApplication(new NullPlatformFacade(), state);
            var initialized = application.Initialize(new[] { catalog }, new ContentVersion(0, 1, 0));
            Assert.That(initialized.IsSuccess, Is.True, initialized.Error.ToString());
            var factory = new QinglanDemoRunFactory(application);
            var descriptor = factory.CreateDescriptor(0x47323750524F4649UL, 0x47323744454C4956UL);
            Assert.That(descriptor.IsSuccess, Is.True, descriptor.Error.ToString());
            var flow = new DemoRunCoordinator(state, factory);
            flow.ShowCharacterSelect();
            flow.ShowMapSelect();
            Assert.That(flow.BeginRun(descriptor.Value), Is.True);
            flow.Tick(0d);

            var found = false;
            for (var tick = 0; tick < 90 && !found; tick++)
            {
                flow.Tick(SimulationClock.TickDurationSeconds);
                var snapshot = flow.Session.RenderSnapshot;
                for (var index = 0; index < snapshot.Count; index++)
                {
                    var entity = snapshot.GetAt(index).Entity;
                    if (entity.Kind != EntityKind.Projectile && entity.Kind != EntityKind.Area) continue;
                    Assert.That(flow.Session.TryGetVisualProfileId(entity, out var profileId), Is.True);
                    Assert.That(profileId.IsValid, Is.True);
                    found = true;
                    break;
                }
            }
            Assert.That(found, Is.True, "the real starter skill must expose an active delivery profile");
            flow.Dispose();
        }

        [Test]
        public void VfxPoolBoundsCapacityEvictsDecorationAndMergesCriticalDanger()
        {
            root = new GameObject("G27VfxPool");
            var texture = new Texture2D(1, 1);
            var sprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), Vector2.one * 0.5f);
            var pool = new VfxRequestPool(root.transform, sprite, 2);
            var decoration = Style(PresentationPriority.Decoration, ProceduralShape.Circle);
            var danger = Style(PresentationPriority.CriticalDanger, ProceduralShape.Ring);

            Assert.That(pool.TrySpawn(new ProceduralVfxRequest(Vector2.zero, decoration, 1f, 1f)), Is.True);
            Assert.That(pool.TrySpawn(new ProceduralVfxRequest(Vector2.one, decoration, 1f, 1f)), Is.True);
            Assert.That(pool.TrySpawn(new ProceduralVfxRequest(Vector2.up, danger, 1f, 1f)), Is.True);
            Assert.That(pool.TrySpawn(new ProceduralVfxRequest(Vector2.down, danger, 1f, 1f)), Is.True);
            Assert.That(pool.TrySpawn(new ProceduralVfxRequest(Vector2.right, danger, 2f, 1f)), Is.True);

            Assert.That(pool.CreatedCount, Is.EqualTo(2));
            Assert.That(pool.ActiveCount, Is.EqualTo(2));
            Assert.That(pool.EvictedLowerPriorityCount, Is.EqualTo(2));
            Assert.That(pool.MergedCriticalCount, Is.EqualTo(1));
            pool.Dispose();
            Object.DestroyImmediate(sprite);
            Object.DestroyImmediate(texture);
        }

        [Test]
        public void AudioRouterReservesCriticalCapacitySuppressesSpamAndNeverExpandsPastBound()
        {
            root = new GameObject("G27AudioPool");
            var router = new AudioRequestRouter(root.transform, 6, 1, 1);

            Assert.That(router.Route(PresentationAudioCue.Hit, PresentationPriority.Combat, 1f), Is.True);
            Assert.That(router.Route(PresentationAudioCue.Hit, PresentationPriority.Combat, 1f), Is.False);
            Assert.That(router.Route(PresentationAudioCue.Pickup, PresentationPriority.Combat, 1f), Is.True);
            Assert.That(router.Route(PresentationAudioCue.Confirm, PresentationPriority.Combat, 1f), Is.True);
            Assert.That(router.Route(PresentationAudioCue.Objective, PresentationPriority.Combat, 1f), Is.False);
            Assert.That(router.Route(PresentationAudioCue.Danger, PresentationPriority.CriticalDanger, 1f), Is.True);
            Assert.That(router.Route(PresentationAudioCue.Danger, PresentationPriority.CriticalDanger, 1f), Is.True);
            Assert.That(router.Route(PresentationAudioCue.Danger, PresentationPriority.CriticalDanger, 1f), Is.True);
            Assert.That(router.Route(PresentationAudioCue.Danger, PresentationPriority.CriticalDanger, 1f), Is.True);
            Assert.That(router.Route(PresentationAudioCue.Danger, PresentationPriority.CriticalDanger, 1f), Is.True);

            Assert.That(router.CreatedSourceCount, Is.EqualTo(6));
            Assert.That(router.PeakActiveCount, Is.EqualTo(4));
            Assert.That(router.SuppressedCooldownCount, Is.EqualTo(1));
            Assert.That(router.DroppedRequestCount, Is.EqualTo(1));
            Assert.That(router.EvictedLowerPriorityCount, Is.EqualTo(3));
            Assert.That(router.MergedCriticalCount, Is.EqualTo(1));
            router.Dispose();
        }

        [Test]
        public void DamageNumbersAggregateAtFixedCapacity()
        {
            root = new GameObject("G27DamageNumbers");
            var canvas = new GameObject("Canvas").AddComponent<Canvas>();
            canvas.transform.SetParent(root.transform, false);
            var pool = new DamageNumberPool(canvas, 12, 4);
            for (var index = 0; index < 20; index++) pool.Spawn(Vector2.zero, index + 1, index == 19);

            Assert.That(pool.CreatedCount, Is.EqualTo(12));
            Assert.That(pool.ActiveCount, Is.EqualTo(12));
            Assert.That(pool.AggregatedCount, Is.EqualTo(8));
            pool.Dispose();
        }

        [Test]
        public void BoundedPoolSteadyTickDoesNotAllocate()
        {
            root = new GameObject("G27Allocation");
            var texture = new Texture2D(1, 1);
            var sprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), Vector2.one * 0.5f);
            var pool = new VfxRequestPool(root.transform, sprite, 8);
            var style = Style(PresentationPriority.Combat, ProceduralShape.Circle);
            for (var index = 0; index < 8; index++)
                pool.TrySpawn(new ProceduralVfxRequest(Vector2.zero, style, 1f, 100f));
            for (var index = 0; index < 10; index++) pool.Tick(0.01f);

            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var index = 0; index < 1000; index++) pool.Tick(0.01f);
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(allocated, Is.Zero);
            pool.Dispose();
            Object.DestroyImmediate(sprite);
            Object.DestroyImmediate(texture);
        }

        private static ProceduralPresentationStyle Style(PresentationPriority priority, ProceduralShape shape) =>
            new ProceduralPresentationStyle(
                shape, Color.white, Color.black, Vector2.one, priority,
                PresentationAudioCue.Hit, priority == PresentationPriority.CriticalDanger);

        private static ContentRegistry LoadRegistry()
        {
            var catalog = LoadCatalog();
            var registry = new ContentRegistry();
            var load = registry.Load(new[] { catalog }, new ContentVersion(0, 1, 0));
            Assert.That(load.IsSuccess, Is.True, load.Error.ToString());
            return registry;
        }

        private static BakedContentCatalog LoadCatalog()
        {
            var path = Path.Combine(UnityEngine.Application.dataPath,
                "GameAssets/Placeholder/QinglanDemo/QinglanDemoContentPack.baked.json");
            var dto = JsonUtility.FromJson<BakedContentCatalogDto>(File.ReadAllText(path));
            var catalog = dto.ToCatalog();
            Assert.That(catalog.IsSuccess, Is.True, catalog.Error.ToString());
            return catalog.Value;
        }

        private static ContentId Id(string value) => ContentId.Create(value).Value;
    }
}
