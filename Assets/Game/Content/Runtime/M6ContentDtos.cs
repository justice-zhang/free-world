using System;
using System.Collections.Generic;
using Game.Core;

namespace Game.Content.Runtime
{
    [Serializable]
    public sealed class M6BuildConditionDto
    {
        public string type;
        public string contentId;
        public string tag;
        public int integerValue;
        public string statId;
        public float floatValue;

        internal Result<BuildCondition> ToCondition(
            ContentId packId,
            ContentId ownerId,
            string sourcePath)
        {
            if (!BuildConditionTypeCodec.TryParse(type, out var conditionType))
                return Failure("Unsupported build condition type '" + (type ?? string.Empty) + "'.", packId, ownerId, sourcePath);

            var parsedContent = default(ContentId);
            if (!string.IsNullOrEmpty(contentId))
            {
                var result = CatalogDtoParsing.ParseCanonicalId(contentId, packId, sourcePath, "build-condition content ID");
                if (!result.IsSuccess) return Result<BuildCondition>.Failure(result.Error);
                parsedContent = result.Value;
            }

            var parsedTag = default(ContentTag);
            if (!string.IsNullOrEmpty(tag))
            {
                if (!ContentId.IsCanonical(tag))
                    return Failure("Build-condition tag must be canonical.", packId, ownerId, sourcePath);
                var result = ContentTag.Create(tag, packId, sourcePath);
                if (!result.IsSuccess) return Result<BuildCondition>.Failure(result.Error);
                parsedTag = result.Value;
            }

            var parsedStat = default(StatId);
            if (!string.IsNullOrEmpty(statId))
            {
                if (!ContentId.IsCanonical(statId))
                    return Failure("Build-condition StatId must be canonical.", packId, ownerId, sourcePath);
                var result = StatId.Create(statId, packId, sourcePath);
                if (!result.IsSuccess) return Result<BuildCondition>.Failure(result.Error);
                parsedStat = result.Value;
            }

            return Result<BuildCondition>.Success(
                new BuildCondition(
                    conditionType,
                    parsedContent,
                    parsedTag,
                    integerValue,
                    parsedStat,
                    floatValue));
        }

        internal static M6BuildConditionDto FromCondition(in BuildCondition condition)
        {
            return new M6BuildConditionDto
            {
                type = BuildConditionTypeCodec.ToSerializedValue(condition.Type),
                contentId = condition.ContentId.Value ?? string.Empty,
                tag = condition.Tag.Value ?? string.Empty,
                integerValue = condition.IntegerValue,
                statId = condition.StatId.Value ?? string.Empty,
                floatValue = condition.FloatValue
            };
        }

        private static Result<BuildCondition> Failure(
            string message,
            ContentId packId,
            ContentId ownerId,
            string sourcePath)
        {
            return Result<BuildCondition>.Failure(
                new Error(ErrorCode.InvalidCatalog, message, ownerId, packId, sourcePath));
        }
    }

    [Serializable]
    public sealed class M6BuildModifierDto
    {
        public string statId;
        public string operation;
        public float value;
        public int priority;
        public string stackingGroup;

        internal Result<RuntimeBuildModifier> ToModifier(
            ContentId packId,
            ContentId ownerId,
            string sourcePath)
        {
            if (!ContentId.IsCanonical(statId))
                return Failure("Build modifier StatId must be canonical.", packId, ownerId, sourcePath);
            var statResult = StatId.Create(statId, packId, sourcePath);
            if (!statResult.IsSuccess) return Result<RuntimeBuildModifier>.Failure(statResult.Error);
            if (!ModifierOperationCodec.TryParse(operation, out var modifierOperation))
                return Failure("Unsupported build modifier operation '" + (operation ?? string.Empty) + "'.", packId, ownerId, sourcePath);

            var group = default(ContentId);
            if (!string.IsNullOrEmpty(stackingGroup))
            {
                var groupResult = CatalogDtoParsing.ParseCanonicalId(stackingGroup, packId, sourcePath, "build modifier stacking group");
                if (!groupResult.IsSuccess) return Result<RuntimeBuildModifier>.Failure(groupResult.Error);
                group = groupResult.Value;
            }

            return Result<RuntimeBuildModifier>.Success(
                new RuntimeBuildModifier(statResult.Value, modifierOperation, value, priority, group));
        }

        internal static M6BuildModifierDto FromModifier(in RuntimeBuildModifier modifier)
        {
            return new M6BuildModifierDto
            {
                statId = modifier.StatId.Value,
                operation = ModifierOperationCodec.ToSerializedValue(modifier.Operation),
                value = modifier.Value,
                priority = modifier.Priority,
                stackingGroup = modifier.StackingGroup.IsValid ? modifier.StackingGroup.Value : string.Empty
            };
        }

        private static Result<RuntimeBuildModifier> Failure(
            string message,
            ContentId packId,
            ContentId ownerId,
            string sourcePath)
        {
            return Result<RuntimeBuildModifier>.Failure(
                new Error(ErrorCode.InvalidCatalog, message, ownerId, packId, sourcePath));
        }
    }

    [Serializable]
    public sealed class M6PassiveLevelModifierDto
    {
        public int level;
        public M6BuildModifierDto modifier;
    }

    [Serializable]
    public sealed class M6SynergyOutputDto
    {
        public string type;
        public M6BuildModifierDto modifier;
        public string sourceId;
        public string targetId;
        public SkillEffectOpDto effect;
    }

    /// <summary>Serialized payload shared by the five schema-5 definition kinds.</summary>
    [Serializable]
    public sealed class M6RuntimeDefinitionDto
    {
        public int maximumLevel;
        public M6PassiveLevelModifierDto[] passiveModifiers;
        public M6BuildModifierDto[] traitModifiers;
        public M6BuildConditionDto[] conditions;
        public M6SynergyOutputDto[] outputs;
        public string requiredSkillId;
        public int requiredSkillLevel;
        public string[] requiredPassiveIds;
        public string resultSkillId;
        public string consumePolicy;
        public string targetContentId;
        public float weight;
        public bool initiallyUnlocked;
        public string[] mutuallyExclusiveIds;

        internal Result<RuntimeContentDefinition> ToDefinition(
            string kind,
            ContentId packId,
            ContentId ownerId,
            string localizedNameKey,
            string localizedDescriptionKey,
            string sourcePath,
            ContentTag[] tags)
        {
            if (kind == RuntimeContentKinds.Passive)
            {
                var source = passiveModifiers ?? Array.Empty<M6PassiveLevelModifierDto>();
                var modifiers = new RuntimePassiveLevelModifier[source.Length];
                for (var index = 0; index < source.Length; index++)
                {
                    if (source[index] == null || source[index].modifier == null)
                        return Failure("Passive contains a null level modifier.", packId, ownerId, sourcePath);
                    var modifier = source[index].modifier.ToModifier(packId, ownerId, sourcePath);
                    if (!modifier.IsSuccess) return Result<RuntimeContentDefinition>.Failure(modifier.Error);
                    modifiers[index] = new RuntimePassiveLevelModifier(source[index].level, modifier.Value);
                }

                return Result<RuntimeContentDefinition>.Success(
                    new RuntimePassiveDefinition(ownerId, localizedNameKey, localizedDescriptionKey, sourcePath, tags, maximumLevel, modifiers));
            }

            if (kind == RuntimeContentKinds.Trait)
            {
                var modifiers = ParseModifiers(traitModifiers, packId, ownerId, sourcePath);
                return modifiers.IsSuccess
                    ? Result<RuntimeContentDefinition>.Success(
                        new RuntimeTraitDefinition(ownerId, localizedNameKey, localizedDescriptionKey, sourcePath, tags, modifiers.Value))
                    : Result<RuntimeContentDefinition>.Failure(modifiers.Error);
            }

            if (kind == RuntimeContentKinds.Synergy)
            {
                var parsedConditions = ParseConditions(conditions, packId, ownerId, sourcePath);
                if (!parsedConditions.IsSuccess) return Result<RuntimeContentDefinition>.Failure(parsedConditions.Error);
                var parsedOutputs = ParseOutputs(outputs, packId, ownerId, sourcePath);
                if (!parsedOutputs.IsSuccess) return Result<RuntimeContentDefinition>.Failure(parsedOutputs.Error);
                return Result<RuntimeContentDefinition>.Success(
                    new RuntimeSynergyDefinition(ownerId, localizedNameKey, localizedDescriptionKey, sourcePath, tags, parsedConditions.Value, parsedOutputs.Value));
            }

            if (kind == RuntimeContentKinds.Evolution)
            {
                var requiredSkill = ParseRequiredId(requiredSkillId, "required skill", packId, ownerId, sourcePath);
                if (!requiredSkill.IsSuccess) return Result<RuntimeContentDefinition>.Failure(requiredSkill.Error);
                var passives = CatalogDtoParsing.ParseIds(requiredPassiveIds, packId, ownerId, sourcePath);
                if (!passives.IsSuccess) return Result<RuntimeContentDefinition>.Failure(passives.Error);
                var parsedConditions = ParseConditions(conditions, packId, ownerId, sourcePath);
                if (!parsedConditions.IsSuccess) return Result<RuntimeContentDefinition>.Failure(parsedConditions.Error);
                var resultSkill = ParseRequiredId(resultSkillId, "result skill", packId, ownerId, sourcePath);
                if (!resultSkill.IsSuccess) return Result<RuntimeContentDefinition>.Failure(resultSkill.Error);
                if (!EvolutionConsumePolicyCodec.TryParse(consumePolicy, out var policy))
                    return Failure("Unsupported evolution consume policy '" + (consumePolicy ?? string.Empty) + "'.", packId, ownerId, sourcePath);
                return Result<RuntimeContentDefinition>.Success(
                    new RuntimeEvolutionDefinition(ownerId, localizedNameKey, localizedDescriptionKey, sourcePath, tags, requiredSkill.Value, requiredSkillLevel, passives.Value, parsedConditions.Value, resultSkill.Value, policy));
            }

            if (kind == RuntimeContentKinds.Offer)
            {
                var target = ParseRequiredId(targetContentId, "offer target", packId, ownerId, sourcePath);
                if (!target.IsSuccess) return Result<RuntimeContentDefinition>.Failure(target.Error);
                var parsedConditions = ParseConditions(conditions, packId, ownerId, sourcePath);
                if (!parsedConditions.IsSuccess) return Result<RuntimeContentDefinition>.Failure(parsedConditions.Error);
                var exclusions = CatalogDtoParsing.ParseIds(mutuallyExclusiveIds, packId, ownerId, sourcePath);
                if (!exclusions.IsSuccess) return Result<RuntimeContentDefinition>.Failure(exclusions.Error);
                return Result<RuntimeContentDefinition>.Success(
                    new RuntimeUpgradeOfferDefinition(ownerId, localizedNameKey, localizedDescriptionKey, sourcePath, tags, target.Value, weight, initiallyUnlocked, parsedConditions.Value, exclusions.Value));
            }

            return Failure("Unsupported schema-5 definition kind '" + (kind ?? string.Empty) + "'.", packId, ownerId, sourcePath);
        }

        internal static M6RuntimeDefinitionDto FromDefinition(RuntimeContentDefinition definition)
        {
            var dto = new M6RuntimeDefinitionDto
            {
                passiveModifiers = Array.Empty<M6PassiveLevelModifierDto>(),
                traitModifiers = Array.Empty<M6BuildModifierDto>(),
                conditions = Array.Empty<M6BuildConditionDto>(),
                outputs = Array.Empty<M6SynergyOutputDto>(),
                requiredSkillId = string.Empty,
                requiredPassiveIds = Array.Empty<string>(),
                resultSkillId = string.Empty,
                consumePolicy = string.Empty,
                targetContentId = string.Empty,
                mutuallyExclusiveIds = Array.Empty<string>()
            };

            if (definition is RuntimePassiveDefinition passive)
            {
                dto.maximumLevel = passive.MaximumLevel;
                dto.passiveModifiers = new M6PassiveLevelModifierDto[passive.LevelModifiers.Count];
                for (var index = 0; index < passive.LevelModifiers.Count; index++)
                {
                    var source = passive.LevelModifiers[index];
                    dto.passiveModifiers[index] = new M6PassiveLevelModifierDto
                    {
                        level = source.Level,
                        modifier = M6BuildModifierDto.FromModifier(source.Modifier)
                    };
                }
            }
            else if (definition is RuntimeTraitDefinition trait)
            {
                dto.traitModifiers = FromModifiers(trait.Modifiers);
            }
            else if (definition is RuntimeSynergyDefinition synergy)
            {
                dto.conditions = FromConditions(synergy.Conditions);
                dto.outputs = new M6SynergyOutputDto[synergy.Outputs.Count];
                for (var index = 0; index < synergy.Outputs.Count; index++)
                {
                    var output = synergy.Outputs[index];
                    dto.outputs[index] = new M6SynergyOutputDto
                    {
                        type = SynergyOutputTypeCodec.ToSerializedValue(output.Type),
                        modifier = output.Type == SynergyOutputType.AddModifier
                            ? M6BuildModifierDto.FromModifier(output.Modifier)
                            : null,
                        sourceId = output.SourceId.IsValid ? output.SourceId.Value : string.Empty,
                        targetId = output.TargetId.IsValid ? output.TargetId.Value : string.Empty,
                        effect = output.Type == SynergyOutputType.AddEffectOp
                            ? SkillEffectOpDto.FromEffect(output.Effect)
                            : null
                    };
                }
            }
            else if (definition is RuntimeEvolutionDefinition evolution)
            {
                dto.requiredSkillId = evolution.RequiredSkillId.Value;
                dto.requiredSkillLevel = evolution.RequiredSkillLevel;
                dto.requiredPassiveIds = new string[evolution.RequiredPassiveIds.Count];
                for (var index = 0; index < evolution.RequiredPassiveIds.Count; index++)
                    dto.requiredPassiveIds[index] = evolution.RequiredPassiveIds[index].Value;
                dto.conditions = FromConditions(evolution.AdditionalConditions);
                dto.resultSkillId = evolution.ResultSkillId.Value;
                dto.consumePolicy = EvolutionConsumePolicyCodec.ToSerializedValue(evolution.ConsumePolicy);
            }
            else if (definition is RuntimeUpgradeOfferDefinition offer)
            {
                dto.targetContentId = offer.TargetContentId.Value;
                dto.weight = offer.Weight;
                dto.initiallyUnlocked = offer.InitiallyUnlocked;
                dto.conditions = FromConditions(offer.Prerequisites);
                dto.mutuallyExclusiveIds = new string[offer.MutuallyExclusiveIds.Count];
                for (var index = 0; index < offer.MutuallyExclusiveIds.Count; index++)
                    dto.mutuallyExclusiveIds[index] = offer.MutuallyExclusiveIds[index].Value;
            }

            return dto;
        }

        private static Result<BuildCondition[]> ParseConditions(
            M6BuildConditionDto[] source,
            ContentId packId,
            ContentId ownerId,
            string sourcePath)
        {
            var input = source ?? Array.Empty<M6BuildConditionDto>();
            var output = new BuildCondition[input.Length];
            for (var index = 0; index < input.Length; index++)
            {
                if (input[index] == null)
                    return Result<BuildCondition[]>.Failure(FailureError("Definition contains a null condition.", packId, ownerId, sourcePath));
                var result = input[index].ToCondition(packId, ownerId, sourcePath);
                if (!result.IsSuccess) return Result<BuildCondition[]>.Failure(result.Error);
                output[index] = result.Value;
            }

            return Result<BuildCondition[]>.Success(output);
        }

        private static Result<RuntimeBuildModifier[]> ParseModifiers(
            M6BuildModifierDto[] source,
            ContentId packId,
            ContentId ownerId,
            string sourcePath)
        {
            var input = source ?? Array.Empty<M6BuildModifierDto>();
            var output = new RuntimeBuildModifier[input.Length];
            for (var index = 0; index < input.Length; index++)
            {
                if (input[index] == null)
                    return Result<RuntimeBuildModifier[]>.Failure(FailureError("Definition contains a null modifier.", packId, ownerId, sourcePath));
                var result = input[index].ToModifier(packId, ownerId, sourcePath);
                if (!result.IsSuccess) return Result<RuntimeBuildModifier[]>.Failure(result.Error);
                output[index] = result.Value;
            }

            return Result<RuntimeBuildModifier[]>.Success(output);
        }

        private static Result<RuntimeSynergyOutput[]> ParseOutputs(
            M6SynergyOutputDto[] source,
            ContentId packId,
            ContentId ownerId,
            string sourcePath)
        {
            var input = source ?? Array.Empty<M6SynergyOutputDto>();
            var output = new RuntimeSynergyOutput[input.Length];
            for (var index = 0; index < input.Length; index++)
            {
                var item = input[index];
                if (item == null || !SynergyOutputTypeCodec.TryParse(item.type, out var type))
                    return Result<RuntimeSynergyOutput[]>.Failure(FailureError("Synergy contains an invalid output type.", packId, ownerId, sourcePath));

                var modifier = default(RuntimeBuildModifier);
                if (type == SynergyOutputType.AddModifier)
                {
                    if (item.modifier == null)
                        return Result<RuntimeSynergyOutput[]>.Failure(FailureError("AddModifier output is missing modifier data.", packId, ownerId, sourcePath));
                    var result = item.modifier.ToModifier(packId, ownerId, sourcePath);
                    if (!result.IsSuccess) return Result<RuntimeSynergyOutput[]>.Failure(result.Error);
                    modifier = result.Value;
                }

                var sourceId = ParseOptionalId(item.sourceId, packId, ownerId, sourcePath);
                if (!sourceId.IsSuccess) return Result<RuntimeSynergyOutput[]>.Failure(sourceId.Error);
                var targetId = ParseOptionalId(item.targetId, packId, ownerId, sourcePath);
                if (!targetId.IsSuccess) return Result<RuntimeSynergyOutput[]>.Failure(targetId.Error);
                var effect = default(EffectOp);
                if (type == SynergyOutputType.AddEffectOp)
                {
                    if (item.effect == null)
                        return Result<RuntimeSynergyOutput[]>.Failure(FailureError("AddEffectOp output is missing effect data.", packId, ownerId, sourcePath));
                    var effectResult = item.effect.ToEffect(packId, ownerId, sourcePath);
                    if (!effectResult.IsSuccess) return Result<RuntimeSynergyOutput[]>.Failure(effectResult.Error);
                    effect = effectResult.Value;
                }

                output[index] = new RuntimeSynergyOutput(type, modifier, sourceId.Value, targetId.Value, effect);
            }

            return Result<RuntimeSynergyOutput[]>.Success(output);
        }

        private static M6BuildConditionDto[] FromConditions(IReadOnlyList<BuildCondition> source)
        {
            var output = new M6BuildConditionDto[source.Count];
            for (var index = 0; index < source.Count; index++)
                output[index] = M6BuildConditionDto.FromCondition(source[index]);
            return output;
        }

        private static M6BuildModifierDto[] FromModifiers(IReadOnlyList<RuntimeBuildModifier> source)
        {
            var output = new M6BuildModifierDto[source.Count];
            for (var index = 0; index < source.Count; index++)
                output[index] = M6BuildModifierDto.FromModifier(source[index]);
            return output;
        }

        private static Result<ContentId> ParseRequiredId(
            string value,
            string label,
            ContentId packId,
            ContentId ownerId,
            string sourcePath)
        {
            if (string.IsNullOrEmpty(value))
                return Result<ContentId>.Failure(FailureError("Definition requires " + label + ".", packId, ownerId, sourcePath));
            return CatalogDtoParsing.ParseCanonicalId(value, packId, sourcePath, label);
        }

        private static Result<ContentId> ParseOptionalId(
            string value,
            ContentId packId,
            ContentId ownerId,
            string sourcePath)
        {
            return string.IsNullOrEmpty(value)
                ? Result<ContentId>.Success(default)
                : CatalogDtoParsing.ParseCanonicalId(value, packId, sourcePath, "synergy output content ID");
        }

        private static Result<RuntimeContentDefinition> Failure(
            string message,
            ContentId packId,
            ContentId ownerId,
            string sourcePath)
        {
            return Result<RuntimeContentDefinition>.Failure(FailureError(message, packId, ownerId, sourcePath));
        }

        private static Error FailureError(
            string message,
            ContentId packId,
            ContentId ownerId,
            string sourcePath)
        {
            return new Error(ErrorCode.InvalidCatalog, message, ownerId, packId, sourcePath);
        }
    }

    internal static class BuildConditionTypeCodec
    {
        public static string ToSerializedValue(BuildConditionType value) =>
            value == BuildConditionType.OwnsContent ? "owns_content" :
            value == BuildConditionType.HasTagCount ? "has_tag_count" :
            value == BuildConditionType.SkillLevelAtLeast ? "skill_level_at_least" :
            value == BuildConditionType.StatAtLeast ? "stat_at_least" :
            value == BuildConditionType.MapHasTag ? "map_has_tag" : string.Empty;

        public static bool TryParse(string value, out BuildConditionType result)
        {
            if (value == "owns_content") result = BuildConditionType.OwnsContent;
            else if (value == "has_tag_count") result = BuildConditionType.HasTagCount;
            else if (value == "skill_level_at_least") result = BuildConditionType.SkillLevelAtLeast;
            else if (value == "stat_at_least") result = BuildConditionType.StatAtLeast;
            else if (value == "map_has_tag") result = BuildConditionType.MapHasTag;
            else { result = default; return false; }
            return true;
        }
    }

    internal static class SynergyOutputTypeCodec
    {
        public static string ToSerializedValue(SynergyOutputType value) =>
            value == SynergyOutputType.AddModifier ? "add_modifier" :
            value == SynergyOutputType.UnlockOffer ? "unlock_offer" :
            value == SynergyOutputType.AddEffectOp ? "add_effect_op" :
            value == SynergyOutputType.TransformSkill ? "transform_skill" :
            value == SynergyOutputType.GrantTrait ? "grant_trait" : string.Empty;

        public static bool TryParse(string value, out SynergyOutputType result)
        {
            if (value == "add_modifier") result = SynergyOutputType.AddModifier;
            else if (value == "unlock_offer") result = SynergyOutputType.UnlockOffer;
            else if (value == "add_effect_op") result = SynergyOutputType.AddEffectOp;
            else if (value == "transform_skill") result = SynergyOutputType.TransformSkill;
            else if (value == "grant_trait") result = SynergyOutputType.GrantTrait;
            else { result = default; return false; }
            return true;
        }
    }

    internal static class EvolutionConsumePolicyCodec
    {
        public static string ToSerializedValue(EvolutionConsumePolicy value) =>
            value == EvolutionConsumePolicy.RetainRequiredPassives
                ? "retain_required_passives"
                : value == EvolutionConsumePolicy.ConsumeRequiredPassives
                    ? "consume_required_passives"
                    : string.Empty;

        public static bool TryParse(string value, out EvolutionConsumePolicy result)
        {
            if (value == "retain_required_passives") result = EvolutionConsumePolicy.RetainRequiredPassives;
            else if (value == "consume_required_passives") result = EvolutionConsumePolicy.ConsumeRequiredPassives;
            else { result = default; return false; }
            return true;
        }
    }
}
