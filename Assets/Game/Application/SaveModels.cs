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

    /// <summary>Defines independently evolving versions for the three save documents.</summary>
    public static class SaveSchema
    {
        public const int SettingsCurrentVersion = 2;
        public const int ProfileCurrentVersion = 3;
        public const int RunRecoveryCurrentVersion = 2;

        [Obsolete("Use GetCurrentVersion(SaveDocumentKind) or the document-specific constant.")]
        public const int CurrentVersion = ProfileCurrentVersion;
        public const string GameVersion = "0.1.0";

        public static int GetCurrentVersion(SaveDocumentKind kind)
        {
            switch (kind)
            {
                case SaveDocumentKind.Settings:
                    return SettingsCurrentVersion;
                case SaveDocumentKind.Profile:
                    return ProfileCurrentVersion;
                case SaveDocumentKind.RunRecovery:
                    return RunRecoveryCurrentVersion;
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }
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
            int schemaVersion = SaveSchema.SettingsCurrentVersion,
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
        private readonly ContentId[] activeMetaLoadoutIds;
        private readonly ContentId[] firstClearMapIds;
        private readonly ContentId[] claimedUniqueRewardIds;
        private readonly ContentId[] completedStoryIds;
        private readonly ContentId[] collectedCollectibleIds;
        private readonly ContentId[] committedTransactionIds;
        private readonly IReadOnlyList<SavePackVersion> packsView;
        private readonly IReadOnlyList<ContentId> unlockedView;
        private readonly IReadOnlyList<SavedContentLevel> metaUpgradesView;
        private readonly IReadOnlyList<SavedCounter> currenciesView;
        private readonly IReadOnlyList<SavedCounter> statisticsView;
        private readonly IReadOnlyList<ContentId> activeMetaLoadoutIdsView;
        private readonly IReadOnlyList<ContentId> firstClearMapIdsView;
        private readonly IReadOnlyList<ContentId> claimedUniqueRewardIdsView;
        private readonly IReadOnlyList<ContentId> completedStoryIdsView;
        private readonly IReadOnlyList<ContentId> collectedCollectibleIdsView;
        private readonly IReadOnlyList<ContentId> committedTransactionIdsView;

        public ProfileSaveData(
            string profileId,
            SavePackVersion[] contentPacks,
            ContentId[] unlockedContentIds,
            SavedContentLevel[] savedMetaUpgrades,
            SavedCounter[] savedCurrencies,
            SavedCounter[] savedStatistics,
            string lastWriteUtc,
            int schemaVersion = SaveSchema.ProfileCurrentVersion,
            string gameVersion = SaveSchema.GameVersion)
            : this(
                profileId,
                contentPacks,
                unlockedContentIds,
                savedMetaUpgrades,
                savedCurrencies,
                savedStatistics,
                lastWriteUtc,
                null,
                null,
                null,
                null,
                null,
                null,
                schemaVersion,
                gameVersion)
        {
        }

        /// <summary>Creates a Profile 3 document with canonical Qinglan permanent-state collections.</summary>
        public ProfileSaveData(
            string profileId,
            SavePackVersion[] contentPacks,
            ContentId[] unlockedContentIds,
            SavedContentLevel[] savedMetaUpgrades,
            SavedCounter[] savedCurrencies,
            SavedCounter[] savedStatistics,
            string lastWriteUtc,
            ContentId[] activeMetaLoadoutIds,
            ContentId[] firstClearMapIds,
            ContentId[] claimedUniqueRewardIds,
            ContentId[] completedStoryIds,
            ContentId[] collectedCollectibleIds,
            ContentId[] committedTransactionIds,
            int schemaVersion = SaveSchema.ProfileCurrentVersion,
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
            this.activeMetaLoadoutIds = Canonicalize(activeMetaLoadoutIds);
            this.firstClearMapIds = Canonicalize(firstClearMapIds);
            this.claimedUniqueRewardIds = Canonicalize(claimedUniqueRewardIds);
            this.completedStoryIds = Canonicalize(completedStoryIds);
            this.collectedCollectibleIds = Canonicalize(collectedCollectibleIds);
            this.committedTransactionIds = Canonicalize(committedTransactionIds);
            packsView = Array.AsReadOnly(packs);
            unlockedView = Array.AsReadOnly(unlocked);
            metaUpgradesView = Array.AsReadOnly(metaUpgrades);
            currenciesView = Array.AsReadOnly(currencies);
            statisticsView = Array.AsReadOnly(statistics);
            activeMetaLoadoutIdsView = Array.AsReadOnly(this.activeMetaLoadoutIds);
            firstClearMapIdsView = Array.AsReadOnly(this.firstClearMapIds);
            claimedUniqueRewardIdsView = Array.AsReadOnly(this.claimedUniqueRewardIds);
            completedStoryIdsView = Array.AsReadOnly(this.completedStoryIds);
            collectedCollectibleIdsView = Array.AsReadOnly(this.collectedCollectibleIds);
            committedTransactionIdsView = Array.AsReadOnly(this.committedTransactionIds);
            LastWriteUtc = lastWriteUtc ?? string.Empty;
        }

        public int SchemaVersion { get; }
        public string GameVersion { get; }
        public string ProfileId { get; }
        public IReadOnlyList<SavePackVersion> ContentPacks => packsView;
        public IReadOnlyList<ContentId> UnlockedContentIds => unlockedView;
        public IReadOnlyList<SavedContentLevel> MetaUpgrades => metaUpgradesView;
        public IReadOnlyList<SavedCounter> Currencies => currenciesView;
        public IReadOnlyList<SavedCounter> Statistics => statisticsView;
        public IReadOnlyList<ContentId> ActiveMetaLoadoutIds => activeMetaLoadoutIdsView;
        public IReadOnlyList<ContentId> FirstClearMapIds => firstClearMapIdsView;
        public IReadOnlyList<ContentId> ClaimedUniqueRewardIds => claimedUniqueRewardIdsView;
        public IReadOnlyList<ContentId> CompletedStoryIds => completedStoryIdsView;
        public IReadOnlyList<ContentId> CollectedCollectibleIds => collectedCollectibleIdsView;
        public IReadOnlyList<ContentId> CommittedTransactionIds => committedTransactionIdsView;
        public string LastWriteUtc { get; }

        private static T[] Clone<T>(T[] source) => source == null ? Array.Empty<T>() : (T[])source.Clone();

        private static ContentId[] Canonicalize(ContentId[] source)
        {
            if (source == null || source.Length == 0) return Array.Empty<ContentId>();
            var copy = (ContentId[])source.Clone();
            for (var index = 0; index < copy.Length; index++)
            {
                if (!copy[index].IsValid)
                    throw new ArgumentException("Profile content ID collections cannot contain invalid IDs.", nameof(source));
            }

            Array.Sort(copy, CompareContentIds);
            var count = 0;
            for (var index = 0; index < copy.Length; index++)
            {
                if (count > 0 && copy[index] == copy[count - 1]) continue;
                copy[count++] = copy[index];
            }

            if (count == copy.Length) return copy;
            var result = new ContentId[count];
            Array.Copy(copy, result, count);
            return result;
        }

        private static int CompareContentIds(ContentId left, ContentId right) =>
            string.CompareOrdinal(left.Value, right.Value);
    }

    /// <summary>Independent run_recovery.json model using stable content IDs only.</summary>
    public sealed class RunRecoverySaveData
    {
        private readonly SavePackVersion[] packs;
        private readonly SavedContentLevel[] ownedContent;
        private readonly IReadOnlyList<SavePackVersion> packsView;
        private readonly IReadOnlyList<SavedContentLevel> ownedContentView;

        public RunRecoverySaveData(
            ulong runSeed,
            long tick,
            ContentId characterId,
            ContentId mapId,
            SavePackVersion[] contentPacks,
            SavedContentLevel[] ownedContentLevels,
            string lastWriteUtc,
            int schemaVersion = SaveSchema.RunRecoveryCurrentVersion,
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
            packsView = Array.AsReadOnly(packs);
            ownedContentView = Array.AsReadOnly(ownedContent);
            LastWriteUtc = lastWriteUtc ?? string.Empty;
        }

        public int SchemaVersion { get; }
        public string GameVersion { get; }
        public ulong RunSeed { get; }
        public long Tick { get; }
        public ContentId CharacterId { get; }
        public ContentId MapId { get; }
        public IReadOnlyList<SavePackVersion> ContentPacks => packsView;
        public IReadOnlyList<SavedContentLevel> OwnedContent => ownedContentView;
        public string LastWriteUtc { get; }
    }
}
