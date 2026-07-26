using System;
using System.Collections.Generic;
using System.Text;
using Game.Core;

namespace Game.Content.Runtime
{
    /// <summary>Condition operations supported by M6 build content.</summary>
    public enum BuildConditionType : byte
    {
        OwnsContent = 1,
        HasTagCount = 2,
        SkillLevelAtLeast = 3,
        StatAtLeast = 4,
        MapHasTag = 5
    }

    /// <summary>Outputs supported by one activated synergy.</summary>
    public enum SynergyOutputType : byte
    {
        AddModifier = 1,
        UnlockOffer = 2,
        AddEffectOp = 3,
        TransformSkill = 4,
        GrantTrait = 5
    }

    /// <summary>Controls whether an evolution consumes its passive requirements.</summary>
    public enum EvolutionConsumePolicy : byte
    {
        RetainRequiredPassives = 1,
        ConsumeRequiredPassives = 2
    }

    /// <summary>Resolved category of an upgrade-offer target.</summary>
    public enum UpgradeTargetKind : byte
    {
        Skill = 1,
        Passive = 2,
        Evolution = 3
    }

    /// <summary>Pure condition operands shared by offers, synergies, and evolutions.</summary>
    public readonly struct BuildCondition
    {
        public BuildCondition(
            BuildConditionType type,
            ContentId contentId,
            ContentTag tag,
            int integerValue,
            StatId statId,
            float floatValue)
        {
            Type = type;
            ContentId = contentId;
            Tag = tag;
            IntegerValue = integerValue;
            StatId = statId;
            FloatValue = floatValue;
        }

        public BuildConditionType Type { get; }
        public ContentId ContentId { get; }
        public ContentTag Tag { get; }
        public int IntegerValue { get; }
        public StatId StatId { get; }
        public float FloatValue { get; }

        internal void AppendDeterministicData(StringBuilder builder)
        {
            ContentHashUtility.AppendInt(builder, (int)Type);
            ContentHashUtility.AppendToken(builder, ContentId.Value);
            ContentHashUtility.AppendToken(builder, Tag.Value);
            ContentHashUtility.AppendInt(builder, IntegerValue);
            ContentHashUtility.AppendToken(builder, StatId.Value);
            ContentHashUtility.AppendFloat(builder, FloatValue);
        }
    }

    /// <summary>Stable author-data form of a permanent build modifier.</summary>
    public readonly struct RuntimeBuildModifier
    {
        public RuntimeBuildModifier(
            StatId statId,
            ModifierOperation operation,
            float value,
            int priority,
            ContentId stackingGroup)
        {
            StatId = statId;
            Operation = operation;
            Value = value;
            Priority = priority;
            StackingGroup = stackingGroup;
        }

        public StatId StatId { get; }
        public ModifierOperation Operation { get; }
        public float Value { get; }
        public int Priority { get; }
        public ContentId StackingGroup { get; }

        internal void AppendDeterministicData(StringBuilder builder)
        {
            ContentHashUtility.AppendToken(builder, StatId.Value);
            ContentHashUtility.AppendInt(builder, (int)Operation);
            ContentHashUtility.AppendFloat(builder, Value);
            ContentHashUtility.AppendInt(builder, Priority);
            ContentHashUtility.AppendToken(builder, StackingGroup.Value);
        }
    }

    /// <summary>One passive modifier that becomes active at a specific level.</summary>
    public readonly struct RuntimePassiveLevelModifier
    {
        public RuntimePassiveLevelModifier(int level, RuntimeBuildModifier modifier)
        {
            Level = level;
            Modifier = modifier;
        }

        public int Level { get; }
        public RuntimeBuildModifier Modifier { get; }

        internal void AppendDeterministicData(StringBuilder builder)
        {
            ContentHashUtility.AppendInt(builder, Level);
            Modifier.AppendDeterministicData(builder);
        }
    }

    /// <summary>Schema-5 passive content with explicit maximum level and modifiers.</summary>
    public sealed class RuntimePassiveDefinition : RuntimeContentDefinition
    {
        private readonly RuntimePassiveLevelModifier[] modifiers;
        private readonly IReadOnlyList<RuntimePassiveLevelModifier> modifiersView;

        public RuntimePassiveDefinition(
            ContentId id,
            string localizedNameKey,
            string localizedDescriptionKey,
            string sourceAssetPath,
            ContentTag[] tags,
            int maximumLevel,
            RuntimePassiveLevelModifier[] levelModifiers)
            : base(id, localizedNameKey, localizedDescriptionKey, sourceAssetPath, tags, Array.Empty<ContentId>())
        {
            MaximumLevel = maximumLevel;
            modifiers = levelModifiers == null
                ? Array.Empty<RuntimePassiveLevelModifier>()
                : (RuntimePassiveLevelModifier[])levelModifiers.Clone();
            modifiersView = Array.AsReadOnly(modifiers);
        }

        public override string Kind => RuntimeContentKinds.Passive;
        public int MaximumLevel { get; }
        public IReadOnlyList<RuntimePassiveLevelModifier> LevelModifiers => modifiersView;

        protected override void AppendTypeSpecificDeterministicData(StringBuilder builder)
        {
            ContentHashUtility.AppendInt(builder, MaximumLevel);
            ContentHashUtility.AppendInt(builder, modifiers.Length);
            for (var index = 0; index < modifiers.Length; index++)
                modifiers[index].AppendDeterministicData(builder);
        }
    }

    /// <summary>Schema-5 trait content granted by character data or synergy outputs.</summary>
    public sealed class RuntimeTraitDefinition : RuntimeContentDefinition
    {
        private readonly RuntimeBuildModifier[] modifiers;
        private readonly IReadOnlyList<RuntimeBuildModifier> modifiersView;

        public RuntimeTraitDefinition(
            ContentId id,
            string localizedNameKey,
            string localizedDescriptionKey,
            string sourceAssetPath,
            ContentTag[] tags,
            RuntimeBuildModifier[] traitModifiers)
            : base(id, localizedNameKey, localizedDescriptionKey, sourceAssetPath, tags, Array.Empty<ContentId>())
        {
            modifiers = traitModifiers == null
                ? Array.Empty<RuntimeBuildModifier>()
                : (RuntimeBuildModifier[])traitModifiers.Clone();
            modifiersView = Array.AsReadOnly(modifiers);
        }

        public override string Kind => RuntimeContentKinds.Trait;
        public IReadOnlyList<RuntimeBuildModifier> Modifiers => modifiersView;

        protected override void AppendTypeSpecificDeterministicData(StringBuilder builder)
        {
            ContentHashUtility.AppendInt(builder, modifiers.Length);
            for (var index = 0; index < modifiers.Length; index++)
                modifiers[index].AppendDeterministicData(builder);
        }
    }

    /// <summary>One generic output applied once when a synergy becomes active.</summary>
    public readonly struct RuntimeSynergyOutput
    {
        public RuntimeSynergyOutput(
            SynergyOutputType type,
            RuntimeBuildModifier modifier,
            ContentId sourceId,
            ContentId targetId,
            EffectOp effect)
        {
            Type = type;
            Modifier = modifier;
            SourceId = sourceId;
            TargetId = targetId;
            Effect = effect;
        }

        public SynergyOutputType Type { get; }
        public RuntimeBuildModifier Modifier { get; }
        public ContentId SourceId { get; }
        public ContentId TargetId { get; }
        public EffectOp Effect { get; }

        internal void AppendDeterministicData(StringBuilder builder)
        {
            ContentHashUtility.AppendInt(builder, (int)Type);
            Modifier.AppendDeterministicData(builder);
            ContentHashUtility.AppendToken(builder, SourceId.Value);
            ContentHashUtility.AppendToken(builder, TargetId.Value);
            Effect.AppendDeterministicData(builder);
        }
    }

    /// <summary>Schema-5 configuration-driven build synergy.</summary>
    public sealed class RuntimeSynergyDefinition : RuntimeContentDefinition
    {
        private readonly BuildCondition[] conditions;
        private readonly RuntimeSynergyOutput[] outputs;
        private readonly IReadOnlyList<BuildCondition> conditionsView;
        private readonly IReadOnlyList<RuntimeSynergyOutput> outputsView;

        public RuntimeSynergyDefinition(
            ContentId id,
            string localizedNameKey,
            string localizedDescriptionKey,
            string sourceAssetPath,
            ContentTag[] tags,
            BuildCondition[] activationConditions,
            RuntimeSynergyOutput[] synergyOutputs)
            : base(
                id,
                localizedNameKey,
                localizedDescriptionKey,
                sourceAssetPath,
                tags,
                CollectReferences(activationConditions, synergyOutputs))
        {
            conditions = activationConditions == null
                ? Array.Empty<BuildCondition>()
                : (BuildCondition[])activationConditions.Clone();
            outputs = synergyOutputs == null
                ? Array.Empty<RuntimeSynergyOutput>()
                : (RuntimeSynergyOutput[])synergyOutputs.Clone();
            conditionsView = Array.AsReadOnly(conditions);
            outputsView = Array.AsReadOnly(outputs);
        }

        public override string Kind => RuntimeContentKinds.Synergy;
        public IReadOnlyList<BuildCondition> Conditions => conditionsView;
        public IReadOnlyList<RuntimeSynergyOutput> Outputs => outputsView;

        protected override void AppendTypeSpecificDeterministicData(StringBuilder builder)
        {
            ContentHashUtility.AppendInt(builder, conditions.Length);
            for (var index = 0; index < conditions.Length; index++)
                conditions[index].AppendDeterministicData(builder);
            ContentHashUtility.AppendInt(builder, outputs.Length);
            for (var index = 0; index < outputs.Length; index++)
                outputs[index].AppendDeterministicData(builder);
        }

        private static ContentId[] CollectReferences(
            BuildCondition[] sourceConditions,
            RuntimeSynergyOutput[] sourceOutputs)
        {
            var result = new List<ContentId>();
            AppendConditionReferences(result, sourceConditions);
            var outputs = sourceOutputs ?? Array.Empty<RuntimeSynergyOutput>();
            for (var index = 0; index < outputs.Length; index++)
            {
                var output = outputs[index];
                AddIfValid(result, output.SourceId);
                AddIfValid(result, output.TargetId);
                AddIfValid(result, output.Effect.ReferenceId0);
                AddIfValid(result, output.Effect.ReferenceId1);
            }

            return result.ToArray();
        }

        internal static void AppendConditionReferences(List<ContentId> result, BuildCondition[] source)
        {
            var conditions = source ?? Array.Empty<BuildCondition>();
            for (var index = 0; index < conditions.Length; index++)
                AddIfValid(result, conditions[index].ContentId);
        }

        internal static void AddIfValid(List<ContentId> result, ContentId id)
        {
            if (id.IsValid && !result.Contains(id)) result.Add(id);
        }
    }

    /// <summary>Schema-5 skill transformation recipe.</summary>
    public sealed class RuntimeEvolutionDefinition : RuntimeContentDefinition
    {
        private readonly ContentId[] requiredPassiveIds;
        private readonly BuildCondition[] additionalConditions;
        private readonly IReadOnlyList<ContentId> requiredPassiveIdsView;
        private readonly IReadOnlyList<BuildCondition> additionalConditionsView;

        public RuntimeEvolutionDefinition(
            ContentId id,
            string localizedNameKey,
            string localizedDescriptionKey,
            string sourceAssetPath,
            ContentTag[] tags,
            ContentId requiredSkillId,
            int requiredSkillLevel,
            ContentId[] passiveIds,
            BuildCondition[] conditions,
            ContentId resultSkillId,
            EvolutionConsumePolicy consumePolicy)
            : base(
                id,
                localizedNameKey,
                localizedDescriptionKey,
                sourceAssetPath,
                tags,
                CollectReferences(requiredSkillId, passiveIds, conditions, resultSkillId))
        {
            RequiredSkillId = requiredSkillId;
            RequiredSkillLevel = requiredSkillLevel;
            requiredPassiveIds = passiveIds == null ? Array.Empty<ContentId>() : (ContentId[])passiveIds.Clone();
            additionalConditions = conditions == null ? Array.Empty<BuildCondition>() : (BuildCondition[])conditions.Clone();
            requiredPassiveIdsView = Array.AsReadOnly(requiredPassiveIds);
            additionalConditionsView = Array.AsReadOnly(additionalConditions);
            ResultSkillId = resultSkillId;
            ConsumePolicy = consumePolicy;
        }

        public override string Kind => RuntimeContentKinds.Evolution;
        public ContentId RequiredSkillId { get; }
        public int RequiredSkillLevel { get; }
        public IReadOnlyList<ContentId> RequiredPassiveIds => requiredPassiveIdsView;
        public IReadOnlyList<BuildCondition> AdditionalConditions => additionalConditionsView;
        public ContentId ResultSkillId { get; }
        public EvolutionConsumePolicy ConsumePolicy { get; }

        protected override void AppendTypeSpecificDeterministicData(StringBuilder builder)
        {
            ContentHashUtility.AppendToken(builder, RequiredSkillId.Value);
            ContentHashUtility.AppendInt(builder, RequiredSkillLevel);
            ContentHashUtility.AppendInt(builder, requiredPassiveIds.Length);
            for (var index = 0; index < requiredPassiveIds.Length; index++)
                ContentHashUtility.AppendToken(builder, requiredPassiveIds[index].Value);
            ContentHashUtility.AppendInt(builder, additionalConditions.Length);
            for (var index = 0; index < additionalConditions.Length; index++)
                additionalConditions[index].AppendDeterministicData(builder);
            ContentHashUtility.AppendToken(builder, ResultSkillId.Value);
            ContentHashUtility.AppendInt(builder, (int)ConsumePolicy);
        }

        private static ContentId[] CollectReferences(
            ContentId skill,
            ContentId[] passives,
            BuildCondition[] conditions,
            ContentId resultSkill)
        {
            var result = new List<ContentId>();
            RuntimeSynergyDefinition.AddIfValid(result, skill);
            var required = passives ?? Array.Empty<ContentId>();
            for (var index = 0; index < required.Length; index++)
                RuntimeSynergyDefinition.AddIfValid(result, required[index]);
            RuntimeSynergyDefinition.AppendConditionReferences(result, conditions);
            RuntimeSynergyDefinition.AddIfValid(result, resultSkill);
            return result.ToArray();
        }
    }

    /// <summary>Schema-5 weighted candidate pointing to a skill, passive, or evolution.</summary>
    public sealed class RuntimeUpgradeOfferDefinition : RuntimeContentDefinition
    {
        private readonly BuildCondition[] conditions;
        private readonly ContentId[] mutuallyExclusiveIds;
        private readonly IReadOnlyList<BuildCondition> conditionsView;
        private readonly IReadOnlyList<ContentId> mutuallyExclusiveIdsView;

        public RuntimeUpgradeOfferDefinition(
            ContentId id,
            string localizedNameKey,
            string localizedDescriptionKey,
            string sourceAssetPath,
            ContentTag[] tags,
            ContentId targetContentId,
            float weight,
            bool initiallyUnlocked,
            BuildCondition[] prerequisites,
            ContentId[] exclusiveIds)
            : base(
                id,
                localizedNameKey,
                localizedDescriptionKey,
                sourceAssetPath,
                tags,
                CollectReferences(targetContentId, prerequisites, exclusiveIds))
        {
            TargetContentId = targetContentId;
            Weight = weight;
            InitiallyUnlocked = initiallyUnlocked;
            conditions = prerequisites == null ? Array.Empty<BuildCondition>() : (BuildCondition[])prerequisites.Clone();
            mutuallyExclusiveIds = exclusiveIds == null ? Array.Empty<ContentId>() : (ContentId[])exclusiveIds.Clone();
            conditionsView = Array.AsReadOnly(conditions);
            mutuallyExclusiveIdsView = Array.AsReadOnly(mutuallyExclusiveIds);
        }

        public override string Kind => RuntimeContentKinds.Offer;
        public ContentId TargetContentId { get; }
        public float Weight { get; }
        public bool InitiallyUnlocked { get; }
        public IReadOnlyList<BuildCondition> Prerequisites => conditionsView;
        public IReadOnlyList<ContentId> MutuallyExclusiveIds => mutuallyExclusiveIdsView;

        protected override void AppendTypeSpecificDeterministicData(StringBuilder builder)
        {
            ContentHashUtility.AppendToken(builder, TargetContentId.Value);
            ContentHashUtility.AppendFloat(builder, Weight);
            ContentHashUtility.AppendInt(builder, InitiallyUnlocked ? 1 : 0);
            ContentHashUtility.AppendInt(builder, conditions.Length);
            for (var index = 0; index < conditions.Length; index++)
                conditions[index].AppendDeterministicData(builder);
            ContentHashUtility.AppendInt(builder, mutuallyExclusiveIds.Length);
            for (var index = 0; index < mutuallyExclusiveIds.Length; index++)
                ContentHashUtility.AppendToken(builder, mutuallyExclusiveIds[index].Value);
        }

        private static ContentId[] CollectReferences(
            ContentId target,
            BuildCondition[] prerequisites,
            ContentId[] exclusiveIds)
        {
            var result = new List<ContentId>();
            RuntimeSynergyDefinition.AddIfValid(result, target);
            RuntimeSynergyDefinition.AppendConditionReferences(result, prerequisites);
            var exclusions = exclusiveIds ?? Array.Empty<ContentId>();
            for (var index = 0; index < exclusions.Length; index++)
                RuntimeSynergyDefinition.AddIfValid(result, exclusions[index]);
            return result.ToArray();
        }
    }
}
