using System;
using System.Collections.Generic;
using Game.Core;

namespace Game.Content.Runtime
{
    /// <summary>
    /// Declares a required content pack and its accepted version range.
    /// </summary>
    public readonly struct ContentPackDependency
    {
        /// <summary>
        /// Initializes a pack dependency.
        /// </summary>
        public ContentPackDependency(
            ContentId packId,
            ContentVersion minimumVersion,
            ContentVersion? maximumVersion = null)
        {
            PackId = packId;
            MinimumVersion = minimumVersion;
            MaximumVersion = maximumVersion;
        }

        /// <summary>
        /// Gets the required pack ID.
        /// </summary>
        public ContentId PackId { get; }

        /// <summary>
        /// Gets the inclusive minimum version.
        /// </summary>
        public ContentVersion MinimumVersion { get; }

        /// <summary>
        /// Gets the optional inclusive maximum version.
        /// </summary>
        public ContentVersion? MaximumVersion { get; }

        /// <summary>
        /// Returns whether a version satisfies this dependency.
        /// </summary>
        public bool Accepts(ContentVersion version)
        {
            return version >= MinimumVersion &&
                   (!MaximumVersion.HasValue || version <= MaximumVersion.Value);
        }
    }

    /// <summary>
    /// Describes one baked content pack without holding Unity objects.
    /// </summary>
    public sealed class ContentPackManifest
    {
        private readonly ContentPackDependency[] dependencies;
        private readonly IReadOnlyList<ContentPackDependency> dependenciesView;

        /// <summary>
        /// Initializes an immutable content pack manifest.
        /// </summary>
        public ContentPackManifest(
            ContentId packId,
            ContentVersion version,
            int schemaVersion,
            ContentVersion minimumGameVersion,
            ContentVersion? maximumGameVersion,
            ContentPackDependency[] dependencies,
            string catalogAddress,
            string assetLabel,
            bool official,
            string sourceAssetPath)
        {
            PackId = packId;
            Version = version;
            SchemaVersion = schemaVersion;
            MinimumGameVersion = minimumGameVersion;
            MaximumGameVersion = maximumGameVersion;
            this.dependencies = dependencies == null
                ? Array.Empty<ContentPackDependency>()
                : (ContentPackDependency[])dependencies.Clone();
            dependenciesView = Array.AsReadOnly(this.dependencies);
            CatalogAddress = catalogAddress ?? string.Empty;
            AssetLabel = assetLabel ?? string.Empty;
            Official = official;
            SourceAssetPath = sourceAssetPath ?? string.Empty;
        }

        /// <summary>
        /// Gets the stable pack ID.
        /// </summary>
        public ContentId PackId { get; }

        /// <summary>
        /// Gets the pack version.
        /// </summary>
        public ContentVersion Version { get; }

        /// <summary>
        /// Gets the content schema version used by the pack.
        /// </summary>
        public int SchemaVersion { get; }

        /// <summary>
        /// Gets the inclusive minimum supported game version.
        /// </summary>
        public ContentVersion MinimumGameVersion { get; }

        /// <summary>
        /// Gets the optional inclusive maximum supported game version.
        /// </summary>
        public ContentVersion? MaximumGameVersion { get; }

        /// <summary>
        /// Gets the declared pack dependencies in author order.
        /// </summary>
        public IReadOnlyList<ContentPackDependency> Dependencies => dependenciesView;

        /// <summary>
        /// Gets the baked catalog address.
        /// </summary>
        public string CatalogAddress { get; }

        /// <summary>
        /// Gets the Addressables pack label.
        /// </summary>
        public string AssetLabel { get; }

        /// <summary>
        /// Gets whether the pack is first-party content.
        /// </summary>
        public bool Official { get; }

        /// <summary>
        /// Gets the authoring manifest asset path.
        /// </summary>
        public string SourceAssetPath { get; }

        /// <summary>
        /// Returns whether a game version is accepted by this manifest.
        /// </summary>
        public bool AcceptsGameVersion(ContentVersion gameVersion)
        {
            return gameVersion >= MinimumGameVersion &&
                   (!MaximumGameVersion.HasValue ||
                    gameVersion <= MaximumGameVersion.Value);
        }
    }
}
