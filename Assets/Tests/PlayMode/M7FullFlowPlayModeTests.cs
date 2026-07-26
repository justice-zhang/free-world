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
            var host = Object.FindFirstObjectByType<M7RuntimeHost>();
            Assert.That(host, Is.Not.Null);
            Assert.That(host.Ui.CurrentPage, Is.EqualTo(UiPageId.MainMenu));
            Assert.That(host.Input.Actions.FindAction("UI/Submit").controls.Count, Is.GreaterThan(0));

            PressKeyboard(Key.Enter);
            Assert.That(host.Flow.CurrentState, Is.EqualTo(GameState.CharacterSelect));
            Assert.That(host.Ui.CurrentPage, Is.EqualTo(UiPageId.CharacterSelect));
            ReleaseKeyboard();

            PressGamepad(GamepadButton.South);
            Assert.That(host.Flow.CurrentState, Is.EqualTo(GameState.MapSelect));
            Assert.That(host.Ui.CurrentPage, Is.EqualTo(UiPageId.MapSelect));
            ReleaseGamepad();

            PressKeyboard(Key.Enter);
            Assert.That(host.Flow.CurrentState, Is.EqualTo(GameState.Loading));
            Assert.That(host.Ui.CurrentPage, Is.EqualTo(UiPageId.Loading));
            ReleaseKeyboard();
            host.TickRuntime(0d);
            host.TickRuntime(SimulationClock.TickDurationSeconds);

            Assert.That(host.Flow.CurrentState, Is.EqualTo(GameState.InRun));
            Assert.That(host.Ui.CurrentPage, Is.EqualTo(UiPageId.RunHud));
            Assert.That(host.Input.GameplayMap.enabled, Is.True);
            Assert.That(host.Presentation.ActiveViewCount, Is.GreaterThan(0));

            PressGamepad(GamepadButton.Start);
            host.TickRuntime(0d);
            Assert.That(host.Flow.CurrentState, Is.EqualTo(GameState.Pause));
            Assert.That(host.Ui.CurrentPage, Is.EqualTo(UiPageId.Pause));
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
            Assert.That(host.Flow.CurrentState, Is.EqualTo(GameState.InRun));
            ReleaseGamepad();

            PressGamepad(GamepadButton.LeftShoulder);
            ReleaseGamepad();
            host.TickRuntime(SimulationClock.TickDurationSeconds);
            Assert.That(host.Flow.CurrentState, Is.EqualTo(GameState.LevelUpChoice));
            Assert.That(host.Ui.CurrentPage, Is.EqualTo(UiPageId.LevelUpDraft));
            Assert.That(host.Ui.RenderedOptionCount, Is.EqualTo(3));

            PressGamepad(GamepadButton.DpadDown);
            Assert.That(host.Ui.RenderedSelectedIndex, Is.EqualTo(1));
            ReleaseGamepad();
            PressGamepad(GamepadButton.South);
            host.TickRuntime(0d);
            Assert.That(host.Flow.CurrentState, Is.EqualTo(GameState.InRun));
            ReleaseGamepad();

            PressKeyboard(Key.Escape);
            host.TickRuntime(0d);
            Assert.That(host.Flow.CurrentState, Is.EqualTo(GameState.Pause));
            ReleaseKeyboard();
            PressKeyboard(Key.DownArrow);
            ReleaseKeyboard();
            PressKeyboard(Key.DownArrow);
            ReleaseKeyboard();
            Assert.That(host.Ui.RenderedSelectedIndex, Is.EqualTo(2));
            PressKeyboard(Key.Enter);
            Assert.That(host.Flow.CurrentState, Is.EqualTo(GameState.RunResult));
            Assert.That(host.Ui.CurrentPage, Is.EqualTo(UiPageId.RunResult));
            Assert.That(host.Flow.LatestResult.ReasonKey, Is.EqualTo("ui.result.reason.abandoned"));
            ReleaseKeyboard();

            PressGamepad(GamepadButton.South);
            Assert.That(host.Flow.CurrentState, Is.EqualTo(GameState.MainMenu));
            Assert.That(host.Ui.CurrentPage, Is.EqualTo(UiPageId.MainMenu));
            ReleaseGamepad();
        }

        [UnityTest]
        public IEnumerator DestroyingBootstrapReleasesViewsPoolsAndInputOwner()
        {
            yield return LoadBootstrapScene();
            var host = Object.FindFirstObjectByType<M7RuntimeHost>();
            host.Flow.ShowCharacterSelect();
            host.Flow.ShowMapSelect();
            host.Flow.BeginRun();
            host.TickRuntime(0d);
            host.TickRuntime(SimulationClock.TickDurationSeconds);
            Assert.That(host.Presentation.ActiveViewCount, Is.GreaterThan(0));

            Object.Destroy(Object.FindFirstObjectByType<GameBootstrapper>().gameObject);
            yield return null;
            yield return null;

            Assert.That(Object.FindObjectsByType<M7RuntimeHost>(FindObjectsInactive.Include, FindObjectsSortMode.None), Is.Empty);
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
