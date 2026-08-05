using System;
using System.Collections.Generic;
using System.Text;
using Game.Core;

namespace Game.Content.Runtime
{
    public enum RewardRepeatPolicy : byte
    {
        OncePerTransaction = 1,
        OncePerRun = 2,
        Repeatable = 3
    }

    public enum RewardOperationCode : byte
    {
        Heal = 1,
        ApplyStatus = 2,
        DamageArea = 3,
        CollectEligiblePickups = 4,
        GrantRelicChoice = 5,
        GrantEvolutionChoice = 6,
        AddCurrency = 7,
        UnlockContent = 8,
        GrantUnique = 9,
        TriggerStory = 10,
        /// <summary>
        /// Spawns bounded enemy children on reward resolution. IntegerValue is the
        /// child count, Value is the child health/damage/reward scale, and an empty
        /// ReferenceId means the defeated enemy archetype.
        /// </summary>
        SpawnEnemy = 11
    }

    public enum ObjectiveState : byte
    {
        Hidden = 1,
        Revealed = 2,
        Available = 3,
        Activating = 4,
        Defending = 5,
        Completed = 6,
        DisabledWithError = 7
    }

    public enum BossPhaseCleanupPolicy : byte
    {
        ExpireOnPhaseExit = 1,
        FinishCurrentTelegraph = 2,
        Persist = 3
    }

    public enum MetaNodeKind : byte
    {
        Branch = 1,
        Terminal = 2
    }

    public readonly struct CharacterMechanicTier
    {
        public CharacterMechanicTier(float threshold, ContentId outputId)
        {
            Threshold = threshold;
            OutputId = outputId;
        }

        public float Threshold { get; }
        public ContentId OutputId { get; }

        internal void AppendDeterministicData(StringBuilder builder)
        {
            ContentHashUtility.AppendFloat(builder, Threshold);
            ContentHashUtility.AppendToken(builder, OutputId.Value);
        }
    }

    public readonly struct RewardOperation
    {
        public RewardOperation(
            RewardOperationCode code,
            float value = 0f,
            int integerValue = 0,
            ContentId referenceId = default,
            ContentTag eligibilityTag = default)
        {
            Code = code;
            Value = value;
            IntegerValue = integerValue;
            ReferenceId = referenceId;
            EligibilityTag = eligibilityTag;
        }

        public RewardOperationCode Code { get; }
        public float Value { get; }
        public int IntegerValue { get; }
        public ContentId ReferenceId { get; }
        public ContentTag EligibilityTag { get; }

        internal void AppendDeterministicData(StringBuilder builder)
        {
            ContentHashUtility.AppendInt(builder, (int)Code);
            ContentHashUtility.AppendFloat(builder, Value);
            ContentHashUtility.AppendInt(builder, IntegerValue);
            ContentHashUtility.AppendToken(builder, ReferenceId.Value);
            ContentHashUtility.AppendToken(builder, EligibilityTag.Value);
        }
    }

    public readonly struct ObjectiveStateTransition
    {
        public ObjectiveStateTransition(ObjectiveState from, ObjectiveState to)
        {
            From = from;
            To = to;
        }

        public ObjectiveState From { get; }
        public ObjectiveState To { get; }

        internal void AppendDeterministicData(StringBuilder builder)
        {
            ContentHashUtility.AppendInt(builder, (int)From);
            ContentHashUtility.AppendInt(builder, (int)To);
        }
    }

    public readonly struct RuntimeBossPhase
    {
        private static readonly IReadOnlyList<ContentId> EmptyRuleIds =
            Array.AsReadOnly(Array.Empty<ContentId>());
        private readonly ContentId[] ruleIds;
        private readonly IReadOnlyList<ContentId> ruleIdsView;

        public RuntimeBossPhase(
            float healthThreshold,
            ContentId[] acceptedRuleIds,
            BossPhaseCleanupPolicy cleanupPolicy)
        {
            HealthThreshold = healthThreshold;
            ruleIds = acceptedRuleIds == null ? Array.Empty<ContentId>() : (ContentId[])acceptedRuleIds.Clone();
            ruleIdsView = Array.AsReadOnly(ruleIds);
            CleanupPolicy = cleanupPolicy;
        }

        public float HealthThreshold { get; }
        public IReadOnlyList<ContentId> AcceptedRuleIds => ruleIdsView ?? EmptyRuleIds;
        public BossPhaseCleanupPolicy CleanupPolicy { get; }

        internal ContentId[] CloneRuleIds() =>
            ruleIds == null ? Array.Empty<ContentId>() : (ContentId[])ruleIds.Clone();

        internal void AppendDeterministicData(StringBuilder builder)
        {
            ContentHashUtility.AppendFloat(builder, HealthThreshold);
            ContentHashUtility.AppendInt(builder, ruleIds.Length);
            for (var index = 0; index < ruleIds.Length; index++)
                ContentHashUtility.AppendToken(builder, ruleIds[index].Value);
            ContentHashUtility.AppendInt(builder, (int)CleanupPolicy);
        }
    }

    public abstract class RuntimeQinglanDefinition : RuntimeContentDefinition
    {
        protected RuntimeQinglanDefinition(
            ContentId id,
            string localizedNameKey,
            string localizedDescriptionKey,
            string sourceAssetPath,
            ContentTag[] tags,
            ContentId presentationProfileId,
            ContentId[] references)
            : base(id, localizedNameKey, localizedDescriptionKey, sourceAssetPath, tags, references)
        {
            PresentationProfileId = presentationProfileId;
        }

        public ContentId PresentationProfileId { get; }

        protected void AppendPresentation(StringBuilder builder)
        {
            ContentHashUtility.AppendToken(builder, PresentationProfileId.Value);
        }
    }

    public sealed class RuntimeCharacterMechanicDefinition : RuntimeQinglanDefinition
    {
        private readonly CharacterMechanicTier[] tiers;
        private readonly IReadOnlyList<CharacterMechanicTier> tiersView;

        public RuntimeCharacterMechanicDefinition(
            ContentId id, string nameKey, string descriptionKey, string sourcePath, ContentTag[] tags,
            ContentId resourceId, float gainPerUnit, float lossOnDamage,
            CharacterMechanicTier[] mechanicTiers, ContentId presentationProfileId)
            : base(id, nameKey, descriptionKey, sourcePath, tags, presentationProfileId,
                QinglanReferences.FromMechanicTiers(mechanicTiers))
        {
            ResourceId = resourceId;
            GainPerUnit = gainPerUnit;
            LossOnDamage = lossOnDamage;
            tiers = mechanicTiers == null ? Array.Empty<CharacterMechanicTier>() : (CharacterMechanicTier[])mechanicTiers.Clone();
            tiersView = Array.AsReadOnly(tiers);
        }

        public override string Kind => RuntimeContentKinds.CharacterMechanic;
        public ContentId ResourceId { get; }
        public float GainPerUnit { get; }
        public float LossOnDamage { get; }
        public IReadOnlyList<CharacterMechanicTier> Tiers => tiersView;

        protected override void AppendTypeSpecificDeterministicData(StringBuilder builder)
        {
            ContentHashUtility.AppendToken(builder, ResourceId.Value);
            ContentHashUtility.AppendFloat(builder, GainPerUnit);
            ContentHashUtility.AppendFloat(builder, LossOnDamage);
            ContentHashUtility.AppendInt(builder, tiers.Length);
            for (var index = 0; index < tiers.Length; index++) tiers[index].AppendDeterministicData(builder);
            AppendPresentation(builder);
        }
    }

    public sealed class RuntimeRewardDefinition : RuntimeQinglanDefinition
    {
        private readonly RewardOperation[] operations;
        private readonly IReadOnlyList<RewardOperation> operationsView;

        public RuntimeRewardDefinition(
            ContentId id, string nameKey, string descriptionKey, string sourcePath, ContentTag[] tags,
            RewardOperation[] rewardOperations, RewardRepeatPolicy repeatPolicy,
            ContentId fallbackRewardId, string uniqueKey, ContentId presentationProfileId)
            : base(id, nameKey, descriptionKey, sourcePath, tags, presentationProfileId,
                QinglanReferences.FromRewardOperations(rewardOperations, fallbackRewardId))
        {
            operations = rewardOperations == null ? Array.Empty<RewardOperation>() : (RewardOperation[])rewardOperations.Clone();
            operationsView = Array.AsReadOnly(operations);
            RepeatPolicy = repeatPolicy;
            FallbackRewardId = fallbackRewardId;
            UniqueKey = uniqueKey ?? string.Empty;
        }

        public override string Kind => RuntimeContentKinds.Reward;
        public IReadOnlyList<RewardOperation> Operations => operationsView;
        public RewardRepeatPolicy RepeatPolicy { get; }
        public ContentId FallbackRewardId { get; }
        public string UniqueKey { get; }

        protected override void AppendTypeSpecificDeterministicData(StringBuilder builder)
        {
            ContentHashUtility.AppendInt(builder, operations.Length);
            for (var index = 0; index < operations.Length; index++) operations[index].AppendDeterministicData(builder);
            ContentHashUtility.AppendInt(builder, (int)RepeatPolicy);
            ContentHashUtility.AppendToken(builder, FallbackRewardId.Value);
            ContentHashUtility.AppendToken(builder, UniqueKey);
            AppendPresentation(builder);
        }
    }

    public sealed class RuntimePickupDefinition : RuntimeQinglanDefinition
    {
        public RuntimePickupDefinition(
            ContentId id, string nameKey, string descriptionKey, string sourcePath, ContentTag[] tags,
            ContentId rewardId, float radius, float lifetimeSeconds, ContentTag eligibilityTag,
            ContentId presentationProfileId)
            : base(id, nameKey, descriptionKey, sourcePath, tags, presentationProfileId,
                QinglanReferences.Optional(rewardId))
        {
            RewardId = rewardId;
            Radius = radius;
            LifetimeSeconds = lifetimeSeconds;
            EligibilityTag = eligibilityTag;
        }

        public override string Kind => RuntimeContentKinds.Pickup;
        public ContentId RewardId { get; }
        public float Radius { get; }
        public float LifetimeSeconds { get; }
        public ContentTag EligibilityTag { get; }

        protected override void AppendTypeSpecificDeterministicData(StringBuilder builder)
        {
            ContentHashUtility.AppendToken(builder, RewardId.Value);
            ContentHashUtility.AppendFloat(builder, Radius);
            ContentHashUtility.AppendFloat(builder, LifetimeSeconds);
            ContentHashUtility.AppendToken(builder, EligibilityTag.Value);
            AppendPresentation(builder);
        }
    }

    public sealed class RuntimeRelicDefinition : RuntimeQinglanDefinition
    {
        private readonly ContentId[] outputIds;
        private readonly ContentId[] prerequisiteIds;
        private readonly ContentId[] mutexIds;
        private readonly IReadOnlyList<ContentId> outputIdsView;
        private readonly IReadOnlyList<ContentId> prerequisiteIdsView;
        private readonly IReadOnlyList<ContentId> mutexIdsView;

        public RuntimeRelicDefinition(
            ContentId id, string nameKey, string descriptionKey, string sourcePath, ContentTag[] tags,
            int maximumLevel, ContentId[] outputs, ContentId[] prerequisites, ContentId[] mutuallyExclusiveIds,
            ContentId presentationProfileId)
            : base(id, nameKey, descriptionKey, sourcePath, tags, presentationProfileId,
                QinglanReferences.Combine(outputs, prerequisites, mutuallyExclusiveIds))
        {
            MaximumLevel = maximumLevel;
            outputIds = QinglanReferences.Clone(outputs);
            prerequisiteIds = QinglanReferences.Clone(prerequisites);
            mutexIds = QinglanReferences.Clone(mutuallyExclusiveIds);
            outputIdsView = Array.AsReadOnly(outputIds);
            prerequisiteIdsView = Array.AsReadOnly(prerequisiteIds);
            mutexIdsView = Array.AsReadOnly(mutexIds);
        }

        public override string Kind => RuntimeContentKinds.Relic;
        public int MaximumLevel { get; }
        public IReadOnlyList<ContentId> OutputIds => outputIdsView;
        public IReadOnlyList<ContentId> PrerequisiteIds => prerequisiteIdsView;
        public IReadOnlyList<ContentId> MutuallyExclusiveIds => mutexIdsView;

        protected override void AppendTypeSpecificDeterministicData(StringBuilder builder)
        {
            ContentHashUtility.AppendInt(builder, MaximumLevel);
            QinglanReferences.Append(builder, outputIds);
            QinglanReferences.Append(builder, prerequisiteIds);
            QinglanReferences.Append(builder, mutexIds);
            AppendPresentation(builder);
        }
    }

    public abstract class RuntimeStateGraphDefinition : RuntimeQinglanDefinition
    {
        private readonly ContentId[] anchorIds;
        private readonly ObjectiveStateTransition[] transitions;
        private readonly IReadOnlyList<ContentId> anchorIdsView;
        private readonly IReadOnlyList<ObjectiveStateTransition> transitionsView;

        protected RuntimeStateGraphDefinition(
            ContentId id, string nameKey, string descriptionKey, string sourcePath, ContentTag[] tags,
            ContentId[] anchors, ObjectiveStateTransition[] stateTransitions, ContentId outputId,
            ContentId presentationProfileId, ContentId[] extraReferences = null)
            : base(id, nameKey, descriptionKey, sourcePath, tags, presentationProfileId,
                QinglanReferences.Combine(QinglanReferences.Optional(outputId), extraReferences))
        {
            anchorIds = QinglanReferences.Clone(anchors);
            transitions = stateTransitions == null ? Array.Empty<ObjectiveStateTransition>() : (ObjectiveStateTransition[])stateTransitions.Clone();
            anchorIdsView = Array.AsReadOnly(anchorIds);
            transitionsView = Array.AsReadOnly(transitions);
            OutputId = outputId;
        }

        public IReadOnlyList<ContentId> AnchorIds => anchorIdsView;
        public IReadOnlyList<ObjectiveStateTransition> StateTransitions => transitionsView;
        public ContentId OutputId { get; }

        protected void AppendStateGraph(StringBuilder builder)
        {
            QinglanReferences.Append(builder, anchorIds);
            ContentHashUtility.AppendInt(builder, transitions.Length);
            for (var index = 0; index < transitions.Length; index++) transitions[index].AppendDeterministicData(builder);
            ContentHashUtility.AppendToken(builder, OutputId.Value);
            AppendPresentation(builder);
        }
    }

    public sealed class RuntimeMapObjectiveDefinition : RuntimeStateGraphDefinition
    {
        public RuntimeMapObjectiveDefinition(
            ContentId id, string nameKey, string descriptionKey, string sourcePath, ContentTag[] tags,
            ContentId[] anchorIds, ObjectiveStateTransition[] transitions, ContentId completionRewardId,
            ContentId presentationProfileId)
            : base(id, nameKey, descriptionKey, sourcePath, tags, anchorIds, transitions,
                completionRewardId, presentationProfileId) { }

        public override string Kind => RuntimeContentKinds.MapObjective;
        protected override void AppendTypeSpecificDeterministicData(StringBuilder builder) => AppendStateGraph(builder);
    }

    public sealed class RuntimeMapEventDefinition : RuntimeStateGraphDefinition
    {
        public RuntimeMapEventDefinition(
            ContentId id, string nameKey, string descriptionKey, string sourcePath, ContentTag[] tags,
            ContentId[] anchorIds, ObjectiveStateTransition[] transitions, float triggerStartSeconds,
            float triggerEndSeconds, ContentId outputId, ContentId presentationProfileId)
            : base(id, nameKey, descriptionKey, sourcePath, tags, anchorIds, transitions,
                outputId, presentationProfileId)
        {
            TriggerStartSeconds = triggerStartSeconds;
            TriggerEndSeconds = triggerEndSeconds;
        }

        public override string Kind => RuntimeContentKinds.MapEvent;
        public float TriggerStartSeconds { get; }
        public float TriggerEndSeconds { get; }
        protected override void AppendTypeSpecificDeterministicData(StringBuilder builder)
        {
            AppendStateGraph(builder);
            ContentHashUtility.AppendFloat(builder, TriggerStartSeconds);
            ContentHashUtility.AppendFloat(builder, TriggerEndSeconds);
        }
    }

    public sealed class RuntimeLandmarkDefinition : RuntimeQinglanDefinition
    {
        public RuntimeLandmarkDefinition(
            ContentId id, string nameKey, string descriptionKey, string sourcePath, ContentTag[] tags,
            ContentId anchorId, ContentId rewardId, ContentId storyId, bool repeatable,
            ContentId presentationProfileId)
            : base(id, nameKey, descriptionKey, sourcePath, tags, presentationProfileId,
                QinglanReferences.Combine(QinglanReferences.Optional(rewardId), QinglanReferences.Optional(storyId)))
        {
            AnchorId = anchorId;
            RewardId = rewardId;
            StoryId = storyId;
            Repeatable = repeatable;
        }

        public override string Kind => RuntimeContentKinds.Landmark;
        public ContentId AnchorId { get; }
        public ContentId RewardId { get; }
        public ContentId StoryId { get; }
        public bool Repeatable { get; }
        protected override void AppendTypeSpecificDeterministicData(StringBuilder builder)
        {
            ContentHashUtility.AppendToken(builder, AnchorId.Value);
            ContentHashUtility.AppendToken(builder, RewardId.Value);
            ContentHashUtility.AppendToken(builder, StoryId.Value);
            ContentHashUtility.AppendInt(builder, Repeatable ? 1 : 0);
            AppendPresentation(builder);
        }
    }

    public sealed class RuntimeBossDefinition : RuntimeQinglanDefinition
    {
        private readonly RuntimeBossPhase[] phases;
        private readonly IReadOnlyList<RuntimeBossPhase> phasesView;

        public RuntimeBossDefinition(
            ContentId id, string nameKey, string descriptionKey, string sourcePath, ContentTag[] tags,
            ContentId enemyId, RuntimeBossPhase[] bossPhases, ContentId rewardId,
            float resistanceMultiplier, ContentId presentationProfileId)
            : base(id, nameKey, descriptionKey, sourcePath, tags, presentationProfileId,
                QinglanReferences.FromBoss(enemyId, rewardId, bossPhases))
        {
            EnemyId = enemyId;
            phases = bossPhases == null ? Array.Empty<RuntimeBossPhase>() : (RuntimeBossPhase[])bossPhases.Clone();
            phasesView = Array.AsReadOnly(phases);
            RewardId = rewardId;
            ResistanceMultiplier = resistanceMultiplier;
        }

        public override string Kind => RuntimeContentKinds.Boss;
        public ContentId EnemyId { get; }
        public IReadOnlyList<RuntimeBossPhase> Phases => phasesView;
        public ContentId RewardId { get; }
        public float ResistanceMultiplier { get; }
        protected override void AppendTypeSpecificDeterministicData(StringBuilder builder)
        {
            ContentHashUtility.AppendToken(builder, EnemyId.Value);
            ContentHashUtility.AppendInt(builder, phases.Length);
            for (var index = 0; index < phases.Length; index++) phases[index].AppendDeterministicData(builder);
            ContentHashUtility.AppendToken(builder, RewardId.Value);
            ContentHashUtility.AppendFloat(builder, ResistanceMultiplier);
            AppendPresentation(builder);
        }
    }

    public sealed class RuntimeEliteAffixDefinition : RuntimeQinglanDefinition
    {
        private readonly ContentTag[] requiredTags;
        private readonly ContentTag[] excludedTags;
        private readonly IReadOnlyList<ContentTag> requiredTagsView;
        private readonly IReadOnlyList<ContentTag> excludedTagsView;

        public RuntimeEliteAffixDefinition(
            ContentId id, string nameKey, string descriptionKey, string sourcePath, ContentTag[] tags,
            ContentTag[] required, ContentTag[] excluded, ContentId modifierOutputId,
            ContentId skillId, ContentId deathRewardId, ContentId presentationProfileId)
            : this(
                id, nameKey, descriptionKey, sourcePath, tags,
                required, excluded, modifierOutputId, skillId, deathRewardId,
                0, 1f, presentationProfileId)
        {
        }

        public RuntimeEliteAffixDefinition(
            ContentId id, string nameKey, string descriptionKey, string sourcePath, ContentTag[] tags,
            ContentTag[] required, ContentTag[] excluded, ContentId modifierOutputId,
            ContentId skillId, ContentId deathRewardId, int maximumGeneration,
            float rewardMultiplier, ContentId presentationProfileId)
            : base(id, nameKey, descriptionKey, sourcePath, tags, presentationProfileId,
                QinglanReferences.Combine(QinglanReferences.Optional(modifierOutputId), QinglanReferences.Optional(skillId), QinglanReferences.Optional(deathRewardId)))
        {
            requiredTags = required == null ? Array.Empty<ContentTag>() : (ContentTag[])required.Clone();
            excludedTags = excluded == null ? Array.Empty<ContentTag>() : (ContentTag[])excluded.Clone();
            requiredTagsView = Array.AsReadOnly(requiredTags);
            excludedTagsView = Array.AsReadOnly(excludedTags);
            ModifierOutputId = modifierOutputId;
            SkillId = skillId;
            DeathRewardId = deathRewardId;
            MaximumGeneration = maximumGeneration;
            RewardMultiplier = rewardMultiplier;
        }

        public override string Kind => RuntimeContentKinds.EliteAffix;
        public IReadOnlyList<ContentTag> RequiredTags => requiredTagsView;
        public IReadOnlyList<ContentTag> ExcludedTags => excludedTagsView;
        public ContentId ModifierOutputId { get; }
        public ContentId SkillId { get; }
        public ContentId DeathRewardId { get; }
        public int MaximumGeneration { get; }
        public float RewardMultiplier { get; }
        protected override void AppendTypeSpecificDeterministicData(StringBuilder builder)
        {
            QinglanReferences.AppendTags(builder, requiredTags);
            QinglanReferences.AppendTags(builder, excludedTags);
            ContentHashUtility.AppendToken(builder, ModifierOutputId.Value);
            ContentHashUtility.AppendToken(builder, SkillId.Value);
            ContentHashUtility.AppendToken(builder, DeathRewardId.Value);
            ContentHashUtility.AppendInt(builder, MaximumGeneration);
            ContentHashUtility.AppendFloat(builder, RewardMultiplier);
            AppendPresentation(builder);
        }
    }

    public abstract class RuntimeMetaDefinition : RuntimeQinglanDefinition
    {
        private readonly ContentId[] prerequisiteIds;
        private readonly ContentId[] outputIds;
        private readonly IReadOnlyList<ContentId> prerequisiteIdsView;
        private readonly IReadOnlyList<ContentId> outputIdsView;

        protected RuntimeMetaDefinition(
            ContentId id, string nameKey, string descriptionKey, string sourcePath, ContentTag[] tags,
            int cost, ContentId[] prerequisites, ContentId[] outputs, ContentId presentationProfileId,
            ContentId[] extraReferences = null)
            : base(id, nameKey, descriptionKey, sourcePath, tags, presentationProfileId,
                QinglanReferences.Combine(prerequisites, outputs, extraReferences))
        {
            Cost = cost;
            prerequisiteIds = QinglanReferences.Clone(prerequisites);
            outputIds = QinglanReferences.Clone(outputs);
            prerequisiteIdsView = Array.AsReadOnly(prerequisiteIds);
            outputIdsView = Array.AsReadOnly(outputIds);
        }

        public int Cost { get; }
        public IReadOnlyList<ContentId> PrerequisiteIds => prerequisiteIdsView;
        public IReadOnlyList<ContentId> OutputIds => outputIdsView;
        protected void AppendMeta(StringBuilder builder)
        {
            ContentHashUtility.AppendInt(builder, Cost);
            QinglanReferences.Append(builder, prerequisiteIds);
            QinglanReferences.Append(builder, outputIds);
            AppendPresentation(builder);
        }
    }

    public sealed class RuntimeMetaNodeDefinition : RuntimeMetaDefinition
    {
        private readonly ContentId[] mutexIds;
        private readonly IReadOnlyList<ContentId> mutexIdsView;
        public RuntimeMetaNodeDefinition(
            ContentId id, string nameKey, string descriptionKey, string sourcePath, ContentTag[] tags,
            MetaNodeKind nodeKind, int cost, ContentId[] prerequisites, ContentId[] mutex,
            ContentId[] outputs, ContentId presentationProfileId)
            : base(id, nameKey, descriptionKey, sourcePath, tags, cost, prerequisites, outputs,
                presentationProfileId, mutex)
        {
            NodeKind = nodeKind;
            mutexIds = QinglanReferences.Clone(mutex);
            mutexIdsView = Array.AsReadOnly(mutexIds);
        }
        public override string Kind => RuntimeContentKinds.MetaNode;
        public MetaNodeKind NodeKind { get; }
        public IReadOnlyList<ContentId> MutuallyExclusiveIds => mutexIdsView;
        protected override void AppendTypeSpecificDeterministicData(StringBuilder builder)
        {
            ContentHashUtility.AppendInt(builder, (int)NodeKind);
            AppendMeta(builder);
            QinglanReferences.Append(builder, mutexIds);
        }
    }

    public sealed class RuntimeMetaInsertDefinition : RuntimeMetaDefinition
    {
        private readonly ContentTag[] slotTags;
        private readonly IReadOnlyList<ContentTag> slotTagsView;
        public RuntimeMetaInsertDefinition(
            ContentId id, string nameKey, string descriptionKey, string sourcePath, ContentTag[] tags,
            int cost, ContentTag[] allowedSlotTags, ContentId[] outputs, ContentId presentationProfileId)
            : base(id, nameKey, descriptionKey, sourcePath, tags, cost, null, outputs, presentationProfileId)
        {
            slotTags = allowedSlotTags == null ? Array.Empty<ContentTag>() : (ContentTag[])allowedSlotTags.Clone();
            slotTagsView = Array.AsReadOnly(slotTags);
        }
        public override string Kind => RuntimeContentKinds.MetaInsert;
        public IReadOnlyList<ContentTag> SlotTags => slotTagsView;
        protected override void AppendTypeSpecificDeterministicData(StringBuilder builder)
        {
            AppendMeta(builder);
            QinglanReferences.AppendTags(builder, slotTags);
        }
    }

    public sealed class RuntimeMetaFacilityDefinition : RuntimeMetaDefinition
    {
        public RuntimeMetaFacilityDefinition(
            ContentId id, string nameKey, string descriptionKey, string sourcePath, ContentTag[] tags,
            ContentId unlockConditionId, ContentId pageProfileId, ContentId presentationProfileId)
            : base(id, nameKey, descriptionKey, sourcePath, tags, 0,
                QinglanReferences.Optional(unlockConditionId), null, presentationProfileId)
        {
            UnlockConditionId = unlockConditionId;
            PageProfileId = pageProfileId;
        }
        public override string Kind => RuntimeContentKinds.MetaFacility;
        public ContentId UnlockConditionId { get; }
        public ContentId PageProfileId { get; }
        protected override void AppendTypeSpecificDeterministicData(StringBuilder builder)
        {
            AppendMeta(builder);
            ContentHashUtility.AppendToken(builder, UnlockConditionId.Value);
            ContentHashUtility.AppendToken(builder, PageProfileId.Value);
        }
    }

    public sealed class RuntimeStoryDefinition : RuntimeQinglanDefinition
    {
        private readonly string[] sequenceKeys;
        private readonly IReadOnlyList<string> sequenceKeysView;
        public RuntimeStoryDefinition(
            ContentId id, string nameKey, string descriptionKey, string sourcePath, ContentTag[] tags,
            string[] localizedSequenceKeys, ContentId unlockConditionId, string uniqueKey,
            ContentId presentationProfileId)
            : base(id, nameKey, descriptionKey, sourcePath, tags, presentationProfileId,
                QinglanReferences.Optional(unlockConditionId))
        {
            sequenceKeys = localizedSequenceKeys == null ? Array.Empty<string>() : (string[])localizedSequenceKeys.Clone();
            sequenceKeysView = Array.AsReadOnly(sequenceKeys);
            UnlockConditionId = unlockConditionId;
            UniqueKey = uniqueKey ?? string.Empty;
        }
        public override string Kind => RuntimeContentKinds.Story;
        public IReadOnlyList<string> SequenceKeys => sequenceKeysView;
        public ContentId UnlockConditionId { get; }
        public string UniqueKey { get; }
        protected override void AppendTypeSpecificDeterministicData(StringBuilder builder)
        {
            ContentHashUtility.AppendInt(builder, sequenceKeys.Length);
            for (var index = 0; index < sequenceKeys.Length; index++) ContentHashUtility.AppendToken(builder, sequenceKeys[index]);
            ContentHashUtility.AppendToken(builder, UnlockConditionId.Value);
            ContentHashUtility.AppendToken(builder, UniqueKey);
            AppendPresentation(builder);
        }
    }

    public sealed class RuntimeCollectibleDefinition : RuntimeQinglanDefinition
    {
        public RuntimeCollectibleDefinition(
            ContentId id, string nameKey, string descriptionKey, string sourcePath, ContentTag[] tags,
            ContentId topicId, ContentId acquireRuleId, string bodyLocalizationKey,
            ContentId fallbackRewardId, ContentId presentationProfileId)
            : base(id, nameKey, descriptionKey, sourcePath, tags, presentationProfileId,
                QinglanReferences.Combine(QinglanReferences.Optional(acquireRuleId), QinglanReferences.Optional(fallbackRewardId)))
        {
            TopicId = topicId;
            AcquireRuleId = acquireRuleId;
            BodyLocalizationKey = bodyLocalizationKey ?? string.Empty;
            FallbackRewardId = fallbackRewardId;
        }
        public override string Kind => RuntimeContentKinds.Collectible;
        public ContentId TopicId { get; }
        public ContentId AcquireRuleId { get; }
        public string BodyLocalizationKey { get; }
        public ContentId FallbackRewardId { get; }
        protected override void AppendTypeSpecificDeterministicData(StringBuilder builder)
        {
            ContentHashUtility.AppendToken(builder, TopicId.Value);
            ContentHashUtility.AppendToken(builder, AcquireRuleId.Value);
            ContentHashUtility.AppendToken(builder, BodyLocalizationKey);
            ContentHashUtility.AppendToken(builder, FallbackRewardId.Value);
            AppendPresentation(builder);
        }
    }

    internal static class QinglanReferences
    {
        public static ContentId[] Clone(ContentId[] source) => source == null ? Array.Empty<ContentId>() : (ContentId[])source.Clone();

        public static ContentId[] Optional(ContentId value) => value.IsValid ? new[] { value } : Array.Empty<ContentId>();

        public static ContentId[] Combine(params ContentId[][] sources)
        {
            var count = 0;
            if (sources != null)
                for (var sourceIndex = 0; sourceIndex < sources.Length; sourceIndex++)
                    if (sources[sourceIndex] != null) count += sources[sourceIndex].Length;
            if (count == 0) return Array.Empty<ContentId>();
            var output = new ContentId[count];
            var destination = 0;
            for (var sourceIndex = 0; sourceIndex < sources.Length; sourceIndex++)
            {
                var source = sources[sourceIndex];
                if (source == null) continue;
                for (var index = 0; index < source.Length; index++) output[destination++] = source[index];
            }
            return output;
        }

        public static ContentId[] FromMechanicTiers(CharacterMechanicTier[] tiers)
        {
            if (tiers == null || tiers.Length == 0) return Array.Empty<ContentId>();
            var output = new ContentId[tiers.Length];
            for (var index = 0; index < tiers.Length; index++) output[index] = tiers[index].OutputId;
            return output;
        }

        public static ContentId[] FromRewardOperations(RewardOperation[] operations, ContentId fallback)
        {
            var count = fallback.IsValid ? 1 : 0;
            if (operations != null)
                for (var index = 0; index < operations.Length; index++) if (operations[index].ReferenceId.IsValid) count++;
            var output = new ContentId[count];
            var destination = 0;
            if (operations != null)
                for (var index = 0; index < operations.Length; index++)
                    if (operations[index].ReferenceId.IsValid) output[destination++] = operations[index].ReferenceId;
            if (fallback.IsValid) output[destination] = fallback;
            return output;
        }

        public static ContentId[] FromBoss(ContentId enemyId, ContentId rewardId, RuntimeBossPhase[] phases)
        {
            var count = (enemyId.IsValid ? 1 : 0) + (rewardId.IsValid ? 1 : 0);
            if (phases != null)
                for (var index = 0; index < phases.Length; index++) count += phases[index].AcceptedRuleIds.Count;
            var output = new ContentId[count];
            var destination = 0;
            if (enemyId.IsValid) output[destination++] = enemyId;
            if (phases != null)
                for (var index = 0; index < phases.Length; index++)
                    for (var rule = 0; rule < phases[index].AcceptedRuleIds.Count; rule++) output[destination++] = phases[index].AcceptedRuleIds[rule];
            if (rewardId.IsValid) output[destination] = rewardId;
            return output;
        }

        public static void Append(StringBuilder builder, ContentId[] values)
        {
            ContentHashUtility.AppendInt(builder, values.Length);
            for (var index = 0; index < values.Length; index++) ContentHashUtility.AppendToken(builder, values[index].Value);
        }

        public static void AppendTags(StringBuilder builder, ContentTag[] values)
        {
            ContentHashUtility.AppendInt(builder, values.Length);
            for (var index = 0; index < values.Length; index++) ContentHashUtility.AppendToken(builder, values[index].Value);
        }
    }
}
