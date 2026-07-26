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

        /// <summary>Enters the paused level-up command state.</summary>
        public void EnterLevelUpChoice()
        {
            CurrentState = GameState.LevelUpChoice;
        }

        /// <summary>Enters the immutable run-result state.</summary>
        public void EnterRunResult()
        {
            CurrentState = GameState.RunResult;
        }
    }
}
