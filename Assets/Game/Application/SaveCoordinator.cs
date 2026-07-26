using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Game.Content.Runtime;
using Game.Core;

namespace Game.Application
{
    /// <summary>Coordinates typed save documents without depending on a file system or Unity API.</summary>
    public sealed class SaveCoordinator
    {
        private readonly ISaveStorage storage;
        private readonly ISaveDocumentCodec codec;
        private readonly ContentRegistry content;

        public SaveCoordinator(ISaveStorage saveStorage, ISaveDocumentCodec documentCodec, ContentRegistry contentRegistry)
        {
            storage = saveStorage ?? throw new ArgumentNullException(nameof(saveStorage));
            codec = documentCodec ?? throw new ArgumentNullException(nameof(documentCodec));
            content = contentRegistry ?? throw new ArgumentNullException(nameof(contentRegistry));
        }

        /// <summary>Encodes and atomically stores settings.</summary>
        public ValueTask<SaveStorageWriteResult> SaveSettingsAsync(SettingsSaveData data, CancellationToken cancellationToken = default) =>
            WriteAsync(SaveSlots.Settings, codec.Encode(data), cancellationToken);

        /// <summary>Encodes and atomically stores the long-lived profile.</summary>
        public ValueTask<SaveStorageWriteResult> SaveProfileAsync(ProfileSaveData data, CancellationToken cancellationToken = default) =>
            WriteAsync(SaveSlots.Profile, codec.Encode(data), cancellationToken);

        /// <summary>Encodes and atomically stores run recovery data.</summary>
        public ValueTask<SaveStorageWriteResult> SaveRunRecoveryAsync(RunRecoverySaveData data, CancellationToken cancellationToken = default) =>
            WriteAsync(SaveSlots.RunRecovery, codec.Encode(data), cancellationToken);

        /// <summary>Deletes primary, temporary, and backup recovery files.</summary>
        public ValueTask<SaveStorageWriteResult> ClearRunRecoveryAsync(CancellationToken cancellationToken = default) =>
            storage.DeleteAsync(SaveSlots.RunRecovery, cancellationToken);

        /// <summary>Loads, verifies, and migrates settings with backup fallback.</summary>
        public ValueTask<SaveLoadResult<SettingsSaveData>> LoadSettingsAsync(CancellationToken cancellationToken = default) =>
            LoadAsync(SaveSlots.Settings, codec.DecodeSettings, null, cancellationToken);

        /// <summary>Loads a profile and reports missing optional content.</summary>
        public ValueTask<SaveLoadResult<ProfileSaveData>> LoadProfileAsync(CancellationToken cancellationToken = default) =>
            LoadAsync(SaveSlots.Profile, codec.DecodeProfile, ValidateProfile, cancellationToken);

        /// <summary>Loads recovery data and rejects missing required content.</summary>
        public ValueTask<SaveLoadResult<RunRecoverySaveData>> LoadRunRecoveryAsync(CancellationToken cancellationToken = default) =>
            LoadAsync(SaveSlots.RunRecovery, codec.DecodeRunRecovery, ValidateRecovery, cancellationToken);

        private async ValueTask<SaveStorageWriteResult> WriteAsync(string slot, SaveEncodeResult encoded, CancellationToken token)
        {
            if (!encoded.IsSuccess) return SaveStorageWriteResult.Failure(encoded.Diagnostic);
            return await storage.WriteAtomicAsync(slot, encoded.Data, token).ConfigureAwait(false);
        }

        private async ValueTask<SaveLoadResult<T>> LoadAsync<T>(
            string slot,
            Func<ReadOnlyMemory<byte>, SaveDecodeResult<T>> decode,
            Func<T, ValidationResult> validate,
            CancellationToken token)
        {
            var read = await storage.ReadAsync(slot, token).ConfigureAwait(false);
            if (!read.IsSuccess) return SaveLoadResult<T>.Failed(read.Diagnostic);
            var primary = read.Primary.IsEmpty
                ? SaveDecodeResult<T>.Failure(new SaveDiagnostic(SaveFailureCode.NotFound, "save.error.not_found"))
                : decode(read.Primary);
            if (primary.IsSuccess)
                return Validate(primary.Value, SaveReadSource.Primary, validate, null);

            if (!read.Backup.IsEmpty)
            {
                var backup = decode(read.Backup);
                if (backup.IsSuccess)
                {
                    var recovered = new[]
                    {
                        new SaveDiagnostic(SaveFailureCode.None, "save.warning.recovered_backup", primary.Diagnostic.MessageKey)
                    };
                    return Validate(backup.Value, SaveReadSource.Backup, validate, recovered);
                }
            }

            return SaveLoadResult<T>.Failed(primary.Diagnostic);
        }

        private static SaveLoadResult<T> Validate<T>(T value, SaveReadSource source, Func<T, ValidationResult> validator, SaveDiagnostic[] initial)
        {
            if (validator == null) return SaveLoadResult<T>.Success(value, source, initial);
            var validation = validator(value);
            if (!validation.IsSuccess) return SaveLoadResult<T>.Failed(validation.Failure);
            if ((initial == null || initial.Length == 0) && validation.Diagnostics.Length == 0)
                return SaveLoadResult<T>.Success(value, source);
            var count = (initial?.Length ?? 0) + validation.Diagnostics.Length;
            var combined = new SaveDiagnostic[count];
            if (initial != null) Array.Copy(initial, combined, initial.Length);
            Array.Copy(validation.Diagnostics, 0, combined, initial?.Length ?? 0, validation.Diagnostics.Length);
            return SaveLoadResult<T>.Success(value, source, combined);
        }

        private ValidationResult ValidateProfile(ProfileSaveData profile)
        {
            var warnings = new List<SaveDiagnostic>();
            for (var index = 0; index < profile.UnlockedContentIds.Count; index++)
                AddMissingWarning(profile.UnlockedContentIds[index], warnings);
            for (var index = 0; index < profile.MetaUpgrades.Count; index++)
                AddMissingWarning(profile.MetaUpgrades[index].ContentId, warnings);
            return ValidationResult.Success(warnings.ToArray());
        }

        private ValidationResult ValidateRecovery(RunRecoverySaveData recovery)
        {
            if (!content.TryGet(recovery.CharacterId, out _)) return MissingRecovery(recovery.CharacterId);
            if (!content.TryGet(recovery.MapId, out _)) return MissingRecovery(recovery.MapId);
            for (var index = 0; index < recovery.OwnedContent.Count; index++)
            {
                var id = recovery.OwnedContent[index].ContentId;
                if (!content.TryGet(id, out _)) return MissingRecovery(id);
            }
            return ValidationResult.Success(Array.Empty<SaveDiagnostic>());
        }

        private void AddMissingWarning(ContentId id, List<SaveDiagnostic> warnings)
        {
            if (!content.TryGet(id, out _))
                warnings.Add(new SaveDiagnostic(SaveFailureCode.None, "save.warning.missing_unlock", id.Value, id));
        }

        private static ValidationResult MissingRecovery(ContentId id) =>
            ValidationResult.Failed(new SaveDiagnostic(SaveFailureCode.MissingContent, "save.error.recovery_missing_content", id.Value, id));

        private readonly struct ValidationResult
        {
            private ValidationResult(bool success, SaveDiagnostic[] diagnostics, SaveDiagnostic failure)
            {
                IsSuccess = success;
                Diagnostics = diagnostics ?? Array.Empty<SaveDiagnostic>();
                Failure = failure;
            }
            public bool IsSuccess { get; }
            public SaveDiagnostic[] Diagnostics { get; }
            public SaveDiagnostic Failure { get; }
            public static ValidationResult Success(SaveDiagnostic[] diagnostics) => new ValidationResult(true, diagnostics, default);
            public static ValidationResult Failed(SaveDiagnostic failure) => new ValidationResult(false, null, failure);
        }
    }
}
