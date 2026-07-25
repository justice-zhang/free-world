using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Content.Authoring
{
    /// <summary>
    /// Authoring representation of a content pack dependency.
    /// </summary>
    [Serializable]
    public sealed class ContentPackDependencyAuthoring
    {
        /// <summary>Gets or sets the required pack ID.</summary>
        public string packId = string.Empty;

        /// <summary>Gets or sets the inclusive minimum pack version.</summary>
        public string minimumVersion = "0.0.0";

        /// <summary>Gets or sets the optional inclusive maximum pack version.</summary>
        public string maximumVersion = string.Empty;
    }

    /// <summary>
    /// ScriptableObject manifest and ordered definition list for one content pack.
    /// </summary>
    [CreateAssetMenu(menuName = "Free World/Content/Content Pack", fileName = "ContentPack")]
    public sealed class ContentPackAuthoring : ScriptableObject
    {
        [SerializeField] private string packId = string.Empty;
        [SerializeField] private string version = "0.1.0";
        [SerializeField] private int schemaVersion = 1;
        [SerializeField] private string minimumGameVersion = "0.1.0";
        [SerializeField] private string maximumGameVersion = string.Empty;
        [SerializeField] private ContentPackDependencyAuthoring[] dependencies =
            Array.Empty<ContentPackDependencyAuthoring>();
        [SerializeField] private string catalogAddress = string.Empty;
        [SerializeField] private string assetLabel = string.Empty;
        [SerializeField] private bool official;
        [SerializeField] private ContentAuthoringBase[] definitions =
            Array.Empty<ContentAuthoringBase>();

        /// <summary>Gets the raw authored pack ID.</summary>
        public string PackIdText => packId;

        /// <summary>Gets the raw authored pack version.</summary>
        public string VersionText => version;

        /// <summary>Gets the authored content schema version.</summary>
        public int SchemaVersion => schemaVersion;

        /// <summary>Gets the raw minimum supported game version.</summary>
        public string MinimumGameVersionText => minimumGameVersion;

        /// <summary>Gets the raw optional maximum supported game version.</summary>
        public string MaximumGameVersionText => maximumGameVersion;

        /// <summary>Gets dependencies in deterministic author order.</summary>
        public IReadOnlyList<ContentPackDependencyAuthoring> Dependencies => dependencies;

        /// <summary>Gets the baked catalog address.</summary>
        public string CatalogAddress => catalogAddress;

        /// <summary>Gets the Addressables label reserved for this pack.</summary>
        public string AssetLabel => assetLabel;

        /// <summary>Gets whether this is a first-party pack.</summary>
        public bool Official => official;

        /// <summary>Gets definitions in deterministic author order.</summary>
        public IReadOnlyList<ContentAuthoringBase> Definitions => definitions;

        /// <summary>
        /// Configures pack metadata and deterministic definition order.
        /// </summary>
        public void Configure(
            string id,
            string packVersion,
            int contentSchemaVersion,
            string minimumSupportedGameVersion,
            string maximumSupportedGameVersion,
            ContentPackDependencyAuthoring[] packDependencies,
            string bakedCatalogAddress,
            string packAssetLabel,
            bool isOfficial,
            ContentAuthoringBase[] orderedDefinitions)
        {
            packId = id ?? string.Empty;
            version = packVersion ?? string.Empty;
            schemaVersion = contentSchemaVersion;
            minimumGameVersion = minimumSupportedGameVersion ?? string.Empty;
            maximumGameVersion = maximumSupportedGameVersion ?? string.Empty;
            dependencies = packDependencies == null
                ? Array.Empty<ContentPackDependencyAuthoring>()
                : (ContentPackDependencyAuthoring[])packDependencies.Clone();
            catalogAddress = bakedCatalogAddress ?? string.Empty;
            assetLabel = packAssetLabel ?? string.Empty;
            official = isOfficial;
            definitions = orderedDefinitions == null
                ? Array.Empty<ContentAuthoringBase>()
                : (ContentAuthoringBase[])orderedDefinitions.Clone();
        }
    }
}
