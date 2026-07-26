using System;
using System.Globalization;
using Game.Application;
using Game.Core;
using Game.Platform.Abstractions;

namespace Game.Infrastructure
{
    /// <summary>Owns M8 persistence and platform event adapters at the composition boundary.</summary>
    public sealed class M8RuntimeServices : IDisposable
    {
        private readonly GameApplication application;
        private readonly SaveCoordinator saves;
        private readonly SavePackVersion[] packs;
        private readonly Func<string> utcNow;
        private readonly PlatformApplicationEventRouter platformRouter;

        public M8RuntimeServices(
            GameApplication gameApplication,
            SaveCoordinator saveCoordinator,
            IPlatformFacade platform,
            SavePackVersion[] loadedPacks,
            Func<string> utcNowProvider = null)
        {
            application = gameApplication ?? throw new ArgumentNullException(nameof(gameApplication));
            saves = saveCoordinator ?? throw new ArgumentNullException(nameof(saveCoordinator));
            packs = loadedPacks == null ? Array.Empty<SavePackVersion>() : (SavePackVersion[])loadedPacks.Clone();
            utcNow = utcNowProvider ?? (() => DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
            platformRouter = new PlatformApplicationEventRouter(application.Events, platform ?? throw new ArgumentNullException(nameof(platform)));
            application.Events.Published += OnApplicationEvent;
        }

        /// <summary>Gets the loaded or default settings.</summary>
        public SettingsSaveData Settings { get; private set; }
        /// <summary>Gets the loaded or default long-lived profile.</summary>
        public ProfileSaveData Profile { get; private set; }
        /// <summary>Gets the most recent persistence diagnostic.</summary>
        public SaveDiagnostic LastDiagnostic { get; private set; }
        /// <summary>Gets the latest platform event result.</summary>
        public PlatformOperationResult LastPlatformOperation => platformRouter.LastOperation;

        /// <summary>Loads settings and profile, falling back to local defaults.</summary>
        public void Initialize()
        {
            var settings = saves.LoadSettingsAsync().AsTask().GetAwaiter().GetResult();
            if (settings.IsSuccess) Settings = settings.Value;
            else
            {
                LastDiagnostic = settings.Failure;
                Settings = CreateDefaultSettings();
            }

            var profile = saves.LoadProfileAsync().AsTask().GetAwaiter().GetResult();
            if (profile.IsSuccess) Profile = profile.Value;
            else
            {
                LastDiagnostic = profile.Failure;
                Profile = CreateDefaultProfile();
            }
        }

        /// <summary>Unsubscribes persistence and platform event handlers.</summary>
        public void Dispose()
        {
            application.Events.Published -= OnApplicationEvent;
            platformRouter.Dispose();
        }

        private void OnApplicationEvent(ApplicationEvent applicationEvent)
        {
            switch (applicationEvent.Type)
            {
                case ApplicationEventType.SettingsChanged:
                    Settings = applicationEvent.Settings;
                    Capture(saves.SaveSettingsAsync(Settings).AsTask().GetAwaiter().GetResult());
                    break;
                case ApplicationEventType.RunStarted:
                    var owned = applicationEvent.InitialSkillId.IsValid
                        ? new[] { new SavedContentLevel(applicationEvent.InitialSkillId, 1) }
                        : Array.Empty<SavedContentLevel>();
                    var recovery = new RunRecoverySaveData(
                        applicationEvent.RunSeed, 0, applicationEvent.CharacterId, applicationEvent.MapId,
                        packs, owned, utcNow());
                    Capture(saves.SaveRunRecoveryAsync(recovery).AsTask().GetAwaiter().GetResult());
                    break;
                case ApplicationEventType.RunCompleted:
                    Profile = IncrementCompletedRuns(Profile);
                    Capture(saves.SaveProfileAsync(Profile).AsTask().GetAwaiter().GetResult());
                    var clear = saves.ClearRunRecoveryAsync().AsTask().GetAwaiter().GetResult();
                    if (!clear.IsSuccess) LastDiagnostic = clear.Diagnostic;
                    break;
            }
        }

        private void Capture(SaveStorageWriteResult result)
        {
            LastDiagnostic = result.IsSuccess ? default : result.Diagnostic;
        }

        private SettingsSaveData CreateDefaultSettings() =>
            new SettingsSaveData("en", 0.15f, 1f, true, 1f, true, AutoAimStrategy.Nearest);

        private ProfileSaveData CreateDefaultProfile() =>
            new ProfileSaveData(Guid.NewGuid().ToString("N"), packs, Array.Empty<ContentId>(),
                Array.Empty<SavedContentLevel>(), Array.Empty<SavedCounter>(), Array.Empty<SavedCounter>(), utcNow());

        private ProfileSaveData IncrementCompletedRuns(ProfileSaveData source)
        {
            const string key = "runs_completed";
            var count = source.Statistics.Count;
            var found = -1;
            for (var index = 0; index < count; index++)
                if (string.Equals(source.Statistics[index].Key, key, StringComparison.Ordinal)) { found = index; break; }
            var stats = new SavedCounter[count + (found < 0 ? 1 : 0)];
            for (var index = 0; index < count; index++) stats[index] = source.Statistics[index];
            if (found < 0) stats[count] = new SavedCounter(key, 1);
            else stats[found] = new SavedCounter(key, stats[found].Value + 1);
            var unlocked = new ContentId[source.UnlockedContentIds.Count];
            for (var index = 0; index < unlocked.Length; index++) unlocked[index] = source.UnlockedContentIds[index];
            var meta = new SavedContentLevel[source.MetaUpgrades.Count];
            for (var index = 0; index < meta.Length; index++) meta[index] = source.MetaUpgrades[index];
            var currencies = new SavedCounter[source.Currencies.Count];
            for (var index = 0; index < currencies.Length; index++) currencies[index] = source.Currencies[index];
            return new ProfileSaveData(source.ProfileId, packs, unlocked, meta, currencies, stats, utcNow());
        }
    }
}
