using Game.Application;
using Game.Content.Runtime;
using Game.Core;
using Game.Platform.Abstractions;
using Game.Platform.Null;
using System.Collections.Generic;
using System;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

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
        [SerializeField] private TextAsset[] additionalBakedCatalogs;
        [SerializeField] private Camera presentationCamera;
        [SerializeField] private InputActionAsset inputActions;
        private GameApplication application;
        private M8RuntimeServices persistence;
        private QinglanDemoRuntimeHost demoHost;

        /// <summary>
        /// Gets the initialized application instance.
        /// </summary>
        public GameApplication Application => application;

        /// <summary>
        /// Gets the platform facade created by this composition root.
        /// </summary>
        public IPlatformFacade PlatformFacade => application?.Platform;

        /// <summary>Gets M8 persistence and application-event services.</summary>
        public M8RuntimeServices Persistence => persistence;
        /// <summary>Gets the active Qinglan Demo UI/input/lifecycle owner.</summary>
        public QinglanDemoRuntimeHost DemoHost => demoHost;

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

#if UNITY_EDITOR
        /// <summary>Editor-only deterministic scene wiring used by M7 setup.</summary>
        public void ConfigureM7Assets(
            TextAsset[] catalogs,
            Camera cameraValue,
            InputActionAsset inputValue)
        {
            additionalBakedCatalogs = catalogs;
            presentationCamera = cameraValue;
            inputActions = inputValue;
        }
#endif

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

            var platform = new NullPlatformFacade();
            application = new GameApplication(
                platform,
                new GameStateMachine(),
                new ContentRegistry());
            application.StateMachine.EnterBootstrap();

            if (bakedTestCatalog == null)
            {
                Debug.LogError("[Bootstrap] Baked test content catalog is not assigned.");
                application.StateMachine.EnterContentError();
                return;
            }

            var textAssets = new List<TextAsset>(1 + (additionalBakedCatalogs?.Length ?? 0))
            {
                bakedTestCatalog
            };
            if (additionalBakedCatalogs != null)
            {
                for (var index = 0; index < additionalBakedCatalogs.Length; index++)
                    if (additionalBakedCatalogs[index] != null) textAssets.Add(additionalBakedCatalogs[index]);
            }
            var catalogs = new List<BakedContentCatalog>(textAssets.Count);
            try
            {
                for (var index = 0; index < textAssets.Count; index++)
                {
                    var dto = JsonUtility.FromJson<BakedContentCatalogDto>(textAssets[index].text);
                    if (dto == null) throw new System.InvalidOperationException("Baked catalog JSON produced no catalog.");
                    var catalogResult = dto.ToCatalog();
                    if (!catalogResult.IsSuccess)
                        throw new System.InvalidOperationException(catalogResult.Error.ToString());
                    catalogs.Add(catalogResult.Value);
                }
            }
            catch (System.Exception exception)
            {
                Debug.LogError("[Bootstrap] Baked catalog JSON is invalid: " + exception.Message);
                application.StateMachine.EnterContentError();
                return;
            }

            var initialization = application.Initialize(
                catalogs,
                GameContentVersion);
            if (!initialization.IsSuccess)
            {
                Debug.LogError("[Bootstrap] Content registry rejected: " + initialization.Error);
                application.StateMachine.EnterContentError();
                return;
            }

            var packVersions = new SavePackVersion[catalogs.Count];
            for (var index = 0; index < catalogs.Count; index++)
                packVersions[index] = new SavePackVersion(catalogs[index].Manifest.PackId, catalogs[index].Manifest.Version);
            var storage = new LocalFileSaveStorage(ResolveSaveRoot());
            var coordinator = new SaveCoordinator(storage, new UnityJsonSaveCodec(), application.ContentRegistry);
            persistence = new M8RuntimeServices(application, coordinator, platform, packVersions);
            persistence.Initialize();

            demoHost = gameObject.AddComponent<QinglanDemoRuntimeHost>();
            demoHost.Initialize(application, presentationCamera, inputActions, persistence);
            if (QinglanG28DevelopmentSmokeRunner.IsRequested())
                gameObject.AddComponent<QinglanG28DevelopmentSmokeRunner>();

            Debug.Log(
                "[Bootstrap] Loaded content: packs=" + initialization.Value.PackCount +
                ", entries=" + initialization.Value.DefinitionCount +
                "; G2.6 Qinglan Demo UI/input, local save/localization, and NullPlatformFacade initialized.");
        }

        private void OnDestroy()
        {
            if (activeInstance == this)
            {
                persistence?.Dispose();
                persistence = null;
                demoHost = null;
                activeInstance = null;
            }
        }

        /// <summary>Resolves the isolated Editor or persistent Player save directory.</summary>
        public static string ResolveSaveRoot()
        {
            var overridePath = Environment.GetEnvironmentVariable("AZURESWORD_SAVE_ROOT");
            if (!string.IsNullOrWhiteSpace(overridePath)) return Path.GetFullPath(overridePath);
#if UNITY_EDITOR
            return Path.Combine(UnityEngine.Application.temporaryCachePath, "AzureSwordEditorSaves", System.Diagnostics.Process.GetCurrentProcess().Id.ToString());
#else
            return Path.Combine(UnityEngine.Application.persistentDataPath, "Saves");
#endif
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            activeInstance = null;
        }
    }
}
