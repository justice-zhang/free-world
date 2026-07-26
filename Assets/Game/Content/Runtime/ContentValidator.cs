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
                                    "Schema 3 skills must contain modular runtime data.",
                                    definition.Id,
                                    packId,
                                    definition.SourceAssetPath));
                        }
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
            else if (definition is RuntimeMapDefinition map &&
                     (string.IsNullOrWhiteSpace(map.RuntimeProviderId) ||
                      string.IsNullOrWhiteSpace(map.SceneAddress)))
            {
                message = "Map runtime provider ID and scene address are required.";
            }
            else if (definition is RuntimeStatusDefinition status)
            {
                message = ValidateStatusDefinition(status);
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

            result = new SkillModuleDefinition(
                source.ModuleId,
                value0,
                value1,
                value2,
                value3,
                int0,
                source.Int1,
                source.PresentationId);
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

                var validType = effect.Code == EffectOpCode.ApplyStatus
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
            }
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
