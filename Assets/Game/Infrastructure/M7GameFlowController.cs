using System;
using System.Numerics;
using Game.Application;
using Game.Core;

namespace Game.Infrastructure
{
    /// <summary>Application command adapter for the full M7 page and run flow.</summary>
    public sealed class M7GameFlowController : IGameFlowController
    {
        private readonly GameApplication application;
        private GameState settingsReturnState;
        private bool runLoadPending;
        private Vector2 movement;

        public M7GameFlowController(GameApplication gameApplication)
        {
            application = gameApplication ?? throw new ArgumentNullException(nameof(gameApplication));
            Settings = new AccessibilitySettings();
        }

        public GameState CurrentState => application.StateMachine.CurrentState;
        public AccessibilitySettings Settings { get; }
        public RunSession Session { get; private set; }
        public string ContentError { get; private set; } = string.Empty;
        public RunResultData LatestResult { get; private set; }
        public bool HasPendingLoad => runLoadPending;
        public int UpgradeChoiceCount => Session?.CurrentOffers?.Count ?? 0;

        public UpgradeChoiceData GetUpgradeChoice(int index)
        {
            var offers = Session?.CurrentOffers;
            if (offers == null || index < 0 || index >= offers.Count)
                throw new ArgumentOutOfRangeException(nameof(index));
            var source = offers.GetAt(index).Source;
            return new UpgradeChoiceData(index, source.LocalizedNameKey, source.LocalizedDescriptionKey);
        }

        public bool ShowCharacterSelect()
        {
            if (CurrentState != GameState.MainMenu && CurrentState != GameState.MapSelect) return false;
            application.StateMachine.EnterCharacterSelect();
            return true;
        }

        public bool ShowMapSelect()
        {
            if (CurrentState != GameState.CharacterSelect) return false;
            application.StateMachine.EnterMapSelect();
            return true;
        }

        public bool BeginRun()
        {
            if (CurrentState != GameState.MapSelect || runLoadPending) return false;
            application.StateMachine.EnterLoading();
            runLoadPending = true;
            return true;
        }

        public bool TogglePause()
        {
            if (Session == null) return false;
            if (CurrentState == GameState.InRun) return Session.Pause();
            if (CurrentState == GameState.Pause) return Session.Resume();
            return false;
        }

        public bool SelectUpgrade(int index)
        {
            return Session != null && Session.SelectAt(index);
        }

        public bool SkipUpgrade() => Session != null && Session.Skip();
        public bool RerollUpgrades() => Session != null && Session.Reroll();

        public bool OpenSettings()
        {
            if (CurrentState != GameState.MainMenu && CurrentState != GameState.Pause) return false;
            settingsReturnState = CurrentState;
            application.StateMachine.EnterSettings();
            return true;
        }

        public bool CloseSettings()
        {
            if (CurrentState != GameState.Settings) return false;
            if (settingsReturnState == GameState.Pause) application.StateMachine.EnterPause();
            else application.StateMachine.EnterMainMenu();
            return true;
        }

        public bool EndRun(RunEndReason reason)
        {
            if (Session == null || !Session.End(reason)) return false;
            CaptureResult();
            return true;
        }

        public bool ReturnToMainMenu()
        {
            if (CurrentState != GameState.RunResult &&
                CurrentState != GameState.ContentError &&
                CurrentState != GameState.CharacterSelect) return false;
            Session = null;
            runLoadPending = false;
            movement = Vector2.Zero;
            ContentError = string.Empty;
            application.StateMachine.EnterMainMenu();
            return true;
        }

        public void SetMovement(Vector2 value)
        {
            if (float.IsNaN(value.X) || float.IsInfinity(value.X) ||
                float.IsNaN(value.Y) || float.IsInfinity(value.Y)) return;
            movement = value.LengthSquared() > 1f ? Vector2.Normalize(value) : value;
        }

        public int Tick(double elapsedSeconds)
        {
            if (runLoadPending && CurrentState == GameState.Loading)
            {
                runLoadPending = false;
                var creation = M7DemoRunFactory.Create(application.ContentRegistry, application.StateMachine);
                if (!creation.IsSuccess)
                {
                    ContentError = creation.Error.ToString();
                    application.StateMachine.EnterContentError();
                    return 0;
                }
                Session = creation.Value.Session;
                return 0;
            }

            if (Session == null || CurrentState != GameState.InRun) return 0;
            Session.SetMoveDirection(movement);
            var ticks = Session.Advance(elapsedSeconds);
            if (Session.HasEnded) CaptureResult();
            return ticks;
        }

        public bool DebugRequestLevelUp()
        {
            return CurrentState == GameState.InRun &&
                   Session != null &&
                   Session.GrantDebugExperience(5f);
        }

        private void CaptureResult()
        {
            var result = Session.Result;
            string reasonKey;
            switch (result.Reason)
            {
                case RunEndReason.Completed: reasonKey = "ui.result.reason.completed"; break;
                case RunEndReason.PlayerDefeated: reasonKey = "ui.result.reason.defeated"; break;
                default: reasonKey = "ui.result.reason.abandoned"; break;
            }
            LatestResult = new RunResultData(
                reasonKey,
                result.DurationSeconds,
                result.Level,
                result.Statistics.EnemyDefeats);
        }
    }
}
