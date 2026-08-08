using System.Collections;
using Game.Application;
using Game.Infrastructure;
using Game.Simulation;
using Game.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Game.Tests.PlayMode
{
    public sealed class QinglanG26UiInputPlayModeTests
    {
        private InputTestFixture fixture;
        private Keyboard keyboard;
        private Gamepad gamepad;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            DestroyBootstrapInstances();
            yield return null;
            fixture = new InputTestFixture();
            fixture.Setup();
            keyboard = InputSystem.AddDevice<Keyboard>();
            gamepad = InputSystem.AddDevice<Gamepad>();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            DestroyBootstrapInstances();
            yield return null;
            fixture?.TearDown();
            fixture = null;
        }

        [UnityTest]
        public IEnumerator KeyboardOnlyCompletesTitleRunChoicePauseSettingsResultHubAndRestart()
        {
            yield return LoadBootstrapScene();
            var host = RequireHost();

            Tap(keyboard.enterKey);
            AssertPage(host, DemoFlowStage.CharacterSelect, QinglanUiPageId.CharacterSelect);
            Tap(keyboard.enterKey);
            AssertPage(host, DemoFlowStage.MapSelect, QinglanUiPageId.MapSelect);
            Tap(keyboard.enterKey);
            Assert.That(host.Ui.CurrentPage, Is.EqualTo(QinglanUiPageId.Loadout));
            Tap(keyboard.enterKey);
            StartPreparedRun(host);

            fixture.Press(keyboard.wKey);
            InputSystem.Update();
            Assert.That(host.Input.Move.y, Is.GreaterThan(0f));
            host.TickRuntime(SimulationClock.TickDurationSeconds);
            fixture.Release(keyboard.wKey);
            InputSystem.Update();

            Tap(keyboard.mKey);
            Assert.That(host.Ui.CurrentPage, Is.EqualTo(QinglanUiPageId.RunHud));
            Assert.That(host.Input.GameplayMap.enabled, Is.False, "map overlay must disable gameplay input");
            Assert.That(host.Input.UiMap.enabled, Is.True);
            Tap(keyboard.escapeKey);
            Assert.That(host.Input.GameplayMap.enabled, Is.True);

            Tap(keyboard.escapeKey);
            AssertPage(host, DemoFlowStage.UserPaused, QinglanUiPageId.Pause);
            Tap(keyboard.downArrowKey);
            Tap(keyboard.enterKey);
            Assert.That(host.Ui.CurrentPage, Is.EqualTo(QinglanUiPageId.Settings));
            Assert.That(host.Input.GameplayMap.enabled, Is.False, "settings overlay must disable gameplay input");
            Tap(keyboard.downArrowKey);
            Tap(keyboard.enterKey);
            Tap(keyboard.escapeKey);
            Assert.That(host.Ui.CurrentPage, Is.EqualTo(QinglanUiPageId.Pause));
            Tap(keyboard.upArrowKey);
            Tap(keyboard.enterKey);
            host.TickRuntime(0d);
            Assert.That(host.Flow.Stage, Is.EqualTo(DemoFlowStage.Active));

            Tap(keyboard.f2Key);
            host.TickRuntime(SimulationClock.TickDurationSeconds);
            AssertPage(host, DemoFlowStage.UpgradePaused, QinglanUiPageId.LevelUpChoice);
            Assert.That(host.Input.GameplayMap.enabled, Is.False, "choice popup must disable gameplay input");
            Assert.That(host.CurrentPage.GetOptionAt(0).TagKey, Is.Not.Empty);
            Assert.That(host.CurrentPage.GetOptionAt(0).RelationKey, Is.Not.Empty);
            Tap(keyboard.enterKey);
            host.TickRuntime(0d);
            Assert.That(host.Flow.Stage, Is.EqualTo(DemoFlowStage.Active));

            Tap(keyboard.escapeKey);
            Tap(keyboard.downArrowKey);
            Tap(keyboard.downArrowKey);
            Tap(keyboard.enterKey);
            host.TickRuntime(0d);
            AssertPage(host, DemoFlowStage.Result, QinglanUiPageId.RunResult);
            yield return WaitForCommit(host);
            FocusWithKeyboard(host, QinglanUiCommand.ContinueToHub);
            Tap(keyboard.enterKey);
            AssertPage(host, DemoFlowStage.Hub, QinglanUiPageId.Hub);
            Assert.That(host.CurrentPage.OptionCount, Is.GreaterThanOrEqualTo(6),
                "hub must project four facilities plus navigation actions");
            FocusWithKeyboard(host, QinglanUiCommand.OpenFacility);
            Tap(keyboard.enterKey);
            Assert.That(host.Ui.CurrentPage, Is.EqualTo(QinglanUiPageId.HubFacility));
            FocusWithKeyboard(host, QinglanUiCommand.ResetLoadout);
            Tap(keyboard.enterKey);
            Assert.That(host.Ui.CurrentPage, Is.EqualTo(QinglanUiPageId.LoadoutConfirmation));
            Tap(keyboard.escapeKey);
            Assert.That(host.Ui.CurrentPage, Is.EqualTo(QinglanUiPageId.HubFacility));
            Tap(keyboard.escapeKey);
            Assert.That(host.Ui.CurrentPage, Is.EqualTo(QinglanUiPageId.Hub));
            FocusWithKeyboard(host, QinglanUiCommand.StartAgain);
            Tap(keyboard.enterKey);
            AssertPage(host, DemoFlowStage.CharacterSelect, QinglanUiPageId.CharacterSelect);
        }

        [UnityTest]
        public IEnumerator GamepadOnlyCompletesFlowAndDisconnectPausesWithValidFocus()
        {
            yield return LoadBootstrapScene();
            var host = RequireHost();

            Tap(gamepad.buttonSouth);
            AssertPage(host, DemoFlowStage.CharacterSelect, QinglanUiPageId.CharacterSelect);
            Tap(gamepad.buttonSouth);
            AssertPage(host, DemoFlowStage.MapSelect, QinglanUiPageId.MapSelect);
            Tap(gamepad.buttonSouth);
            Assert.That(host.Ui.CurrentPage, Is.EqualTo(QinglanUiPageId.Loadout));
            Tap(gamepad.buttonSouth);
            StartPreparedRun(host);

            fixture.Set(gamepad.leftStick, Vector2.up);
            InputSystem.Update();
            Assert.That(host.Input.Move.y, Is.GreaterThan(0f));
            host.TickRuntime(SimulationClock.TickDurationSeconds);
            fixture.Set(gamepad.leftStick, Vector2.zero);
            InputSystem.Update();

            gamepad = InputSystem.AddDevice<Gamepad>();
            InputSystem.Update();
            Assert.That(host.Flow.Stage, Is.EqualTo(DemoFlowStage.Active),
                "connecting another gamepad must restore focus without pausing an active run");
            InputSystem.RemoveDevice(gamepad);
            InputSystem.Update();
            AssertPage(host, DemoFlowStage.UserPaused, QinglanUiPageId.Pause);
            Assert.That(host.CurrentPage.GetOptionAt(host.CurrentPage.SelectedIndex).Enabled, Is.True,
                "disconnect focus must remain on a visible enabled option");
            gamepad = InputSystem.AddDevice<Gamepad>();
            InputSystem.Update();
            Assert.That(host.CurrentPage.GetOptionAt(host.CurrentPage.SelectedIndex).Enabled, Is.True);
            Tap(gamepad.buttonSouth);
            host.TickRuntime(0d);
            Assert.That(host.Flow.Stage, Is.EqualTo(DemoFlowStage.Active));

            Tap(gamepad.leftShoulder);
            host.TickRuntime(SimulationClock.TickDurationSeconds);
            AssertPage(host, DemoFlowStage.UpgradePaused, QinglanUiPageId.LevelUpChoice);
            Tap(gamepad.buttonSouth);
            host.TickRuntime(0d);
            Assert.That(host.Flow.Stage, Is.EqualTo(DemoFlowStage.Active));

            Tap(gamepad.startButton);
            Tap(gamepad.dpad.down);
            Tap(gamepad.dpad.down);
            Tap(gamepad.buttonSouth);
            host.TickRuntime(0d);
            AssertPage(host, DemoFlowStage.Result, QinglanUiPageId.RunResult);
            yield return WaitForCommit(host);
            FocusWithGamepad(host, QinglanUiCommand.ContinueToHub);
            Tap(gamepad.buttonSouth);
            AssertPage(host, DemoFlowStage.Hub, QinglanUiPageId.Hub);
            FocusWithGamepad(host, QinglanUiCommand.StartAgain);
            Tap(gamepad.buttonSouth);
            AssertPage(host, DemoFlowStage.CharacterSelect, QinglanUiPageId.CharacterSelect);
        }

        private void Tap(ButtonControl control)
        {
            fixture.Press(control);
            InputSystem.Update();
            fixture.Release(control);
            InputSystem.Update();
        }

        private static void StartPreparedRun(QinglanDemoRuntimeHost host)
        {
            Assert.That(host.Flow.Stage, Is.EqualTo(DemoFlowStage.Preparing));
            host.TickRuntime(0d);
            host.TickRuntime(SimulationClock.TickDurationSeconds);
            AssertPage(host, DemoFlowStage.Active, QinglanUiPageId.RunHud);
            Assert.That(host.Input.GameplayMap.enabled, Is.True);
        }

        private static IEnumerator WaitForCommit(QinglanDemoRuntimeHost host)
        {
            for (var index = 0; index < 60 && !host.Flow.LastCommit.IsSuccess; index++)
            {
                host.TickRuntime(0.1d);
                yield return null;
            }
            Assert.That(host.Flow.LastCommit.IsSuccess, Is.True,
                host.Flow.LastCommit.Diagnostic.MessageKey);
            host.TickRuntime(0.1d);
            yield return null;
        }

        private void FocusWithKeyboard(QinglanDemoRuntimeHost host, QinglanUiCommand command)
        {
            for (var attempt = 0; attempt <= host.CurrentPage.OptionCount; attempt++)
            {
                var option = host.CurrentPage.GetOptionAt(host.CurrentPage.SelectedIndex);
                if (option.Command == command && option.Enabled) return;
                Tap(keyboard.downArrowKey);
            }
            Assert.Fail("Could not focus command " + command + " with keyboard.");
        }

        private void FocusWithGamepad(QinglanDemoRuntimeHost host, QinglanUiCommand command)
        {
            for (var attempt = 0; attempt <= host.CurrentPage.OptionCount; attempt++)
            {
                var option = host.CurrentPage.GetOptionAt(host.CurrentPage.SelectedIndex);
                if (option.Command == command && option.Enabled) return;
                Tap(gamepad.dpad.down);
            }
            Assert.Fail("Could not focus command " + command + " with gamepad.");
        }

        private static QinglanDemoRuntimeHost RequireHost()
        {
            var host = Object.FindFirstObjectByType<QinglanDemoRuntimeHost>();
            Assert.That(host, Is.Not.Null);
            AssertPage(host, DemoFlowStage.Title, QinglanUiPageId.TitleProfile);
            return host;
        }

        private static void AssertPage(
            QinglanDemoRuntimeHost host,
            DemoFlowStage stage,
            QinglanUiPageId page)
        {
            Assert.That(host.Flow.Stage, Is.EqualTo(stage));
            Assert.That(host.Ui.CurrentPage, Is.EqualTo(page));
        }

        private static IEnumerator LoadBootstrapScene()
        {
            var operation = SceneManager.LoadSceneAsync("Bootstrap", LoadSceneMode.Single);
            while (!operation.isDone) yield return null;
            yield return null;
        }

        private static void DestroyBootstrapInstances()
        {
            var instances = Object.FindObjectsByType<GameBootstrapper>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (var index = 0; index < instances.Length; index++) Object.Destroy(instances[index].gameObject);
        }
    }
}
