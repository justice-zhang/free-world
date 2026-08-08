using System;
using Game.Application;
using Game.Content.Runtime;
using Game.Core;
using Game.Presentation;
using Game.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Infrastructure
{
    /// <summary>Single Unity owner for the G2.6 Demo UI, input, View, and run lifecycle.</summary>
    [DefaultExecutionOrder(-9000)]
    public sealed class QinglanDemoRuntimeHost : MonoBehaviour
    {
        private const double UiRefreshIntervalSeconds = 0.1d;
        private QinglanDemoPresenter presenter;
        private PresentationCameraRig cameraRig;
        private RunSession lastSession;
        private DemoFlowStage lastStage;
        private double uiRefreshAccumulator;
        private string lastLocaleCode = string.Empty;
        private bool initialized;

        public QinglanDemoFlowController Flow { get; private set; }
        public M7InputRouter Input { get; private set; }
        public QinglanRuntimeUiRoot Ui { get; private set; }
        public PresentationCoordinator Presentation { get; private set; }
        public ILocalizationService Localization { get; private set; }
        public QinglanPageViewModel CurrentPage => presenter?.CurrentPage;

        public void Initialize(
            GameApplication application,
            Camera presentationCamera,
            InputActionAsset inputActions,
            M8RuntimeServices runtimeServices)
        {
            if (initialized) throw new InvalidOperationException("QinglanDemoRuntimeHost is already initialized.");
            if (application == null) throw new ArgumentNullException(nameof(application));
            if (runtimeServices == null) throw new ArgumentNullException(nameof(runtimeServices));
            bootstrapApplication = application;
            Localization = new UnityLocalizationService();
            Localization.SelectLocale(runtimeServices.Settings.LocaleCode);

            Input = gameObject.AddComponent<M7InputRouter>();
            Input.Initialize(inputActions);
            Input.ApplyBindingOverrides(runtimeServices.Settings.BindingOverrides);
            Flow = new QinglanDemoFlowController(application, runtimeServices, Input, Localization);

            var uiObject = new GameObject("Qinglan_Demo_UI");
            uiObject.transform.SetParent(transform, false);
            Ui = uiObject.AddComponent<QinglanRuntimeUiRoot>();
            Ui.Initialize(Localization, ResolveContentNameKey);

            var presentationObject = new GameObject("Qinglan_Demo_Presentation");
            presentationObject.transform.SetParent(transform, false);
            Presentation = presentationObject.AddComponent<PresentationCoordinator>();
            Presentation.Initialize(Ui.SharedCanvas, Flow.Settings);
            presenter = new QinglanDemoPresenter(Flow, Ui);

            if (presentationCamera == null)
            {
                var cameraObject = new GameObject("Qinglan_ProgrammaticCamera");
                cameraObject.transform.SetParent(transform, false);
                presentationCamera = cameraObject.AddComponent<Camera>();
                presentationCamera.orthographic = true;
                presentationCamera.transform.position = new Vector3(0f, 0f, -10f);
            }
            cameraRig = presentationCamera.GetComponent<PresentationCameraRig>();
            if (cameraRig == null) cameraRig = presentationCamera.gameObject.AddComponent<PresentationCameraRig>();

            Input.Navigate += presenter.Navigate;
            Input.Submit += OnSubmit;
            Input.Cancel += OnCancel;
            Input.Pause += OnPause;
            Input.Map += OnMap;
            Input.Tab += OnTab;
            Input.Page += OnPage;
            Input.FocusRestoreRequested += OnFocusRestore;
            Input.GamepadDisconnected += OnGamepadDisconnected;
            Input.DebugLevelUp += OnDebugLevelUp;
            Input.DebugCompleteRun += OnDebugCompleteRun;
            lastStage = Flow.Stage;
            lastLocaleCode = Localization.SelectedLocaleCode;
            ApplyInputMode();
            initialized = true;
        }

        private void Update() => TickRuntime(Time.unscaledDeltaTime);

        public void TickRuntime(double elapsedSeconds)
        {
            if (!initialized) return;
            if (double.IsNaN(elapsedSeconds) || double.IsInfinity(elapsedSeconds) || elapsedSeconds < 0d)
                throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
            Input.SetStickDeadzone(Flow.Settings.StickDeadzone);
            var move = Input.Move;
            Flow.SetMovement(new System.Numerics.Vector2(move.x, move.y));
            Flow.SetInteractHeld(Input.InteractHeld);
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
                    if (Presentation.LastDeathRequestCount > 0 && Flow.Settings.ScreenShakeEnabled)
                        cameraRig.RequestShake(0.18f * Flow.Settings.FlashIntensity, 0.2f);
                }
                Presentation.Sync(session.RenderSnapshot, session.InterpolationAlpha, session);
                if (Presentation.TryGetView(session.Player, out var playerView)) cameraRig.SetTarget(playerView.transform);
            }
            Presentation.TickEffects((float)elapsedSeconds);

            var stageChanged = lastStage != Flow.Stage;
            var localeChanged = !string.Equals(lastLocaleCode, Localization.SelectedLocaleCode, StringComparison.Ordinal);
            if (localeChanged) lastLocaleCode = Localization.SelectedLocaleCode;
            if (stageChanged || localeChanged)
            {
                lastStage = Flow.Stage;
                presenter.Refresh(true);
                ApplyInputMode();
            }
            uiRefreshAccumulator += elapsedSeconds;
            if (uiRefreshAccumulator >= UiRefreshIntervalSeconds)
            {
                uiRefreshAccumulator -= UiRefreshIntervalSeconds;
                presenter.Refresh(false);
                ApplyInputMode();
            }
        }

        private string ResolveContentNameKey(string value)
        {
            var id = ContentId.Create(value);
            return id.IsSuccess && bootstrapApplication.ContentRegistry.TryGet(id.Value, out ContentRegistryEntry entry)
                ? entry.Definition.LocalizedNameKey
                : string.Empty;
        }

        private GameApplication bootstrapApplication;

        private void ApplyInputMode() => Input.SetGameplayMode(Flow.IsGameplayInputEnabled);

        private void OnPause()
        {
            if (Flow.TogglePause()) { presenter.Refresh(true); ApplyInputMode(); }
        }

        private void OnSubmit()
        {
            presenter.Submit();
            ApplyInputMode();
        }

        private void OnCancel()
        {
            presenter.Cancel();
            ApplyInputMode();
        }

        private void OnMap()
        {
            if (Flow.ToggleRunMap()) { presenter.Refresh(true); ApplyInputMode(); }
        }

        private void OnFocusRestore()
        {
            presenter.Refresh(true);
            presenter.RestoreFocus();
            ApplyInputMode();
        }

        private void OnGamepadDisconnected()
        {
            if (Flow.Stage == DemoFlowStage.Active) Flow.TogglePause();
        }

        private void OnTab(float value) => presenter.Tab(value > 0f ? 1 : -1);

        private void OnPage(float value) => presenter.Page(value > 0f ? 1 : -1);

        private void OnDebugLevelUp()
        {
            if (Input.DebugEnabled && Flow.DebugRequestLevelUp()) presenter.Refresh(true);
        }

        private void OnDebugCompleteRun()
        {
            if (Input.DebugEnabled && Flow.DebugCompleteRun()) presenter.Refresh(true);
        }

        private void OnDestroy()
        {
            if (!initialized || Input == null) return;
            Input.Navigate -= presenter.Navigate;
            Input.Submit -= OnSubmit;
            Input.Cancel -= OnCancel;
            Input.Pause -= OnPause;
            Input.Map -= OnMap;
            Input.Tab -= OnTab;
            Input.Page -= OnPage;
            Input.FocusRestoreRequested -= OnFocusRestore;
            Input.GamepadDisconnected -= OnGamepadDisconnected;
            Input.DebugLevelUp -= OnDebugLevelUp;
            Input.DebugCompleteRun -= OnDebugCompleteRun;
            Flow.Dispose();
            initialized = false;
        }
    }
}
