using System;
using System.Collections.Generic;
using System.Text;
using Game.Core;

namespace Game.Content.Runtime
{
    /// <summary>
    /// Validates content pack compatibility and produces a stable dependency-first order.
    /// </summary>
    public static class ContentPackTopology
    {
        /// <summary>
        /// Gets the oldest content schema version understood by this runtime.
        /// </summary>
        public const int MinimumSupportedSchemaVersion = 1;

        /// <summary>
        /// Gets the M6-era maximum retained for one public-API compatibility cycle.
        /// </summary>
        [Obsolete("Use LatestSupportedSchemaVersion for new content validation.")]
        public const int SupportedSchemaVersion = 5;

        /// <summary>
        /// Gets the first schema version that permits serialized status definitions.
        /// </summary>
        public const int StatusDefinitionSchemaVersion = 2;

        /// <summary>
        /// Gets the first schema version that permits executable modular skills.
        /// </summary>
        public const int ModularSkillSchemaVersion = 3;

        /// <summary>
        /// Gets the first schema version that permits executable enemies, maps, and encounters.
        /// </summary>
        public const int EnemyMapEncounterSchemaVersion = 4;

        /// <summary>
        /// Gets the first schema version that permits passive, trait, offer,
        /// synergy, and evolution definitions.
        /// </summary>
        public const int BuildProgressionSchemaVersion = 5;

        /// <summary>First schema that admits Qinglan's general-purpose runtime definition families.</summary>
        public const int QinglanDemoSchemaVersion = 6;

        /// <summary>Gets the newest content schema version understood by this runtime.</summary>
        public const int LatestSupportedSchemaVersion = QinglanDemoSchemaVersion;

        /// <summary>
        /// Returns whether a content schema version can be loaded by this runtime.
        /// </summary>
        public static bool IsSchemaVersionSupported(int schemaVersion)
        {
            return schemaVersion >= MinimumSupportedSchemaVersion &&
                   schemaVersion <= LatestSupportedSchemaVersion;
        }

        /// <summary>
        /// Sorts manifests so dependencies precede consumers while preserving input order on ties.
        /// </summary>
        public static Result<ContentPackManifest[]> Sort(
            IReadOnlyList<ContentPackManifest> manifests,
            ContentVersion gameVersion)
        {
            if (manifests == null)
            {
                return Result<ContentPackManifest[]>.Failure(
                    new Error(ErrorCode.InvalidCatalog, "Manifest collection is missing."));
            }

            var count = manifests.Count;
            var source = new ContentPackManifest[count];
            var indexByPack = new Dictionary<ContentId, int>(count);
            for (var index = 0; index < count; index++)
            {
                var manifest = manifests[index];
                if (manifest == null || !manifest.PackId.IsValid)
                {
                    return Result<ContentPackManifest[]>.Failure(
                        new Error(
                            ErrorCode.InvalidCatalog,
                            "Manifest at input index " + index + " is null or has no pack ID."));
                }

                if (indexByPack.TryGetValue(manifest.PackId, out var previousIndex))
                {
                    var previous = manifests[previousIndex];
                    return Result<ContentPackManifest[]>.Failure(
                        new Error(
                            ErrorCode.DuplicatePackId,
                            "Pack '" + manifest.PackId + "' is declared by both '" +
                            previous.SourceAssetPath + "' and '" +
                            manifest.SourceAssetPath + "'.",
                            default,
                            manifest.PackId,
                            manifest.SourceAssetPath));
                }

                if (!IsSchemaVersionSupported(manifest.SchemaVersion))
                {
                    return Result<ContentPackManifest[]>.Failure(
                        new Error(
                            ErrorCode.UnsupportedSchemaVersion,
                            "Pack '" + manifest.PackId + "' uses schema " +
                            manifest.SchemaVersion + "; supported schema range is [" +
                            MinimumSupportedSchemaVersion + ", " +
                            LatestSupportedSchemaVersion + "].",
                            default,
                            manifest.PackId,
                            manifest.SourceAssetPath));
                }

                if (!manifest.AcceptsGameVersion(gameVersion))
                {
                    return Result<ContentPackManifest[]>.Failure(
                        new Error(
                            ErrorCode.IncompatibleVersion,
                            "Pack '" + manifest.PackId + "' version " +
                            manifest.Version + " does not support game version " +
                            gameVersion + ".",
                            default,
                            manifest.PackId,
                            manifest.SourceAssetPath));
                }

                source[index] = manifest;
                indexByPack.Add(manifest.PackId, index);
            }

            var indegrees = new int[count];
            var dependents = new List<int>[count];
            for (var index = 0; index < count; index++)
            {
                dependents[index] = new List<int>();
            }

            for (var ownerIndex = 0; ownerIndex < count; ownerIndex++)
            {
                var owner = source[ownerIndex];
                var dependenciesSeen = new HashSet<ContentId>();
                for (var dependencyIndex = 0;
                     dependencyIndex < owner.Dependencies.Count;
                     dependencyIndex++)
                {
                    var dependency = owner.Dependencies[dependencyIndex];
                    if (!dependenciesSeen.Add(dependency.PackId))
                    {
                        return Result<ContentPackManifest[]>.Failure(
                            new Error(
                                ErrorCode.InvalidCatalog,
                                "Pack '" + owner.PackId + "' declares dependency '" +
                                dependency.PackId + "' more than once.",
                                default,
                                owner.PackId,
                                owner.SourceAssetPath));
                    }

                    if (!indexByPack.TryGetValue(dependency.PackId, out var requiredIndex))
                    {
                        return Result<ContentPackManifest[]>.Failure(
                            new Error(
                                ErrorCode.MissingDependency,
                                "Pack '" + owner.PackId + "' requires missing pack '" +
                                dependency.PackId + "'.",
                                default,
                                owner.PackId,
                                owner.SourceAssetPath));
                    }

                    var required = source[requiredIndex];
                    if (!dependency.Accepts(required.Version))
                    {
                        return Result<ContentPackManifest[]>.Failure(
                            new Error(
                                ErrorCode.IncompatibleVersion,
                                "Pack '" + owner.PackId + "' requires pack '" +
                                dependency.PackId + "' in range [" +
                                dependency.MinimumVersion + ", " +
                                (dependency.MaximumVersion.HasValue
                                    ? dependency.MaximumVersion.Value.ToString()
                                    : "unbounded") + "], but loaded version is " +
                                required.Version + ".",
                                default,
                                owner.PackId,
                                owner.SourceAssetPath));
                    }

                    indegrees[ownerIndex]++;
                    dependents[requiredIndex].Add(ownerIndex);
                }
            }

            var sorted = new ContentPackManifest[count];
            var emitted = new bool[count];
            for (var outputIndex = 0; outputIndex < count; outputIndex++)
            {
                var selected = -1;
                for (var candidate = 0; candidate < count; candidate++)
                {
                    if (!emitted[candidate] && indegrees[candidate] == 0)
                    {
                        selected = candidate;
                        break;
                    }
                }

                if (selected < 0)
                {
                    var cycle = new StringBuilder();
                    for (var candidate = 0; candidate < count; candidate++)
                    {
                        if (emitted[candidate])
                        {
                            continue;
                        }

                        if (cycle.Length > 0)
                        {
                            cycle.Append(", ");
                        }

                        cycle.Append(source[candidate].PackId.Value);
                    }

                    var firstCycleIndex = FindFirstNotEmitted(emitted);
                    var firstCyclePack = source[firstCycleIndex];
                    return Result<ContentPackManifest[]>.Failure(
                        new Error(
                            ErrorCode.DependencyCycle,
                            "Content pack dependency cycle includes: " + cycle + ".",
                            default,
                            firstCyclePack.PackId,
                            firstCyclePack.SourceAssetPath));
                }

                emitted[selected] = true;
                sorted[outputIndex] = source[selected];
                for (var dependentIndex = 0;
                     dependentIndex < dependents[selected].Count;
                     dependentIndex++)
                {
                    indegrees[dependents[selected][dependentIndex]]--;
                }
            }

            return Result<ContentPackManifest[]>.Success(sorted);
        }

        private static int FindFirstNotEmitted(bool[] emitted)
        {
            for (var index = 0; index < emitted.Length; index++)
            {
                if (!emitted[index])
                {
                    return index;
                }
            }

            return 0;
        }
    }
}
