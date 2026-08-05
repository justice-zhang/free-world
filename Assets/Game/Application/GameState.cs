namespace Game.Application
{
    /// <summary>
    /// Identifies the application state exposed to presentation and UI.
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
        RunResult = 4,

        /// <summary>The bootstrap shell is initializing content and services.</summary>
        Bootstrap = 5,

        /// <summary>The player is choosing a character.</summary>
        CharacterSelect = 6,

        /// <summary>The player is choosing a map.</summary>
        MapSelect = 7,

        /// <summary>A run is being assembled without advancing simulation.</summary>
        Loading = 8,

        /// <summary>An active run is paused by the player.</summary>
        Pause = 9,

        /// <summary>The settings page is open.</summary>
        Settings = 10,

        /// <summary>Content startup or run assembly failed.</summary>
        ContentError = 11,

        /// <summary>An active run is paused for a controlled reward choice.</summary>
        RewardChoice = 12
    }
}
