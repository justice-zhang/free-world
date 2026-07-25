using System;
using System.Collections.Generic;
using Game.Core;

namespace Game.Content.Runtime
{
    /// <summary>
    /// Associates a runtime definition with its load-local index and source pack.
    /// </summary>
    public sealed class ContentRegistryEntry
    {
        internal ContentRegistryEntry(
            RuntimeContentIndex index,
            RuntimeContentDefinition definition,
            ContentId sourcePackId)
        {
            Index = index;
            Definition = definition;
            SourcePackId = sourcePackId;
        }

        /// <summary>
        /// Gets the index assigned for this registry load.
        /// </summary>
        public RuntimeContentIndex Index { get; }

        /// <summary>
        /// Gets the pure runtime definition.
        /// </summary>
        public RuntimeContentDefinition Definition { get; }

        /// <summary>
        /// Gets the pack that declared the definition.
        /// </summary>
        public ContentId SourcePackId { get; }
    }

    /// <summary>
    /// Summarizes one successful content registry load.
    /// </summary>
    public readonly struct ContentRegistrySummary
    {
        /// <summary>
        /// Initializes a successful registry load summary.
        /// </summary>
        public ContentRegistrySummary(int packCount, int definitionCount)
        {
            PackCount = packCount;
            DefinitionCount = definitionCount;
        }

        /// <summary>Gets the number of loaded packs.</summary>
        public int PackCount { get; }

        /// <summary>Gets the number of registered definitions.</summary>
        public int DefinitionCount { get; }
    }

    /// <summary>
    /// Provides stable-ID lookup and compact indices for a validated catalog set.
    /// </summary>
    public sealed class ContentRegistry
    {
        private Dictionary<ContentId, ContentRegistryEntry> entriesById =
            new Dictionary<ContentId, ContentRegistryEntry>();
        private ContentRegistryEntry[] entriesByIndex = Array.Empty<ContentRegistryEntry>();
        private IReadOnlyList<ContentId> loadedPackIds =
            Array.AsReadOnly(Array.Empty<ContentId>());

        /// <summary>
        /// Gets the number of registered definitions.
        /// </summary>
        public int Count => entriesByIndex.Length;

        /// <summary>
        /// Gets dependency-sorted pack IDs for the current load.
        /// </summary>
        public IReadOnlyList<ContentId> LoadedPackIds => loadedPackIds;

        /// <summary>
        /// Validates and atomically replaces registry contents.
        /// </summary>
        public Result<ContentRegistrySummary> Load(
            IReadOnlyList<BakedContentCatalog> catalogs,
            ContentVersion gameVersion)
        {
            var validation = ContentValidator.ValidateCatalogs(catalogs, gameVersion);
            if (!validation.IsValid)
            {
                return Result<ContentRegistrySummary>.Failure(validation.Errors[0]);
            }

            var manifests = new ContentPackManifest[catalogs.Count];
            var catalogByPack = new Dictionary<ContentId, BakedContentCatalog>(catalogs.Count);
            var definitionCount = 0;
            for (var index = 0; index < catalogs.Count; index++)
            {
                var catalog = catalogs[index];
                manifests[index] = catalog.Manifest;
                catalogByPack.Add(catalog.Manifest.PackId, catalog);
                definitionCount += catalog.Definitions.Count;
            }

            var topology = ContentPackTopology.Sort(manifests, gameVersion);
            if (!topology.IsSuccess)
            {
                return Result<ContentRegistrySummary>.Failure(topology.Error);
            }

            var nextById = new Dictionary<ContentId, ContentRegistryEntry>(definitionCount);
            var nextByIndex = new ContentRegistryEntry[definitionCount];
            var nextPackIds = new ContentId[topology.Value.Length];
            var runtimeIndex = 0;
            for (var packIndex = 0; packIndex < topology.Value.Length; packIndex++)
            {
                var manifest = topology.Value[packIndex];
                nextPackIds[packIndex] = manifest.PackId;
                var catalog = catalogByPack[manifest.PackId];
                for (var definitionIndex = 0;
                     definitionIndex < catalog.Definitions.Count;
                     definitionIndex++)
                {
                    var definition = catalog.Definitions[definitionIndex];
                    var entry = new ContentRegistryEntry(
                        new RuntimeContentIndex(runtimeIndex),
                        definition,
                        manifest.PackId);
                    if (!nextById.TryAdd(definition.Id, entry))
                    {
                        var first = nextById[definition.Id];
                        return Result<ContentRegistrySummary>.Failure(
                            ContentValidator.CreateDuplicateError(
                                definition.Id,
                                new ContentValidator.ContentOrigin(
                                    first.SourcePackId,
                                    first.Definition.SourceAssetPath),
                                new ContentValidator.ContentOrigin(
                                    manifest.PackId,
                                    definition.SourceAssetPath)));
                    }

                    nextByIndex[runtimeIndex] = entry;
                    runtimeIndex++;
                }
            }

            entriesById = nextById;
            entriesByIndex = nextByIndex;
            loadedPackIds = Array.AsReadOnly(nextPackIds);
            return Result<ContentRegistrySummary>.Success(
                new ContentRegistrySummary(nextPackIds.Length, nextByIndex.Length));
        }

        /// <summary>
        /// Looks up a registered definition by stable ID.
        /// </summary>
        public bool TryGet(ContentId id, out ContentRegistryEntry entry)
        {
            return entriesById.TryGetValue(id, out entry);
        }

        /// <summary>
        /// Looks up and type-checks a registered definition by stable ID.
        /// </summary>
        public bool TryGet<TDefinition>(ContentId id, out TDefinition definition)
            where TDefinition : RuntimeContentDefinition
        {
            definition = null;
            if (!entriesById.TryGetValue(id, out var entry) ||
                !(entry.Definition is TDefinition typedDefinition))
            {
                return false;
            }

            definition = typedDefinition;
            return true;
        }

        /// <summary>
        /// Resolves a load-local index.
        /// </summary>
        public Result<ContentRegistryEntry> Get(RuntimeContentIndex index)
        {
            if (!index.IsValid || index.Value >= entriesByIndex.Length)
            {
                return Result<ContentRegistryEntry>.Failure(
                    new Error(
                        ErrorCode.InvalidRuntimeContentIndex,
                        "Runtime content index '" + index + "' is outside the loaded registry."));
            }

            return Result<ContentRegistryEntry>.Success(entriesByIndex[index.Value]);
        }
    }
}
