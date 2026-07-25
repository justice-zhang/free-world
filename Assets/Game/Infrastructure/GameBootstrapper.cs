using Game.Application;
using Game.Platform.Abstractions;
using Game.Platform.Null;
using UnityEngine;

namespace Game.Infrastructure
{
    /// <summary>
    /// Acts as the sole Unity composition root for the M0 framework.
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    public sealed class GameBootstrapper : MonoBehaviour
    {
        private static GameBootstrapper activeInstance;
        private GameApplication application;

        /// <summary>
        /// Gets the initialized application instance.
        /// </summary>
        public GameApplication Application => application;

        /// <summary>
        /// Gets the platform facade created by this composition root.
        /// </summary>
        public IPlatformFacade PlatformFacade => application?.Platform;

        /// <summary>
        /// Gets the current high-level state.
        /// </summary>
        public GameState CurrentState =>
            application == null ? GameState.None : application.StateMachine.CurrentState;

        private void Awake()
        {
            if (activeInstance != null && activeInstance != this)
            {
                Debug.LogWarning("[Bootstrap] Duplicate GameBootstrapper rejected.");
                Destroy(gameObject);
                return;
            }

            activeInstance = this;
            DontDestroyOnLoad(gameObject);

            application = new GameApplication(
                new NullPlatformFacade(),
                new GameStateMachine());
            application.Initialize();

            Debug.Log("[Bootstrap] NullPlatformFacade initialized; entered MainMenu.");
        }

        private void OnDestroy()
        {
            if (activeInstance == this)
            {
                activeInstance = null;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            activeInstance = null;
        }
    }
}
