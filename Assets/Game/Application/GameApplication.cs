using System;
using System.Collections.Generic;
using Game.Content.Runtime;
using Game.Core;
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
            : this(platform, stateMachine, new ContentRegistry())
        {
        }

        /// <summary>
        /// Initializes a new application instance with explicit platform, state, and content services.
        /// </summary>
        public GameApplication(
            IPlatformFacade platform,
            GameStateMachine stateMachine,
            ContentRegistry contentRegistry)
        {
            Platform = platform ?? throw new ArgumentNullException(nameof(platform));
            StateMachine = stateMachine ?? throw new ArgumentNullException(nameof(stateMachine));
            ContentRegistry = contentRegistry ??
                throw new ArgumentNullException(nameof(contentRegistry));
            Events = new ApplicationEventStream();
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
        /// Gets the stable-ID content registry owned by the application.
        /// </summary>
        public ContentRegistry ContentRegistry { get; }

        /// <summary>Gets application events used by persistence and platform adapters.</summary>
        public ApplicationEventStream Events { get; }

        /// <summary>
        /// Gets the summary from the successful startup content load.
        /// </summary>
        public ContentRegistrySummary ContentSummary { get; private set; }

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

        /// <summary>
        /// Loads and validates content before entering the empty main-menu state.
        /// </summary>
        public Result<ContentRegistrySummary> Initialize(
            IReadOnlyList<BakedContentCatalog> catalogs,
            ContentVersion gameVersion)
        {
            if (IsInitialized)
            {
                return Result<ContentRegistrySummary>.Failure(
                    new Error(
                        ErrorCode.InvalidCatalog,
                        "GameApplication has already been initialized."));
            }

            var loadResult = ContentRegistry.Load(catalogs, gameVersion);
            if (!loadResult.IsSuccess)
            {
                return loadResult;
            }

            ContentSummary = loadResult.Value;
            StateMachine.EnterMainMenu();
            IsInitialized = true;
            return loadResult;
        }
    }
}
