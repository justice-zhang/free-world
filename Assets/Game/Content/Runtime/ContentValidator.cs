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
            var definitionsById = new Dictionary<ContentId, RuntimeContentDefinition>();
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
                        definitionsById.Add(definition.Id, definition);
                    }

                    if (definition is RuntimeStatusDefinition &&
                        catalog.Manifest.SchemaVersion <
                        ContentPackTopology.StatusDefinitionSchemaVersion)
                    {
                        report.Add(
                            new Error(
                                ErrorCode.UnsupportedSchemaVersion,
                                "Status definitions require content schema " +
                                ContentPackTopology.StatusDefinitionSchemaVersion +
                                " or newer.",
                                definition.Id,
                                packId,
                            definition.SourceAssetPath));
                    }

                    if (definition is RuntimeSkillDefinition schemaSkill)
                    {
                        if (schemaSkill.IsExecutable &&
                            catalog.Manifest.SchemaVersion <
                            ContentPackTopology.ModularSkillSchemaVersion)
                        {
                            report.Add(
                                new Error(
                                    ErrorCode.UnsupportedSchemaVersion,
                                    "Executable modular skills require content schema " +
                                    ContentPackTopology.ModularSkillSchemaVersion + " or newer.",
                                    definition.Id,
                                    packId,
                                    definition.SourceAssetPath));
                        }
                        else if (!schemaSkill.IsExecutable &&
                                 catalog.Manifest.SchemaVersion >=
                                 ContentPackTopology.ModularSkillSchemaVersion)
                        {
                            report.Add(
                                new Error(
                                    ErrorCode.InvalidAuthoringData,
                                    "Skills in schema 3 or newer packs must contain modular runtime data.",
                                    definition.Id,
                                    packId,
                                    definition.SourceAssetPath));
                        }
                    }

                    if (definition is RuntimeEnemyDefinition schemaEnemy)
                    {
                        ValidateM5SchemaState(
                            schemaEnemy.HasM5Data,
                            "Enemy",
                            catalog.Manifest.SchemaVersion,
                            definition,
                            packId,
                            report);
                    }

                    if (definition is RuntimeMapDefinition schemaMap)
                    {
                        ValidateM5SchemaState(
                            schemaMap.HasM5Data,
                            "Map",
                            catalog.Manifest.SchemaVersion,
                            definition,
                            packId,
                            report);
                    }

                    if (definition is RuntimeEncounterSchedule &&
                        catalog.Manifest.SchemaVersion <
                        ContentPackTopology.EnemyMapEncounterSchemaVersion)
                    {
                        report.Add(
                            new Error(
                                ErrorCode.UnsupportedSchemaVersion,
                                "Encounter schedules require content schema " +
                                ContentPackTopology.EnemyMapEncounterSchemaVersion + " or newer.",
                                definition.Id,
                                packId,
                                definition.SourceAssetPath));
                    }

                    if (M6ContentValidation.IsBuildProgressionDefinition(definition) &&
                        catalog.Manifest.SchemaVersion <
                        ContentPackTopology.BuildProgressionSchemaVersion)
                    {
                        report.Add(
                            new Error(
                                ErrorCode.UnsupportedSchemaVersion,
                                "Build/progression definitions require content schema " +
                                ContentPackTopology.BuildProgressionSchemaVersion + " or newer.",
                                definition.Id,
                                packId,
                                definition.SourceAssetPath));
                    }

                    if (QinglanContentValidation.IsDefinition(definition) &&
                        catalog.Manifest.SchemaVersion < ContentPackTopology.QinglanDemoSchemaVersion)
                    {
                        report.Add(
                            new Error(
                                ErrorCode.UnsupportedSchemaVersion,
                                "Qinglan definitions require content schema " +
                                ContentPackTopology.QinglanDemoSchemaVersion + " or newer.",
                                definition.Id,
                                packId,
                                definition.SourceAssetPath));
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

                    if (definition is RuntimeSkillDefinition skill && skill.IsExecutable)
                    {
                        ValidateSkillReferenceTypes(
                            skill,
                            definitionsById,
                            packId,
                            report);
                    }

                    if (definition is RuntimeCharacterDefinition character)
                    {
                        for (var mechanicIndex = 0; mechanicIndex < character.MechanicIds.Count; mechanicIndex++)
                        {
                            ValidateReferenceType(
                                character,
                                character.MechanicIds[mechanicIndex],
                                definitionsById,
                                packId,
                                report,
                                referenced => referenced is RuntimeCharacterMechanicDefinition,
                                "a CharacterMechanic");
                        }
                    }

                    if (definition is RuntimeEnemyDefinition enemy && enemy.HasM5Data)
                    {
                        ValidateReferenceType(
                            enemy,
                            enemy.AttackSkillId,
                            definitionsById,
                            packId,
                            report,
                            referenced => referenced is RuntimeSkillDefinition referencedSkill &&
                                          referencedSkill.IsExecutable,
                            "an executable Skill");
                    }

                    if (definition is RuntimeMapDefinition map && map.HasM5Data)
                    {
                        ValidateReferenceType(
                            map,
                            map.EncounterScheduleId,
                            definitionsById,
                            packId,
                            report,
                            referenced => referenced is RuntimeEncounterSchedule,
                            "an Encounter schedule");
                        ValidateReferenceList(map, map.ObjectiveIds, definitionsById, packId, report,
                            referenced => referenced is RuntimeMapObjectiveDefinition, "a MapObjective");
                        ValidateReferenceList(map, map.EventIds, definitionsById, packId, report,
                            referenced => referenced is RuntimeMapEventDefinition, "a MapEvent");
                        ValidateReferenceList(map, map.LandmarkIds, definitionsById, packId, report,
                            referenced => referenced is RuntimeLandmarkDefinition, "a Landmark");
                        ValidateMapOwnedAnchors(map, definitionsById, packId, report);
                    }

                    if (definition is RuntimeEncounterSchedule encounter)
                    {
                        for (var phaseIndex = 0; phaseIndex < encounter.Phases.Count; phaseIndex++)
                        {
                            var phase = encounter.Phases[phaseIndex];
                            for (var entryIndex = 0; entryIndex < phase.EnemyEntries.Count; entryIndex++)
                            {
                                var entry = phase.EnemyEntries[entryIndex];
                                ValidateReferenceType(
                                    encounter,
                                    entry.EnemyId,
                                    definitionsById,
                                    packId,
                                    report,
                                    referenced => referenced is RuntimeEnemyDefinition referencedEnemy &&
                                                  referencedEnemy.HasM5Data,
                                    "a schema-4 Enemy");
                                ValidateReferenceList(
                                    encounter,
                                    entry.AffixPoolIds,
                                    definitionsById,
                                    packId,
                                    report,
                                    referenced => referenced is RuntimeEliteAffixDefinition,
                                    "an EliteAffix");
                            }

                            for (var eliteIndex = 0; eliteIndex < phase.EliteRules.Count; eliteIndex++)
                            {
                                var eliteRule = phase.EliteRules[eliteIndex];
                                ValidateReferenceType(
                                    encounter,
                                    eliteRule.EnemyId,
                                    definitionsById,
                                    packId,
                                    report,
                                    referenced => referenced is RuntimeEnemyDefinition referencedEnemy &&
                                                  referencedEnemy.HasM5Data,
                                    "a schema-4 Enemy");
                                ValidateReferenceList(
                                    encounter,
                                    eliteRule.AffixPoolIds,
                                    definitionsById,
                                    packId,
                                    report,
                                    referenced => referenced is RuntimeEliteAffixDefinition,
                                    "an EliteAffix");
                            }

                            for (var bossIndex = 0; bossIndex < phase.BossRules.Count; bossIndex++)
                            {
                                var bossRule = phase.BossRules[bossIndex];
                                ValidateReferenceType(
                                    encounter,
                                    bossRule.EnemyId,
                                    definitionsById,
                                    packId,
                                    report,
                                    referenced => referenced is RuntimeEnemyDefinition referencedEnemy &&
                                                  referencedEnemy.HasM5Data,
                                    "a schema-4 Enemy");
                                if (bossRule.BossDefinitionId.IsValid)
                                {
                                    ValidateReferenceType(
                                        encounter,
                                        bossRule.BossDefinitionId,
                                        definitionsById,
                                        packId,
                                        report,
                                        referenced => referenced is RuntimeBossDefinition,
                                        "a Boss definition");
                                }
                            }
                        }
                    }


                    M6ContentValidation.ValidateReferenceTypes(
                        definition,
                        definitionsById,
                        packId,
                        report);

                    QinglanContentValidation.ValidateReferenceTypes(
                        definition,
                        definitionsById,
                        packId,
                        report);
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
            else if (definition is RuntimeSkillDefinition skill)
            {
                message = ValidateSkillDefinition(skill);
            }
            else if (definition is RuntimeEnemyDefinition enemy &&
                     (enemy.BaseMaxHealth <= 0f || enemy.CollisionRadius <= 0f))
            {
                message = "Enemy health and collision radius must be positive.";
            }
            else if (definition is RuntimeEnemyDefinition runtimeEnemy && runtimeEnemy.HasM5Data)
            {
                message = ValidateEnemyDefinition(runtimeEnemy);
            }
            else if (definition is RuntimeMapDefinition map &&
                     (string.IsNullOrWhiteSpace(map.RuntimeProviderId) ||
                      string.IsNullOrWhiteSpace(map.SceneAddress)))
            {
                message = "Map runtime provider ID and scene address are required.";
            }
            else if (definition is RuntimeMapDefinition runtimeMap && runtimeMap.HasM5Data)
            {
                message = ValidateMapDefinition(runtimeMap);
            }
            else if (definition is RuntimeEncounterSchedule encounter)
            {
                message = ValidateEncounterDefinition(encounter);
            }
            else if (definition is RuntimeStatusDefinition status)
            {
                message = ValidateStatusDefinition(status);
            }
            else if (M6ContentValidation.IsBuildProgressionDefinition(definition))
            {
                message = M6ContentValidation.ValidateDefinitionValues(definition);
            }
            else if (QinglanContentValidation.IsDefinition(definition))
            {
                message = QinglanContentValidation.ValidateValues(definition);
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

        private static void ValidateM5SchemaState(
            bool hasM5Data,
            string kind,
            int schemaVersion,
            RuntimeContentDefinition definition,
            ContentId packId,
            ContentValidationReport report)
        {
            var required = ContentPackTopology.EnemyMapEncounterSchemaVersion;
            if (hasM5Data && schemaVersion < required)
            {
                report.Add(
                    new Error(
                        ErrorCode.UnsupportedSchemaVersion,
                        kind + " runtime data requires content schema " + required + " or newer.",
                        definition.Id,
                        packId,
                        definition.SourceAssetPath));
            }
            else if (!hasM5Data && schemaVersion >= required)
            {
                report.Add(
                    new Error(
                        ErrorCode.InvalidAuthoringData,
                        kind + " definitions in schema 4 packs require M5 runtime data.",
                        definition.Id,
                        packId,
                        definition.SourceAssetPath));
            }
        }

        private static string ValidateEnemyDefinition(RuntimeEnemyDefinition enemy)
        {
            var behavior = enemy.Behavior;
            if (!IsFinite(enemy.BaseMoveSpeed) || enemy.BaseMoveSpeed <= 0f ||
                !IsFinite(enemy.BaseDamage) || enemy.BaseDamage < 0f ||
                !IsFinite(enemy.AttackRange) || enemy.AttackRange <= 0f ||
                !enemy.AttackSkillId.IsValid || !enemy.VisualProfileId.IsValid ||
                !IsFinite(enemy.ExperienceReward) || enemy.ExperienceReward < 0f ||
                !IsFinite(enemy.LootReward) || enemy.LootReward < 0f ||
                behavior.MovementMode < EnemyMovementMode.Chase ||
                behavior.MovementMode > EnemyMovementMode.Ranged ||
                !IsFinite(behavior.PreferredDistance) || behavior.PreferredDistance < 0f ||
                !IsFinite(behavior.DecisionIntervalSeconds) || behavior.DecisionIntervalSeconds <= 0f ||
                !IsFinite(behavior.ChargeWindupSeconds) || behavior.ChargeWindupSeconds < 0f ||
                !IsFinite(behavior.ChargeDurationSeconds) || behavior.ChargeDurationSeconds < 0f ||
                !IsFinite(behavior.ChargeSpeedMultiplier) || behavior.ChargeSpeedMultiplier <= 0f ||
                !IsFinite(behavior.AttackCooldownSeconds) || behavior.AttackCooldownSeconds < 0f ||
                !IsFinite(behavior.SeparationRadius) || behavior.SeparationRadius < 0f ||
                !IsFinite(behavior.SeparationWeight) || behavior.SeparationWeight < 0f ||
                !IsFinite(behavior.ObstacleAvoidanceWeight) || behavior.ObstacleAvoidanceWeight < 0f)
            {
                return "Schema-4 enemy combat, reward, or behavior data is invalid.";
            }

            return null;
        }

        private static string ValidateMapDefinition(RuntimeMapDefinition map)
        {
            if (map.BoundsMode < MapBoundsMode.Finite ||
                map.BoundsMode > MapBoundsMode.ChunkedInfinite ||
                !IsFinite(map.Minimum.X) || !IsFinite(map.Minimum.Y) ||
                !IsFinite(map.Maximum.X) || !IsFinite(map.Maximum.Y) ||
                map.Minimum.X >= map.Maximum.X || map.Minimum.Y >= map.Maximum.Y ||
                !IsFinite(map.ChunkSize) || map.ChunkSize <= 0f ||
                map.ActiveChunkRadius < 1 || !map.EncounterScheduleId.IsValid ||
                !map.VisualProfileId.IsValid)
            {
                return "Schema-4 map bounds, chunk, encounter, or visual data is invalid.";
            }

            for (var index = 0; index < map.Obstacles.Count; index++)
            {
                var obstacle = map.Obstacles[index];
                if (!IsFinite(obstacle.Minimum.X) || !IsFinite(obstacle.Minimum.Y) ||
                    !IsFinite(obstacle.Maximum.X) || !IsFinite(obstacle.Maximum.Y) ||
                    obstacle.Minimum.X >= obstacle.Maximum.X ||
                    obstacle.Minimum.Y >= obstacle.Maximum.Y)
                {
                    return "Schema-4 map contains invalid obstacle bounds.";
                }
            }

            var anchors = new HashSet<ContentId>();
            for (var index = 0; index < map.Anchors.Count; index++)
            {
                var anchor = map.Anchors[index];
                if (!anchor.Id.IsValid || !IsFinite(anchor.Position.X) ||
                    !IsFinite(anchor.Position.Y) || !anchors.Add(anchor.Id))
                {
                    return "Schema-4 map anchors must be finite, valid, and unique.";
                }
            }

            return null;
        }

        private static string ValidateEncounterDefinition(RuntimeEncounterSchedule encounter)
        {
            if (encounter.MaximumConcurrentEnemies <= 0 ||
                !IsFinite(encounter.MinimumSpawnDistance) || encounter.MinimumSpawnDistance < 0f ||
                !IsFinite(encounter.MaximumSpawnDistance) ||
                encounter.MaximumSpawnDistance < encounter.MinimumSpawnDistance ||
                encounter.Phases.Count == 0)
            {
                return "Encounter limits, spawn distances, or phases are invalid.";
            }

            var expectedStart = 0f;
            for (var phaseIndex = 0; phaseIndex < encounter.Phases.Count; phaseIndex++)
            {
                var phase = encounter.Phases[phaseIndex];
                if (phase == null || Math.Abs(phase.StartTimeSeconds - expectedStart) > 0.0001f ||
                    !IsFinite(phase.EndTimeSeconds) || phase.EndTimeSeconds <= phase.StartTimeSeconds ||
                    !IsFinite(phase.BudgetPerSecondStart) || phase.BudgetPerSecondStart < 0f ||
                    !IsFinite(phase.BudgetPerSecondEnd) || phase.BudgetPerSecondEnd < 0f ||
                    !IsFinite(phase.SpawnIntervalStart) || phase.SpawnIntervalStart <= 0f ||
                    !IsFinite(phase.SpawnIntervalEnd) || phase.SpawnIntervalEnd <= 0f ||
                    phase.MaximumConcurrentEnemies <= 0 ||
                    phase.MaximumConcurrentEnemies > encounter.MaximumConcurrentEnemies ||
                    phase.SpawnPattern < SpawnPattern.Ring || phase.SpawnPattern > SpawnPattern.OffscreenRandom ||
                    RequiresAnchor(phase.SpawnPattern) && !phase.AnchorId.IsValid ||
                    phase.EnemyEntries.Count == 0)
                {
                    return "Encounter phases must be contiguous and contain valid curves, limits, patterns, and entries.";
                }

                for (var entryIndex = 0; entryIndex < phase.EnemyEntries.Count; entryIndex++)
                {
                    var entry = phase.EnemyEntries[entryIndex];
                    if (!entry.EnemyId.IsValid || !IsFinite(entry.Weight) || entry.Weight <= 0f ||
                        !IsFinite(entry.BudgetCost) || entry.BudgetCost <= 0f ||
                        entry.MinimumGroupSize <= 0 || entry.MaximumGroupSize < entry.MinimumGroupSize)
                    {
                        return "Encounter enemy entry weight, cost, or group size is invalid.";
                    }
                }

                for (var eliteIndex = 0; eliteIndex < phase.EliteRules.Count; eliteIndex++)
                {
                    var elite = phase.EliteRules[eliteIndex];
                    if (!elite.EnemyId.IsValid || !IsFinite(elite.SpawnTimeSeconds) ||
                        elite.SpawnTimeSeconds < phase.StartTimeSeconds ||
                        elite.SpawnTimeSeconds >= phase.EndTimeSeconds ||
                        elite.Pattern < SpawnPattern.Ring || elite.Pattern > SpawnPattern.OffscreenRandom ||
                        RequiresAnchor(elite.Pattern) && !elite.AnchorId.IsValid ||
                        elite.AffixPoolIds.Count == 0)
                    {
                        return "Encounter elite rules contain invalid time, pattern, anchor, or affix pool data.";
                    }
                }

                for (var bossIndex = 0; bossIndex < phase.BossRules.Count; bossIndex++)
                {
                    var boss = phase.BossRules[bossIndex];
                    if (!boss.EnemyId.IsValid || !IsFinite(boss.SpawnTimeSeconds) ||
                        boss.SpawnTimeSeconds < phase.StartTimeSeconds ||
                        boss.SpawnTimeSeconds >= phase.EndTimeSeconds ||
                        boss.Pattern < SpawnPattern.Ring || boss.Pattern > SpawnPattern.OffscreenRandom ||
                        RequiresAnchor(boss.Pattern) && !boss.AnchorId.IsValid)
                    {
                        return "Encounter boss rules contain invalid time, pattern, or anchor data.";
                    }
                }

                expectedStart = phase.EndTimeSeconds;
            }

            return null;
        }

        private static bool RequiresAnchor(SpawnPattern pattern)
        {
            return pattern == SpawnPattern.FixedAnchor || pattern == SpawnPattern.Portal;
        }

        private static string ValidateSkillDefinition(RuntimeSkillDefinition skill)
        {
            if (!IsFinite(skill.CooldownSeconds) || skill.CooldownSeconds < 0f)
            {
                return "Skill cooldown must be finite and non-negative.";
            }

            if (!skill.IsExecutable)
            {
                return null;
            }

            if (!IsFinite(skill.ResourceCost) || skill.ResourceCost < 0f)
            {
                return "Skill resource cost must be finite and non-negative.";
            }

            if (!SkillModuleIds.IsTrigger(skill.Trigger.ModuleId))
            {
                return "Skill trigger module ID is not explicitly registered.";
            }

            if (!SkillModuleIds.IsCondition(skill.Condition.ModuleId))
            {
                return "Skill condition module ID is not explicitly registered.";
            }

            if (!SkillModuleIds.IsTargeting(skill.Targeting.ModuleId))
            {
                return "Skill targeting module ID is not explicitly registered.";
            }

            if (!SkillModuleIds.IsDelivery(skill.Delivery.ModuleId))
            {
                return "Skill delivery module ID is not explicitly registered.";
            }

            if (!ValidateModuleNumbers(skill.Trigger) ||
                !ValidateModuleNumbers(skill.Condition) ||
                !ValidateModuleNumbers(skill.Targeting) ||
                !ValidateModuleNumbers(skill.Delivery))
            {
                return "Skill module numeric parameters must be finite.";
            }

            var moduleMessage = ValidateSchema6SkillModules(skill);
            if (moduleMessage != null)
            {
                return moduleMessage;
            }

            if (skill.Delivery.ModuleId != SkillModuleIds.DeliveryInstant &&
                !skill.Delivery.PresentationId.IsValid)
            {
                return "Non-instant delivery requires a stable presentation ID.";
            }

            if (skill.Effects.Count == 0)
            {
                return "Executable skill requires at least one effect operation.";
            }

            for (var index = 0; index < skill.Effects.Count; index++)
            {
                var effect = skill.Effects[index];
                var effectMessage = ValidateEffect(effect);
                if (effectMessage != null)
                {
                    return effectMessage;
                }
            }

            var previousLevel = 1;
            for (var index = 0; index < skill.LevelPatches.Count; index++)
            {
                var patch = skill.LevelPatches[index];
                var canonicalPath = SkillLevelPatchPath.GetPath(
                    patch.Target,
                    patch.TargetIndex);
                var pathIsConsistent = SkillLevelPatchPath.TryResolve(
                    canonicalPath,
                    skill.Effects.Count,
                    out var resolvedTarget,
                    out var resolvedIndex,
                    out var resolvedType) &&
                    resolvedTarget == patch.Target &&
                    resolvedIndex == patch.TargetIndex &&
                    resolvedType == patch.ValueType;
                if (patch.Level < 2 || patch.Level < previousLevel ||
                    patch.Level > previousLevel + 1 ||
                    patch.Operation < SkillPatchOperation.Add ||
                    patch.Operation > SkillPatchOperation.Override ||
                    (patch.ValueType == SkillPatchValueType.Float && !IsFinite(patch.FloatValue)) ||
                    (patch.ValueType != SkillPatchValueType.Float &&
                     patch.ValueType != SkillPatchValueType.Integer) ||
                    ((patch.Target >= SkillPatchTarget.EffectValue0 &&
                      patch.Target <= SkillPatchTarget.EffectInt1) &&
                     (patch.TargetIndex < 0 || patch.TargetIndex >= skill.Effects.Count)) ||
                    !pathIsConsistent)
                {
                    return "Skill contains an invalid, discontinuous, or type-unsafe LevelPatch.";
                }

                previousLevel = patch.Level;
            }

            var levelPatchMessage = ValidateLevelPatchResults(skill);
            if (levelPatchMessage != null)
            {
                return levelPatchMessage;
            }

            return null;
        }

        private static string ValidateEffect(in EffectOp effect)
        {
            if (!SkillModuleIds.GetEffectId(effect.Code).IsValid ||
                !IsFinite(effect.Value0) ||
                !IsFinite(effect.Value1) ||
                !IsFinite(effect.Value2))
            {
                return "Skill contains an invalid effect operation.";
            }

            switch (effect.Code)
            {
                case EffectOpCode.Damage:
                    if (effect.Value0 < 0f || effect.Value1 < 0f || effect.Value1 > 1f ||
                        effect.Int0 < (int)DamageType.Physical ||
                        effect.Int0 > (int)DamageType.True ||
                        (effect.Flags & ~EffectOpFlags.CanCritical) != 0)
                    {
                        return "Skill damage effect operands are invalid.";
                    }
                    break;
                case EffectOpCode.Heal:
                case EffectOpCode.Knockback:
                case EffectOpCode.Pull:
                case EffectOpCode.GrantShield:
                case EffectOpCode.GainResource:
                    if (effect.Value0 < 0f)
                    {
                        return "Skill effect value cannot be negative.";
                    }
                    break;
                case EffectOpCode.ApplyStatus:
                case EffectOpCode.SpawnSecondarySkill:
                    if (!effect.ReferenceId0.IsValid)
                    {
                        return "Skill effect is missing its required content reference.";
                    }
                    break;
                case EffectOpCode.ConsumeStatus:
                    if ((!effect.ReferenceId0.IsValid && !effect.Tag0.IsValid) ||
                        effect.Int0 < 1 ||
                        effect.Int1 < (int)StatusConsumeMissingPolicy.RequireExact ||
                        effect.Int1 > (int)StatusConsumeMissingPolicy.ConsumeAvailable)
                    {
                        return "ConsumeStatus effect operands are invalid.";
                    }
                    break;
                case EffectOpCode.DetonateStatus:
                    if ((!effect.ReferenceId0.IsValid && !effect.Tag0.IsValid) ||
                        effect.Value0 < 0f ||
                        effect.Int0 < 1)
                    {
                        return "DetonateStatus effect operands are invalid.";
                    }
                    break;
                case EffectOpCode.RemoveStatus:
                    if (!effect.Tag0.IsValid)
                    {
                        return "RemoveStatus effect requires a canonical tag.";
                    }
                    break;
                case EffectOpCode.ModifyStat:
                    if (!effect.StatId0.IsValid ||
                        effect.Int0 < (int)ModifierOperation.AddFlat ||
                        effect.Int0 > (int)ModifierOperation.Override ||
                        effect.Value1 < 0f)
                    {
                        return "ModifyStat effect operands are invalid.";
                    }
                    break;
            }

            return null;
        }

        private static void ValidateMapOwnedAnchors(
            RuntimeMapDefinition map,
            IReadOnlyDictionary<ContentId, RuntimeContentDefinition> definitionsById,
            ContentId packId,
            ContentValidationReport report)
        {
            if (map.ObjectiveIds.Count > 32 || map.EventIds.Count > 16 || map.LandmarkIds.Count > 32)
            {
                AddMapAuthoringError(
                    map,
                    packId,
                    report,
                    "Map exceeds the runtime capacity of 32 objectives, 16 events, or 32 landmarks.");
            }

            var walkableAnchors = new HashSet<ContentId>();
            for (var index = 0; index < map.Anchors.Count; index++)
            {
                var anchor = map.Anchors[index];
                if (IsMapPositionWalkable(map, anchor.Position)) walkableAnchors.Add(anchor.Id);
                else
                {
                    AddMapAuthoringError(
                        map,
                        packId,
                        report,
                        "Map anchor '" + anchor.Id + "' must be inside finite bounds and outside obstacles.");
                }
            }

            for (var index = 0; index < map.ObjectiveIds.Count; index++)
            {
                if (!definitionsById.TryGetValue(map.ObjectiveIds[index], out var definition) ||
                    !(definition is RuntimeMapObjectiveDefinition objective))
                    continue;
                ValidateOwnedAnchorList(map, objective.Id, objective.AnchorIds, walkableAnchors, packId, report);
            }

            for (var index = 0; index < map.EventIds.Count; index++)
            {
                if (!definitionsById.TryGetValue(map.EventIds[index], out var definition) ||
                    !(definition is RuntimeMapEventDefinition mapEvent))
                    continue;
                ValidateOwnedAnchorList(map, mapEvent.Id, mapEvent.AnchorIds, walkableAnchors, packId, report);
                if (definitionsById.TryGetValue(mapEvent.OutputId, out var output) &&
                    output is RuntimeMapObjectiveDefinition &&
                    !ContainsMapContentId(map.ObjectiveIds, mapEvent.OutputId))
                {
                    AddMapAuthoringError(
                        map,
                        packId,
                        report,
                        "Map event '" + mapEvent.Id + "' outputs objective '" + mapEvent.OutputId +
                        "' that is not owned by the same map.");
                }
            }

            for (var index = 0; index < map.LandmarkIds.Count; index++)
            {
                if (!definitionsById.TryGetValue(map.LandmarkIds[index], out var definition) ||
                    !(definition is RuntimeLandmarkDefinition landmark))
                    continue;
                if (!walkableAnchors.Contains(landmark.AnchorId))
                {
                    AddMapAuthoringError(
                        map,
                        packId,
                        report,
                        "Map landmark '" + landmark.Id + "' references missing or non-walkable anchor '" +
                        landmark.AnchorId + "'.");
                }
            }
        }

        private static void ValidateOwnedAnchorList(
            RuntimeMapDefinition map,
            ContentId ownerId,
            IReadOnlyList<ContentId> anchorIds,
            HashSet<ContentId> walkableAnchors,
            ContentId packId,
            ContentValidationReport report)
        {
            for (var index = 0; index < anchorIds.Count; index++)
            {
                if (walkableAnchors.Contains(anchorIds[index])) continue;
                AddMapAuthoringError(
                    map,
                    packId,
                    report,
                    "Map content '" + ownerId + "' references missing or non-walkable anchor '" +
                    anchorIds[index] + "'.");
            }
        }

        private static bool IsMapPositionWalkable(RuntimeMapDefinition map, System.Numerics.Vector2 position)
        {
            if (map.BoundsMode == MapBoundsMode.Finite &&
                (position.X < map.Minimum.X || position.X > map.Maximum.X ||
                 position.Y < map.Minimum.Y || position.Y > map.Maximum.Y))
                return false;
            for (var index = 0; index < map.Obstacles.Count; index++)
            {
                var obstacle = map.Obstacles[index];
                if (position.X >= obstacle.Minimum.X && position.X <= obstacle.Maximum.X &&
                    position.Y >= obstacle.Minimum.Y && position.Y <= obstacle.Maximum.Y)
                    return false;
            }
            return true;
        }

        private static bool ContainsMapContentId(IReadOnlyList<ContentId> values, ContentId expected)
        {
            for (var index = 0; index < values.Count; index++)
                if (values[index] == expected) return true;
            return false;
        }

        private static void AddMapAuthoringError(
            RuntimeMapDefinition map,
            ContentId packId,
            ContentValidationReport report,
            string message)
        {
            report.Add(new Error(
                ErrorCode.InvalidAuthoringData,
                message,
                map.Id,
                packId,
                map.SourceAssetPath));
        }

        private static string ValidateSchema6SkillModules(RuntimeSkillDefinition skill)
        {
            var condition = skill.Condition;
            if (condition.ModuleId == SkillModuleIds.ConditionStatusCountAtLeast)
            {
                if ((!condition.ReferenceId0.IsValid && !condition.Tag0.IsValid) ||
                    condition.Int0 < 1 ||
                    condition.Int1 < (int)StatusQueryTarget.Owner ||
                    condition.Int1 > (int)StatusQueryTarget.Target)
                {
                    return "Status-count condition operands are invalid.";
                }
            }
            else if (condition.ModuleId == SkillModuleIds.ConditionTargetHasStatus)
            {
                if ((!condition.ReferenceId0.IsValid && !condition.Tag0.IsValid) ||
                    condition.Int0 < (int)StatusQueryTarget.Owner ||
                    condition.Int0 > (int)StatusQueryTarget.Target)
                {
                    return "Target-has-status condition operands are invalid.";
                }
            }

            var targeting = skill.Targeting;
            if (targeting.ModuleId == SkillModuleIds.TargetingTriggerPosition &&
                (targeting.Value0 < 0f || targeting.Int0 < 0))
            {
                return "Trigger-position targeting operands are invalid.";
            }

            var delivery = skill.Delivery;
            if (delivery.ModuleId == SkillModuleIds.DeliveryOutboundReturn &&
                (delivery.Value0 <= 0f || delivery.Value1 <= 0f ||
                 delivery.Value2 < 0f || delivery.Value3 <= 0f ||
                 delivery.Int0 < 1 || delivery.Int0 > 16))
            {
                return "Outbound-return delivery operands are invalid.";
            }

            return null;
        }

        private static string ValidateLevelPatchResults(RuntimeSkillDefinition skill)
        {
            if (skill.LevelPatches.Count == 0)
            {
                return null;
            }

            var cooldown = skill.CooldownSeconds;
            var resourceCost = skill.ResourceCost;
            var trigger = skill.Trigger;
            var targeting = skill.Targeting;
            var delivery = skill.Delivery;
            var effects = new EffectOp[skill.Effects.Count];
            for (var index = 0; index < effects.Length; index++)
            {
                effects[index] = skill.Effects[index];
            }

            var patchIndex = 0;
            while (patchIndex < skill.LevelPatches.Count)
            {
                var level = skill.LevelPatches[patchIndex].Level;
                while (patchIndex < skill.LevelPatches.Count &&
                       skill.LevelPatches[patchIndex].Level == level)
                {
                    if (!TryApplyPatch(
                            skill.LevelPatches[patchIndex++],
                            ref cooldown,
                            ref resourceCost,
                            ref trigger,
                            ref targeting,
                            ref delivery,
                            effects))
                    {
                        return "LevelPatch integer arithmetic overflowed its runtime slot.";
                    }
                }

                if (!IsFinite(cooldown) || cooldown < 0f ||
                    !IsFinite(resourceCost) || resourceCost < 0f ||
                    !ValidateModuleNumbers(trigger) ||
                    !ValidateModuleNumbers(targeting) ||
                    !ValidateModuleNumbers(delivery))
                {
                    return "LevelPatch produced invalid runtime numeric values.";
                }

                for (var effectIndex = 0; effectIndex < effects.Length; effectIndex++)
                {
                    if (ValidateEffect(effects[effectIndex]) != null)
                    {
                        return "LevelPatch produced invalid effect operands.";
                    }
                }
            }

            return null;
        }

        private static bool TryApplyPatch(
            in SkillLevelPatch patch,
            ref float cooldown,
            ref float resourceCost,
            ref SkillModuleDefinition trigger,
            ref SkillModuleDefinition targeting,
            ref SkillModuleDefinition delivery,
            EffectOp[] effects)
        {
            if (patch.Target == SkillPatchTarget.Cooldown)
            {
                cooldown = Patch(cooldown, patch.Operation, patch.FloatValue);
                return true;
            }

            if (patch.Target == SkillPatchTarget.ResourceCost)
            {
                resourceCost = Patch(resourceCost, patch.Operation, patch.FloatValue);
                return true;
            }

            if (patch.Target >= SkillPatchTarget.TriggerValue0 &&
                patch.Target <= SkillPatchTarget.TriggerInt0)
            {
                if (!TryPatchModule(trigger, patch, out var patchedTrigger)) return false;
                trigger = patchedTrigger;
                return true;
            }

            if (patch.Target >= SkillPatchTarget.TargetingValue0 &&
                patch.Target <= SkillPatchTarget.TargetingInt0)
            {
                if (!TryPatchModule(targeting, patch, out var patchedTargeting)) return false;
                targeting = patchedTargeting;
                return true;
            }

            if (patch.Target >= SkillPatchTarget.DeliveryValue0 &&
                patch.Target <= SkillPatchTarget.DeliveryInt0)
            {
                if (!TryPatchModule(delivery, patch, out var patchedDelivery)) return false;
                delivery = patchedDelivery;
                return true;
            }

            var source = effects[patch.TargetIndex];
            var value0 = source.Value0;
            var value1 = source.Value1;
            var value2 = source.Value2;
            var int0 = source.Int0;
            var int1 = source.Int1;
            switch (patch.Target)
            {
                case SkillPatchTarget.EffectValue0:
                    value0 = Patch(value0, patch.Operation, patch.FloatValue);
                    break;
                case SkillPatchTarget.EffectValue1:
                    value1 = Patch(value1, patch.Operation, patch.FloatValue);
                    break;
                case SkillPatchTarget.EffectValue2:
                    value2 = Patch(value2, patch.Operation, patch.FloatValue);
                    break;
                case SkillPatchTarget.EffectInt0:
                    if (!TryPatch(int0, patch.Operation, patch.IntegerValue, out int0)) return false;
                    break;
                case SkillPatchTarget.EffectInt1:
                    if (!TryPatch(int1, patch.Operation, patch.IntegerValue, out int1)) return false;
                    break;
            }

            effects[patch.TargetIndex] = new EffectOp(
                source.Code,
                value0,
                value1,
                value2,
                int0,
                int1,
                source.ReferenceId0,
                source.ReferenceId1,
                source.Tag0,
                source.StatId0,
                source.Flags,
                source.Reference0,
                source.Reference1);
            return true;
        }

        private static bool TryPatchModule(
            in SkillModuleDefinition source,
            in SkillLevelPatch patch,
            out SkillModuleDefinition result)
        {
            var value0 = source.Value0;
            var value1 = source.Value1;
            var value2 = source.Value2;
            var value3 = source.Value3;
            var int0 = source.Int0;
            switch (patch.Target)
            {
                case SkillPatchTarget.TriggerValue0:
                case SkillPatchTarget.TargetingValue0:
                case SkillPatchTarget.DeliveryValue0:
                    value0 = Patch(value0, patch.Operation, patch.FloatValue);
                    break;
                case SkillPatchTarget.TriggerValue1:
                case SkillPatchTarget.TargetingValue1:
                case SkillPatchTarget.DeliveryValue1:
                    value1 = Patch(value1, patch.Operation, patch.FloatValue);
                    break;
                case SkillPatchTarget.DeliveryValue2:
                    value2 = Patch(value2, patch.Operation, patch.FloatValue);
                    break;
                case SkillPatchTarget.DeliveryValue3:
                    value3 = Patch(value3, patch.Operation, patch.FloatValue);
                    break;
                case SkillPatchTarget.TriggerInt0:
                case SkillPatchTarget.TargetingInt0:
                case SkillPatchTarget.DeliveryInt0:
                    if (!TryPatch(int0, patch.Operation, patch.IntegerValue, out int0))
                    {
                        result = default;
                        return false;
                    }
                    break;
            }

            result = SkillModuleDefinition.CreateReferenced(
                source.ModuleId,
                value0,
                value1,
                value2,
                value3,
                int0,
                source.Int1,
                source.PresentationId,
                source.ReferenceId0,
                source.ReferenceId1,
                source.Tag0,
                source.Tag1,
                source.Reference0,
                source.Reference1);
            return true;
        }

        private static float Patch(
            float current,
            SkillPatchOperation operation,
            float operand)
        {
            if (operation == SkillPatchOperation.Add) return current + operand;
            return operation == SkillPatchOperation.Multiply ? current * operand : operand;
        }

        private static bool TryPatch(
            int current,
            SkillPatchOperation operation,
            int operand,
            out int result)
        {
            var candidate = operation == SkillPatchOperation.Add
                ? (long)current + operand
                : operation == SkillPatchOperation.Multiply
                    ? (long)current * operand
                    : operand;
            if (candidate < int.MinValue || candidate > int.MaxValue)
            {
                result = default;
                return false;
            }

            result = (int)candidate;
            return true;
        }

        private static void ValidateSkillReferenceTypes(
            RuntimeSkillDefinition skill,
            Dictionary<ContentId, RuntimeContentDefinition> definitionsById,
            ContentId packId,
            ContentValidationReport report)
        {
            for (var index = 0; index < skill.Effects.Count; index++)
            {
                var effect = skill.Effects[index];
                if (!effect.ReferenceId0.IsValid ||
                    !definitionsById.TryGetValue(effect.ReferenceId0, out var referenced))
                {
                    continue;
                }

                var validType = effect.Code == EffectOpCode.ApplyStatus ||
                                effect.Code == EffectOpCode.ConsumeStatus ||
                                effect.Code == EffectOpCode.DetonateStatus
                    ? referenced is RuntimeStatusDefinition
                    : effect.Code != EffectOpCode.SpawnSecondarySkill ||
                      referenced is RuntimeSkillDefinition referencedSkill &&
                      referencedSkill.IsExecutable;
                if (!validType)
                {
                    var requirement = effect.Code == EffectOpCode.SpawnSecondarySkill
                        ? "an executable Skill"
                        : "the required runtime definition type";
                    report.Add(
                        new Error(
                            ErrorCode.InvalidAuthoringData,
                            "Skill effect reference '" + effect.ReferenceId0 +
                            "' must reference " + requirement + " for " + effect.Code + ".",
                            skill.Id,
                            packId,
                            skill.SourceAssetPath));
                }

                if (effect.Code == EffectOpCode.SpawnSecondarySkill &&
                    effect.ReferenceId1.IsValid)
                {
                    ValidateReferenceType(
                        skill,
                        effect.ReferenceId1,
                        definitionsById,
                        packId,
                        report,
                        value => value is RuntimeSkillDefinition alternate && alternate.IsExecutable,
                        "an executable Skill");
                }
            }

            ValidateStatusModuleReference(
                skill,
                skill.Condition,
                definitionsById,
                packId,
                report);
            if (skill.Delivery.ModuleId == SkillModuleIds.DeliveryOutboundReturn)
            {
                ValidateReferenceType(
                    skill,
                    skill.Delivery.ReferenceId0,
                    definitionsById,
                    packId,
                    report,
                    value => value is RuntimeSkillDefinition secondary && secondary.IsExecutable,
                    "an executable Skill");
                ValidateReferenceType(
                    skill,
                    skill.Delivery.ReferenceId1,
                    definitionsById,
                    packId,
                    report,
                    value => value is RuntimeTraitDefinition,
                    "a Trait mechanic output");
            }
        }

        private static void ValidateStatusModuleReference(
            RuntimeSkillDefinition skill,
            in SkillModuleDefinition module,
            Dictionary<ContentId, RuntimeContentDefinition> definitionsById,
            ContentId packId,
            ContentValidationReport report)
        {
            if (module.ModuleId != SkillModuleIds.ConditionStatusCountAtLeast &&
                module.ModuleId != SkillModuleIds.ConditionTargetHasStatus)
            {
                return;
            }

            ValidateReferenceType(
                skill,
                module.ReferenceId0,
                definitionsById,
                packId,
                report,
                value => value is RuntimeStatusDefinition,
                "a Status");
        }

        private static void ValidateReferenceType(
            RuntimeContentDefinition owner,
            ContentId referenceId,
            Dictionary<ContentId, RuntimeContentDefinition> definitionsById,
            ContentId packId,
            ContentValidationReport report,
            Func<RuntimeContentDefinition, bool> predicate,
            string requirement)
        {
            if (!referenceId.IsValid ||
                !definitionsById.TryGetValue(referenceId, out var referenced))
            {
                return;
            }

            if (!predicate(referenced))
            {
                report.Add(
                    new Error(
                        ErrorCode.InvalidAuthoringData,
                        "Content reference '" + referenceId + "' must reference " + requirement + ".",
                        owner.Id,
                        packId,
                        owner.SourceAssetPath));
            }
        }

        private static void ValidateReferenceList(
            RuntimeContentDefinition owner,
            IReadOnlyList<ContentId> references,
            Dictionary<ContentId, RuntimeContentDefinition> definitionsById,
            ContentId packId,
            ContentValidationReport report,
            Func<RuntimeContentDefinition, bool> predicate,
            string requirement)
        {
            for (var index = 0; index < references.Count; index++)
                ValidateReferenceType(
                    owner,
                    references[index],
                    definitionsById,
                    packId,
                    report,
                    predicate,
                    requirement);
        }

        private static bool ValidateModuleNumbers(in SkillModuleDefinition module)
        {
            return IsFinite(module.Value0) &&
                   IsFinite(module.Value1) &&
                   IsFinite(module.Value2) &&
                   IsFinite(module.Value3);
        }

        private static string ValidateStatusDefinition(RuntimeStatusDefinition status)
        {
            if (status.StackingPolicy != StatusStackingPolicy.RefreshDuration &&
                status.StackingPolicy != StatusStackingPolicy.AddStacks &&
                status.StackingPolicy != StatusStackingPolicy.ReplaceIfStronger &&
                status.StackingPolicy != StatusStackingPolicy.IndependentInstances)
            {
                return "Status stacking policy is invalid.";
            }

            if (!IsFinite(status.DurationSeconds) || status.DurationSeconds <= 0f)
            {
                return "Status duration must be finite and positive.";
            }

            if (status.MaxStacks < 1)
            {
                return "Status max stacks must be at least one.";
            }

            if ((status.StackingPolicy == StatusStackingPolicy.RefreshDuration ||
                 status.StackingPolicy == StatusStackingPolicy.ReplaceIfStronger) &&
                status.MaxStacks != 1)
            {
                return "Refresh-duration and replace-if-stronger statuses must use one stack.";
            }

            if (!IsFinite(status.TickIntervalSeconds) ||
                status.TickIntervalSeconds < 0f)
            {
                return "Status tick interval must be finite and cannot be negative.";
            }

            var behavior = status.Behavior;
            var modifier = behavior.Modifier;
            if (modifier.Enabled &&
                (!modifier.StatId.IsValid ||
                 modifier.Operation < ModifierOperation.AddFlat ||
                 modifier.Operation > ModifierOperation.Override ||
                 !IsFinite(modifier.Value) ||
                 (!modifier.StackingGroup.IsValid &&
                  !string.IsNullOrEmpty(modifier.StackingGroup.Value))))
            {
                return "Status modifier behavior is invalid.";
            }

            var periodic = behavior.PeriodicDamage;
            const DamageTags knownDamageTags =
                DamageTags.Direct |
                DamageTags.DamageOverTime |
                DamageTags.Status |
                DamageTags.Secondary;
            if (periodic.Enabled &&
                (periodic.DamageType < DamageType.Physical ||
                 periodic.DamageType > DamageType.True ||
                 (periodic.Tags & ~knownDamageTags) != 0 ||
                 !IsFinite(periodic.BaseValue) ||
                 periodic.BaseValue < 0f ||
                 !IsFinite(periodic.ProcCoefficient) ||
                 periodic.ProcCoefficient < 0f ||
                 periodic.ProcCoefficient > 1f ||
                 !IsFinite(periodic.Knockback.X) ||
                 !IsFinite(periodic.Knockback.Y) ||
                 status.TickIntervalSeconds <= 0f))
            {
                return "Status periodic damage behavior is invalid.";
            }

            if (!IsFinite(behavior.ShieldCapacity) || behavior.ShieldCapacity < 0f)
            {
                return "Status shield capacity must be finite and non-negative.";
            }

            var seen = new HashSet<ContentTag>();
            for (var index = 0; index < status.DispelTags.Count; index++)
            {
                if (!status.DispelTags[index].IsValid)
                {
                    return "Status dispel tags must be valid canonical tags.";
                }

                if (!seen.Add(status.DispelTags[index]))
                {
                    return "Status dispel tags cannot contain duplicates.";
                }
            }

            seen.Clear();
            for (var index = 0; index < status.ImmunityTags.Count; index++)
            {
                if (!status.ImmunityTags[index].IsValid)
                {
                    return "Status immunity tags must be valid canonical tags.";
                }

                if (!seen.Add(status.ImmunityTags[index]))
                {
                    return "Status immunity tags cannot contain duplicates.";
                }
            }

            return null;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
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
