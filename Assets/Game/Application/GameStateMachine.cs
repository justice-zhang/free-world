namespace Game.Application
{
    /// <summary>
    /// Tracks the current high-level application state.
    /// </summary>
    public sealed class GameStateMachine
    {
        /// <summary>
        /// Gets the current application state.
        /// </summary>
        public GameState CurrentState { get; private set; }

        /// <summary>Enters the bootstrap state.</summary>
        public void EnterBootstrap()
        {
            CurrentState = GameState.Bootstrap;
        }

        /// <summary>
        /// Enters the empty main-menu state.
        /// </summary>
        public void EnterMainMenu()
        {
            CurrentState = GameState.MainMenu;
        }

        /// <summary>Enters an actively advancing run.</summary>
        public void EnterRun()
        {
            CurrentState = GameState.InRun;
        }

        /// <summary>Enters character selection.</summary>
        public void EnterCharacterSelect()
        {
            CurrentState = GameState.CharacterSelect;
        }

        /// <summary>Enters map selection.</summary>
        public void EnterMapSelect()
        {
            CurrentState = GameState.MapSelect;
        }

        /// <summary>Enters the run loading state.</summary>
        public void EnterLoading()
        {
            CurrentState = GameState.Loading;
        }

        /// <summary>Enters the player pause state.</summary>
        public void EnterPause()
        {
            CurrentState = GameState.Pause;
        }

        /// <summary>Enters the paused level-up command state.</summary>
        public void EnterLevelUpChoice()
        {
            CurrentState = GameState.LevelUpChoice;
        }

        /// <summary>Enters the paused controlled-reward command state.</summary>
        public void EnterRewardChoice()
        {
            CurrentState = GameState.RewardChoice;
        }

        /// <summary>Enters the immutable run-result state.</summary>
        public void EnterRunResult()
        {
            CurrentState = GameState.RunResult;
        }

        /// <summary>Enters settings.</summary>
        public void EnterSettings()
        {
            CurrentState = GameState.Settings;
        }

        /// <summary>Enters the recoverable content-error page.</summary>
        public void EnterContentError()
        {
            CurrentState = GameState.ContentError;
        }
    }
}
