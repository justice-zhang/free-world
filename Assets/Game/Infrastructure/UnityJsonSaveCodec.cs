using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Game.Application;
using Game.Core;
using UnityEngine;

namespace Game.Infrastructure
{
    /// <summary>Explicit JsonUtility DTO codec with a SHA-256 envelope and migration chain.</summary>
    public sealed class UnityJsonSaveCodec : ISaveDocumentCodec
    {
        private readonly SaveMigrationRegistry migrations;

        public UnityJsonSaveCodec(SaveMigrationRegistry migrationRegistry = null)
        {
            migrations = migrationRegistry ?? CreateDefaultMigrationRegistry();
        }

        /// <summary>Creates built-in contiguous migrations for each independently versioned document.</summary>
        public static SaveMigrationRegistry CreateDefaultMigrationRegistry()
        {
            var registry = new SaveMigrationRegistry();
            registry.Register(new SettingsV1ToV2Migration());
            registry.Register(new ProfileV1ToV2Migration());
            registry.Register(new ProfileV2ToV3Migration());
            registry.Register(new RunRecoveryV1ToV2Migration());
            return registry;
        }

        /// <summary>Encodes settings into a checksummed UTF-8 envelope.</summary>
        public SaveEncodeResult Encode(SettingsSaveData data) => EncodeDto(SaveDocumentKind.Settings, data?.SchemaVersion ?? 0, ToDto(data));
        /// <summary>Encodes a profile into a checksummed UTF-8 envelope.</summary>
        public SaveEncodeResult Encode(ProfileSaveData data) => EncodeDto(SaveDocumentKind.Profile, data?.SchemaVersion ?? 0, ToDto(data));
        /// <summary>Encodes run recovery into a checksummed UTF-8 envelope.</summary>
        public SaveEncodeResult Encode(RunRecoverySaveData data) => EncodeDto(SaveDocumentKind.RunRecovery, data?.SchemaVersion ?? 0, ToDto(data));

        /// <summary>Verifies, migrates, and decodes settings.</summary>
        public SaveDecodeResult<SettingsSaveData> DecodeSettings(ReadOnlyMemory<byte> data)
        {
            var payload = DecodePayload(data, SaveDocumentKind.Settings);
            if (!payload.IsSuccess) return SaveDecodeResult<SettingsSaveData>.Failure(payload.Diagnostic);
            try { return FromSettingsDto(JsonUtility.FromJson<SettingsDto>(payload.PayloadJson)); }
            catch (Exception exception) when (IsFormatFailure(exception)) { return FormatFailure<SettingsSaveData>(exception); }
        }

        /// <summary>Verifies, migrates, and decodes a profile.</summary>
        public SaveDecodeResult<ProfileSaveData> DecodeProfile(ReadOnlyMemory<byte> data)
        {
            var payload = DecodePayload(data, SaveDocumentKind.Profile);
            if (!payload.IsSuccess) return SaveDecodeResult<ProfileSaveData>.Failure(payload.Diagnostic);
            try { return FromProfileDto(JsonUtility.FromJson<ProfileDto>(payload.PayloadJson)); }
            catch (Exception exception) when (IsFormatFailure(exception)) { return FormatFailure<ProfileSaveData>(exception); }
        }

        /// <summary>Verifies, migrates, and decodes run recovery.</summary>
        public SaveDecodeResult<RunRecoverySaveData> DecodeRunRecovery(ReadOnlyMemory<byte> data)
        {
            var payload = DecodePayload(data, SaveDocumentKind.RunRecovery);
            if (!payload.IsSuccess) return SaveDecodeResult<RunRecoverySaveData>.Failure(payload.Diagnostic);
            try { return FromRecoveryDto(JsonUtility.FromJson<RunRecoveryDto>(payload.PayloadJson)); }
            catch (Exception exception) when (IsFormatFailure(exception)) { return FormatFailure<RunRecoverySaveData>(exception); }
        }

        /// <summary>Creates a checksummed envelope for import tools and fixed migration samples.</summary>
        public byte[] EncodeRawPayload(SaveDocumentKind kind, int schemaVersion, string payloadJson)
        {
            if (schemaVersion < 1) throw new ArgumentOutOfRangeException(nameof(schemaVersion));
            var payload = Encoding.UTF8.GetBytes(payloadJson ?? string.Empty);
            var envelope = new EnvelopeDto
            {
                documentKind = KindToken(kind),
                schemaVersion = schemaVersion,
                checksumSha256 = ComputeSha256(payload),
                payloadBase64 = Convert.ToBase64String(payload)
            };
            return Encoding.UTF8.GetBytes(JsonUtility.ToJson(envelope, true));
        }

        private SaveEncodeResult EncodeDto(SaveDocumentKind kind, int schemaVersion, object dto)
        {
            if (dto == null || schemaVersion != SaveSchema.GetCurrentVersion(kind))
                return SaveEncodeResult.Failure(new SaveDiagnostic(SaveFailureCode.UnsupportedSchema, "save.error.schema_newer"));
            try { return SaveEncodeResult.Success(EncodeRawPayload(kind, schemaVersion, JsonUtility.ToJson(dto, false))); }
            catch (Exception exception) when (IsFormatFailure(exception))
            {
                return SaveEncodeResult.Failure(new SaveDiagnostic(SaveFailureCode.InvalidFormat, "save.error.invalid_format", exception.GetType().Name));
            }
        }

        private DecodedPayload DecodePayload(ReadOnlyMemory<byte> data, SaveDocumentKind expectedKind)
        {
            try
            {
                if (data.IsEmpty) return DecodedPayload.Failure(new SaveDiagnostic(SaveFailureCode.InvalidFormat, "save.error.invalid_format"));
                var envelope = JsonUtility.FromJson<EnvelopeDto>(Encoding.UTF8.GetString(data.ToArray()));
                if (envelope == null || !string.Equals(envelope.documentKind, KindToken(expectedKind), StringComparison.Ordinal) || envelope.schemaVersion < 1)
                    return DecodedPayload.Failure(new SaveDiagnostic(SaveFailureCode.InvalidFormat, "save.error.invalid_format"));
                var payload = Convert.FromBase64String(envelope.payloadBase64 ?? string.Empty);
                if (!FixedEquals(envelope.checksumSha256, ComputeSha256(payload)))
                    return DecodedPayload.Failure(new SaveDiagnostic(SaveFailureCode.ChecksumMismatch, "save.error.checksum"));
                var json = Encoding.UTF8.GetString(payload);
                var migration = migrations.Migrate(
                    expectedKind,
                    envelope.schemaVersion,
                    SaveSchema.GetCurrentVersion(expectedKind),
                    json);
                return migration.IsSuccess
                    ? DecodedPayload.Success(migration.PayloadJson)
                    : DecodedPayload.Failure(migration.Diagnostic);
            }
            catch (Exception exception) when (IsFormatFailure(exception))
            {
                return DecodedPayload.Failure(new SaveDiagnostic(SaveFailureCode.InvalidFormat, "save.error.invalid_format", exception.GetType().Name));
            }
        }

        private static SettingsDto ToDto(SettingsSaveData data)
        {
            if (data == null) return null;
            var bindings = new BindingDto[data.BindingOverrides.Count];
            for (var index = 0; index < bindings.Length; index++)
            {
                var item = data.BindingOverrides[index];
                bindings[index] = new BindingDto { actionName = item.ActionName, bindingIndex = item.BindingIndex, controlPath = item.ControlPath };
            }
            return new SettingsDto
            {
                schemaVersion = data.SchemaVersion,
                gameVersion = data.GameVersion,
                localeCode = data.LocaleCode,
                stickDeadzone = data.StickDeadzone,
                vibrationIntensity = data.VibrationIntensity,
                screenShakeEnabled = data.ScreenShakeEnabled,
                flashIntensity = data.FlashIntensity,
                damageNumbersEnabled = data.DamageNumbersEnabled,
                autoAim = (int)data.AutoAim,
                bindingOverrides = bindings
            };
        }

        private static ProfileDto ToDto(ProfileSaveData data)
        {
            if (data == null) return null;
            return new ProfileDto
            {
                schemaVersion = data.SchemaVersion,
                gameVersion = data.GameVersion,
                profileId = data.ProfileId,
                contentPacks = ToPackDtos(data.ContentPacks),
                unlockedContentIds = ToContentIds(data.UnlockedContentIds),
                metaUpgrades = ToContentLevelDtos(data.MetaUpgrades),
                currencies = ToCounterDtos(data.Currencies),
                statistics = ToCounterDtos(data.Statistics),
                activeMetaLoadoutIds = ToContentIds(data.ActiveMetaLoadoutIds),
                firstClearMapIds = ToContentIds(data.FirstClearMapIds),
                claimedUniqueRewardIds = ToContentIds(data.ClaimedUniqueRewardIds),
                completedStoryIds = ToContentIds(data.CompletedStoryIds),
                collectedCollectibleIds = ToContentIds(data.CollectedCollectibleIds),
                committedTransactionIds = ToContentIds(data.CommittedTransactionIds),
                lastWriteUtc = data.LastWriteUtc
            };
        }

        private static RunRecoveryDto ToDto(RunRecoverySaveData data)
        {
            if (data == null) return null;
            return new RunRecoveryDto
            {
                schemaVersion = data.SchemaVersion,
                gameVersion = data.GameVersion,
                runSeed = data.RunSeed.ToString(CultureInfo.InvariantCulture),
                tick = data.Tick,
                characterId = data.CharacterId.Value,
                mapId = data.MapId.Value,
                contentPacks = ToPackDtos(data.ContentPacks),
                ownedContent = ToContentLevelDtos(data.OwnedContent),
                lastWriteUtc = data.LastWriteUtc
            };
        }

        private static SaveDecodeResult<SettingsSaveData> FromSettingsDto(SettingsDto dto)
        {
            if (dto == null || dto.schemaVersion != SaveSchema.SettingsCurrentVersion || string.IsNullOrWhiteSpace(dto.localeCode))
                return Invalid<SettingsSaveData>();
            var source = dto.bindingOverrides ?? Array.Empty<BindingDto>();
            var bindings = new SavedBindingOverride[source.Length];
            for (var index = 0; index < source.Length; index++)
                bindings[index] = new SavedBindingOverride(source[index].actionName, source[index].bindingIndex, source[index].controlPath);
            return SaveDecodeResult<SettingsSaveData>.Success(new SettingsSaveData(
                dto.localeCode, dto.stickDeadzone, dto.vibrationIntensity, dto.screenShakeEnabled,
                dto.flashIntensity, dto.damageNumbersEnabled, (AutoAimStrategy)dto.autoAim, bindings,
                dto.schemaVersion, dto.gameVersion));
        }

        private static SaveDecodeResult<ProfileSaveData> FromProfileDto(ProfileDto dto)
        {
            if (dto == null || dto.schemaVersion != SaveSchema.ProfileCurrentVersion || string.IsNullOrWhiteSpace(dto.profileId))
                return Invalid<ProfileSaveData>();
            return SaveDecodeResult<ProfileSaveData>.Success(new ProfileSaveData(
                dto.profileId,
                FromPackDtos(dto.contentPacks),
                FromContentIds(dto.unlockedContentIds),
                FromContentLevelDtos(dto.metaUpgrades),
                FromCounterDtos(dto.currencies),
                FromCounterDtos(dto.statistics),
                dto.lastWriteUtc,
                FromContentIds(dto.activeMetaLoadoutIds),
                FromContentIds(dto.firstClearMapIds),
                FromContentIds(dto.claimedUniqueRewardIds),
                FromContentIds(dto.completedStoryIds),
                FromContentIds(dto.collectedCollectibleIds),
                FromContentIds(dto.committedTransactionIds),
                dto.schemaVersion,
                dto.gameVersion));
        }

        private static SaveDecodeResult<RunRecoverySaveData> FromRecoveryDto(RunRecoveryDto dto)
        {
            if (dto == null || dto.schemaVersion != SaveSchema.RunRecoveryCurrentVersion ||
                !ulong.TryParse(dto.runSeed, NumberStyles.None, CultureInfo.InvariantCulture, out var seed))
                return Invalid<RunRecoverySaveData>();
            return SaveDecodeResult<RunRecoverySaveData>.Success(new RunRecoverySaveData(
                seed, dto.tick, RequireId(dto.characterId), RequireId(dto.mapId),
                FromPackDtos(dto.contentPacks), FromContentLevelDtos(dto.ownedContent),
                dto.lastWriteUtc, dto.schemaVersion, dto.gameVersion));
        }

        private static PackDto[] ToPackDtos(IReadOnlyList<SavePackVersion> source)
        {
            var result = new PackDto[source.Count];
            for (var index = 0; index < result.Length; index++)
                result[index] = new PackDto { packId = source[index].PackId.Value, version = source[index].Version.ToString() };
            return result;
        }

        private static SavePackVersion[] FromPackDtos(PackDto[] source)
        {
            source = source ?? Array.Empty<PackDto>();
            var result = new SavePackVersion[source.Length];
            for (var index = 0; index < result.Length; index++)
            {
                if (!ContentVersion.TryParse(source[index].version, out var version)) throw new FormatException("Invalid pack version.");
                result[index] = new SavePackVersion(RequireId(source[index].packId), version);
            }
            return result;
        }

        private static string[] ToContentIds(IReadOnlyList<ContentId> source)
        {
            var result = new string[source.Count];
            for (var index = 0; index < result.Length; index++) result[index] = source[index].Value;
            return result;
        }

        private static ContentId[] FromContentIds(string[] source)
        {
            source = source ?? Array.Empty<string>();
            var result = new ContentId[source.Length];
            for (var index = 0; index < result.Length; index++) result[index] = RequireId(source[index]);
            return result;
        }

        private static ContentLevelDto[] ToContentLevelDtos(IReadOnlyList<SavedContentLevel> source)
        {
            var result = new ContentLevelDto[source.Count];
            for (var index = 0; index < result.Length; index++)
                result[index] = new ContentLevelDto { contentId = source[index].ContentId.Value, level = source[index].Level };
            return result;
        }

        private static SavedContentLevel[] FromContentLevelDtos(ContentLevelDto[] source)
        {
            source = source ?? Array.Empty<ContentLevelDto>();
            var result = new SavedContentLevel[source.Length];
            for (var index = 0; index < result.Length; index++)
                result[index] = new SavedContentLevel(RequireId(source[index].contentId), source[index].level);
            return result;
        }

        private static CounterDto[] ToCounterDtos(IReadOnlyList<SavedCounter> source)
        {
            var result = new CounterDto[source.Count];
            for (var index = 0; index < result.Length; index++) result[index] = new CounterDto { key = source[index].Key, value = source[index].Value };
            return result;
        }

        private static SavedCounter[] FromCounterDtos(CounterDto[] source)
        {
            source = source ?? Array.Empty<CounterDto>();
            var result = new SavedCounter[source.Length];
            for (var index = 0; index < result.Length; index++) result[index] = new SavedCounter(source[index].key, source[index].value);
            return result;
        }

        private static ContentId RequireId(string value)
        {
            var result = ContentId.Deserialize(value);
            if (!result.IsSuccess) throw new FormatException(result.Error.Message);
            return result.Value;
        }

        private static SaveDecodeResult<T> Invalid<T>() => SaveDecodeResult<T>.Failure(new SaveDiagnostic(SaveFailureCode.InvalidFormat, "save.error.invalid_format"));
        private static SaveDecodeResult<T> FormatFailure<T>(Exception exception) => SaveDecodeResult<T>.Failure(new SaveDiagnostic(SaveFailureCode.InvalidFormat, "save.error.invalid_format", exception.GetType().Name));
        private static bool IsFormatFailure(Exception exception) => exception is ArgumentException || exception is FormatException || exception is OverflowException;
        private static string KindToken(SaveDocumentKind kind) => kind == SaveDocumentKind.Settings ? "settings" : kind == SaveDocumentKind.Profile ? "profile" : "run_recovery";

        private static string ComputeSha256(byte[] data)
        {
            using (var algorithm = SHA256.Create())
            {
                var hash = algorithm.ComputeHash(data);
                var builder = new StringBuilder(hash.Length * 2);
                for (var index = 0; index < hash.Length; index++) builder.Append(hash[index].ToString("x2", CultureInfo.InvariantCulture));
                return builder.ToString();
            }
        }

        private static bool FixedEquals(string left, string right)
        {
            if (left == null || right == null || left.Length != right.Length) return false;
            var different = 0;
            for (var index = 0; index < left.Length; index++) different |= left[index] ^ right[index];
            return different == 0;
        }

        private readonly struct DecodedPayload
        {
            private DecodedPayload(bool success, string json, SaveDiagnostic diagnostic) { IsSuccess = success; PayloadJson = json; Diagnostic = diagnostic; }
            public bool IsSuccess { get; }
            public string PayloadJson { get; }
            public SaveDiagnostic Diagnostic { get; }
            public static DecodedPayload Success(string json) => new DecodedPayload(true, json, default);
            public static DecodedPayload Failure(SaveDiagnostic diagnostic) => new DecodedPayload(false, string.Empty, diagnostic);
        }

        [Serializable] private sealed class EnvelopeDto { public string documentKind; public int schemaVersion; public string checksumSha256; public string payloadBase64; }
        [Serializable] private sealed class BindingDto { public string actionName; public int bindingIndex; public string controlPath; }
        [Serializable] private sealed class PackDto { public string packId; public string version; }
        [Serializable] private sealed class ContentLevelDto { public string contentId; public int level; }
        [Serializable] private sealed class CounterDto { public string key; public long value; }
        [Serializable] private sealed class SettingsDto
        {
            public int schemaVersion; public string gameVersion; public string localeCode; public float stickDeadzone; public float vibrationIntensity;
            public bool screenShakeEnabled; public float flashIntensity; public bool damageNumbersEnabled; public int autoAim; public BindingDto[] bindingOverrides;
        }
        [Serializable] private sealed class ProfileDto
        {
            public int schemaVersion; public string gameVersion; public string profileId; public PackDto[] contentPacks; public string[] unlockedContentIds;
            public ContentLevelDto[] metaUpgrades; public CounterDto[] currencies; public CounterDto[] statistics;
            public string[] activeMetaLoadoutIds; public string[] firstClearMapIds; public string[] claimedUniqueRewardIds;
            public string[] completedStoryIds; public string[] collectedCollectibleIds; public string[] committedTransactionIds;
            public string lastWriteUtc;
        }
        [Serializable] private sealed class RunRecoveryDto
        {
            public int schemaVersion; public string gameVersion; public string runSeed; public long tick; public string characterId; public string mapId;
            public PackDto[] contentPacks; public ContentLevelDto[] ownedContent; public string lastWriteUtc;
        }
        [Serializable] private sealed class SettingsV1Dto { public int schemaVersion; public string localeCode; public float stickDeadzone; }
        [Serializable] private sealed class ProfileV1Dto { public int schemaVersion; public string profileId; public string[] unlockedContentIds; public string lastWriteUtc; }
        [Serializable] private sealed class RunRecoveryV1Dto { public int schemaVersion; public string runSeed; public long tick; public string characterId; public string mapId; public string[] ownedContentIds; public string lastWriteUtc; }

        private sealed class SettingsV1ToV2Migration : ISaveMigration
        {
            public SaveDocumentKind DocumentKind => SaveDocumentKind.Settings;
            public int FromVersion => 1;
            public int ToVersion => 2;
            public SaveMigrationResult Migrate(string payloadJson)
            {
                var old = JsonUtility.FromJson<SettingsV1Dto>(payloadJson);
                if (old == null || string.IsNullOrWhiteSpace(old.localeCode)) return SaveMigrationResult.Failure(new SaveDiagnostic(SaveFailureCode.InvalidFormat, "save.error.invalid_format"));
                var dto = new SettingsDto
                {
                    schemaVersion = 2, gameVersion = SaveSchema.GameVersion, localeCode = old.localeCode,
                    stickDeadzone = old.stickDeadzone <= 0f ? 0.15f : old.stickDeadzone,
                    vibrationIntensity = 1f, screenShakeEnabled = true, flashIntensity = 1f,
                    damageNumbersEnabled = true, autoAim = 0, bindingOverrides = Array.Empty<BindingDto>()
                };
                return SaveMigrationResult.Success(JsonUtility.ToJson(dto, false));
            }
        }

        private sealed class ProfileV1ToV2Migration : ISaveMigration
        {
            public SaveDocumentKind DocumentKind => SaveDocumentKind.Profile;
            public int FromVersion => 1;
            public int ToVersion => 2;
            public SaveMigrationResult Migrate(string payloadJson)
            {
                var old = JsonUtility.FromJson<ProfileV1Dto>(payloadJson);
                if (old == null || string.IsNullOrWhiteSpace(old.profileId)) return SaveMigrationResult.Failure(new SaveDiagnostic(SaveFailureCode.InvalidFormat, "save.error.invalid_format"));
                return SaveMigrationResult.Success(JsonUtility.ToJson(new ProfileDto
                {
                    schemaVersion = 2, gameVersion = SaveSchema.GameVersion, profileId = old.profileId,
                    contentPacks = Array.Empty<PackDto>(), unlockedContentIds = old.unlockedContentIds ?? Array.Empty<string>(),
                    metaUpgrades = Array.Empty<ContentLevelDto>(), currencies = Array.Empty<CounterDto>(), statistics = Array.Empty<CounterDto>(), lastWriteUtc = old.lastWriteUtc
                }, false));
            }
        }

        private sealed class ProfileV2ToV3Migration : ISaveMigration
        {
            public SaveDocumentKind DocumentKind => SaveDocumentKind.Profile;
            public int FromVersion => 2;
            public int ToVersion => 3;

            public SaveMigrationResult Migrate(string payloadJson)
            {
                var dto = JsonUtility.FromJson<ProfileDto>(payloadJson);
                if (dto == null || dto.schemaVersion != 2 || string.IsNullOrWhiteSpace(dto.profileId))
                {
                    return SaveMigrationResult.Failure(
                        new SaveDiagnostic(SaveFailureCode.InvalidFormat, "save.error.invalid_format"));
                }

                dto.schemaVersion = 3;
                dto.gameVersion = string.IsNullOrWhiteSpace(dto.gameVersion)
                    ? SaveSchema.GameVersion
                    : dto.gameVersion;
                dto.activeMetaLoadoutIds = Array.Empty<string>();
                dto.firstClearMapIds = Array.Empty<string>();
                dto.claimedUniqueRewardIds = Array.Empty<string>();
                dto.completedStoryIds = Array.Empty<string>();
                dto.collectedCollectibleIds = Array.Empty<string>();
                dto.committedTransactionIds = Array.Empty<string>();
                return SaveMigrationResult.Success(JsonUtility.ToJson(dto, false));
            }
        }

        private sealed class RunRecoveryV1ToV2Migration : ISaveMigration
        {
            public SaveDocumentKind DocumentKind => SaveDocumentKind.RunRecovery;
            public int FromVersion => 1;
            public int ToVersion => 2;
            public SaveMigrationResult Migrate(string payloadJson)
            {
                var old = JsonUtility.FromJson<RunRecoveryV1Dto>(payloadJson);
                if (old == null) return SaveMigrationResult.Failure(new SaveDiagnostic(SaveFailureCode.InvalidFormat, "save.error.invalid_format"));
                var owned = old.ownedContentIds ?? Array.Empty<string>();
                var levels = new ContentLevelDto[owned.Length];
                for (var index = 0; index < owned.Length; index++) levels[index] = new ContentLevelDto { contentId = owned[index], level = 1 };
                return SaveMigrationResult.Success(JsonUtility.ToJson(new RunRecoveryDto
                {
                    schemaVersion = 2, gameVersion = SaveSchema.GameVersion, runSeed = old.runSeed, tick = old.tick,
                    characterId = old.characterId, mapId = old.mapId, contentPacks = Array.Empty<PackDto>(),
                    ownedContent = levels, lastWriteUtc = old.lastWriteUtc
                }, false));
            }
        }
    }
}
