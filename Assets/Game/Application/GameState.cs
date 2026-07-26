namespace Game.Application
{
    /// <summary>
    /// Identifies the application state available in the M0 framework.
    /// </summary>
    public enum GameState
    {
        /// <summary>
        /// The application has not entered a state.
        /// </summary>
        None = 0,

        /// <summary>
        /// The empty main-menu state is active.
        /// </summary>
        MainMenu = 1,

        /// <summary>A fixed-tick run is actively advancing.</summary>
        InRun = 2,

        /// <summary>A run is paused for an application-level upgrade command.</summary>
        LevelUpChoice = 3,

        /// <summary>The immutable result of the latest run is available.</summary>
        RunResult = 4
    }
}
