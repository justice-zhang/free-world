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
            if (!ContentPackTopology.IsSchemaVersionSupported(
                    runtimeManifest.SchemaVersion))
            {
                return Result<BakedContentCatalog>.Failure(
                    new Error(
                        ErrorCode.UnsupportedSchemaVersion,
                        "Serialized pack schema " + runtimeManifest.SchemaVersion +
                        " is outside the supported range [" +
                        ContentPackTopology.MinimumSupportedSchemaVersion + ", " +
                        ContentPackTopology.LatestSupportedSchemaVersion + "].",
                        default,
                        runtimeManifest.PackId,
                        runtimeManifest.SourceAssetPath));
            }

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

                var definitionResult = sourceDefinitions[index].ToDefinition(
                    runtimeManifest.PackId,
                    runtimeManifest.SchemaVersion);
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
    /// Union DTO for the explicitly supported runtime definition set.
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

        /// <summary>Gets or sets schema-6 generic character-mechanic IDs.</summary>
        public string[] mechanicIds;

        /// <summary>Gets or sets cooldown metadata for skill definitions.</summary>
        public float cooldownSeconds;

        /// <summary>Gets or sets whether the skill contains schema-3 runtime modules.</summary>
        public bool modularSkill;

        /// <summary>Gets or sets resource consumed by one successful activation.</summary>
        public float resourceCost;

        /// <summary>Gets or sets the schema-3 trigger module.</summary>
        public SkillModuleDefinitionDto triggerModule;

        /// <summary>Gets or sets the schema-3 condition module.</summary>
        public SkillModuleDefinitionDto conditionModule;

        /// <summary>Gets or sets the schema-3 targeting module.</summary>
        public SkillModuleDefinitionDto targetingModule;

        /// <summary>Gets or sets the schema-3 delivery module.</summary>
        public SkillModuleDefinitionDto deliveryModule;

        /// <summary>Gets or sets baked schema-3 effect operations.</summary>
        public SkillEffectOpDto[] effectOps;

        /// <summary>Gets or sets path-validated schema-3 level patches.</summary>
        public SkillLevelPatchDto[] levelPatches;

        /// <summary>Gets or sets collision radius for enemy definitions.</summary>
        public float collisionRadius;

        /// <summary>Gets or sets schema-4 enemy runtime data.</summary>
        public EnemyRuntimeDefinitionDto enemyRuntime;

        /// <summary>Gets or sets the registered map runtime provider ID.</summary>
        public string runtimeProviderId;

        /// <summary>Gets or sets the map scene address.</summary>
        public string sceneAddress;

        /// <summary>Gets or sets schema-4 map runtime data.</summary>
        public MapRuntimeDefinitionDto mapRuntime;

        /// <summary>Gets or sets schema-4 encounter schedule data.</summary>
        public EncounterScheduleDefinitionDto encounterSchedule;

        /// <summary>Gets or sets schema-5 build/progression data.</summary>
        public M6RuntimeDefinitionDto buildProgression;

        /// <summary>Gets or sets schema-6 Qinglan runtime data.</summary>
        public QinglanRuntimeDefinitionDto qinglanRuntime;

        /// <summary>Gets or sets the stable status stacking-policy token.</summary>
        public string stackingPolicy;

        /// <summary>Gets or sets the default status lifetime in seconds.</summary>
        public float durationSeconds;

        /// <summary>Gets or sets the maximum status stack or instance count.</summary>
        public int maxStacks;

        /// <summary>Gets or sets the status periodic tick interval in seconds.</summary>
        public float tickIntervalSeconds;

        /// <summary>Gets or sets canonical tags that may dispel the status.</summary>
        public string[] dispelTags;

        /// <summary>Gets or sets canonical target immunity tags that block the status.</summary>
        public string[] immunityTags;

        /// <summary>Gets or sets whether the status installs a statistic modifier.</summary>
        public bool statusModifierEnabled;

        /// <summary>Gets or sets the stable statistic ID modified by the status.</summary>
        public string statusModifierStatId;

        /// <summary>Gets or sets the stable modifier-operation token.</summary>
        public string statusModifierOperation;

        /// <summary>Gets or sets the modifier value at strength one and one stack.</summary>
        public float statusModifierValue;

        /// <summary>Gets or sets modifier priority.</summary>
        public int statusModifierPriority;

        /// <summary>Gets or sets the optional stable modifier stacking-group ID.</summary>
        public string statusModifierStackingGroup;

        /// <summary>Gets or sets whether the status deals periodic damage.</summary>
        public bool periodicDamageEnabled;

        /// <summary>Gets or sets the stable periodic damage-type token.</summary>
        public string periodicDamageType;

        /// <summary>Gets or sets the periodic damage tag mask.</summary>
        public ulong periodicDamageTags;

        /// <summary>Gets or sets periodic damage at strength one and one stack.</summary>
        public float periodicDamageValue;

        /// <summary>Gets or sets periodic critical eligibility.</summary>
        public bool periodicCanCritical;

        /// <summary>Gets or sets the periodic proc coefficient.</summary>
        public float periodicProcCoefficient;

        /// <summary>Gets or sets periodic knockback X.</summary>
        public float periodicKnockbackX;

        /// <summary>Gets or sets periodic knockback Y.</summary>
        public float periodicKnockbackY;

        /// <summary>Gets or sets temporary shield capacity granted by the status.</summary>
        public float shieldCapacity;

        internal Result<RuntimeContentDefinition> ToDefinition(
            ContentId packId,
            int schemaVersion)
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

                    var mechanicResult = schemaVersion >= ContentPackTopology.QinglanDemoSchemaVersion
                        ? CatalogDtoParsing.ParseIds(
                            mechanicIds,
                            packId,
                            idResult.Value,
                            sourceAssetPath)
                        : Result<ContentId[]>.Success(Array.Empty<ContentId>());
                    if (!mechanicResult.IsSuccess)
                        return Result<RuntimeContentDefinition>.Failure(mechanicResult.Error);

                    return Result<RuntimeContentDefinition>.Success(
                        new RuntimeCharacterDefinition(
                            idResult.Value,
                            localizedNameKey,
                            localizedDescriptionKey,
                            sourceAssetPath,
                            tagResult.Value,
                            baseMaxHealth,
                            moveSpeed,
                            skillResult.Value,
                            mechanicResult.Value));
                }

                case RuntimeContentKinds.Skill:
                    return ToSkillDefinition(
                        packId,
                        schemaVersion,
                        idResult.Value,
                        tagResult.Value);

                case RuntimeContentKinds.Enemy:
                    if (schemaVersion >= ContentPackTopology.EnemyMapEncounterSchemaVersion)
                    {
                        if (enemyRuntime == null)
                        {
                            return Result<RuntimeContentDefinition>.Failure(
                                new Error(
                                    ErrorCode.InvalidCatalog,
                                    "Schema 4 enemy is missing runtime data.",
                                    idResult.Value,
                                    packId,
                                    sourceAssetPath));
                        }

                        return enemyRuntime.ToDefinition(
                            packId,
                            idResult.Value,
                            localizedNameKey,
                            localizedDescriptionKey,
                            sourceAssetPath,
                            tagResult.Value,
                            baseMaxHealth,
                            collisionRadius);
                    }

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
                    if (schemaVersion >= ContentPackTopology.EnemyMapEncounterSchemaVersion)
                    {
                        if (mapRuntime == null)
                        {
                            return Result<RuntimeContentDefinition>.Failure(
                                new Error(
                                    ErrorCode.InvalidCatalog,
                                    "Schema 4 map is missing runtime data.",
                                    idResult.Value,
                                    packId,
                                    sourceAssetPath));
                        }

                        return mapRuntime.ToDefinition(
                            packId,
                            idResult.Value,
                            localizedNameKey,
                            localizedDescriptionKey,
                            sourceAssetPath,
                            tagResult.Value,
                            runtimeProviderId,
                            sceneAddress,
                            schemaVersion);
                    }

                    return Result<RuntimeContentDefinition>.Success(
                        new RuntimeMapDefinition(
                            idResult.Value,
                            localizedNameKey,
                            localizedDescriptionKey,
                            sourceAssetPath,
                            tagResult.Value,
                            runtimeProviderId,
                            sceneAddress));

                case RuntimeContentKinds.Encounter:
                    if (schemaVersion < ContentPackTopology.EnemyMapEncounterSchemaVersion ||
                        encounterSchedule == null)
                    {
                        return Result<RuntimeContentDefinition>.Failure(
                            new Error(
                                ErrorCode.UnsupportedSchemaVersion,
                                "Encounter schedules require content schema " +
                                ContentPackTopology.EnemyMapEncounterSchemaVersion + " runtime data.",
                                idResult.Value,
                                packId,
                                sourceAssetPath));
                    }

                    return encounterSchedule.ToDefinition(
                        packId,
                        idResult.Value,
                        localizedNameKey,
                        localizedDescriptionKey,
                        sourceAssetPath,
                        tagResult.Value,
                        schemaVersion);

                case RuntimeContentKinds.Status:
                {
                    if (schemaVersion <
                        ContentPackTopology.StatusDefinitionSchemaVersion)
                    {
                        return Result<RuntimeContentDefinition>.Failure(
                            new Error(
                                ErrorCode.UnsupportedSchemaVersion,
                                "Serialized status definitions require content schema " +
                                ContentPackTopology.StatusDefinitionSchemaVersion +
                                " or newer.",
                                idResult.Value,
                                packId,
                                sourceAssetPath));
                    }

                    if (!StatusStackingPolicyCodec.TryParse(
                            stackingPolicy,
                            out var runtimeStackingPolicy))
                    {
                        return Result<RuntimeContentDefinition>.Failure(
                            new Error(
                                ErrorCode.InvalidCatalog,
                                "Unsupported status stacking policy '" +
                                (stackingPolicy ?? string.Empty) + "'.",
                                idResult.Value,
                                packId,
                                sourceAssetPath));
                    }

                    var dispelResult = CatalogDtoParsing.ParseTags(
                        dispelTags,
                        packId,
                        idResult.Value,
                        sourceAssetPath);
                    if (!dispelResult.IsSuccess)
                    {
                        return Result<RuntimeContentDefinition>.Failure(dispelResult.Error);
                    }

                    var immunityResult = CatalogDtoParsing.ParseTags(
                        immunityTags,
                        packId,
                        idResult.Value,
                        sourceAssetPath);
                    if (!immunityResult.IsSuccess)
                    {
                        return Result<RuntimeContentDefinition>.Failure(immunityResult.Error);
                    }

                    var behaviorResult = ToStatusBehavior(
                        packId,
                        idResult.Value);
                    if (!behaviorResult.IsSuccess)
                    {
                        return Result<RuntimeContentDefinition>.Failure(behaviorResult.Error);
                    }

                    return Result<RuntimeContentDefinition>.Success(
                        new RuntimeStatusDefinition(
                            idResult.Value,
                            localizedNameKey,
                            localizedDescriptionKey,
                            sourceAssetPath,
                            tagResult.Value,
                            runtimeStackingPolicy,
                            durationSeconds,
                            maxStacks,
                            tickIntervalSeconds,
                            dispelResult.Value,
                            immunityResult.Value,
                            behaviorResult.Value));
                }

                case RuntimeContentKinds.Passive:
                case RuntimeContentKinds.Trait:
                case RuntimeContentKinds.Offer:
                case RuntimeContentKinds.Synergy:
                case RuntimeContentKinds.Evolution:
                    if (schemaVersion < ContentPackTopology.BuildProgressionSchemaVersion ||
                        buildProgression == null)
                    {
                        return Result<RuntimeContentDefinition>.Failure(
                            new Error(
                                ErrorCode.UnsupportedSchemaVersion,
                                "Build/progression definitions require content schema " +
                                ContentPackTopology.BuildProgressionSchemaVersion + " runtime data.",
                                idResult.Value,
                                packId,
                                sourceAssetPath));
                    }

                    return buildProgression.ToDefinition(
                        kind,
                        packId,
                        idResult.Value,
                        localizedNameKey,
                        localizedDescriptionKey,
                        sourceAssetPath,
                        tagResult.Value);

                case RuntimeContentKinds.CharacterMechanic:
                case RuntimeContentKinds.Reward:
                case RuntimeContentKinds.Pickup:
                case RuntimeContentKinds.Relic:
                case RuntimeContentKinds.MapObjective:
                case RuntimeContentKinds.MapEvent:
                case RuntimeContentKinds.Landmark:
                case RuntimeContentKinds.Boss:
                case RuntimeContentKinds.EliteAffix:
                case RuntimeContentKinds.MetaNode:
                case RuntimeContentKinds.MetaInsert:
                case RuntimeContentKinds.MetaFacility:
                case RuntimeContentKinds.Story:
                case RuntimeContentKinds.Collectible:
                    if (schemaVersion < ContentPackTopology.QinglanDemoSchemaVersion || qinglanRuntime == null)
                    {
                        return Result<RuntimeContentDefinition>.Failure(
                            new Error(
                                ErrorCode.UnsupportedSchemaVersion,
                                "Qinglan definitions require content schema " +
                                ContentPackTopology.QinglanDemoSchemaVersion + " runtime data.",
                                idResult.Value,
                                packId,
                                sourceAssetPath));
                    }

                    return qinglanRuntime.ToDefinition(
                        kind,
                        packId,
                        idResult.Value,
                        localizedNameKey,
                        localizedDescriptionKey,
                        sourceAssetPath,
                        tagResult.Value);

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
                sceneAddress = string.Empty,
                stackingPolicy = string.Empty,
                statusModifierStatId = string.Empty,
                statusModifierOperation = string.Empty,
                statusModifierStackingGroup = string.Empty,
                periodicDamageType = string.Empty,
                dispelTags = Array.Empty<string>(),
                immunityTags = Array.Empty<string>(),
                effectOps = Array.Empty<SkillEffectOpDto>(),
                levelPatches = Array.Empty<SkillLevelPatchDto>()
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
                dto.mechanicIds = new string[character.MechanicIds.Count];
                for (var index = 0; index < character.MechanicIds.Count; index++)
                    dto.mechanicIds[index] = character.MechanicIds[index].Value;
            }
            else if (definition is RuntimeSkillDefinition skill)
            {
                dto.cooldownSeconds = skill.CooldownSeconds;
                dto.modularSkill = skill.IsExecutable;
                if (skill.IsExecutable)
                {
                    dto.resourceCost = skill.ResourceCost;
                    dto.triggerModule =
                        SkillModuleDefinitionDto.FromDefinition(skill.Trigger);
                    dto.conditionModule =
                        SkillModuleDefinitionDto.FromDefinition(skill.Condition);
                    dto.targetingModule =
                        SkillModuleDefinitionDto.FromDefinition(skill.Targeting);
                    dto.deliveryModule =
                        SkillModuleDefinitionDto.FromDefinition(skill.Delivery);
                    dto.effectOps = new SkillEffectOpDto[skill.Effects.Count];
                    for (var index = 0; index < skill.Effects.Count; index++)
                    {
                        dto.effectOps[index] =
                            SkillEffectOpDto.FromEffect(skill.Effects[index]);
                    }

                    dto.levelPatches = new SkillLevelPatchDto[skill.LevelPatches.Count];
                    for (var index = 0; index < skill.LevelPatches.Count; index++)
                    {
                        dto.levelPatches[index] =
                            SkillLevelPatchDto.FromPatch(skill.LevelPatches[index]);
                    }
                }
            }
            else if (definition is RuntimeEnemyDefinition enemy)
            {
                dto.baseMaxHealth = enemy.BaseMaxHealth;
                dto.collisionRadius = enemy.CollisionRadius;
                if (enemy.HasM5Data)
                    dto.enemyRuntime = EnemyRuntimeDefinitionDto.FromDefinition(enemy);
            }
            else if (definition is RuntimeMapDefinition map)
            {
                dto.runtimeProviderId = map.RuntimeProviderId;
                dto.sceneAddress = map.SceneAddress;
                if (map.HasM5Data)
                    dto.mapRuntime = MapRuntimeDefinitionDto.FromDefinition(map);
            }
            else if (definition is RuntimeEncounterSchedule encounter)
            {
                dto.encounterSchedule =
                    EncounterScheduleDefinitionDto.FromDefinition(encounter);
            }
            else if (definition is RuntimeStatusDefinition status)
            {
                dto.stackingPolicy =
                    StatusStackingPolicyCodec.ToSerializedValue(status.StackingPolicy);
                dto.durationSeconds = status.DurationSeconds;
                dto.maxStacks = status.MaxStacks;
                dto.tickIntervalSeconds = status.TickIntervalSeconds;
                dto.dispelTags = new string[status.DispelTags.Count];
                for (var index = 0; index < status.DispelTags.Count; index++)
                {
                    dto.dispelTags[index] = status.DispelTags[index].Value;
                }

                dto.immunityTags = new string[status.ImmunityTags.Count];
                for (var index = 0; index < status.ImmunityTags.Count; index++)
                {
                    dto.immunityTags[index] = status.ImmunityTags[index].Value;
                }

                var behavior = status.Behavior;
                var modifier = behavior.Modifier;
                dto.statusModifierEnabled = modifier.Enabled;
                if (modifier.Enabled)
                {
                    dto.statusModifierStatId = modifier.StatId.Value;
                    dto.statusModifierOperation =
                        ModifierOperationCodec.ToSerializedValue(modifier.Operation);
                    dto.statusModifierValue = modifier.Value;
                    dto.statusModifierPriority = modifier.Priority;
                    dto.statusModifierStackingGroup = modifier.StackingGroup.IsValid
                        ? modifier.StackingGroup.Value
                        : string.Empty;
                }

                var periodic = behavior.PeriodicDamage;
                dto.periodicDamageEnabled = periodic.Enabled;
                if (periodic.Enabled)
                {
                    dto.periodicDamageType =
                        DamageTypeCodec.ToSerializedValue(periodic.DamageType);
                    dto.periodicDamageTags = (ulong)periodic.Tags;
                    dto.periodicDamageValue = periodic.BaseValue;
                    dto.periodicCanCritical = periodic.CanCritical;
                    dto.periodicProcCoefficient = periodic.ProcCoefficient;
                    dto.periodicKnockbackX = periodic.Knockback.X;
                    dto.periodicKnockbackY = periodic.Knockback.Y;
                }

                dto.shieldCapacity = behavior.ShieldCapacity;
            }
            else if (definition is RuntimePassiveDefinition ||
                     definition is RuntimeTraitDefinition ||
                     definition is RuntimeUpgradeOfferDefinition ||
                     definition is RuntimeSynergyDefinition ||
                     definition is RuntimeEvolutionDefinition)
            {
                dto.buildProgression = M6RuntimeDefinitionDto.FromDefinition(definition);
            }
            else if (definition is RuntimeQinglanDefinition qinglan)
            {
                dto.qinglanRuntime = QinglanRuntimeDefinitionDto.FromDefinition(qinglan);
            }
            else
            {
                throw new ArgumentException(
                    "Unsupported runtime definition type " + definition.GetType().FullName + ".",
                    nameof(definition));
            }

            return dto;
        }

        private Result<RuntimeContentDefinition> ToSkillDefinition(
            ContentId packId,
            int schemaVersion,
            ContentId ownerId,
            ContentTag[] runtimeTags)
        {
            if (schemaVersion < ContentPackTopology.ModularSkillSchemaVersion)
            {
                if (modularSkill)
                {
                    return Result<RuntimeContentDefinition>.Failure(
                        new Error(
                            ErrorCode.UnsupportedSchemaVersion,
                            "Serialized modular skills require content schema " +
                            ContentPackTopology.ModularSkillSchemaVersion + " or newer.",
                            ownerId,
                            packId,
                            sourceAssetPath));
                }

                return Result<RuntimeContentDefinition>.Success(
                    new RuntimeSkillDefinition(
                        ownerId,
                        localizedNameKey,
                        localizedDescriptionKey,
                        sourceAssetPath,
                        runtimeTags,
                        cooldownSeconds));
            }

            if (!modularSkill || triggerModule == null || conditionModule == null ||
                targetingModule == null || deliveryModule == null)
            {
                return Result<RuntimeContentDefinition>.Failure(
                    new Error(
                        ErrorCode.InvalidCatalog,
                        "Schema 3 skill is missing required modular runtime data.",
                        ownerId,
                        packId,
                        sourceAssetPath));
            }

            var triggerResult = triggerModule.ToDefinition(
                packId,
                ownerId,
                sourceAssetPath,
                "trigger");
            if (!triggerResult.IsSuccess) return Result<RuntimeContentDefinition>.Failure(triggerResult.Error);
            var conditionResult = conditionModule.ToDefinition(
                packId,
                ownerId,
                sourceAssetPath,
                "condition");
            if (!conditionResult.IsSuccess) return Result<RuntimeContentDefinition>.Failure(conditionResult.Error);
            var targetingResult = targetingModule.ToDefinition(
                packId,
                ownerId,
                sourceAssetPath,
                "targeting");
            if (!targetingResult.IsSuccess) return Result<RuntimeContentDefinition>.Failure(targetingResult.Error);
            var deliveryResult = deliveryModule.ToDefinition(
                packId,
                ownerId,
                sourceAssetPath,
                "delivery");
            if (!deliveryResult.IsSuccess) return Result<RuntimeContentDefinition>.Failure(deliveryResult.Error);

            var sourceEffects = effectOps ?? Array.Empty<SkillEffectOpDto>();
            if (sourceEffects.Length == 0)
            {
                return Result<RuntimeContentDefinition>.Failure(
                    new Error(
                        ErrorCode.InvalidCatalog,
                        "Schema 3 skill requires at least one effect operation.",
                        ownerId,
                        packId,
                        sourceAssetPath));
            }

            var runtimeEffects = new EffectOp[sourceEffects.Length];
            for (var index = 0; index < sourceEffects.Length; index++)
            {
                if (sourceEffects[index] == null)
                {
                    return Result<RuntimeContentDefinition>.Failure(
                        new Error(
                            ErrorCode.InvalidCatalog,
                            "Schema 3 skill contains a null effect operation.",
                            ownerId,
                            packId,
                            sourceAssetPath));
                }

                var effectResult = sourceEffects[index].ToEffect(
                    packId,
                    ownerId,
                    sourceAssetPath);
                if (!effectResult.IsSuccess) return Result<RuntimeContentDefinition>.Failure(effectResult.Error);
                runtimeEffects[index] = effectResult.Value;
            }

            var sourcePatches = levelPatches ?? Array.Empty<SkillLevelPatchDto>();
            var runtimePatches = new SkillLevelPatch[sourcePatches.Length];
            for (var index = 0; index < sourcePatches.Length; index++)
            {
                if (sourcePatches[index] == null)
                {
                    return Result<RuntimeContentDefinition>.Failure(
                        new Error(
                            ErrorCode.InvalidCatalog,
                            "Schema 3 skill contains a null level patch.",
                            ownerId,
                            packId,
                            sourceAssetPath));
                }

                var patchResult = sourcePatches[index].ToPatch(
                    runtimeEffects.Length,
                    packId,
                    ownerId,
                    sourceAssetPath);
                if (!patchResult.IsSuccess) return Result<RuntimeContentDefinition>.Failure(patchResult.Error);
                runtimePatches[index] = patchResult.Value;
            }

            return Result<RuntimeContentDefinition>.Success(
                new RuntimeSkillDefinition(
                    ownerId,
                    localizedNameKey,
                    localizedDescriptionKey,
                    sourceAssetPath,
                    runtimeTags,
                    cooldownSeconds,
                    resourceCost,
                    triggerResult.Value,
                    conditionResult.Value,
                    targetingResult.Value,
                    deliveryResult.Value,
                    runtimeEffects,
                    runtimePatches));
        }

        private Result<RuntimeStatusBehavior> ToStatusBehavior(
            ContentId packId,
            ContentId ownerId)
        {
            var modifier = default(RuntimeStatusModifier);
            if (statusModifierEnabled)
            {
                if (!ContentId.IsCanonical(statusModifierStatId))
                {
                    return StatusBehaviorFailure(
                        "Serialized status modifier StatId must be lowercase canonical text.",
                        packId,
                        ownerId);
                }

                var statResult = StatId.Create(
                    statusModifierStatId,
                    packId,
                    sourceAssetPath);
                if (!statResult.IsSuccess)
                {
                    return Result<RuntimeStatusBehavior>.Failure(statResult.Error);
                }

                if (!ModifierOperationCodec.TryParse(
                        statusModifierOperation,
                        out var operation))
                {
                    return StatusBehaviorFailure(
                        "Unsupported serialized status modifier operation '" +
                        (statusModifierOperation ?? string.Empty) + "'.",
                        packId,
                        ownerId);
                }

                var stackingGroup = default(ContentId);
                if (!string.IsNullOrEmpty(statusModifierStackingGroup))
                {
                    var groupResult = CatalogDtoParsing.ParseCanonicalId(
                        statusModifierStackingGroup,
                        packId,
                        sourceAssetPath,
                        "status modifier stacking-group ID");
                    if (!groupResult.IsSuccess)
                    {
                        return Result<RuntimeStatusBehavior>.Failure(groupResult.Error);
                    }

                    stackingGroup = groupResult.Value;
                }

                modifier = new RuntimeStatusModifier(
                    statResult.Value,
                    operation,
                    statusModifierValue,
                    statusModifierPriority,
                    stackingGroup);
            }

            var periodic = default(RuntimeStatusPeriodicDamage);
            if (periodicDamageEnabled)
            {
                if (!DamageTypeCodec.TryParse(
                        periodicDamageType,
                        out var damageType))
                {
                    return StatusBehaviorFailure(
                        "Unsupported serialized periodic damage type '" +
                        (periodicDamageType ?? string.Empty) + "'.",
                        packId,
                        ownerId);
                }

                periodic = new RuntimeStatusPeriodicDamage(
                    damageType,
                    (DamageTags)periodicDamageTags,
                    periodicDamageValue,
                    periodicCanCritical,
                    periodicProcCoefficient,
                    new System.Numerics.Vector2(
                        periodicKnockbackX,
                        periodicKnockbackY));
            }

            return Result<RuntimeStatusBehavior>.Success(
                new RuntimeStatusBehavior(modifier, periodic, shieldCapacity));
        }

        private Result<RuntimeStatusBehavior> StatusBehaviorFailure(
            string message,
            ContentId packId,
            ContentId ownerId)
        {
            return Result<RuntimeStatusBehavior>.Failure(
                new Error(
                    ErrorCode.InvalidCatalog,
                    message,
                    ownerId,
                    packId,
                    sourceAssetPath));
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
