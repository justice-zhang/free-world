using System;
using System.IO;
using System.Linq;
using Game.Application;
using Game.Content.Runtime;
using Game.Core;
using Game.Infrastructure;
using Game.Platform.Null;
using Game.Presentation;
using Game.Simulation;
using Game.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Game.Tests.EditMode
{
    public sealed class QinglanG26UiInputTests
    {
        private GameObject root;

        [TearDown]
        public void TearDown()
        {
            if (root != null) Object.DestroyImmediate(root);
        }

        [Test]
        public void SettingsV2MigratesToIndependentV3DefaultsAndRoundTripsAllAccessibilityFields()
        {
            var codec = new UnityJsonSaveCodec();
            const string v2 = "{\"schemaVersion\":2,\"gameVersion\":\"0.1.0\",\"localeCode\":\"zh-Hans\",\"stickDeadzone\":0.2,\"vibrationIntensity\":0.5,\"screenShakeEnabled\":false,\"flashIntensity\":0.25,\"damageNumbersEnabled\":false,\"autoAim\":3,\"bindingOverrides\":[]}";
            var migrated = codec.DecodeSettings(codec.EncodeRawPayload(SaveDocumentKind.Settings, 2, v2));

            Assert.That(migrated.IsSuccess, Is.True, migrated.Diagnostic.MessageKey);
            Assert.That(migrated.Value.SchemaVersion, Is.EqualTo(3));
            Assert.That(migrated.Value.FontScale, Is.EqualTo(1f));
            Assert.That(migrated.Value.ColorVision, Is.EqualTo(ColorVisionMode.Standard));
            Assert.That(migrated.Value.MasterVolume, Is.EqualTo(1f));
            Assert.That(migrated.Value.SubtitlesEnabled, Is.True);

            var full = new SettingsSaveData(
                "en", 0.3f, 0.25f, false, 0.2f, false, AutoAimStrategy.MovementDirection,
                1.5f, ColorVisionMode.HighContrast, 0.75f, 0.5f, 0.25f, 0f, false,
                new[] { new SavedBindingOverride("UI/Submit", 0, "<Keyboard>/numpadEnter") });
            var encoded = codec.Encode(full);
            var decoded = codec.DecodeSettings(encoded.Data);

            Assert.That(encoded.IsSuccess, Is.True, encoded.Diagnostic.MessageKey);
            Assert.That(decoded.IsSuccess, Is.True, decoded.Diagnostic.MessageKey);
            Assert.That(decoded.Value.FontScale, Is.EqualTo(1.5f));
            Assert.That(decoded.Value.ColorVision, Is.EqualTo(ColorVisionMode.HighContrast));
            Assert.That(decoded.Value.MasterVolume, Is.EqualTo(0.75f));
            Assert.That(decoded.Value.MusicVolume, Is.EqualTo(0.5f));
            Assert.That(decoded.Value.AmbienceVolume, Is.EqualTo(0.25f));
            Assert.That(decoded.Value.EffectsVolume, Is.Zero);
            Assert.That(decoded.Value.SubtitlesEnabled, Is.False);
        }

        [Test]
        public void InputContractHasAllMapsActionsBindingsAndRejectsConflicts()
        {
            root = new GameObject("G26Input");
            var router = root.AddComponent<M7InputRouter>();
            router.Initialize();

            Assert.That(router.Actions.FindAction("Gameplay/Move"), Is.Not.Null);
            Assert.That(router.Actions.FindAction("Gameplay/Map"), Is.Not.Null);
            Assert.That(router.Actions.FindAction("Gameplay/Pause"), Is.Not.Null);
            Assert.That(router.Actions.FindAction("Gameplay/Interact"), Is.Not.Null);
            Assert.That(router.Actions.FindAction("UI/Navigate"), Is.Not.Null);
            Assert.That(router.Actions.FindAction("UI/Submit"), Is.Not.Null);
            Assert.That(router.Actions.FindAction("UI/Cancel"), Is.Not.Null);
            Assert.That(router.Actions.FindAction("UI/Tab"), Is.Not.Null);
            Assert.That(router.Actions.FindAction("UI/Page"), Is.Not.Null);
            Assert.That(router.Actions.FindAction("Gameplay/Move").bindings.Any(x => x.path == "<Keyboard>/upArrow"), Is.True);
            Assert.That(router.Actions.FindAction("UI/Navigate").bindings.Any(x => x.path == "<Mouse>/scroll"), Is.True);
            Assert.That(router.Actions.FindAction("UI/Submit").bindings.Any(x => x.path == "<Mouse>/leftButton"), Is.True);
            Assert.That(router.ApplyBindingOverride("UI/Submit", 0, "<Keyboard>/escape"), Is.False);
            Assert.That(router.LastRebindDiagnosticKey, Is.EqualTo("ui.qinglan.rebind.conflict"));
            Assert.That(router.ApplyBindingOverride("UI/Submit", 0, "<Keyboard>/w"), Is.False,
                "composite movement parts must participate in conflict detection");
            Assert.That(router.ApplyBindingOverride("UI/Submit", 0, "<Keyboard>/numpadEnter"), Is.True);
            Assert.That(router.LastRebindDiagnosticKey, Is.EqualTo("ui.qinglan.rebind.applied"));
        }

        [Test]
        public void PageFocusSkipsDisabledItemsAndRestoresAVisibleSelection()
        {
            var model = new QinglanPageViewModel(4);
            model.Reset(QinglanUiPageId.Hub, "ui.qinglan.hub.title");
            model.Add(new QinglanUiOption("locked", "ui.qinglan.hub.locked", "", QinglanUiCommand.None, false));
            model.Add(new QinglanUiOption("first", "ui.qinglan.hub.start_again", "", QinglanUiCommand.StartAgain));
            model.Add(new QinglanUiOption("hidden", "ui.qinglan.hub.locked", "", QinglanUiCommand.None, false));
            model.Add(new QinglanUiOption("second", "ui.qinglan.hub.return_title", "", QinglanUiCommand.ReturnToTitle));

            model.RestoreSelection(0);
            Assert.That(model.SelectedIndex, Is.EqualTo(1));
            Assert.That(model.MoveSelection(1), Is.True);
            Assert.That(model.SelectedIndex, Is.EqualTo(3));
            Assert.That(model.MoveSelection(1), Is.True);
            Assert.That(model.SelectedIndex, Is.EqualTo(1));
        }

        [Test]
        public void ProceduralCanvasSupportsScaleColorShapeAndLocalizedHud()
        {
            root = new GameObject("G26UiRoot");
            var view = root.AddComponent<QinglanRuntimeUiRoot>();
            view.Initialize(new EchoLocalization(), id => "content." + id + ".name");
            var page = new QinglanPageViewModel();
            page.Reset(QinglanUiPageId.Settings, "ui.qinglan.settings.title", "ui.qinglan.settings.description");
            page.Add(new QinglanUiOption("font", "ui.qinglan.settings.font_scale", "", QinglanUiCommand.CycleSetting, true, "150%"));
            page.RestoreSelection(0);
            view.ShowPage(page);
            var settings = new AccessibilitySettings();
            settings.SetFontScale(1.5f);
            settings.SetColorVision(ColorVisionMode.HighContrast);
            settings.SetFlashIntensity(0f);
            settings.SetVibrationIntensity(0f);
            settings.SetDamageNumbersEnabled(false);
            view.ApplyAccessibility(settings);

            var snapshot = new RunUiSnapshot();
            snapshot.Health = 75f;
            snapshot.MaximumHealth = 100f;
            snapshot.Level = 3;
            snapshot.Experience = 4f;
            snapshot.RequiredExperience = 10f;
            snapshot.MechanicTier = 2;
            snapshot.MechanicValue = 18f;
            snapshot.AddBuild("qinglan.skill.test", 2, 8, 1);
            snapshot.AddMap("qinglan.objective.test", 1, 3, 0.5f);
            view.ShowHud(snapshot);

            Assert.That(view.SharedCanvas, Is.Not.Null);
            Assert.That(view.RenderedPageText, Does.Contain("loc:ui.qinglan.settings.title"));
            Assert.That(view.RenderedHudText, Does.Contain("loc:content.qinglan.skill.test.name"));
            Assert.That(view.RenderedHudText, Does.Contain("50%"));
            var texts = root.GetComponentsInChildren<Text>();
            Assert.That(texts.Max(x => x.fontSize), Is.GreaterThanOrEqualTo(36));
            Assert.That(texts.Any(x => x.text.Contains("▲")), Is.True,
                "danger communication must retain a non-color shape channel");
            Assert.That(view.SupportsCharacter('剑'), Is.True, "the runtime font fallback must cover Simplified Chinese");
        }

        [Test]
        public void RealQinglanRunCopiesHudTruthIntoReusableBufferWithoutSteadyAllocations()
        {
            var catalog = LoadCatalog();
            var state = new GameStateMachine();
            var application = new GameApplication(new NullPlatformFacade(), state);
            var initialized = application.Initialize(new[] { catalog }, new ContentVersion(0, 1, 0));
            Assert.That(initialized.IsSuccess, Is.True, initialized.Error.ToString());
            var factory = new QinglanDemoRunFactory(application);
            var descriptor = factory.CreateDescriptor(0x4732365549485544UL, 0x473236534E415053UL);
            Assert.That(descriptor.IsSuccess, Is.True, descriptor.Error.ToString());
            var flow = new DemoRunCoordinator(state, factory);
            flow.ShowCharacterSelect();
            flow.ShowMapSelect();
            flow.BeginRun(descriptor.Value);
            flow.Tick(0d);
            var snapshot = new RunUiSnapshot();
            Assert.That(flow.Session.CaptureUiSnapshot(snapshot), Is.True);
            Assert.That(snapshot.BuildCount, Is.GreaterThanOrEqualTo(1));
            Assert.That(snapshot.MapCount, Is.EqualTo(11));
            Assert.That(snapshot.MaximumHealth, Is.GreaterThan(0f));

            for (var index = 0; index < 10; index++) flow.Session.CaptureUiSnapshot(snapshot);
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var index = 0; index < 500; index++) flow.Session.CaptureUiSnapshot(snapshot);
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(allocated, Is.Zero, "reusing the HUD projection must not allocate on steady refresh");
            flow.Dispose();
        }

        [Test]
        public void HeldInteractionUsesRunSessionCommandBoundaryToCompleteNearbyMapObjective()
        {
            var catalog = LoadCatalog();
            var state = new GameStateMachine();
            var application = new GameApplication(new NullPlatformFacade(), state);
            var initialized = application.Initialize(new[] { catalog }, new ContentVersion(0, 1, 0));
            Assert.That(initialized.IsSuccess, Is.True, initialized.Error.ToString());
            Assert.That(application.ContentRegistry.TryGet(
                ContentId.Create(QinglanDemoRunFactory.MapId).Value,
                out RuntimeMapDefinition map), Is.True);
            Assert.That(application.ContentRegistry.TryGet(
                map.ObjectiveIds[0], out RuntimeMapObjectiveDefinition objective), Is.True);

            var modules = SkillModuleRegistry.CreateDefault();
            var skills = SkillRuntimeCatalog.Build(application.ContentRegistry, modules);
            var enemies = EnemyRuntimeCatalog.Build(application.ContentRegistry);
            var builds = BuildRuntimeCatalog.Build(application.ContentRegistry, modules);
            Assert.That(skills.IsSuccess, Is.True, skills.Error.ToString());
            Assert.That(enemies.IsSuccess, Is.True, enemies.Error.ToString());
            Assert.That(builds.IsSuccess, Is.True, builds.Error.ToString());
            var hub = new QinglanRuntimeHub();
            var mapInitialized = hub.MapObjectives.Initialize(application.ContentRegistry, map.Id, 0x473236494E544552UL);
            Assert.That(mapInitialized.IsSuccess, Is.True, mapInitialized.Error.ToString());
            var world = new SimulationWorld(
                hub, 0x473236494E505554UL, 256, 2f, SimulationPipeline.CreateQinglanDemo(),
                new RuntimeStatusCatalog(application.ContentRegistry), null,
                new SkillRuntime(skills.Value, 7UL, 256),
                new EnemyRuntime(enemies.Value, DifficultySnapshot.Default, 256));
            var position = System.Numerics.Vector2.Zero;
            for (var index = 0; index < map.Anchors.Count; index++)
                if (map.Anchors[index].Id == objective.AnchorIds[0]) position = map.Anchors[index].Position;
            var player = world.CreateActor(
                SimulationEntityState.Create(position, System.Numerics.Vector2.Zero),
                ActorCombatInitialization.CreateDefault());
            world.SetPlayer(player);
            world.InitializeProgression(builds.Value, player, 9UL, 0x473236494E544552UL);
            Assert.That(hub.MapObjectives.RevealObjective(objective.Id), Is.EqualTo(MapCommandStatus.Applied));
            Assert.That(hub.MapObjectives.MakeObjectiveAvailable(objective.Id), Is.EqualTo(MapCommandStatus.Applied));

            var session = new RunSession(world, player, state);
            session.SetInteractHeld(true);
            for (var tick = 0; tick < 125 && !hub.MapObjectives.IsObjectiveCompleted(objective.Id); tick++)
                session.Advance(SimulationClock.TickDurationSeconds);

            Assert.That(hub.MapObjectives.IsObjectiveCompleted(objective.Id), Is.True);
            var snapshot = new RunUiSnapshot();
            Assert.That(session.CaptureUiSnapshot(snapshot), Is.True);
            Assert.That(Enumerable.Range(0, snapshot.MapCount).Any(index =>
                snapshot.GetMapAt(index).ContentId == objective.Id.Value &&
                snapshot.GetMapAt(index).Progress >= 1f), Is.True);
            QinglanDemoRunFactory.DisposeWorld(world);
        }

        [Test]
        public void UiAssemblyStillExposesNoSimulationTypes()
        {
            var uiTypes = typeof(QinglanDemoPresenter).Assembly.GetTypes();
            foreach (var type in uiTypes)
            {
                Assert.That(type.GetFields().Any(field => field.FieldType.Namespace == typeof(SimulationWorld).Namespace),
                    Is.False, type.FullName);
                Assert.That(type.GetMethods().Any(method => method.ReturnType.Namespace == typeof(SimulationWorld).Namespace),
                    Is.False, type.FullName);
            }
        }

        private static BakedContentCatalog LoadCatalog()
        {
            var path = Path.Combine(
                UnityEngine.Application.dataPath,
                "GameAssets/Placeholder/QinglanDemo/QinglanDemoContentPack.baked.json");
            var dto = JsonUtility.FromJson<BakedContentCatalogDto>(File.ReadAllText(path));
            var result = dto.ToCatalog();
            Assert.That(result.IsSuccess, Is.True, result.Error.ToString());
            return result.Value;
        }

        private sealed class EchoLocalization : ILocalizationService
        {
            public string SelectedLocaleCode => "en";
            public string Resolve(string localizationKey) => "loc:" + localizationKey;
            public bool SelectLocale(string localeCode) => true;
            public bool SelectNextLocale() => true;
        }
    }
}
