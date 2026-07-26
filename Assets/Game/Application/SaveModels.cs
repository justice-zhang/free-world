using System;
using System.Collections.Generic;
using Game.Core;

namespace Game.Application
{
    /// <summary>Identifies one independently versioned save document.</summary>
    public enum SaveDocumentKind : byte
    {
        Settings = 0,
        Profile = 1,
        RunRecovery = 2
    }

    /// <summary>Defines the current save schema shared by the three M8 documents.</summary>
    public static class SaveSchema
    {
        public const int CurrentVersion = 2;
        public const string GameVersion = "0.1.0";
    }

    /// <summary>Stable content-pack identity recorded in a save.</summary>
    public readonly struct SavePackVersion
    {
        public SavePackVersion(ContentId packId, ContentVersion version)
        {
            if (!packId.IsValid) throw new ArgumentException("Pack ID must be valid.", nameof(packId));
            PackId = packId;
            Version = version;
        }

        public ContentId PackId { get; }
        public ContentVersion Version { get; }
    }

    /// <summary>One serializable Input System binding override.</summary>
    public readonly struct SavedBindingOverride
    {
        public SavedBindingOverride(string actionName, int bindingIndex, string controlPath)
        {
            if (string.IsNullOrWhiteSpace(actionName)) throw new ArgumentException("Action name is required.", nameof(actionName));
            if (bindingIndex < 0) throw new ArgumentOutOfRangeException(nameof(bindingIndex));
            ActionName = actionName;
            BindingIndex = bindingIndex;
            ControlPath = controlPath ?? string.Empty;
        }

        public string ActionName { get; }
        public int BindingIndex { get; }
        public string ControlPath { get; }
    }

    /// <summary>Stable string key and integer value used by profile counters.</summary>
    public readonly struct SavedCounter
    {
        public SavedCounter(string key, long value)
        {
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("Counter key is required.", nameof(key));
            Key = key;
            Value = value;
        }

        public string Key { get; }
        public long Value { get; }
    }

    /// <summary>Stable content identity and level used by profile or recovery data.</summary>
    public readonly struct SavedContentLevel
    {
        public SavedContentLevel(ContentId contentId, int level)
        {
            if (!contentId.IsValid) throw new ArgumentException("Content ID must be valid.", nameof(contentId));
            if (level < 1) throw new ArgumentOutOfRangeException(nameof(level));
            ContentId = contentId;
            Level = level;
        }

        public ContentId ContentId { get; }
        public int Level { get; }
    }

    /// <summary>Independent settings.json model containing pure data only.</summary>
    public sealed class SettingsSaveData
    {
        private readonly SavedBindingOverride[] bindings;
        private readonly IReadOnlyList<SavedBindingOverride> bindingsView;

        public SettingsSaveData(
            string localeCode,
            float stickDeadzone,
            float vibrationIntensity,
            bool screenShakeEnabled,
            float flashIntensity,
            bool damageNumbersEnabled,
            AutoAimStrategy autoAim,
            SavedBindingOverride[] bindingOverrides = null,
            int schemaVersion = SaveSchema.CurrentVersion,
            string gameVersion = SaveSchema.GameVersion)
        {
            if (schemaVersion < 1) throw new ArgumentOutOfRangeException(nameof(schemaVersion));
            if (string.IsNullOrWhiteSpace(localeCode)) throw new ArgumentException("Locale code is required.", nameof(localeCode));
            SchemaVersion = schemaVersion;
            GameVersion = gameVersion ?? string.Empty;
            LocaleCode = localeCode;
            StickDeadzone = RequireRange(stickDeadzone, 0f, 0.95f, nameof(stickDeadzone));
            VibrationIntensity = RequireRange(vibrationIntensity, 0f, 1f, nameof(vibrationIntensity));
            ScreenShakeEnabled = screenShakeEnabled;
            FlashIntensity = RequireRange(flashIntensity, 0f, 1f, nameof(flashIntensity));
            DamageNumbersEnabled = damageNumbersEnabled;
            if (autoAim < AutoAimStrategy.Nearest || autoAim > AutoAimStrategy.Disabled)
                throw new ArgumentOutOfRangeException(nameof(autoAim));
            AutoAim = autoAim;
            bindings = bindingOverrides == null ? Array.Empty<SavedBindingOverride>() : (SavedBindingOverride[])bindingOverrides.Clone();
            bindingsView = Array.AsReadOnly(bindings);
        }

        public int SchemaVersion { get; }
        public string GameVersion { get; }
        public string LocaleCode { get; }
        public float StickDeadzone { get; }
        public float VibrationIntensity { get; }
        public bool ScreenShakeEnabled { get; }
        public float FlashIntensity { get; }
        public bool DamageNumbersEnabled { get; }
        public AutoAimStrategy AutoAim { get; }
        public IReadOnlyList<SavedBindingOverride> BindingOverrides => bindingsView;

        private static float RequireRange(float value, float minimum, float maximum, string parameter)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < minimum || value > maximum)
                throw new ArgumentOutOfRangeException(parameter);
            return value;
        }
    }

    /// <summary>Independent profile.json model containing long-lived pure data.</summary>
    public sealed class ProfileSaveData
    {
        private readonly SavePackVersion[] packs;
        private readonly ContentId[] unlocked;
        private readonly SavedContentLevel[] metaUpgrades;
        private readonly SavedCounter[] currencies;
        private readonly SavedCounter[] statistics;

        public ProfileSaveData(
            string profileId,
            SavePackVersion[] contentPacks,
            ContentId[] unlockedContentIds,
            SavedContentLevel[] savedMetaUpgrades,
            SavedCounter[] savedCurrencies,
            SavedCounter[] savedStatistics,
            string lastWriteUtc,
            int schemaVersion = SaveSchema.CurrentVersion,
            string gameVersion = SaveSchema.GameVersion)
        {
            if (schemaVersion < 1) throw new ArgumentOutOfRangeException(nameof(schemaVersion));
            if (string.IsNullOrWhiteSpace(profileId)) throw new ArgumentException("Profile ID is required.", nameof(profileId));
            SchemaVersion = schemaVersion;
            GameVersion = gameVersion ?? string.Empty;
            ProfileId = profileId;
            packs = Clone(contentPacks);
            unlocked = Clone(unlockedContentIds);
            metaUpgrades = Clone(savedMetaUpgrades);
            currencies = Clone(savedCurrencies);
            statistics = Clone(savedStatistics);
            LastWriteUtc = lastWriteUtc ?? string.Empty;
        }

        public int SchemaVersion { get; }
        public string GameVersion { get; }
        public string ProfileId { get; }
        public IReadOnlyList<SavePackVersion> ContentPacks => Array.AsReadOnly(packs);
        public IReadOnlyList<ContentId> UnlockedContentIds => Array.AsReadOnly(unlocked);
        public IReadOnlyList<SavedContentLevel> MetaUpgrades => Array.AsReadOnly(metaUpgrades);
        public IReadOnlyList<SavedCounter> Currencies => Array.AsReadOnly(currencies);
        public IReadOnlyList<SavedCounter> Statistics => Array.AsReadOnly(statistics);
        public string LastWriteUtc { get; }

        private static T[] Clone<T>(T[] source) => source == null ? Array.Empty<T>() : (T[])source.Clone();
    }

    /// <summary>Independent run_recovery.json model using stable content IDs only.</summary>
    public sealed class RunRecoverySaveData
    {
        private readonly SavePackVersion[] packs;
        private readonly SavedContentLevel[] ownedContent;

        public RunRecoverySaveData(
            ulong runSeed,
            long tick,
            ContentId characterId,
            ContentId mapId,
            SavePackVersion[] contentPacks,
            SavedContentLevel[] ownedContentLevels,
            string lastWriteUtc,
            int schemaVersion = SaveSchema.CurrentVersion,
            string gameVersion = SaveSchema.GameVersion)
        {
            if (schemaVersion < 1) throw new ArgumentOutOfRangeException(nameof(schemaVersion));
            if (tick < 0) throw new ArgumentOutOfRangeException(nameof(tick));
            if (!characterId.IsValid) throw new ArgumentException("Character ID must be valid.", nameof(characterId));
            if (!mapId.IsValid) throw new ArgumentException("Map ID must be valid.", nameof(mapId));
            SchemaVersion = schemaVersion;
            GameVersion = gameVersion ?? string.Empty;
            RunSeed = runSeed;
            Tick = tick;
            CharacterId = characterId;
            MapId = mapId;
            packs = contentPacks == null ? Array.Empty<SavePackVersion>() : (SavePackVersion[])contentPacks.Clone();
            ownedContent = ownedContentLevels == null ? Array.Empty<SavedContentLevel>() : (SavedContentLevel[])ownedContentLevels.Clone();
            LastWriteUtc = lastWriteUtc ?? string.Empty;
        }

        public int SchemaVersion { get; }
        public string GameVersion { get; }
        public ulong RunSeed { get; }
        public long Tick { get; }
        public ContentId CharacterId { get; }
        public ContentId MapId { get; }
        public IReadOnlyList<SavePackVersion> ContentPacks => Array.AsReadOnly(packs);
        public IReadOnlyList<SavedContentLevel> OwnedContent => Array.AsReadOnly(ownedContent);
        public string LastWriteUtc { get; }
    }
}
