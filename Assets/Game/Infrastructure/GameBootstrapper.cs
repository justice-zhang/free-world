using Game.Application;
using Game.Content.Runtime;
using Game.Core;
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
        private static readonly ContentVersion GameContentVersion =
            new ContentVersion(0, 1, 0);

        private static GameBootstrapper activeInstance;
        [SerializeField] private TextAsset bakedTestCatalog;
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

        /// <summary>
        /// Gets the number of packs and entries loaded during startup.
        /// </summary>
        public ContentRegistrySummary ContentSummary =>
            application == null ? default : application.ContentSummary;

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

            if (bakedTestCatalog == null)
            {
                Debug.LogError("[Bootstrap] Baked test content catalog is not assigned.");
                return;
            }

            BakedContentCatalogDto dto;
            try
            {
                dto = JsonUtility.FromJson<BakedContentCatalogDto>(bakedTestCatalog.text);
            }
            catch (System.Exception exception)
            {
                Debug.LogError("[Bootstrap] Baked catalog JSON is invalid: " + exception.Message);
                return;
            }

            if (dto == null)
            {
                Debug.LogError("[Bootstrap] Baked catalog JSON produced no catalog.");
                return;
            }

            var catalogResult = dto.ToCatalog();
            if (!catalogResult.IsSuccess)
            {
                Debug.LogError("[Bootstrap] Content catalog rejected: " + catalogResult.Error);
                return;
            }

            application = new GameApplication(
                new NullPlatformFacade(),
                new GameStateMachine(),
                new ContentRegistry());
            var initialization = application.Initialize(
                new[] { catalogResult.Value },
                GameContentVersion);
            if (!initialization.IsSuccess)
            {
                application = null;
                Debug.LogError("[Bootstrap] Content registry rejected: " + initialization.Error);
                return;
            }

            Debug.Log(
                "[Bootstrap] Loaded content: packs=" + initialization.Value.PackCount +
                ", entries=" + initialization.Value.DefinitionCount +
                "; NullPlatformFacade initialized; entered MainMenu.");
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
