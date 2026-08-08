using System;
using Game.Core;

namespace Game.Application
{
    /// <summary>Low-frequency application events observed by persistence and platform adapters.</summary>
    public enum ApplicationEventType : byte
    {
        SettingsChanged = 0,
        RunStarted = 1,
        RunCompleted = 2,
        RunResultCommitted = 3
    }

    /// <summary>Application-owned event consumed by persistence and platform adapters.</summary>
    public readonly struct ApplicationEvent
    {
        private ApplicationEvent(
            ApplicationEventType type,
            SettingsSaveData settings,
            ulong runSeed,
            ContentId characterId,
            ContentId mapId,
            ContentId initialSkillId,
            RunResultData result,
            RunResult committedResult)
        {
            Type = type;
            Settings = settings;
            RunSeed = runSeed;
            CharacterId = characterId;
            MapId = mapId;
            InitialSkillId = initialSkillId;
            Result = result;
            CommittedResult = committedResult;
        }

        public ApplicationEventType Type { get; }
        public SettingsSaveData Settings { get; }
        public ulong RunSeed { get; }
        public ContentId CharacterId { get; }
        public ContentId MapId { get; }
        public ContentId InitialSkillId { get; }
        public RunResultData Result { get; }
        public RunResult CommittedResult { get; }

        /// <summary>Creates a settings-changed event.</summary>
        public static ApplicationEvent SettingsChanged(SettingsSaveData settings) =>
            new ApplicationEvent(ApplicationEventType.SettingsChanged, settings ?? throw new ArgumentNullException(nameof(settings)), 0, default, default, default, default, default);

        /// <summary>Creates a run-started event using stable content identities.</summary>
        public static ApplicationEvent RunStarted(ulong seed, ContentId characterId, ContentId mapId, ContentId initialSkillId) =>
            new ApplicationEvent(ApplicationEventType.RunStarted, null, seed, characterId, mapId, initialSkillId, default, default);

        /// <summary>Creates a run-completed event.</summary>
        public static ApplicationEvent RunCompleted(RunResultData result) =>
            new ApplicationEvent(ApplicationEventType.RunCompleted, null, 0, default, default, default, result, default);

        /// <summary>Creates the post-save event for one durable Qinglan result transaction.</summary>
        public static ApplicationEvent RunResultCommitted(RunResult result) =>
            new ApplicationEvent(ApplicationEventType.RunResultCommitted, null, 0, default, default, default, default, result);
    }

    /// <summary>Single application event source; Simulation has no reference to it.</summary>
    public sealed class ApplicationEventStream
    {
        /// <summary>Raised synchronously when an application event is published.</summary>
        public event Action<ApplicationEvent> Published;
        /// <summary>Publishes one low-frequency application event.</summary>
        public void Publish(ApplicationEvent applicationEvent) => Published?.Invoke(applicationEvent);
    }
}
