using System;
using Game.Platform.Abstractions;

namespace Game.Application
{
    /// <summary>
    /// Owns the M0 application services assembled by the composition root.
    /// </summary>
    public sealed class GameApplication
    {
        /// <summary>
        /// Initializes a new application instance with explicit dependencies.
        /// </summary>
        /// <param name="platform">The platform facade.</param>
        /// <param name="stateMachine">The high-level state machine.</param>
        public GameApplication(IPlatformFacade platform, GameStateMachine stateMachine)
        {
            Platform = platform ?? throw new ArgumentNullException(nameof(platform));
            StateMachine = stateMachine ?? throw new ArgumentNullException(nameof(stateMachine));
        }

        /// <summary>
        /// Gets the platform boundary selected by the composition root.
        /// </summary>
        public IPlatformFacade Platform { get; }

        /// <summary>
        /// Gets the application state machine.
        /// </summary>
        public GameStateMachine StateMachine { get; }

        /// <summary>
        /// Gets a value indicating whether initialization has completed.
        /// </summary>
        public bool IsInitialized { get; private set; }

        /// <summary>
        /// Initializes the application once and enters the empty main-menu state.
        /// </summary>
        /// <returns><see langword="true"/> only for the first initialization.</returns>
        public bool Initialize()
        {
            if (IsInitialized)
            {
                return false;
            }

            StateMachine.EnterMainMenu();
            IsInitialized = true;
            return true;
        }
    }
}
