using System;
using System.Collections.Generic;
using Game.Core;

namespace Game.Content.Runtime
{
    /// <summary>
    /// Contains all content validation failures found in one pass.
    /// </summary>
    public sealed class ContentValidationReport
    {
        private readonly List<Error> errors = new List<Error>();
        private readonly IReadOnlyList<Error> errorsView;

        internal ContentValidationReport()
        {
            errorsView = errors.AsReadOnly();
        }

        /// <summary>
        /// Gets whether no validation failures were found.
        /// </summary>
        public bool IsValid => errors.Count == 0;

        /// <summary>
        /// Gets the ordered validation failures.
        /// </summary>
        public IReadOnlyList<Error> Errors => errorsView;

        internal void Add(Error error)
        {
            errors.Add(error);
        }
    }

    /// <summary>
    /// Validates canonical IDs, pack graphs, duplicates, references, and compatibility.
    /// </summary>
    public static class ContentValidator
    {
        /// <summary>
        /// Validates that an authoring string is already canonical instead of silently normalizing it.
        /// </summary>
        public static Result<ContentId> ValidateAuthoringId(
            string rawId,
            ContentId packId,
            string authorAssetPath)
        {
            if (!ContentId.IsCanonical(rawId))
            {
                return Result<ContentId>.Failure(
                    new Error(
                        ErrorCode.InvalidContentId,
                        "Authoring ContentId must already be lowercase canonical text: '" +
                        (rawId ?? string.Empty) + "'.",
                        default,
                        packId,
                        authorAssetPath));
            }

            return ContentId.Create(rawId, packId, authorAssetPath);
        }

        /// <summary>
        /// Validates a complete set of baked catalogs.
        /// </summary>
        public static ContentValidationReport ValidateCatalogs(
            IReadOnlyList<BakedContentCatalog> catalogs,
            ContentVersion gameVersion)
        {
            var report = new ContentValidationReport();
            if (catalogs == null)
            {
                report.Add(new Error(ErrorCode.InvalidCatalog, "Catalog collection is missing."));
                return report;
            }

            var manifests = new ContentPackManifest[catalogs.Count];
            for (var index = 0; index < catalogs.Count; index++)
            {
                if (catalogs[index] == null)
                {
                    report.Add(
                        new Error(
                            ErrorCode.InvalidCatalog,
                            "Catalog at input index " + index + " is null."));
                    return report;
                }

                manifests[index] = catalogs[index].Manifest;
            }

            var topology = ContentPackTopology.Sort(manifests, gameVersion);
            if (!topology.IsSuccess)
            {
                report.Add(topology.Error);
            }

            var origins = new Dictionary<ContentId, ContentOrigin>();
            for (var catalogIndex = 0; catalogIndex < catalogs.Count; catalogIndex++)
            {
                var catalog = catalogs[catalogIndex];
                var packId = catalog.Manifest.PackId;
                for (var definitionIndex = 0;
                     definitionIndex < catalog.Definitions.Count;
                     definitionIndex++)
                {
                    var definition = catalog.Definitions[definitionIndex];
                    if (definition == null)
                    {
                        report.Add(
                            new Error(
                                ErrorCode.InvalidCatalog,
                                "Pack contains a null runtime definition at index " +
                                definitionIndex + ".",
                                default,
                                packId,
                                catalog.Manifest.SourceAssetPath));
                        continue;
                    }

                    if (!definition.Id.IsValid)
                    {
                        report.Add(
                            new Error(
                                ErrorCode.InvalidContentId,
                                "Runtime definition has an invalid ContentId.",
                                definition.Id,
                                packId,
                                definition.SourceAssetPath));
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(definition.SourceAssetPath))
                    {
                        report.Add(
                            new Error(
                                ErrorCode.InvalidAuthoringData,
                                "Runtime definition has no author asset path.",
                                definition.Id,
                                packId,
                                definition.SourceAssetPath));
                    }

                    if (string.IsNullOrWhiteSpace(definition.LocalizedNameKey) ||
                        string.IsNullOrWhiteSpace(definition.LocalizedDescriptionKey))
                    {
                        report.Add(
                            new Error(
                                ErrorCode.InvalidAuthoringData,
                                "Runtime definition must provide name and description localization keys.",
                                definition.Id,
                                packId,
                                definition.SourceAssetPath));
                    }

                    if (origins.TryGetValue(definition.Id, out var firstOrigin))
                    {
                        report.Add(
                            CreateDuplicateError(
                                definition.Id,
                                firstOrigin,
                                new ContentOrigin(packId, definition.SourceAssetPath)));
                    }
                    else
                    {
                        origins.Add(
                            definition.Id,
                            new ContentOrigin(packId, definition.SourceAssetPath));
                    }

                    ValidateDefinitionValues(definition, packId, report);
                }
            }

            for (var catalogIndex = 0; catalogIndex < catalogs.Count; catalogIndex++)
            {
                var catalog = catalogs[catalogIndex];
                var packId = catalog.Manifest.PackId;
                for (var definitionIndex = 0;
                     definitionIndex < catalog.Definitions.Count;
                     definitionIndex++)
                {
                    var definition = catalog.Definitions[definitionIndex];
                    if (definition == null)
                    {
                        continue;
                    }

                    for (var referenceIndex = 0;
                         referenceIndex < definition.ReferencedContentIds.Count;
                         referenceIndex++)
                    {
                        var referencedId = definition.ReferencedContentIds[referenceIndex];
                        if (!origins.ContainsKey(referencedId))
                        {
                            report.Add(
                                new Error(
                                    ErrorCode.MissingReference,
                                    "Content '" + definition.Id + "' references missing content '" +
                                    referencedId + "'.",
                                    definition.Id,
                                    packId,
                                    definition.SourceAssetPath));
                        }
                    }
                }
            }

            return report;
        }

        internal static Error CreateDuplicateError(
            ContentId duplicateId,
            ContentOrigin first,
            ContentOrigin second)
        {
            return new Error(
                ErrorCode.DuplicateContentId,
                "ContentId '" + duplicateId + "' is declared by pack '" +
                first.PackId + "' at '" + first.AuthorAssetPath +
                "' and pack '" + second.PackId + "' at '" +
                second.AuthorAssetPath + "'. Silent override is forbidden.",
                duplicateId,
                second.PackId,
                second.AuthorAssetPath);
        }

        private static void ValidateDefinitionValues(
            RuntimeContentDefinition definition,
            ContentId packId,
            ContentValidationReport report)
        {
            string message = null;
            if (definition is RuntimeCharacterDefinition character &&
                (character.BaseMaxHealth <= 0f || character.MoveSpeed < 0f))
            {
                message = "Character health must be positive and move speed cannot be negative.";
            }
            else if (definition is RuntimeSkillDefinition skill &&
                     skill.CooldownSeconds < 0f)
            {
                message = "Skill cooldown cannot be negative.";
            }
            else if (definition is RuntimeEnemyDefinition enemy &&
                     (enemy.BaseMaxHealth <= 0f || enemy.CollisionRadius <= 0f))
            {
                message = "Enemy health and collision radius must be positive.";
            }
            else if (definition is RuntimeMapDefinition map &&
                     (string.IsNullOrWhiteSpace(map.RuntimeProviderId) ||
                      string.IsNullOrWhiteSpace(map.SceneAddress)))
            {
                message = "Map runtime provider ID and scene address are required.";
            }

            if (message != null)
            {
                report.Add(
                    new Error(
                        ErrorCode.InvalidAuthoringData,
                        message,
                        definition.Id,
                        packId,
                        definition.SourceAssetPath));
            }
        }

        internal readonly struct ContentOrigin
        {
            public ContentOrigin(ContentId packId, string authorAssetPath)
            {
                PackId = packId;
                AuthorAssetPath = authorAssetPath ?? string.Empty;
            }

            public ContentId PackId { get; }

            public string AuthorAssetPath { get; }
        }
    }
}
