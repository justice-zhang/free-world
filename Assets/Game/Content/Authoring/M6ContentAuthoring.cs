using System;
using Game.Content.Runtime;
using Game.Core;
using UnityEngine;

namespace Game.Content.Authoring
{
    [Serializable]
    public sealed class BuildModifierAuthoringData
    {
        public string statId = string.Empty;
        public ModifierOperation operation = ModifierOperation.AddFlat;
        public float value;
        public int priority;
        public string stackingGroup = string.Empty;
    }

    [Serializable]
    public sealed class PassiveLevelModifierAuthoringData
    {
        public int level = 1;
        public BuildModifierAuthoringData modifier = new BuildModifierAuthoringData();
    }

    [Serializable]
    public sealed class BuildConditionAuthoringData
    {
        public BuildConditionType type = BuildConditionType.OwnsContent;
        public ContentAuthoringBase content;
        public string tag = string.Empty;
        public int integerValue = 1;
        public string statId = string.Empty;
        public float floatValue;
    }

    [Serializable]
    public sealed class SynergyOutputAuthoringData
    {
        public SynergyOutputType type = SynergyOutputType.AddModifier;
        public BuildModifierAuthoringData modifier;
        public ContentAuthoringBase sourceContent;
        public ContentAuthoringBase targetContent;
        public SkillEffectAuthoringData effect;
    }

    public abstract class PassiveAuthoringBase : ContentAuthoringBase
    {
        [SerializeField] private int maximumLevel = 1;
        [SerializeField] private PassiveLevelModifierAuthoringData[] levelModifiers =
            Array.Empty<PassiveLevelModifierAuthoringData>();

        public void Configure(int maxLevel, PassiveLevelModifierAuthoringData[] modifiers)
        {
            maximumLevel = maxLevel;
            levelModifiers = modifiers == null
                ? Array.Empty<PassiveLevelModifierAuthoringData>()
                : (PassiveLevelModifierAuthoringData[])modifiers.Clone();
        }

        internal override Result<RuntimeContentDefinition> Bake(ContentId packId, string authorAssetPath)
        {
            var commonResult = BakeCommon(packId, authorAssetPath);
            if (!commonResult.IsSuccess) return Result<RuntimeContentDefinition>.Failure(commonResult.Error);
            var common = commonResult.Value;
            var source = levelModifiers ?? Array.Empty<PassiveLevelModifierAuthoringData>();
            var output = new RuntimePassiveLevelModifier[source.Length];
            for (var index = 0; index < source.Length; index++)
            {
                if (source[index] == null || source[index].modifier == null)
                    return Failure("Passive contains a null level modifier.", common, packId);
                var modifier = M6AuthoringBake.ParseModifier(source[index].modifier, common, packId);
                if (!modifier.IsSuccess) return Result<RuntimeContentDefinition>.Failure(modifier.Error);
                output[index] = new RuntimePassiveLevelModifier(source[index].level, modifier.Value);
            }

            var definition = new RuntimePassiveDefinition(
                common.Id,
                common.LocalizedNameKey,
                common.LocalizedDescriptionKey,
                common.AuthorAssetPath,
                common.Tags,
                maximumLevel,
                output);
            return Validate(definition, common, packId);
        }

        private static Result<RuntimeContentDefinition> Validate(
            RuntimeContentDefinition definition,
            in AuthoringCommonData common,
            ContentId packId)
        {
            var message = M6ContentValidation.ValidateDefinitionValuesForAuthoring(definition);
            return message == null
                ? Result<RuntimeContentDefinition>.Success(definition)
                : Failure(message, common, packId);
        }

        private static Result<RuntimeContentDefinition> Failure(string message, in AuthoringCommonData common, ContentId packId) =>
            M6AuthoringBake.Failure(message, common, packId);
    }

    public abstract class TraitAuthoringBase : ContentAuthoringBase
    {
        [SerializeField] private BuildModifierAuthoringData[] modifiers = Array.Empty<BuildModifierAuthoringData>();

        public void Configure(BuildModifierAuthoringData[] traitModifiers)
        {
            modifiers = traitModifiers == null
                ? Array.Empty<BuildModifierAuthoringData>()
                : (BuildModifierAuthoringData[])traitModifiers.Clone();
        }

        internal override Result<RuntimeContentDefinition> Bake(ContentId packId, string authorAssetPath)
        {
            var commonResult = BakeCommon(packId, authorAssetPath);
            if (!commonResult.IsSuccess) return Result<RuntimeContentDefinition>.Failure(commonResult.Error);
            var common = commonResult.Value;
            var parsed = M6AuthoringBake.ParseModifiers(modifiers, common, packId);
            if (!parsed.IsSuccess) return Result<RuntimeContentDefinition>.Failure(parsed.Error);
            var definition = new RuntimeTraitDefinition(
                common.Id,
                common.LocalizedNameKey,
                common.LocalizedDescriptionKey,
                common.AuthorAssetPath,
                common.Tags,
                parsed.Value);
            return M6AuthoringBake.Validate(definition, common, packId);
        }
    }

    public abstract class SynergyAuthoringBase : ContentAuthoringBase
    {
        [SerializeField] private BuildConditionAuthoringData[] conditions = Array.Empty<BuildConditionAuthoringData>();
        [SerializeField] private SynergyOutputAuthoringData[] outputs = Array.Empty<SynergyOutputAuthoringData>();

        public void Configure(BuildConditionAuthoringData[] activationConditions, SynergyOutputAuthoringData[] synergyOutputs)
        {
            conditions = activationConditions == null ? Array.Empty<BuildConditionAuthoringData>() : (BuildConditionAuthoringData[])activationConditions.Clone();
            outputs = synergyOutputs == null ? Array.Empty<SynergyOutputAuthoringData>() : (SynergyOutputAuthoringData[])synergyOutputs.Clone();
        }

        internal override Result<RuntimeContentDefinition> Bake(ContentId packId, string authorAssetPath)
        {
            var commonResult = BakeCommon(packId, authorAssetPath);
            if (!commonResult.IsSuccess) return Result<RuntimeContentDefinition>.Failure(commonResult.Error);
            var common = commonResult.Value;
            var parsedConditions = M6AuthoringBake.ParseConditions(conditions, common, packId);
            if (!parsedConditions.IsSuccess) return Result<RuntimeContentDefinition>.Failure(parsedConditions.Error);
            var source = outputs ?? Array.Empty<SynergyOutputAuthoringData>();
            var parsedOutputs = new RuntimeSynergyOutput[source.Length];
            for (var index = 0; index < source.Length; index++)
            {
                var item = source[index];
                if (item == null) return M6AuthoringBake.Failure("Synergy contains a null output.", common, packId);
                var modifier = default(RuntimeBuildModifier);
                if (item.type == SynergyOutputType.AddModifier)
                {
                    if (item.modifier == null) return M6AuthoringBake.Failure("AddModifier output requires modifier data.", common, packId);
                    var result = M6AuthoringBake.ParseModifier(item.modifier, common, packId);
                    if (!result.IsSuccess) return Result<RuntimeContentDefinition>.Failure(result.Error);
                    modifier = result.Value;
                }

                var sourceId = M6AuthoringBake.ParseOptionalContent(item.sourceContent, common, packId);
                if (!sourceId.IsSuccess) return Result<RuntimeContentDefinition>.Failure(sourceId.Error);
                var targetId = M6AuthoringBake.ParseOptionalContent(item.targetContent, common, packId);
                if (!targetId.IsSuccess) return Result<RuntimeContentDefinition>.Failure(targetId.Error);
                var effect = default(EffectOp);
                if (item.type == SynergyOutputType.AddEffectOp)
                {
                    var result = M6AuthoringBake.ParseEffect(item.effect, common, packId);
                    if (!result.IsSuccess) return Result<RuntimeContentDefinition>.Failure(result.Error);
                    effect = result.Value;
                }

                parsedOutputs[index] = new RuntimeSynergyOutput(item.type, modifier, sourceId.Value, targetId.Value, effect);
            }

            var definition = new RuntimeSynergyDefinition(
                common.Id,
                common.LocalizedNameKey,
                common.LocalizedDescriptionKey,
                common.AuthorAssetPath,
                common.Tags,
                parsedConditions.Value,
                parsedOutputs);
            return M6AuthoringBake.Validate(definition, common, packId);
        }
    }

    public abstract class EvolutionAuthoringBase : ContentAuthoringBase
    {
        [SerializeField] private SkillAuthoring requiredSkill;
        [SerializeField] private int requiredSkillLevel = 1;
        [SerializeField] private PassiveAuthoring[] requiredPassives = Array.Empty<PassiveAuthoring>();
        [SerializeField] private BuildConditionAuthoringData[] additionalConditions = Array.Empty<BuildConditionAuthoringData>();
        [SerializeField] private SkillAuthoring resultSkill;
        [SerializeField] private EvolutionConsumePolicy consumePolicy = EvolutionConsumePolicy.RetainRequiredPassives;

        public void Configure(
            SkillAuthoring skill,
            int skillLevel,
            PassiveAuthoring[] passives,
            BuildConditionAuthoringData[] conditions,
            SkillAuthoring result,
            EvolutionConsumePolicy policy)
        {
            requiredSkill = skill;
            requiredSkillLevel = skillLevel;
            requiredPassives = passives == null ? Array.Empty<PassiveAuthoring>() : (PassiveAuthoring[])passives.Clone();
            additionalConditions = conditions == null ? Array.Empty<BuildConditionAuthoringData>() : (BuildConditionAuthoringData[])conditions.Clone();
            resultSkill = result;
            consumePolicy = policy;
        }

        internal override Result<RuntimeContentDefinition> Bake(ContentId packId, string authorAssetPath)
        {
            var commonResult = BakeCommon(packId, authorAssetPath);
            if (!commonResult.IsSuccess) return Result<RuntimeContentDefinition>.Failure(commonResult.Error);
            var common = commonResult.Value;
            if (requiredSkill == null || resultSkill == null)
                return M6AuthoringBake.Failure("Evolution requires source and result skills.", common, packId);
            var skillId = M6AuthoringBake.ParseRequiredContent(requiredSkill, "required skill", common, packId);
            if (!skillId.IsSuccess) return Result<RuntimeContentDefinition>.Failure(skillId.Error);
            var resultId = M6AuthoringBake.ParseRequiredContent(resultSkill, "result skill", common, packId);
            if (!resultId.IsSuccess) return Result<RuntimeContentDefinition>.Failure(resultId.Error);
            var sourcePassives = requiredPassives ?? Array.Empty<PassiveAuthoring>();
            var passiveIds = new ContentId[sourcePassives.Length];
            for (var index = 0; index < sourcePassives.Length; index++)
            {
                var parsed = M6AuthoringBake.ParseRequiredContent(sourcePassives[index], "required passive", common, packId);
                if (!parsed.IsSuccess) return Result<RuntimeContentDefinition>.Failure(parsed.Error);
                passiveIds[index] = parsed.Value;
            }
            var parsedConditions = M6AuthoringBake.ParseConditions(additionalConditions, common, packId);
            if (!parsedConditions.IsSuccess) return Result<RuntimeContentDefinition>.Failure(parsedConditions.Error);
            var definition = new RuntimeEvolutionDefinition(
                common.Id,
                common.LocalizedNameKey,
                common.LocalizedDescriptionKey,
                common.AuthorAssetPath,
                common.Tags,
                skillId.Value,
                requiredSkillLevel,
                passiveIds,
                parsedConditions.Value,
                resultId.Value,
                consumePolicy);
            return M6AuthoringBake.Validate(definition, common, packId);
        }
    }

    public abstract class UpgradeOfferAuthoringBase : ContentAuthoringBase
    {
        [SerializeField] private ContentAuthoringBase targetContent;
        [SerializeField] private float weight = 1f;
        [SerializeField] private bool initiallyUnlocked = true;
        [SerializeField] private BuildConditionAuthoringData[] prerequisites = Array.Empty<BuildConditionAuthoringData>();
        [SerializeField] private ContentAuthoringBase[] mutuallyExclusiveContent = Array.Empty<ContentAuthoringBase>();

        public void Configure(
            ContentAuthoringBase target,
            float offerWeight,
            bool unlocked,
            BuildConditionAuthoringData[] conditions,
            ContentAuthoringBase[] exclusions)
        {
            targetContent = target;
            weight = offerWeight;
            initiallyUnlocked = unlocked;
            prerequisites = conditions == null ? Array.Empty<BuildConditionAuthoringData>() : (BuildConditionAuthoringData[])conditions.Clone();
            mutuallyExclusiveContent = exclusions == null ? Array.Empty<ContentAuthoringBase>() : (ContentAuthoringBase[])exclusions.Clone();
        }

        internal override Result<RuntimeContentDefinition> Bake(ContentId packId, string authorAssetPath)
        {
            var commonResult = BakeCommon(packId, authorAssetPath);
            if (!commonResult.IsSuccess) return Result<RuntimeContentDefinition>.Failure(commonResult.Error);
            var common = commonResult.Value;
            var target = M6AuthoringBake.ParseRequiredContent(targetContent, "offer target", common, packId);
            if (!target.IsSuccess) return Result<RuntimeContentDefinition>.Failure(target.Error);
            var parsedConditions = M6AuthoringBake.ParseConditions(prerequisites, common, packId);
            if (!parsedConditions.IsSuccess) return Result<RuntimeContentDefinition>.Failure(parsedConditions.Error);
            var sourceExclusions = mutuallyExclusiveContent ?? Array.Empty<ContentAuthoringBase>();
            var exclusions = new ContentId[sourceExclusions.Length];
            for (var index = 0; index < sourceExclusions.Length; index++)
            {
                var parsed = M6AuthoringBake.ParseRequiredContent(sourceExclusions[index], "mutually-exclusive content", common, packId);
                if (!parsed.IsSuccess) return Result<RuntimeContentDefinition>.Failure(parsed.Error);
                exclusions[index] = parsed.Value;
            }
            var definition = new RuntimeUpgradeOfferDefinition(
                common.Id,
                common.LocalizedNameKey,
                common.LocalizedDescriptionKey,
                common.AuthorAssetPath,
                common.Tags,
                target.Value,
                weight,
                initiallyUnlocked,
                parsedConditions.Value,
                exclusions);
            return M6AuthoringBake.Validate(definition, common, packId);
        }
    }

    internal static class M6AuthoringBake
    {
        public static Result<RuntimeBuildModifier[]> ParseModifiers(
            BuildModifierAuthoringData[] source,
            in AuthoringCommonData common,
            ContentId packId)
        {
            var input = source ?? Array.Empty<BuildModifierAuthoringData>();
            var output = new RuntimeBuildModifier[input.Length];
            for (var index = 0; index < input.Length; index++)
            {
                if (input[index] == null)
                    return Result<RuntimeBuildModifier[]>.Failure(Error("Definition contains a null modifier.", common, packId));
                var parsed = ParseModifier(input[index], common, packId);
                if (!parsed.IsSuccess) return Result<RuntimeBuildModifier[]>.Failure(parsed.Error);
                output[index] = parsed.Value;
            }
            return Result<RuntimeBuildModifier[]>.Success(output);
        }

        public static Result<RuntimeBuildModifier> ParseModifier(
            BuildModifierAuthoringData source,
            in AuthoringCommonData common,
            ContentId packId)
        {
            if (source == null || !ContentId.IsCanonical(source.statId))
                return Result<RuntimeBuildModifier>.Failure(Error("Build modifier StatId must be canonical.", common, packId));
            var stat = StatId.Create(source.statId, packId, common.AuthorAssetPath);
            if (!stat.IsSuccess) return Result<RuntimeBuildModifier>.Failure(stat.Error);
            var group = default(ContentId);
            if (!string.IsNullOrEmpty(source.stackingGroup))
            {
                var parsed = ContentId.Create(source.stackingGroup, packId, common.AuthorAssetPath);
                if (!parsed.IsSuccess) return Result<RuntimeBuildModifier>.Failure(parsed.Error);
                group = parsed.Value;
            }
            return Result<RuntimeBuildModifier>.Success(
                new RuntimeBuildModifier(stat.Value, source.operation, source.value, source.priority, group));
        }

        public static Result<BuildCondition[]> ParseConditions(
            BuildConditionAuthoringData[] source,
            in AuthoringCommonData common,
            ContentId packId)
        {
            var input = source ?? Array.Empty<BuildConditionAuthoringData>();
            var output = new BuildCondition[input.Length];
            for (var index = 0; index < input.Length; index++)
            {
                var item = input[index];
                if (item == null) return Result<BuildCondition[]>.Failure(Error("Definition contains a null condition.", common, packId));
                var content = ParseOptionalContent(item.content, common, packId);
                if (!content.IsSuccess) return Result<BuildCondition[]>.Failure(content.Error);
                var tag = default(ContentTag);
                if (!string.IsNullOrEmpty(item.tag))
                {
                    if (!ContentId.IsCanonical(item.tag)) return Result<BuildCondition[]>.Failure(Error("Build condition tag must be canonical.", common, packId));
                    var parsed = ContentTag.Create(item.tag, packId, common.AuthorAssetPath);
                    if (!parsed.IsSuccess) return Result<BuildCondition[]>.Failure(parsed.Error);
                    tag = parsed.Value;
                }
                var stat = default(StatId);
                if (!string.IsNullOrEmpty(item.statId))
                {
                    if (!ContentId.IsCanonical(item.statId)) return Result<BuildCondition[]>.Failure(Error("Build condition StatId must be canonical.", common, packId));
                    var parsed = StatId.Create(item.statId, packId, common.AuthorAssetPath);
                    if (!parsed.IsSuccess) return Result<BuildCondition[]>.Failure(parsed.Error);
                    stat = parsed.Value;
                }
                output[index] = new BuildCondition(item.type, content.Value, tag, item.integerValue, stat, item.floatValue);
            }
            return Result<BuildCondition[]>.Success(output);
        }

        public static Result<EffectOp> ParseEffect(
            SkillEffectAuthoringData source,
            in AuthoringCommonData common,
            ContentId packId)
        {
            if (source == null || !ContentId.IsCanonical(source.moduleId))
                return Result<EffectOp>.Failure(Error("AddEffectOp requires a canonical effect module ID.", common, packId));
            var module = ContentId.Create(source.moduleId, packId, common.AuthorAssetPath);
            if (!module.IsSuccess) return Result<EffectOp>.Failure(module.Error);
            if (!SkillModuleIds.TryGetEffectCode(module.Value, out var code))
                return Result<EffectOp>.Failure(Error("AddEffectOp module is not registered.", common, packId));
            var reference0 = ParseOptionalId(source.referenceId0, common, packId);
            if (!reference0.IsSuccess) return Result<EffectOp>.Failure(reference0.Error);
            var reference1 = ParseOptionalId(source.referenceId1, common, packId);
            if (!reference1.IsSuccess) return Result<EffectOp>.Failure(reference1.Error);
            var tag = default(ContentTag);
            if (!string.IsNullOrEmpty(source.tag0))
            {
                if (!ContentId.IsCanonical(source.tag0)) return Result<EffectOp>.Failure(Error("Effect tag must be canonical.", common, packId));
                var parsed = ContentTag.Create(source.tag0, packId, common.AuthorAssetPath);
                if (!parsed.IsSuccess) return Result<EffectOp>.Failure(parsed.Error);
                tag = parsed.Value;
            }
            var stat = default(StatId);
            if (!string.IsNullOrEmpty(source.statId0))
            {
                if (!ContentId.IsCanonical(source.statId0)) return Result<EffectOp>.Failure(Error("Effect StatId must be canonical.", common, packId));
                var parsed = StatId.Create(source.statId0, packId, common.AuthorAssetPath);
                if (!parsed.IsSuccess) return Result<EffectOp>.Failure(parsed.Error);
                stat = parsed.Value;
            }
            return Result<EffectOp>.Success(
                new EffectOp(code, source.value0, source.value1, source.value2, source.int0, source.int1, reference0.Value, reference1.Value, tag, stat, source.flags));
        }

        public static Result<ContentId> ParseRequiredContent(
            ContentAuthoringBase source,
            string label,
            in AuthoringCommonData common,
            ContentId packId)
        {
            if (source == null) return Result<ContentId>.Failure(Error("Definition requires " + label + ".", common, packId));
            return ContentId.Create(source.ContentIdText, packId, common.AuthorAssetPath);
        }

        public static Result<ContentId> ParseOptionalContent(
            ContentAuthoringBase source,
            in AuthoringCommonData common,
            ContentId packId)
        {
            return source == null
                ? Result<ContentId>.Success(default)
                : ContentId.Create(source.ContentIdText, packId, common.AuthorAssetPath);
        }

        public static Result<RuntimeContentDefinition> Validate(
            RuntimeContentDefinition definition,
            in AuthoringCommonData common,
            ContentId packId)
        {
            var message = M6ContentValidation.ValidateDefinitionValuesForAuthoring(definition);
            return message == null
                ? Result<RuntimeContentDefinition>.Success(definition)
                : Failure(message, common, packId);
        }

        public static Result<RuntimeContentDefinition> Failure(string message, in AuthoringCommonData common, ContentId packId) =>
            Result<RuntimeContentDefinition>.Failure(Error(message, common, packId));

        private static Result<ContentId> ParseOptionalId(string value, in AuthoringCommonData common, ContentId packId) =>
            string.IsNullOrEmpty(value)
                ? Result<ContentId>.Success(default)
                : ContentId.Create(value, packId, common.AuthorAssetPath);

        private static Error Error(string message, in AuthoringCommonData common, ContentId packId) =>
            new Error(ErrorCode.InvalidAuthoringData, message, common.Id, packId, common.AuthorAssetPath);
    }
}
