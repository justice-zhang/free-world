using System;
using System.Collections.Generic;
using Game.Content.Runtime;
using Game.Core;
using UnityEngine;

namespace Game.Content.Authoring
{
    /// <summary>
    /// Resolves deterministic diagnostic paths without coupling authoring to UnityEditor.
    /// </summary>
    public interface IAuthoringPathResolver
    {
        /// <summary>
        /// Gets the stable project-relative diagnostic path for an authoring asset.
        /// </summary>
        string GetPath(UnityEngine.Object authoringAsset);
    }

    /// <summary>
    /// Converts ScriptableObject author data into a validated pure runtime catalog.
    /// </summary>
    public static class ContentBaker
    {
        /// <summary>
        /// Bakes one content pack without retaining Unity object references.
        /// </summary>
        public static Result<BakedContentCatalog> Bake(
            ContentPackAuthoring pack,
            IAuthoringPathResolver pathResolver)
        {
            if (pack == null)
            {
                return Result<BakedContentCatalog>.Failure(
                    new Error(
                        ErrorCode.InvalidAuthoringData,
                        "ContentPackAuthoring is missing."));
            }

            if (pathResolver == null)
            {
                throw new ArgumentNullException(nameof(pathResolver));
            }

            var packPath = pathResolver.GetPath(pack) ?? string.Empty;
            var packIdResult = ContentValidator.ValidateAuthoringId(
                pack.PackIdText,
                default,
                packPath);
            if (!packIdResult.IsSuccess)
            {
                return Result<BakedContentCatalog>.Failure(packIdResult.Error);
            }

            var packId = packIdResult.Value;
            var versionResult = ContentVersion.Parse(pack.VersionText, packId, packPath);
            if (!versionResult.IsSuccess)
            {
                return Result<BakedContentCatalog>.Failure(versionResult.Error);
            }

            var minimumGameResult = ContentVersion.Parse(
                pack.MinimumGameVersionText,
                packId,
                packPath);
            if (!minimumGameResult.IsSuccess)
            {
                return Result<BakedContentCatalog>.Failure(minimumGameResult.Error);
            }

            var maximumGameResult = ParseOptionalVersion(
                pack.MaximumGameVersionText,
                packId,
                packPath);
            if (!maximumGameResult.IsSuccess)
            {
                return Result<BakedContentCatalog>.Failure(maximumGameResult.Error);
            }

            if (maximumGameResult.Value.HasValue &&
                maximumGameResult.Value.Value < minimumGameResult.Value)
            {
                return Result<BakedContentCatalog>.Failure(
                    new Error(
                        ErrorCode.IncompatibleVersion,
                        "Pack maximum game version is below its minimum game version.",
                        default,
                        packId,
                        packPath));
            }

            if (pack.SchemaVersion != ContentPackTopology.SupportedSchemaVersion)
            {
                return Result<BakedContentCatalog>.Failure(
                    new Error(
                        ErrorCode.UnsupportedSchemaVersion,
                        "Authoring pack schema " + pack.SchemaVersion +
                        " is not supported.",
                        default,
                        packId,
                        packPath));
            }

            if (string.IsNullOrWhiteSpace(pack.CatalogAddress) ||
                string.IsNullOrWhiteSpace(pack.AssetLabel))
            {
                return Result<BakedContentCatalog>.Failure(
                    new Error(
                        ErrorCode.InvalidAuthoringData,
                        "Catalog address and pack asset label are required.",
                        default,
                        packId,
                        packPath));
            }

            var dependenciesResult = BakeDependencies(pack, packId, packPath);
            if (!dependenciesResult.IsSuccess)
            {
                return Result<BakedContentCatalog>.Failure(dependenciesResult.Error);
            }

            var definitionPaths = new string[pack.Definitions.Count];
            var originById = new Dictionary<ContentId, string>();
            for (var index = 0; index < pack.Definitions.Count; index++)
            {
                var authoring = pack.Definitions[index];
                if (authoring == null)
                {
                    return Result<BakedContentCatalog>.Failure(
                        new Error(
                            ErrorCode.InvalidAuthoringData,
                            "Pack definition reference is null at index " + index + ".",
                            default,
                            packId,
                            packPath));
                }

                var definitionPath = pathResolver.GetPath(authoring) ?? string.Empty;
                if (string.IsNullOrWhiteSpace(definitionPath))
                {
                    var unresolvedId = ContentId.Create(authoring.ContentIdText);
                    return Result<BakedContentCatalog>.Failure(
                        new Error(
                            ErrorCode.InvalidAuthoringData,
                            "Authoring asset path cannot be resolved.",
                            unresolvedId.IsSuccess ? unresolvedId.Value : default,
                            packId,
                            packPath));
                }

                var definitionIdResult = ContentValidator.ValidateAuthoringId(
                    authoring.ContentIdText,
                    packId,
                    definitionPath);
                if (!definitionIdResult.IsSuccess)
                {
                    return Result<BakedContentCatalog>.Failure(definitionIdResult.Error);
                }

                if (originById.TryGetValue(definitionIdResult.Value, out var firstPath))
                {
                    return Result<BakedContentCatalog>.Failure(
                        new Error(
                            ErrorCode.DuplicateContentId,
                            "ContentId '" + definitionIdResult.Value +
                            "' is declared by pack '" +
                            packId + "' at '" + firstPath + "' and pack '" +
                            packId + "' at '" + definitionPath +
                            "'. Silent override is forbidden.",
                            definitionIdResult.Value,
                            packId,
                            definitionPath));
                }

                originById.Add(definitionIdResult.Value, definitionPath);
                definitionPaths[index] = definitionPath;
            }

            var definitions = new RuntimeContentDefinition[pack.Definitions.Count];
            for (var index = 0; index < pack.Definitions.Count; index++)
            {
                var definitionResult = pack.Definitions[index].Bake(
                    packId,
                    definitionPaths[index]);
                if (!definitionResult.IsSuccess)
                {
                    return Result<BakedContentCatalog>.Failure(definitionResult.Error);
                }

                definitions[index] = definitionResult.Value;
            }

            var manifest = new ContentPackManifest(
                packId,
                versionResult.Value,
                pack.SchemaVersion,
                minimumGameResult.Value,
                maximumGameResult.Value,
                dependenciesResult.Value,
                pack.CatalogAddress,
                pack.AssetLabel,
                pack.Official,
                packPath);
            return Result<BakedContentCatalog>.Success(
                BakedContentCatalog.Create(manifest, definitions));
        }

        private static Result<ContentPackDependency[]> BakeDependencies(
            ContentPackAuthoring pack,
            ContentId packId,
            string packPath)
        {
            var output = new ContentPackDependency[pack.Dependencies.Count];
            var seen = new HashSet<ContentId>();
            for (var index = 0; index < pack.Dependencies.Count; index++)
            {
                var dependency = pack.Dependencies[index];
                if (dependency == null)
                {
                    return Result<ContentPackDependency[]>.Failure(
                        new Error(
                            ErrorCode.InvalidAuthoringData,
                            "Pack dependency is null at index " + index + ".",
                            default,
                            packId,
                            packPath));
                }

                var dependencyIdResult = ContentValidator.ValidateAuthoringId(
                    dependency.packId,
                    packId,
                    packPath);
                if (!dependencyIdResult.IsSuccess)
                {
                    return Result<ContentPackDependency[]>.Failure(dependencyIdResult.Error);
                }

                if (!seen.Add(dependencyIdResult.Value))
                {
                    return Result<ContentPackDependency[]>.Failure(
                        new Error(
                            ErrorCode.InvalidAuthoringData,
                            "Dependency '" + dependencyIdResult.Value +
                            "' is declared more than once.",
                            default,
                            packId,
                            packPath));
                }

                var minimumResult = ContentVersion.Parse(
                    dependency.minimumVersion,
                    packId,
                    packPath);
                if (!minimumResult.IsSuccess)
                {
                    return Result<ContentPackDependency[]>.Failure(minimumResult.Error);
                }

                var maximumResult = ParseOptionalVersion(
                    dependency.maximumVersion,
                    packId,
                    packPath);
                if (!maximumResult.IsSuccess)
                {
                    return Result<ContentPackDependency[]>.Failure(maximumResult.Error);
                }

                if (maximumResult.Value.HasValue &&
                    maximumResult.Value.Value < minimumResult.Value)
                {
                    return Result<ContentPackDependency[]>.Failure(
                        new Error(
                            ErrorCode.IncompatibleVersion,
                            "Dependency maximum version is below its minimum version.",
                            default,
                            packId,
                            packPath));
                }

                output[index] = new ContentPackDependency(
                    dependencyIdResult.Value,
                    minimumResult.Value,
                    maximumResult.Value);
            }

            return Result<ContentPackDependency[]>.Success(output);
        }

        private static Result<ContentVersion?> ParseOptionalVersion(
            string value,
            ContentId packId,
            string sourcePath)
        {
            if (string.IsNullOrEmpty(value))
            {
                return Result<ContentVersion?>.Success(null);
            }

            var result = ContentVersion.Parse(value, packId, sourcePath);
            return result.IsSuccess
                ? Result<ContentVersion?>.Success(result.Value)
                : Result<ContentVersion?>.Failure(result.Error);
        }
    }
}
