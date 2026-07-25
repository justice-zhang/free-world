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
    }
}
