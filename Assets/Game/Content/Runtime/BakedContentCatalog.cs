using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Game.Core;

namespace Game.Content.Runtime
{
    /// <summary>
    /// Contains one validated, pure runtime content pack.
    /// </summary>
    public sealed class BakedContentCatalog
    {
        private readonly RuntimeContentDefinition[] definitions;
        private readonly IReadOnlyList<RuntimeContentDefinition> definitionsView;

        private BakedContentCatalog(
            ContentPackManifest manifest,
            RuntimeContentDefinition[] definitions,
            string contentHash)
        {
            Manifest = manifest;
            this.definitions = definitions == null
                ? Array.Empty<RuntimeContentDefinition>()
                : (RuntimeContentDefinition[])definitions.Clone();
            definitionsView = Array.AsReadOnly(this.definitions);
            ContentHash = contentHash ?? string.Empty;
        }

        /// <summary>
        /// Gets the source pack manifest.
        /// </summary>
        public ContentPackManifest Manifest { get; }

        /// <summary>
        /// Gets all runtime definitions in deterministic author order.
        /// </summary>
        public IReadOnlyList<RuntimeContentDefinition> Definitions => definitionsView;

        /// <summary>
        /// Gets the lowercase SHA-256 hash of manifest and definition data.
        /// </summary>
        public string ContentHash { get; }

        /// <summary>
        /// Creates a catalog and computes its deterministic content hash.
        /// </summary>
        public static BakedContentCatalog Create(
            ContentPackManifest manifest,
            RuntimeContentDefinition[] definitions)
        {
            if (manifest == null)
            {
                throw new ArgumentNullException(nameof(manifest));
            }

            var safeDefinitions = definitions == null
                ? Array.Empty<RuntimeContentDefinition>()
                : (RuntimeContentDefinition[])definitions.Clone();
            var hash = ContentHashUtility.Compute(manifest, safeDefinitions);
            return new BakedContentCatalog(manifest, safeDefinitions, hash);
        }

        /// <summary>
        /// Creates a catalog only when the supplied hash matches its deterministic payload.
        /// </summary>
        public static Result<BakedContentCatalog> CreateVerified(
            ContentPackManifest manifest,
            RuntimeContentDefinition[] definitions,
            string expectedHash)
        {
            if (manifest == null)
            {
                return Result<BakedContentCatalog>.Failure(
                    new Error(ErrorCode.InvalidCatalog, "Catalog manifest is missing."));
            }

            var catalog = Create(manifest, definitions);
            if (!string.Equals(
                    catalog.ContentHash,
                    expectedHash ?? string.Empty,
                    StringComparison.OrdinalIgnoreCase))
            {
                return Result<BakedContentCatalog>.Failure(
                    new Error(
                        ErrorCode.ContentHashMismatch,
                        "Catalog content hash mismatch. Expected '" +
                        (expectedHash ?? string.Empty) + "', computed '" +
                        catalog.ContentHash + "'.",
                        default,
                        manifest.PackId,
                        manifest.SourceAssetPath));
            }

            return Result<BakedContentCatalog>.Success(catalog);
        }

        /// <summary>
        /// Converts the pure runtime catalog into a Unity-serializable string DTO.
        /// </summary>
        public BakedContentCatalogDto ToDto()
        {
            return BakedContentCatalogDto.FromCatalog(this);
        }
    }

    internal static class ContentHashUtility
    {
        public static string Compute(
            ContentPackManifest manifest,
            RuntimeContentDefinition[] definitions)
        {
            var builder = new StringBuilder(1024);
            AppendToken(builder, manifest.PackId.Value);
            AppendToken(builder, manifest.Version.ToString());
            AppendInt(builder, manifest.SchemaVersion);
            AppendToken(builder, manifest.MinimumGameVersion.ToString());
            AppendToken(
                builder,
                manifest.MaximumGameVersion.HasValue
                    ? manifest.MaximumGameVersion.Value.ToString()
                    : string.Empty);
            AppendToken(builder, manifest.CatalogAddress);
            AppendToken(builder, manifest.AssetLabel);
            AppendInt(builder, manifest.Official ? 1 : 0);
            AppendToken(builder, manifest.SourceAssetPath);
            AppendInt(builder, manifest.Dependencies.Count);
            for (var index = 0; index < manifest.Dependencies.Count; index++)
            {
                var dependency = manifest.Dependencies[index];
                AppendToken(builder, dependency.PackId.Value);
                AppendToken(builder, dependency.MinimumVersion.ToString());
                AppendToken(
                    builder,
                    dependency.MaximumVersion.HasValue
                        ? dependency.MaximumVersion.Value.ToString()
                        : string.Empty);
            }

            AppendInt(builder, definitions.Length);
            for (var index = 0; index < definitions.Length; index++)
            {
                definitions[index].AppendDeterministicData(builder);
            }

            var bytes = Encoding.UTF8.GetBytes(builder.ToString());
            using (var sha256 = SHA256.Create())
            {
                var hash = sha256.ComputeHash(bytes);
                var output = new StringBuilder(hash.Length * 2);
                for (var index = 0; index < hash.Length; index++)
                {
                    output.Append(hash[index].ToString("x2", CultureInfo.InvariantCulture));
                }

                return output.ToString();
            }
        }

        public static void AppendToken(StringBuilder builder, string value)
        {
            var safeValue = value ?? string.Empty;
            builder.Append(safeValue.Length);
            builder.Append(':');
            builder.Append(safeValue);
            builder.Append('|');
        }

        public static void AppendInt(StringBuilder builder, int value)
        {
            AppendToken(builder, value.ToString(CultureInfo.InvariantCulture));
        }

        public static void AppendFloat(StringBuilder builder, float value)
        {
            AppendToken(builder, value.ToString("R", CultureInfo.InvariantCulture));
        }
    }
}
