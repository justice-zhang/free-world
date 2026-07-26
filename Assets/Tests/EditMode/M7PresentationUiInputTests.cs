using System;
using System.Linq;
using Game.Application;
using Game.Core;
using Game.Presentation;
using Game.Simulation;
using Game.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using NumericsVector2 = System.Numerics.Vector2;

namespace Game.Tests.EditMode
{
    public sealed class M7PresentationUiInputTests
    {
        private GameObject root;

        [TearDown]
        public void TearDown()
        {
            if (root != null) Object.DestroyImmediate(root);
        }

        [Test]
        public void EntityViewBindsInterpolatesAndRejectsDifferentHandle()
        {
            root = new GameObject("M7ViewTest");
            var view = root.AddComponent<ActorView>();
            var entity = new SpatialEntity(EntityKind.Actor, new EntityHandle(2, 1));
            var other = new SpatialEntity(EntityKind.Actor, new EntityHandle(2, 2));
            view.Bind(entity);
            var entry = new RenderEntitySnapshot(
                entity,
                new NumericsVector2(0f, 0f),
                new NumericsVector2(10f, 4f),
                0f,
                Mathf.PI,
                SimulationStateFlags.Active,
                SimulationStateFlags.Active | SimulationStateFlags.Moving);

            Assert.That(view.Apply(entry, 0.5f, 7), Is.True);
            Assert.That(view.transform.position.x, Is.EqualTo(5f).Within(0.001f));
            Assert.That(view.transform.position.y, Is.EqualTo(2f).Within(0.001f));
            Assert.That(view.LastSnapshotTick, Is.EqualTo(7));
            var wrong = new RenderEntitySnapshot(other, NumericsVector2.Zero, NumericsVector2.One, 0f, 0f,
                SimulationStateFlags.Active, SimulationStateFlags.Active);
            Assert.That(view.Apply(wrong, 1f, 8), Is.False);
        }

        [Test]
        public void CoordinatorPoolsAllKindsAndRejectsStaleRelease()
        {
            root = new GameObject("M7CoordinatorTest");
            var canvas = new GameObject("Canvas").AddComponent<Canvas>();
            canvas.transform.SetParent(root.transform);
            var coordinator = root.AddComponent<PresentationCoordinator>();
            coordinator.Initialize(canvas, new AccessibilitySettings());
            var world = new SimulationWorld();
            var actor = world.CreateActor(SimulationEntityState.Create(NumericsVector2.Zero, NumericsVector2.Zero));
            world.CreateProjectile(SimulationEntityState.Create(NumericsVector2.One, NumericsVector2.Zero));
            world.CreateArea(SimulationEntityState.Create(NumericsVector2.UnitY, NumericsVector2.Zero));
            world.CreatePickup(SimulationEntityState.Create(new NumericsVector2(2f, 2f), NumericsVector2.Zero));
            var runner = new FixedTickRunner(world);
            runner.Advance(SimulationClock.TickDurationSeconds);

            coordinator.Sync(world.RenderSnapshot, 0.5f);

            Assert.That(coordinator.ActiveViewCount, Is.EqualTo(4));
            Assert.That(coordinator.MissingProfileFallbackCount, Is.EqualTo(4));
            Assert.That(coordinator.TryGetView(new SpatialEntity(EntityKind.Actor, actor), out var actorView), Is.True);
            Assert.That(actorView.GetComponent<SpriteRenderer>().sprite, Is.Not.Null, "missing profile must use fallback");
            Assert.That(coordinator.Release(new SpatialEntity(EntityKind.Actor, new EntityHandle(actor.Index, (ushort)(actor.Generation + 1)))), Is.False);
            Assert.That(coordinator.ActiveViewCount, Is.EqualTo(4));
            coordinator.Clear();
            Assert.That(coordinator.ActiveViewCount, Is.Zero);
        }

        [Test]
        public void InterpolationClampsAndUsesShortestFacingArc()
        {
            var entity = new SpatialEntity(EntityKind.Projectile, new EntityHandle(0, 1));
            var snapshot = new RenderEntitySnapshot(
                entity,
                NumericsVector2.Zero,
                new NumericsVector2(6f, -2f),
                170f * Mathf.Deg2Rad,
                -170f * Mathf.Deg2Rad,
                SimulationStateFlags.Active,
                SimulationStateFlags.Active);

            Assert.That(snapshot.InterpolatePosition(-1f), Is.EqualTo(NumericsVector2.Zero));
            Assert.That(snapshot.InterpolatePosition(2f), Is.EqualTo(new NumericsVector2(6f, -2f)));
            Assert.That(Mathf.Abs(snapshot.InterpolateFacing(0.5f)), Is.EqualTo(Mathf.PI).Within(0.001f));
        }

        [Test]
        public void InputMapsSwitchAndKeyboardGamepadBindingsExist()
        {
            root = new GameObject("M7InputTest");
            var router = root.AddComponent<M7InputRouter>();
            router.Initialize();

            Assert.That(router.Actions.actionMaps.Select(value => value.name),
                Is.EquivalentTo(new[] { "Gameplay", "UI", "Debug" }));
            Assert.That(router.Actions.FindAction("Gameplay/Move").bindings.Any(value => value.path.Contains("Keyboard")), Is.True);
            Assert.That(router.Actions.FindAction("Gameplay/Move").bindings.Any(value => value.path.Contains("Gamepad")), Is.True);
            Assert.That(router.Actions.FindAction("UI/Submit").bindings.Any(value => value.path.Contains("Gamepad")), Is.True);
            router.SetGameplayMode(true);
            Assert.That(router.GameplayMap.enabled, Is.True);
            Assert.That(router.UiMap.enabled, Is.False);
            router.SetGameplayMode(false);
            Assert.That(router.GameplayMap.enabled, Is.False);
            Assert.That(router.UiMap.enabled, Is.True);
            Assert.That(router.ApplyBindingOverride("UI/Submit", 0, "<Keyboard>/numpadEnter"), Is.True);
            Assert.That(router.Actions.FindAction("UI/Submit").bindings[0].overridePath, Is.EqualTo("<Keyboard>/numpadEnter"));
        }

        [Test]
        public void AccessibilitySettingsClampAndToggle()
        {
            var settings = new AccessibilitySettings();
            settings.SetStickDeadzone(2f);
            settings.SetVibrationIntensity(-1f);
            settings.SetFlashIntensity(0.4f);
            settings.SetScreenShakeEnabled(false);
            settings.SetDamageNumbersEnabled(false);
            settings.SetAutoAim(AutoAimStrategy.MovementDirection);

            Assert.That(settings.StickDeadzone, Is.EqualTo(0.95f));
            Assert.That(settings.VibrationIntensity, Is.Zero);
            Assert.That(settings.FlashIntensity, Is.EqualTo(0.4f));
            Assert.That(settings.ScreenShakeEnabled, Is.False);
            Assert.That(settings.DamageNumbersEnabled, Is.False);
            Assert.That(settings.AutoAim, Is.EqualTo(AutoAimStrategy.MovementDirection));
        }

        [Test]
        public void UiAssemblyAndViewsExposeNoSimulationStoreWrites()
        {
            var forbidden = new[] { typeof(SimulationWorld), typeof(ActorStore), typeof(ProjectileStore), typeof(AreaStore), typeof(PickupStore) };
            var uiTypes = typeof(GameFlowPresenter).Assembly.GetTypes();
            foreach (var type in uiTypes)
            foreach (var method in type.GetMethods())
                Assert.That(forbidden.Contains(method.ReturnType), Is.False, type.FullName + "." + method.Name);

            var viewMethods = typeof(EntityView).GetMethods();
            Assert.That(viewMethods.Any(value => value.Name.Contains("Damage", StringComparison.OrdinalIgnoreCase)), Is.False);
        }

        [Test]
        public void CameraBoundsAndEffectsToggleAreHonored()
        {
            root = new GameObject("M7CameraTest");
            var target = new GameObject("Target");
            target.transform.SetParent(root.transform);
            target.transform.position = new Vector3(20f, -20f, 0f);
            var cameraObject = new GameObject("Camera");
            cameraObject.transform.SetParent(root.transform);
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            var rig = cameraObject.AddComponent<PresentationCameraRig>();
            rig.SetTarget(target.transform);
            rig.SetBounds(new Rect(-5f, -4f, 10f, 8f));
            rig.EffectsEnabled = false;
            rig.RequestShake(3f, 1f);
            rig.TickCamera(0.2f);

            Assert.That(rig.transform.position.x, Is.EqualTo(5f));
            Assert.That(rig.transform.position.y, Is.EqualTo(-4f));
        }

        [Test]
        public void CombatEventsBecomePooledHitAndDeathRequests()
        {
            root = new GameObject("M7CombatPresentationTest");
            var canvas = new GameObject("Canvas").AddComponent<Canvas>();
            canvas.transform.SetParent(root.transform);
            var coordinator = root.AddComponent<PresentationCoordinator>();
            coordinator.Initialize(canvas, new AccessibilitySettings());
            var world = new SimulationWorld();
            var source = world.CreateActor(
                SimulationEntityState.Create(NumericsVector2.Zero, NumericsVector2.Zero),
                ActorCombatInitialization.CreateDefault(100f));
            var target = world.CreateActor(
                SimulationEntityState.Create(NumericsVector2.One, NumericsVector2.Zero),
                ActorCombatInitialization.CreateDefault(5f));
            var sourceId = ContentId.Create("test.presentation.hit").Value;
            world.QueueDamage(new DamagePacket(
                new SpatialEntity(EntityKind.Actor, source),
                new SpatialEntity(EntityKind.Actor, target),
                sourceId,
                DamageType.Physical,
                DamageTags.Direct,
                10f,
                false,
                1f,
                NumericsVector2.Zero,
                NumericsVector2.One,
                0));
            var runner = new FixedTickRunner(world);
            runner.Advance(SimulationClock.TickDurationSeconds);

            coordinator.ConsumeLatestEvents(world.RenderSnapshot.Tick, world.Events, world.CombatEvents);
            coordinator.Sync(world.RenderSnapshot, 1f);

            Assert.That(coordinator.LastHitRequestCount, Is.EqualTo(1));
            Assert.That(coordinator.LastDeathRequestCount, Is.EqualTo(1));
            Assert.That(coordinator.ActiveVfxCount, Is.EqualTo(2));
            Assert.That(coordinator.ActiveDamageNumberCount, Is.EqualTo(1));
            coordinator.TickEffects(1f);
            Assert.That(coordinator.ActiveVfxCount, Is.Zero);
            Assert.That(coordinator.ActiveDamageNumberCount, Is.Zero);
        }

        [Test]
        public void PresentationRequestBufferSupportsStatusRequestsWithoutSimulationMutation()
        {
            var buffer = new PresentationRequestBuffer(1);
            var target = new SpatialEntity(EntityKind.Actor, new EntityHandle(3, 1));
            var status = new PresentationRequest(
                PresentationRequestType.Status,
                target,
                new NumericsVector2(2f, 4f),
                2f,
                false,
                ContentId.Create("test.status.presentation").Value);

            buffer.Add(status);

            Assert.That(buffer.Count, Is.EqualTo(1));
            Assert.That(buffer.GetAt(0).Type, Is.EqualTo(PresentationRequestType.Status));
            Assert.That(buffer.GetAt(0).Target, Is.EqualTo(target));
            buffer.Clear();
            Assert.That(buffer.Count, Is.Zero);
        }
    }
}
