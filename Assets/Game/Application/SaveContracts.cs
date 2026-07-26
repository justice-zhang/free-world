using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Game.Core;

namespace Game.Application
{
    /// <summary>Stable local file names for independently versioned save documents.</summary>
    public static class SaveSlots
    {
        public const string Settings = "settings.json";
        public const string Profile = "profile.json";
        public const string RunRecovery = "run_recovery.json";
    }

    /// <summary>Machine-readable reasons returned by persistence operations.</summary>
    public enum SaveFailureCode : byte
    {
        None = 0,
        NotFound = 1,
        Cancelled = 2,
        IoFailure = 3,
        InvalidFormat = 4,
        ChecksumMismatch = 5,
        UnsupportedSchema = 6,
        MissingContent = 7
    }

    /// <summary>Identifies which validated file supplied a loaded document.</summary>
    public enum SaveReadSource : byte
    {
        None = 0,
        Primary = 1,
        Backup = 2
    }

    /// <summary>Application-safe save result with a localizable message key.</summary>
    public readonly struct SaveDiagnostic
    {
        public SaveDiagnostic(SaveFailureCode code, string messageKey, string detail = "", ContentId contentId = default)
        {
            Code = code;
            MessageKey = messageKey ?? string.Empty;
            Detail = detail ?? string.Empty;
            ContentId = contentId;
        }

        public SaveFailureCode Code { get; }
        public string MessageKey { get; }
        public string Detail { get; }
        public ContentId ContentId { get; }
        public bool IsError => Code != SaveFailureCode.None;
    }

    /// <summary>Raw primary and backup bytes returned by a storage backend.</summary>
    public readonly struct SaveStorageReadResult
    {
        private SaveStorageReadResult(bool success, ReadOnlyMemory<byte> primary, ReadOnlyMemory<byte> backup, SaveDiagnostic diagnostic)
        {
            IsSuccess = success;
            Primary = primary;
            Backup = backup;
            Diagnostic = diagnostic;
        }

        public bool IsSuccess { get; }
        public ReadOnlyMemory<byte> Primary { get; }
        public ReadOnlyMemory<byte> Backup { get; }
        public SaveDiagnostic Diagnostic { get; }

        public static SaveStorageReadResult Success(ReadOnlyMemory<byte> primary, ReadOnlyMemory<byte> backup) =>
            new SaveStorageReadResult(true, primary, backup, default);

        public static SaveStorageReadResult Failure(SaveDiagnostic diagnostic) =>
            new SaveStorageReadResult(false, default, default, diagnostic);
    }

    /// <summary>Result of a write or delete operation.</summary>
    public readonly struct SaveStorageWriteResult
    {
        private SaveStorageWriteResult(bool success, SaveDiagnostic diagnostic)
        {
            IsSuccess = success;
            Diagnostic = diagnostic;
        }

        public bool IsSuccess { get; }
        public SaveDiagnostic Diagnostic { get; }
        public static SaveStorageWriteResult Success() => new SaveStorageWriteResult(true, default);
        public static SaveStorageWriteResult Failure(SaveDiagnostic diagnostic) => new SaveStorageWriteResult(false, diagnostic);
    }

    /// <summary>Replaceable raw storage boundary with atomic write semantics.</summary>
    public interface ISaveStorage
    {
        ValueTask<SaveStorageReadResult> ReadAsync(string slot, CancellationToken cancellationToken);
        ValueTask<SaveStorageWriteResult> WriteAtomicAsync(string slot, ReadOnlyMemory<byte> data, CancellationToken cancellationToken);
        ValueTask<SaveStorageWriteResult> DeleteAsync(string slot, CancellationToken cancellationToken);
    }

    /// <summary>Result of encoding one typed document into validated bytes.</summary>
    public readonly struct SaveEncodeResult
    {
        private SaveEncodeResult(bool success, byte[] data, SaveDiagnostic diagnostic)
        {
            IsSuccess = success;
            Data = data ?? Array.Empty<byte>();
            Diagnostic = diagnostic;
        }

        public bool IsSuccess { get; }
        public byte[] Data { get; }
        public SaveDiagnostic Diagnostic { get; }
        public static SaveEncodeResult Success(byte[] data) => new SaveEncodeResult(true, data, default);
        public static SaveEncodeResult Failure(SaveDiagnostic diagnostic) => new SaveEncodeResult(false, null, diagnostic);
    }

    /// <summary>Result of decoding and migrating one typed document.</summary>
    public readonly struct SaveDecodeResult<T>
    {
        private readonly T value;

        private SaveDecodeResult(bool success, T decoded, SaveDiagnostic diagnostic)
        {
            IsSuccess = success;
            value = decoded;
            Diagnostic = diagnostic;
        }

        public bool IsSuccess { get; }
        public T Value => IsSuccess ? value : throw new InvalidOperationException("Failed save decode has no value.");
        public SaveDiagnostic Diagnostic { get; }
        public static SaveDecodeResult<T> Success(T value) => new SaveDecodeResult<T>(true, value, default);
        public static SaveDecodeResult<T> Failure(SaveDiagnostic diagnostic) => new SaveDecodeResult<T>(false, default, diagnostic);
    }

    /// <summary>Typed encoding and decoding boundary for the three save documents.</summary>
    public interface ISaveDocumentCodec
    {
        SaveEncodeResult Encode(SettingsSaveData data);
        SaveEncodeResult Encode(ProfileSaveData data);
        SaveEncodeResult Encode(RunRecoverySaveData data);
        SaveDecodeResult<SettingsSaveData> DecodeSettings(ReadOnlyMemory<byte> data);
        SaveDecodeResult<ProfileSaveData> DecodeProfile(ReadOnlyMemory<byte> data);
        SaveDecodeResult<RunRecoverySaveData> DecodeRunRecovery(ReadOnlyMemory<byte> data);
    }

    /// <summary>Validated application document with source and non-fatal diagnostics.</summary>
    public sealed class SaveLoadResult<T>
    {
        private readonly T value;
        private readonly SaveDiagnostic[] diagnostics;

        private SaveLoadResult(bool success, T loaded, SaveReadSource source, SaveDiagnostic[] items, SaveDiagnostic failure)
        {
            IsSuccess = success;
            value = loaded;
            Source = source;
            diagnostics = items ?? Array.Empty<SaveDiagnostic>();
            Failure = failure;
        }

        public bool IsSuccess { get; }
        public T Value => IsSuccess ? value : throw new InvalidOperationException("Failed save load has no value.");
        public SaveReadSource Source { get; }
        public IReadOnlyList<SaveDiagnostic> Diagnostics => Array.AsReadOnly(diagnostics);
        public SaveDiagnostic Failure { get; }

        public static SaveLoadResult<T> Success(T value, SaveReadSource source, SaveDiagnostic[] diagnostics = null) =>
            new SaveLoadResult<T>(true, value, source, diagnostics, default);

        public static SaveLoadResult<T> Failed(SaveDiagnostic failure) =>
            new SaveLoadResult<T>(false, default, SaveReadSource.None, null, failure);
    }
}
