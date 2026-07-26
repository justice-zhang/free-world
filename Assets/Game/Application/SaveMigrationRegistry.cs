using System;
using System.Collections.Generic;

namespace Game.Application
{
    /// <summary>One explicit migration between adjacent schema versions.</summary>
    public interface ISaveMigration
    {
        SaveDocumentKind DocumentKind { get; }
        int FromVersion { get; }
        int ToVersion { get; }
        SaveMigrationResult Migrate(string payloadJson);
    }

    /// <summary>Payload JSON or a localizable migration failure.</summary>
    public readonly struct SaveMigrationResult
    {
        private SaveMigrationResult(bool success, string json, SaveDiagnostic diagnostic)
        {
            IsSuccess = success;
            PayloadJson = json ?? string.Empty;
            Diagnostic = diagnostic;
        }

        public bool IsSuccess { get; }
        public string PayloadJson { get; }
        public SaveDiagnostic Diagnostic { get; }
        public static SaveMigrationResult Success(string json) => new SaveMigrationResult(true, json, default);
        public static SaveMigrationResult Failure(SaveDiagnostic diagnostic) => new SaveMigrationResult(false, string.Empty, diagnostic);
    }

    /// <summary>Runs explicit, contiguous, one-way save migrations.</summary>
    public sealed class SaveMigrationRegistry
    {
        private readonly Dictionary<MigrationKey, ISaveMigration> migrations = new Dictionary<MigrationKey, ISaveMigration>();

        public void Register(ISaveMigration migration)
        {
            if (migration == null) throw new ArgumentNullException(nameof(migration));
            if (migration.FromVersion < 1 || migration.ToVersion != migration.FromVersion + 1)
                throw new ArgumentException("Save migrations must advance exactly one schema version.", nameof(migration));
            var key = new MigrationKey(migration.DocumentKind, migration.FromVersion);
            if (!migrations.TryAdd(key, migration))
                throw new InvalidOperationException("Duplicate save migration for " + migration.DocumentKind + " v" + migration.FromVersion + ".");
        }

        public SaveMigrationResult Migrate(SaveDocumentKind kind, int sourceVersion, int targetVersion, string payloadJson)
        {
            if (sourceVersion > targetVersion)
                return SaveMigrationResult.Failure(new SaveDiagnostic(SaveFailureCode.UnsupportedSchema, "save.error.schema_newer"));
            var current = sourceVersion;
            var json = payloadJson ?? string.Empty;
            while (current < targetVersion)
            {
                if (!migrations.TryGetValue(new MigrationKey(kind, current), out var migration))
                    return SaveMigrationResult.Failure(new SaveDiagnostic(SaveFailureCode.UnsupportedSchema, "save.error.migration_missing", kind + " v" + current));
                var result = migration.Migrate(json);
                if (!result.IsSuccess) return result;
                json = result.PayloadJson;
                current = migration.ToVersion;
            }
            return SaveMigrationResult.Success(json);
        }

        private readonly struct MigrationKey : IEquatable<MigrationKey>
        {
            public MigrationKey(SaveDocumentKind kind, int version) { Kind = kind; Version = version; }
            private SaveDocumentKind Kind { get; }
            private int Version { get; }
            public bool Equals(MigrationKey other) => Kind == other.Kind && Version == other.Version;
            public override bool Equals(object obj) => obj is MigrationKey other && Equals(other);
            public override int GetHashCode() => ((int)Kind * 397) ^ Version;
        }
    }
}
