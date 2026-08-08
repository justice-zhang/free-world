using System.Collections;
using Game.Application;
using Game.Infrastructure;
using Game.Presentation;
using Game.Simulation;
using Game.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Game.Tests.PlayMode
{
    public sealed class M7FullFlowPlayModeTests
    {
        private InputTestFixture inputFixture;
        private Keyboard keyboard;
        private Gamepad gamepad;
        private Key pressedKey;
        private GamepadButton pressedGamepadButton;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            DestroyBootstrapInstances();
            yield return null;
            inputFixture = new InputTestFixture();
            inputFixture.Setup();
            keyboard = InputSystem.AddDevice<Keyboard>();
            gamepad = InputSystem.AddDevice<Gamepad>();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            DestroyBootstrapInstances();
            yield return null;
            inputFixture?.TearDown();
            inputFixture = null;
        }

        [UnityTest]
        public IEnumerator KeyboardAndGamepadCompleteMenuUpgradePauseAndResultFlow()
        {
            yield return LoadBootstrapScene();
            var host = Object.FindFirstObjectByType<QinglanDemoRuntimeHost>();
            Assert.That(host, Is.Not.Null);
            Assert.That(host.Ui.CurrentPage, Is.EqualTo(QinglanUiPageId.TitleProfile));
            Assert.That(host.Input.Actions.FindAction("UI/Submit").controls.Count, Is.GreaterThan(0));

            PressKeyboard(Key.Enter);
            Assert.That(host.Flow.Stage, Is.EqualTo(DemoFlowStage.CharacterSelect));
            Assert.That(host.Ui.CurrentPage, Is.EqualTo(QinglanUiPageId.CharacterSelect));
            ReleaseKeyboard();

            PressGamepad(GamepadButton.South);
            Assert.That(host.Flow.Stage, Is.EqualTo(DemoFlowStage.MapSelect));
            Assert.That(host.Ui.CurrentPage, Is.EqualTo(QinglanUiPageId.MapSelect));
            ReleaseGamepad();

            PressKeyboard(Key.Enter);
            Assert.That(host.Ui.CurrentPage, Is.EqualTo(QinglanUiPageId.Loadout));
            ReleaseKeyboard();
            PressKeyboard(Key.Enter);
            Assert.That(host.Flow.Stage, Is.EqualTo(DemoFlowStage.Preparing));
            Assert.That(host.Ui.CurrentPage, Is.EqualTo(QinglanUiPageId.Loading));
            ReleaseKeyboard();
            host.TickRuntime(0d);
            host.TickRuntime(SimulationClock.TickDurationSeconds);

            Assert.That(host.Flow.Stage, Is.EqualTo(DemoFlowStage.Active));
            Assert.That(host.Ui.CurrentPage, Is.EqualTo(QinglanUiPageId.RunHud));
            Assert.That(host.Input.GameplayMap.enabled, Is.True);
            Assert.That(host.Presentation.ActiveViewCount, Is.GreaterThan(0));

            PressGamepad(GamepadButton.Start);
            host.TickRuntime(0d);
            Assert.That(host.Flow.Stage, Is.EqualTo(DemoFlowStage.UserPaused));
            Assert.That(host.Ui.CurrentPage, Is.EqualTo(QinglanUiPageId.Pause));
            Assert.That(host.Input.UiMap.enabled, Is.True);
            ReleaseGamepad();
            var pausedTick = host.Flow.Session.RenderSnapshot.Tick;
            yield return null;
            yield return null;
            Assert.That(host.Flow.Session.RenderSnapshot.Tick, Is.EqualTo(pausedTick));

            PressGamepad(GamepadButton.DpadDown);
            Assert.That(host.Ui.RenderedSelectedIndex, Is.EqualTo(1), "paused UI must remain responsive");
            ReleaseGamepad();
            PressGamepad(GamepadButton.DpadUp);
            ReleaseGamepad();
            PressGamepad(GamepadButton.South);
            host.TickRuntime(0d);
            Assert.That(host.Flow.Stage, Is.EqualTo(DemoFlowStage.Active));
            ReleaseGamepad();

            PressGamepad(GamepadButton.LeftShoulder);
            ReleaseGamepad();
            host.TickRuntime(SimulationClock.TickDurationSeconds);
            Assert.That(host.Flow.Stage, Is.EqualTo(DemoFlowStage.UpgradePaused));
            Assert.That(host.Ui.CurrentPage, Is.EqualTo(QinglanUiPageId.LevelUpChoice));
            Assert.That(host.Ui.RenderedOptionCount, Is.GreaterThanOrEqualTo(3));

            PressGamepad(GamepadButton.DpadDown);
            Assert.That(host.Ui.RenderedSelectedIndex, Is.EqualTo(1));
            ReleaseGamepad();
            PressGamepad(GamepadButton.South);
            host.TickRuntime(0d);
            Assert.That(host.Flow.Stage, Is.EqualTo(DemoFlowStage.Active));
            ReleaseGamepad();

            PressKeyboard(Key.Escape);
            host.TickRuntime(0d);
            Assert.That(host.Flow.Stage, Is.EqualTo(DemoFlowStage.UserPaused));
            ReleaseKeyboard();
            PressKeyboard(Key.DownArrow);
            ReleaseKeyboard();
            PressKeyboard(Key.DownArrow);
            ReleaseKeyboard();
            Assert.That(host.Ui.RenderedSelectedIndex, Is.EqualTo(2));
            PressKeyboard(Key.Enter);
            host.TickRuntime(0d);
            Assert.That(host.Flow.Stage, Is.EqualTo(DemoFlowStage.Result));
            Assert.That(host.Ui.CurrentPage, Is.EqualTo(QinglanUiPageId.RunResult));
            ReleaseKeyboard();

            for (var index = 0; index < 30 && !host.Flow.LastCommit.IsSuccess; index++)
            {
                host.TickRuntime(0.1d);
                yield return null;
            }
            Assert.That(host.Flow.LastCommit.IsSuccess, Is.True);
            host.TickRuntime(0.1d);
            yield return null;
            FocusCommand(host, QinglanUiCommand.ContinueToHub);
            PressGamepad(GamepadButton.South);
            ReleaseGamepad();
            Assert.That(host.Flow.Stage, Is.EqualTo(DemoFlowStage.Hub));
            FocusCommand(host, QinglanUiCommand.StartAgain);
            PressGamepad(GamepadButton.South);
            ReleaseGamepad();
            Assert.That(host.Flow.Stage, Is.EqualTo(DemoFlowStage.CharacterSelect));
        }

        [UnityTest]
        public IEnumerator DestroyingBootstrapReleasesViewsPoolsAndInputOwner()
        {
            yield return LoadBootstrapScene();
            var host = Object.FindFirstObjectByType<QinglanDemoRuntimeHost>();
            host.Flow.Execute(QinglanUiCommand.Start, "start", 0);
            host.Flow.Execute(QinglanUiCommand.Continue, "character", 0);
            host.Flow.Execute(QinglanUiCommand.OpenLoadout, "map", 0);
            host.Flow.Execute(QinglanUiCommand.BeginRun, "begin", 0);
            host.TickRuntime(0d);
            host.TickRuntime(SimulationClock.TickDurationSeconds);
            Assert.That(host.Presentation.ActiveViewCount, Is.GreaterThan(0));

            Object.Destroy(Object.FindFirstObjectByType<GameBootstrapper>().gameObject);
            yield return null;
            yield return null;

            Assert.That(Object.FindObjectsByType<QinglanDemoRuntimeHost>(FindObjectsInactive.Include, FindObjectsSortMode.None), Is.Empty);
            Assert.That(Object.FindObjectsByType<EntityView>(FindObjectsInactive.Include, FindObjectsSortMode.None), Is.Empty);
        }

        private void PressKeyboard(Key key)
        {
            pressedKey = key;
            inputFixture.Press(keyboard[key]);
            InputSystem.Update();
        }

        private void ReleaseKeyboard()
        {
            inputFixture.Release(keyboard[pressedKey]);
            InputSystem.Update();
        }

        private void PressGamepad(GamepadButton button)
        {
            pressedGamepadButton = button;
            inputFixture.Press(gamepad[button]);
            InputSystem.Update();
        }

        private void ReleaseGamepad()
        {
            inputFixture.Release(gamepad[pressedGamepadButton]);
            InputSystem.Update();
        }

        private void FocusCommand(QinglanDemoRuntimeHost host, QinglanUiCommand command)
        {
            for (var attempts = 0; attempts <= host.CurrentPage.OptionCount; attempts++)
            {
                var page = host.CurrentPage;
                var option = page.GetOptionAt(page.SelectedIndex);
                if (option.Command == command && option.Enabled) return;
                PressGamepad(GamepadButton.DpadDown);
                ReleaseGamepad();
            }
            Assert.Fail("Could not focus command " + command + ".");
        }

        private static IEnumerator LoadBootstrapScene()
        {
            var operation = SceneManager.LoadSceneAsync("Bootstrap", LoadSceneMode.Single);
            while (!operation.isDone) yield return null;
            yield return null;
        }

        private static void DestroyBootstrapInstances()
        {
            var all = Object.FindObjectsByType<GameBootstrapper>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (var index = 0; index < all.Length; index++) Object.Destroy(all[index].gameObject);
        }
    }
}
