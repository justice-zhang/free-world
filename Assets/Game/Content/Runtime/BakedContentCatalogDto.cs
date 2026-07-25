using System;
using Game.Core;

namespace Game.Content.Runtime
{
    /// <summary>
    /// Unity-serializable DTO for a baked content catalog. All IDs remain strings on disk.
    /// </summary>
    [Serializable]
    public sealed class BakedContentCatalogDto
    {
        /// <summary>Gets or sets the serialized manifest.</summary>
        public ContentPackManifestDto manifest;

        /// <summary>Gets or sets serialized runtime definitions.</summary>
        public RuntimeContentDefinitionDto[] definitions;

        /// <summary>Gets or sets the expected lowercase SHA-256 content hash.</summary>
        public string contentHash;

        /// <summary>
        /// Converts this serialized DTO into a verified pure runtime catalog.
        /// </summary>
        public Result<BakedContentCatalog> ToCatalog()
        {
            if (manifest == null)
            {
                return Result<BakedContentCatalog>.Failure(
                    new Error(ErrorCode.InvalidCatalog, "Catalog manifest DTO is missing."));
            }

            var manifestResult = manifest.ToManifest();
            if (!manifestResult.IsSuccess)
            {
                return Result<BakedContentCatalog>.Failure(manifestResult.Error);
            }

            var runtimeManifest = manifestResult.Value;
            var sourceDefinitions = definitions ?? Array.Empty<RuntimeContentDefinitionDto>();
            var runtimeDefinitions = new RuntimeContentDefinition[sourceDefinitions.Length];
            for (var index = 0; index < sourceDefinitions.Length; index++)
            {
                if (sourceDefinitions[index] == null)
                {
                    return Result<BakedContentCatalog>.Failure(
                        new Error(
                            ErrorCode.InvalidCatalog,
                            "Catalog contains a null definition DTO at index " + index + ".",
                            default,
                            runtimeManifest.PackId,
                            runtimeManifest.SourceAssetPath));
                }

                var definitionResult =
                    sourceDefinitions[index].ToDefinition(runtimeManifest.PackId);
                if (!definitionResult.IsSuccess)
                {
                    return Result<BakedContentCatalog>.Failure(definitionResult.Error);
                }

                runtimeDefinitions[index] = definitionResult.Value;
            }

            return BakedContentCatalog.CreateVerified(
                runtimeManifest,
                runtimeDefinitions,
                contentHash);
        }

        internal static BakedContentCatalogDto FromCatalog(BakedContentCatalog catalog)
        {
            var dto = new BakedContentCatalogDto
            {
                manifest = ContentPackManifestDto.FromManifest(catalog.Manifest),
                definitions = new RuntimeContentDefinitionDto[catalog.Definitions.Count],
                contentHash = catalog.ContentHash
            };

            for (var index = 0; index < catalog.Definitions.Count; index++)
            {
                dto.definitions[index] =
                    RuntimeContentDefinitionDto.FromDefinition(catalog.Definitions[index]);
            }

            return dto;
        }
    }

    /// <summary>
    /// Serialized string DTO for a content pack manifest.
    /// </summary>
    [Serializable]
    public sealed class ContentPackManifestDto
    {
        /// <summary>Gets or sets the stable pack ID.</summary>
        public string packId;

        /// <summary>Gets or sets the pack version.</summary>
        public string version;

        /// <summary>Gets or sets the content schema version.</summary>
        public int schemaVersion;

        /// <summary>Gets or sets the inclusive minimum game version.</summary>
        public string minimumGameVersion;

        /// <summary>Gets or sets the optional inclusive maximum game version.</summary>
        public string maximumGameVersion;

        /// <summary>Gets or sets serialized pack dependencies.</summary>
        public ContentPackDependencyDto[] dependencies;

        /// <summary>Gets or sets the baked catalog address.</summary>
        public string catalogAddress;

        /// <summary>Gets or sets the Addressables pack label.</summary>
        public string assetLabel;

        /// <summary>Gets or sets whether the pack is first-party content.</summary>
        public bool official;

        /// <summary>Gets or sets the authoring manifest path used for diagnostics.</summary>
        public string sourceAssetPath;

        internal Result<ContentPackManifest> ToManifest()
        {
            var packResult = CatalogDtoParsing.ParseCanonicalId(
                packId,
                default,
                sourceAssetPath,
                "pack ID");
            if (!packResult.IsSuccess)
            {
                return Result<ContentPackManifest>.Failure(packResult.Error);
            }

            var canonicalPackId = packResult.Value;
            var versionResult = ContentVersion.Parse(
                version,
                canonicalPackId,
                sourceAssetPath);
            if (!versionResult.IsSuccess)
            {
                return Result<ContentPackManifest>.Failure(versionResult.Error);
            }

            var minimumResult = ContentVersion.Parse(
                minimumGameVersion,
                canonicalPackId,
                sourceAssetPath);
            if (!minimumResult.IsSuccess)
            {
                return Result<ContentPackManifest>.Failure(minimumResult.Error);
            }

            ContentVersion? maximum = null;
            if (!string.IsNullOrEmpty(maximumGameVersion))
            {
                var maximumResult = ContentVersion.Parse(
                    maximumGameVersion,
                    canonicalPackId,
                    sourceAssetPath);
                if (!maximumResult.IsSuccess)
                {
                    return Result<ContentPackManifest>.Failure(maximumResult.Error);
                }

                maximum = maximumResult.Value;
            }

            var sourceDependencies = dependencies ?? Array.Empty<ContentPackDependencyDto>();
            var runtimeDependencies = new ContentPackDependency[sourceDependencies.Length];
            for (var index = 0; index < sourceDependencies.Length; index++)
            {
                if (sourceDependencies[index] == null)
                {
                    return Result<ContentPackManifest>.Failure(
                        new Error(
                            ErrorCode.InvalidCatalog,
                            "Manifest contains a null dependency at index " + index + ".",
                            default,
                            canonicalPackId,
                            sourceAssetPath));
                }

                var dependencyResult = sourceDependencies[index].ToDependency(
                    canonicalPackId,
                    sourceAssetPath);
                if (!dependencyResult.IsSuccess)
                {
                    return Result<ContentPackManifest>.Failure(dependencyResult.Error);
                }

                runtimeDependencies[index] = dependencyResult.Value;
            }

            return Result<ContentPackManifest>.Success(
                new ContentPackManifest(
                    canonicalPackId,
                    versionResult.Value,
                    schemaVersion,
                    minimumResult.Value,
                    maximum,
                    runtimeDependencies,
                    catalogAddress,
                    assetLabel,
                    official,
                    sourceAssetPath));
        }

        internal static ContentPackManifestDto FromManifest(ContentPackManifest manifest)
        {
            var dto = new ContentPackManifestDto
            {
                packId = manifest.PackId.Value,
                version = manifest.Version.ToString(),
                schemaVersion = manifest.SchemaVersion,
                minimumGameVersion = manifest.MinimumGameVersion.ToString(),
                maximumGameVersion = manifest.MaximumGameVersion.HasValue
                    ? manifest.MaximumGameVersion.Value.ToString()
                    : string.Empty,
                dependencies = new ContentPackDependencyDto[manifest.Dependencies.Count],
                catalogAddress = manifest.CatalogAddress,
                assetLabel = manifest.AssetLabel,
                official = manifest.Official,
                sourceAssetPath = manifest.SourceAssetPath
            };

            for (var index = 0; index < manifest.Dependencies.Count; index++)
            {
                dto.dependencies[index] =
                    ContentPackDependencyDto.FromDependency(manifest.Dependencies[index]);
            }

            return dto;
        }
    }

    /// <summary>
    /// Serialized string DTO for a content pack dependency.
    /// </summary>
    [Serializable]
    public sealed class ContentPackDependencyDto
    {
        /// <summary>Gets or sets the required pack ID.</summary>
        public string packId;

        /// <summary>Gets or sets the inclusive minimum pack version.</summary>
        public string minimumVersion;

        /// <summary>Gets or sets the optional inclusive maximum pack version.</summary>
        public string maximumVersion;

        internal Result<ContentPackDependency> ToDependency(
            ContentId ownerPackId,
            string sourceAssetPath)
        {
            var idResult = CatalogDtoParsing.ParseCanonicalId(
                packId,
                ownerPackId,
                sourceAssetPath,
                "dependency pack ID");
            if (!idResult.IsSuccess)
            {
                return Result<ContentPackDependency>.Failure(idResult.Error);
            }

            var minimumResult = ContentVersion.Parse(
                minimumVersion,
                ownerPackId,
                sourceAssetPath);
            if (!minimumResult.IsSuccess)
            {
                return Result<ContentPackDependency>.Failure(minimumResult.Error);
            }

            ContentVersion? maximum = null;
            if (!string.IsNullOrEmpty(maximumVersion))
            {
                var maximumResult = ContentVersion.Parse(
                    maximumVersion,
                    ownerPackId,
                    sourceAssetPath);
                if (!maximumResult.IsSuccess)
                {
                    return Result<ContentPackDependency>.Failure(maximumResult.Error);
                }

                maximum = maximumResult.Value;
            }

            return Result<ContentPackDependency>.Success(
                new ContentPackDependency(idResult.Value, minimumResult.Value, maximum));
        }

        internal static ContentPackDependencyDto FromDependency(
            ContentPackDependency dependency)
        {
            return new ContentPackDependencyDto
            {
                packId = dependency.PackId.Value,
                minimumVersion = dependency.MinimumVersion.ToString(),
                maximumVersion = dependency.MaximumVersion.HasValue
                    ? dependency.MaximumVersion.Value.ToString()
                    : string.Empty
            };
        }
    }

    /// <summary>
    /// Union DTO for the typed M1 runtime definition set.
    /// </summary>
    [Serializable]
    public sealed class RuntimeContentDefinitionDto
    {
        /// <summary>Gets or sets the stable runtime definition kind.</summary>
        public string kind;

        /// <summary>Gets or sets the stable content ID.</summary>
        public string id;

        /// <summary>Gets or sets the display-name localization key.</summary>
        public string localizedNameKey;

        /// <summary>Gets or sets the description localization key.</summary>
        public string localizedDescriptionKey;

        /// <summary>Gets or sets the authoring asset path used for diagnostics.</summary>
        public string sourceAssetPath;

        /// <summary>Gets or sets canonical content tags.</summary>
        public string[] tags;

        /// <summary>Gets or sets base maximum health for applicable kinds.</summary>
        public float baseMaxHealth;

        /// <summary>Gets or sets movement speed for character definitions.</summary>
        public float moveSpeed;

        /// <summary>Gets or sets starting skill IDs for character definitions.</summary>
        public string[] startingSkillIds;

        /// <summary>Gets or sets cooldown metadata for skill definitions.</summary>
        public float cooldownSeconds;

        /// <summary>Gets or sets collision radius for enemy definitions.</summary>
        public float collisionRadius;

        /// <summary>Gets or sets the registered map runtime provider ID.</summary>
        public string runtimeProviderId;

        /// <summary>Gets or sets the map scene address.</summary>
        public string sceneAddress;

        internal Result<RuntimeContentDefinition> ToDefinition(ContentId packId)
        {
            var idResult = CatalogDtoParsing.ParseCanonicalId(
                id,
                packId,
                sourceAssetPath,
                "content ID");
            if (!idResult.IsSuccess)
            {
                return Result<RuntimeContentDefinition>.Failure(idResult.Error);
            }

            var tagResult = CatalogDtoParsing.ParseTags(tags, packId, idResult.Value, sourceAssetPath);
            if (!tagResult.IsSuccess)
            {
                return Result<RuntimeContentDefinition>.Failure(tagResult.Error);
            }

            switch (kind)
            {
                case RuntimeContentKinds.Character:
                {
                    var skillResult = CatalogDtoParsing.ParseIds(
                        startingSkillIds,
                        packId,
                        idResult.Value,
                        sourceAssetPath);
                    if (!skillResult.IsSuccess)
                    {
                        return Result<RuntimeContentDefinition>.Failure(skillResult.Error);
                    }

                    return Result<RuntimeContentDefinition>.Success(
                        new RuntimeCharacterDefinition(
                            idResult.Value,
                            localizedNameKey,
                            localizedDescriptionKey,
                            sourceAssetPath,
                            tagResult.Value,
                            baseMaxHealth,
                            moveSpeed,
                            skillResult.Value));
                }

                case RuntimeContentKinds.Skill:
                    return Result<RuntimeContentDefinition>.Success(
                        new RuntimeSkillDefinition(
                            idResult.Value,
                            localizedNameKey,
                            localizedDescriptionKey,
                            sourceAssetPath,
                            tagResult.Value,
                            cooldownSeconds));

                case RuntimeContentKinds.Enemy:
                    return Result<RuntimeContentDefinition>.Success(
                        new RuntimeEnemyDefinition(
                            idResult.Value,
                            localizedNameKey,
                            localizedDescriptionKey,
                            sourceAssetPath,
                            tagResult.Value,
                            baseMaxHealth,
                            collisionRadius));

                case RuntimeContentKinds.Map:
                    return Result<RuntimeContentDefinition>.Success(
                        new RuntimeMapDefinition(
                            idResult.Value,
                            localizedNameKey,
                            localizedDescriptionKey,
                            sourceAssetPath,
                            tagResult.Value,
                            runtimeProviderId,
                            sceneAddress));

                default:
                    return Result<RuntimeContentDefinition>.Failure(
                        new Error(
                            ErrorCode.InvalidCatalog,
                            "Unsupported runtime definition kind '" + (kind ?? string.Empty) + "'.",
                            idResult.Value,
                            packId,
                            sourceAssetPath));
            }
        }

        internal static RuntimeContentDefinitionDto FromDefinition(
            RuntimeContentDefinition definition)
        {
            var dto = new RuntimeContentDefinitionDto
            {
                kind = definition.Kind,
                id = definition.Id.Value,
                localizedNameKey = definition.LocalizedNameKey,
                localizedDescriptionKey = definition.LocalizedDescriptionKey,
                sourceAssetPath = definition.SourceAssetPath,
                tags = new string[definition.Tags.Count],
                startingSkillIds = Array.Empty<string>(),
                runtimeProviderId = string.Empty,
                sceneAddress = string.Empty
            };

            for (var index = 0; index < definition.Tags.Count; index++)
            {
                dto.tags[index] = definition.Tags[index].Value;
            }

            if (definition is RuntimeCharacterDefinition character)
            {
                dto.baseMaxHealth = character.BaseMaxHealth;
                dto.moveSpeed = character.MoveSpeed;
                dto.startingSkillIds = new string[character.StartingSkillIds.Count];
                for (var index = 0; index < character.StartingSkillIds.Count; index++)
                {
                    dto.startingSkillIds[index] = character.StartingSkillIds[index].Value;
                }
            }
            else if (definition is RuntimeSkillDefinition skill)
            {
                dto.cooldownSeconds = skill.CooldownSeconds;
            }
            else if (definition is RuntimeEnemyDefinition enemy)
            {
                dto.baseMaxHealth = enemy.BaseMaxHealth;
                dto.collisionRadius = enemy.CollisionRadius;
            }
            else if (definition is RuntimeMapDefinition map)
            {
                dto.runtimeProviderId = map.RuntimeProviderId;
                dto.sceneAddress = map.SceneAddress;
            }
            else
            {
                throw new ArgumentException(
                    "Unsupported runtime definition type " + definition.GetType().FullName + ".",
                    nameof(definition));
            }

            return dto;
        }
    }

    internal static class CatalogDtoParsing
    {
        public static Result<ContentId> ParseCanonicalId(
            string rawId,
            ContentId packId,
            string sourceAssetPath,
            string fieldName)
        {
            if (!ContentId.IsCanonical(rawId))
            {
                return Result<ContentId>.Failure(
                    new Error(
                        ErrorCode.InvalidContentId,
                        "Serialized " + fieldName + " is not canonical: '" +
                        (rawId ?? string.Empty) + "'.",
                        default,
                        packId,
                        sourceAssetPath));
            }

            return ContentId.Create(rawId, packId, sourceAssetPath);
        }

        public static Result<ContentTag[]> ParseTags(
            string[] rawTags,
            ContentId packId,
            ContentId ownerId,
            string sourceAssetPath)
        {
            var source = rawTags ?? Array.Empty<string>();
            var output = new ContentTag[source.Length];
            for (var index = 0; index < source.Length; index++)
            {
                if (!ContentId.IsCanonical(source[index]))
                {
                    return Result<ContentTag[]>.Failure(
                        new Error(
                            ErrorCode.InvalidContentTag,
                            "Serialized content tag is not canonical: '" +
                            (source[index] ?? string.Empty) + "'.",
                            ownerId,
                            packId,
                            sourceAssetPath));
                }

                var result = ContentTag.Create(source[index], packId, sourceAssetPath);
                if (!result.IsSuccess)
                {
                    return Result<ContentTag[]>.Failure(result.Error);
                }

                output[index] = result.Value;
            }

            return Result<ContentTag[]>.Success(output);
        }

        public static Result<ContentId[]> ParseIds(
            string[] rawIds,
            ContentId packId,
            ContentId ownerId,
            string sourceAssetPath)
        {
            var source = rawIds ?? Array.Empty<string>();
            var output = new ContentId[source.Length];
            for (var index = 0; index < source.Length; index++)
            {
                if (!ContentId.IsCanonical(source[index]))
                {
                    return Result<ContentId[]>.Failure(
                        new Error(
                            ErrorCode.InvalidContentId,
                            "Serialized referenced content ID is not canonical: '" +
                            (source[index] ?? string.Empty) + "'.",
                            ownerId,
                            packId,
                            sourceAssetPath));
                }

                var result = ContentId.Create(source[index], packId, sourceAssetPath);
                if (!result.IsSuccess)
                {
                    return Result<ContentId[]>.Failure(result.Error);
                }

                output[index] = result.Value;
            }

            return Result<ContentId[]>.Success(output);
        }
    }
}
