using System;
using Game.Application;
using Game.Presentation;
using Game.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Infrastructure
{
    /// <summary>Unity composition host for M7 presentation, input, UI, and camera.</summary>
    [DefaultExecutionOrder(-9000)]
    public sealed class M7RuntimeHost : MonoBehaviour
    {
        private GameFlowPresenter presenter;
        private PresentationCameraRig cameraRig;
        private RunSession lastSession;
        private GameState lastState;
        private bool initialized;

        public M7GameFlowController Flow { get; private set; }
        public M7InputRouter Input { get; private set; }
        public RuntimeUiRoot Ui { get; private set; }
        public PresentationCoordinator Presentation { get; private set; }

        public void Initialize(
            GameApplication application,
            Camera presentationCamera,
            InputActionAsset inputActions = null)
        {
            if (initialized) throw new InvalidOperationException("M7RuntimeHost is already initialized.");
            var uiObject = new GameObject("M7_UI");
            uiObject.transform.SetParent(transform, false);
            Ui = uiObject.AddComponent<RuntimeUiRoot>();
            Ui.Initialize();

            var presentationObject = new GameObject("M7_Presentation");
            presentationObject.transform.SetParent(transform, false);
            Presentation = presentationObject.AddComponent<PresentationCoordinator>();

            Input = gameObject.AddComponent<M7InputRouter>();
            Input.Initialize(inputActions);
            Flow = new M7GameFlowController(application);
            Presentation.Initialize(Ui.SharedCanvas, Flow.Settings);
            presenter = new GameFlowPresenter(Flow, Ui, Input);

            if (presentationCamera == null)
            {
                var cameraObject = new GameObject("M7_ProgrammaticCamera");
                cameraObject.transform.SetParent(transform, false);
                presentationCamera = cameraObject.AddComponent<Camera>();
                presentationCamera.orthographic = true;
                presentationCamera.transform.position = new Vector3(0f, 0f, -10f);
            }
            cameraRig = presentationCamera.GetComponent<PresentationCameraRig>();
            if (cameraRig == null) cameraRig = presentationCamera.gameObject.AddComponent<PresentationCameraRig>();

            Input.Navigate += presenter.Navigate;
            Input.Submit += presenter.Submit;
            Input.Cancel += presenter.Cancel;
            Input.Pause += presenter.TogglePause;
            Input.DebugLevelUp += OnDebugLevelUp;
            Input.DebugCompleteRun += OnDebugCompleteRun;
            lastState = Flow.CurrentState;
            ApplyStateMode();
            initialized = true;
        }

        private void Update()
        {
            TickRuntime(Time.unscaledDeltaTime);
        }

        /// <summary>Advances one presentation frame; exposed for deterministic PlayMode driving.</summary>
        public void TickRuntime(double elapsedSeconds)
        {
            if (!initialized) return;
            if (double.IsNaN(elapsedSeconds) || double.IsInfinity(elapsedSeconds) || elapsedSeconds < 0d)
                throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
            Input.SetStickDeadzone(Flow.Settings.StickDeadzone);
            var move = Input.Move;
            Flow.SetMovement(new System.Numerics.Vector2(move.x, move.y));
            var executedTicks = Flow.Tick(elapsedSeconds);
            var session = Flow.Session;
            if (lastSession != session)
            {
                Presentation.Clear();
                cameraRig.SetTarget(null);
                lastSession = session;
            }

            if (session != null)
            {
                cameraRig.EffectsEnabled = Flow.Settings.ScreenShakeEnabled;
                if (executedTicks > 0)
                {
                    Presentation.ConsumeLatestEvents(
                        session.RenderSnapshot.Tick,
                        session.SimulationEvents,
                        session.CombatEvents);
                    if (Presentation.LastDeathRequestCount > 0)
                        cameraRig.RequestShake(0.18f, 0.2f);
                }
                Presentation.Sync(session.RenderSnapshot, session.InterpolationAlpha, session);
                if (Presentation.TryGetView(session.Player, out var playerView))
                    cameraRig.SetTarget(playerView.transform);
            }

            Presentation.TickEffects((float)elapsedSeconds);
            if (lastState != Flow.CurrentState)
            {
                lastState = Flow.CurrentState;
                presenter.Refresh();
                ApplyStateMode();
            }
        }

        private void ApplyStateMode()
        {
            Input.SetGameplayMode(Flow.CurrentState == GameState.InRun);
        }

        private void OnDebugLevelUp()
        {
            Flow.DebugRequestLevelUp();
        }

        private void OnDebugCompleteRun()
        {
            Flow.EndRun(RunEndReason.Completed);
        }

        private void OnDestroy()
        {
            if (!initialized || Input == null) return;
            Input.Navigate -= presenter.Navigate;
            Input.Submit -= presenter.Submit;
            Input.Cancel -= presenter.Cancel;
            Input.Pause -= presenter.TogglePause;
            Input.DebugLevelUp -= OnDebugLevelUp;
            Input.DebugCompleteRun -= OnDebugCompleteRun;
            initialized = false;
        }
    }
}
