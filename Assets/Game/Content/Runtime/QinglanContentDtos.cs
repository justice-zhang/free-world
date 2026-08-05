using System;
using Game.Core;

namespace Game.Content.Runtime
{
    [Serializable]
    public sealed class QinglanMechanicTierDto
    {
        public float threshold;
        public string outputId;
    }

    [Serializable]
    public sealed class QinglanRewardOperationDto
    {
        public int code;
        public float value;
        public int integerValue;
        public string referenceId;
        public string eligibilityTag;
    }

    [Serializable]
    public sealed class QinglanStateTransitionDto
    {
        public int from;
        public int to;
    }

    [Serializable]
    public sealed class QinglanBossPhaseDto
    {
        public float healthThreshold;
        public string[] acceptedRuleIds;
        public int cleanupPolicy;
    }

    /// <summary>Schema-6 payload shared by Qinglan's fourteen explicit definition kinds.</summary>
    [Serializable]
    public sealed class QinglanRuntimeDefinitionDto
    {
        public string presentationProfileId;
        public string resourceId;
        public float value0;
        public float value1;
        public int int0;
        public int enum0;
        public bool bool0;
        public string text0;
        public string referenceId0;
        public string referenceId1;
        public string referenceId2;
        public string[] references0;
        public string[] references1;
        public string[] references2;
        public string[] tags0;
        public string[] tags1;
        public string[] localizedSequenceKeys;
        public QinglanMechanicTierDto[] mechanicTiers;
        public QinglanRewardOperationDto[] rewardOperations;
        public QinglanStateTransitionDto[] stateTransitions;
        public QinglanBossPhaseDto[] bossPhases;

        public Result<RuntimeContentDefinition> ToDefinition(
            string kind,
            ContentId packId,
            ContentId id,
            string nameKey,
            string descriptionKey,
            string sourcePath,
            ContentTag[] tags)
        {
            var presentationResult = ParseOptionalId(presentationProfileId, packId, id, sourcePath, "presentation profile");
            if (!presentationResult.IsSuccess) return Result<RuntimeContentDefinition>.Failure(presentationResult.Error);
            var presentation = presentationResult.Value;

            switch (kind)
            {
                case RuntimeContentKinds.CharacterMechanic:
                    return ToCharacterMechanic(packId, id, nameKey, descriptionKey, sourcePath, tags, presentation);
                case RuntimeContentKinds.Reward:
                    return ToReward(packId, id, nameKey, descriptionKey, sourcePath, tags, presentation);
                case RuntimeContentKinds.Pickup:
                {
                    var reward = ParseRequiredId(referenceId0, packId, id, sourcePath, "pickup reward");
                    var eligibility = ParseOptionalTag(tags0, packId, id, sourcePath, "pickup eligibility");
                    if (!reward.IsSuccess) return Result<RuntimeContentDefinition>.Failure(reward.Error);
                    if (!eligibility.IsSuccess) return Result<RuntimeContentDefinition>.Failure(eligibility.Error);
                    return Result<RuntimeContentDefinition>.Success(new RuntimePickupDefinition(
                        id, nameKey, descriptionKey, sourcePath, tags, reward.Value, value0, value1,
                        eligibility.Value, presentation));
                }
                case RuntimeContentKinds.Relic:
                    return ToRelic(packId, id, nameKey, descriptionKey, sourcePath, tags, presentation);
                case RuntimeContentKinds.MapObjective:
                case RuntimeContentKinds.MapEvent:
                    return ToStateGraph(kind, packId, id, nameKey, descriptionKey, sourcePath, tags, presentation);
                case RuntimeContentKinds.Landmark:
                    return ToLandmark(packId, id, nameKey, descriptionKey, sourcePath, tags, presentation);
                case RuntimeContentKinds.Boss:
                    return ToBoss(packId, id, nameKey, descriptionKey, sourcePath, tags, presentation);
                case RuntimeContentKinds.EliteAffix:
                    return ToEliteAffix(packId, id, nameKey, descriptionKey, sourcePath, tags, presentation);
                case RuntimeContentKinds.MetaNode:
                    return ToMetaNode(packId, id, nameKey, descriptionKey, sourcePath, tags, presentation);
                case RuntimeContentKinds.MetaInsert:
                    return ToMetaInsert(packId, id, nameKey, descriptionKey, sourcePath, tags, presentation);
                case RuntimeContentKinds.MetaFacility:
                    return ToMetaFacility(packId, id, nameKey, descriptionKey, sourcePath, tags, presentation);
                case RuntimeContentKinds.Story:
                    return ToStory(packId, id, nameKey, descriptionKey, sourcePath, tags, presentation);
                case RuntimeContentKinds.Collectible:
                    return ToCollectible(packId, id, nameKey, descriptionKey, sourcePath, tags, presentation);
                default:
                    return Failure("Unsupported schema-6 kind '" + (kind ?? string.Empty) + "'.", packId, id, sourcePath);
            }
        }

        internal static QinglanRuntimeDefinitionDto FromDefinition(RuntimeQinglanDefinition definition)
        {
            var dto = new QinglanRuntimeDefinitionDto
            {
                presentationProfileId = definition.PresentationProfileId.Value,
                resourceId = string.Empty,
                text0 = string.Empty,
                referenceId0 = string.Empty,
                referenceId1 = string.Empty,
                referenceId2 = string.Empty,
                references0 = Array.Empty<string>(),
                references1 = Array.Empty<string>(),
                references2 = Array.Empty<string>(),
                tags0 = Array.Empty<string>(),
                tags1 = Array.Empty<string>(),
                localizedSequenceKeys = Array.Empty<string>(),
                mechanicTiers = Array.Empty<QinglanMechanicTierDto>(),
                rewardOperations = Array.Empty<QinglanRewardOperationDto>(),
                stateTransitions = Array.Empty<QinglanStateTransitionDto>(),
                bossPhases = Array.Empty<QinglanBossPhaseDto>()
            };

            if (definition is RuntimeCharacterMechanicDefinition mechanic)
            {
                dto.resourceId = mechanic.ResourceId.Value;
                dto.value0 = mechanic.GainPerUnit;
                dto.value1 = mechanic.LossOnDamage;
                dto.mechanicTiers = new QinglanMechanicTierDto[mechanic.Tiers.Count];
                for (var index = 0; index < mechanic.Tiers.Count; index++)
                    dto.mechanicTiers[index] = new QinglanMechanicTierDto { threshold = mechanic.Tiers[index].Threshold, outputId = mechanic.Tiers[index].OutputId.Value };
            }
            else if (definition is RuntimeRewardDefinition reward)
            {
                dto.enum0 = (int)reward.RepeatPolicy;
                dto.referenceId0 = reward.FallbackRewardId.Value;
                dto.text0 = reward.UniqueKey;
                dto.rewardOperations = new QinglanRewardOperationDto[reward.Operations.Count];
                for (var index = 0; index < reward.Operations.Count; index++)
                {
                    var operation = reward.Operations[index];
                    dto.rewardOperations[index] = new QinglanRewardOperationDto
                    {
                        code = (int)operation.Code,
                        value = operation.Value,
                        integerValue = operation.IntegerValue,
                        referenceId = operation.ReferenceId.Value,
                        eligibilityTag = operation.EligibilityTag.Value
                    };
                }
            }
            else if (definition is RuntimePickupDefinition pickup)
            {
                dto.referenceId0 = pickup.RewardId.Value;
                dto.value0 = pickup.Radius;
                dto.value1 = pickup.LifetimeSeconds;
                dto.tags0 = pickup.EligibilityTag.IsValid ? new[] { pickup.EligibilityTag.Value } : Array.Empty<string>();
            }
            else if (definition is RuntimeRelicDefinition relic)
            {
                dto.int0 = relic.MaximumLevel;
                dto.references0 = ToStrings(relic.OutputIds);
                dto.references1 = ToStrings(relic.PrerequisiteIds);
                dto.references2 = ToStrings(relic.MutuallyExclusiveIds);
            }
            else if (definition is RuntimeStateGraphDefinition graph)
            {
                dto.references0 = ToStrings(graph.AnchorIds);
                dto.referenceId0 = graph.OutputId.Value;
                dto.stateTransitions = new QinglanStateTransitionDto[graph.StateTransitions.Count];
                for (var index = 0; index < graph.StateTransitions.Count; index++)
                    dto.stateTransitions[index] = new QinglanStateTransitionDto { from = (int)graph.StateTransitions[index].From, to = (int)graph.StateTransitions[index].To };
                if (definition is RuntimeMapEventDefinition mapEvent)
                {
                    dto.value0 = mapEvent.TriggerStartSeconds;
                    dto.value1 = mapEvent.TriggerEndSeconds;
                }
            }
            else if (definition is RuntimeLandmarkDefinition landmark)
            {
                dto.referenceId0 = landmark.AnchorId.Value;
                dto.referenceId1 = landmark.RewardId.Value;
                dto.referenceId2 = landmark.StoryId.Value;
                dto.bool0 = landmark.Repeatable;
            }
            else if (definition is RuntimeBossDefinition boss)
            {
                dto.referenceId0 = boss.EnemyId.Value;
                dto.referenceId1 = boss.RewardId.Value;
                dto.value0 = boss.ResistanceMultiplier;
                dto.bossPhases = new QinglanBossPhaseDto[boss.Phases.Count];
                for (var index = 0; index < boss.Phases.Count; index++)
                    dto.bossPhases[index] = new QinglanBossPhaseDto { healthThreshold = boss.Phases[index].HealthThreshold, acceptedRuleIds = ToStrings(boss.Phases[index].AcceptedRuleIds), cleanupPolicy = (int)boss.Phases[index].CleanupPolicy };
            }
            else if (definition is RuntimeEliteAffixDefinition affix)
            {
                dto.value0 = affix.RewardMultiplier;
                dto.int0 = affix.MaximumGeneration;
                dto.tags0 = ToTagStrings(affix.RequiredTags);
                dto.tags1 = ToTagStrings(affix.ExcludedTags);
                dto.referenceId0 = affix.ModifierOutputId.Value;
                dto.referenceId1 = affix.SkillId.Value;
                dto.referenceId2 = affix.DeathRewardId.Value;
            }
            else if (definition is RuntimeMetaNodeDefinition node)
            {
                dto.enum0 = (int)node.NodeKind;
                dto.int0 = node.Cost;
                dto.references0 = ToStrings(node.PrerequisiteIds);
                dto.references1 = ToStrings(node.OutputIds);
                dto.references2 = ToStrings(node.MutuallyExclusiveIds);
            }
            else if (definition is RuntimeMetaInsertDefinition insert)
            {
                dto.int0 = insert.Cost;
                dto.references1 = ToStrings(insert.OutputIds);
                dto.tags0 = ToTagStrings(insert.SlotTags);
            }
            else if (definition is RuntimeMetaFacilityDefinition facility)
            {
                dto.referenceId0 = facility.UnlockConditionId.Value;
                dto.referenceId1 = facility.PageProfileId.Value;
            }
            else if (definition is RuntimeStoryDefinition story)
            {
                dto.localizedSequenceKeys = ToStringValues(story.SequenceKeys);
                dto.referenceId0 = story.UnlockConditionId.Value;
                dto.text0 = story.UniqueKey;
            }
            else if (definition is RuntimeCollectibleDefinition collectible)
            {
                dto.referenceId0 = collectible.TopicId.Value;
                dto.referenceId1 = collectible.AcquireRuleId.Value;
                dto.referenceId2 = collectible.FallbackRewardId.Value;
                dto.text0 = collectible.BodyLocalizationKey;
            }
            else
            {
                throw new ArgumentException("Unsupported schema-6 definition type " + definition.GetType().FullName + ".", nameof(definition));
            }

            return dto;
        }

        private Result<RuntimeContentDefinition> ToCharacterMechanic(ContentId packId, ContentId id, string name, string description, string path, ContentTag[] tags, ContentId presentation)
        {
            var resource = ParseRequiredId(resourceId, packId, id, path, "mechanic resource");
            if (!resource.IsSuccess) return Result<RuntimeContentDefinition>.Failure(resource.Error);
            var source = mechanicTiers ?? Array.Empty<QinglanMechanicTierDto>();
            var tiers = new CharacterMechanicTier[source.Length];
            for (var index = 0; index < source.Length; index++)
            {
                if (source[index] == null) return Failure("Mechanic tier is null.", packId, id, path);
                var output = ParseRequiredId(source[index].outputId, packId, id, path, "mechanic tier output");
                if (!output.IsSuccess) return Result<RuntimeContentDefinition>.Failure(output.Error);
                tiers[index] = new CharacterMechanicTier(source[index].threshold, output.Value);
            }
            return Result<RuntimeContentDefinition>.Success(new RuntimeCharacterMechanicDefinition(
                id, name, description, path, tags, resource.Value, value0, value1, tiers, presentation));
        }

        private Result<RuntimeContentDefinition> ToReward(ContentId packId, ContentId id, string name, string description, string path, ContentTag[] tags, ContentId presentation)
        {
            var fallback = ParseOptionalId(referenceId0, packId, id, path, "fallback reward");
            if (!fallback.IsSuccess) return Result<RuntimeContentDefinition>.Failure(fallback.Error);
            var source = rewardOperations ?? Array.Empty<QinglanRewardOperationDto>();
            var operations = new RewardOperation[source.Length];
            for (var index = 0; index < source.Length; index++)
            {
                if (source[index] == null) return Failure("Reward operation is null.", packId, id, path);
                var reference = ParseOptionalId(source[index].referenceId, packId, id, path, "reward operation reference");
                var tag = ParseOptionalTag(source[index].eligibilityTag, packId, id, path, "reward operation tag");
                if (!reference.IsSuccess) return Result<RuntimeContentDefinition>.Failure(reference.Error);
                if (!tag.IsSuccess) return Result<RuntimeContentDefinition>.Failure(tag.Error);
                operations[index] = new RewardOperation((RewardOperationCode)source[index].code, source[index].value, source[index].integerValue, reference.Value, tag.Value);
            }
            return Result<RuntimeContentDefinition>.Success(new RuntimeRewardDefinition(
                id, name, description, path, tags, operations, (RewardRepeatPolicy)enum0, fallback.Value, text0, presentation));
        }

        private Result<RuntimeContentDefinition> ToRelic(ContentId packId, ContentId id, string name, string description, string path, ContentTag[] tags, ContentId presentation)
        {
            var outputs = ParseIds(references0, packId, id, path, "relic output");
            var prerequisites = ParseIds(references1, packId, id, path, "relic prerequisite");
            var mutex = ParseIds(references2, packId, id, path, "relic mutex");
            if (!outputs.IsSuccess) return Result<RuntimeContentDefinition>.Failure(outputs.Error);
            if (!prerequisites.IsSuccess) return Result<RuntimeContentDefinition>.Failure(prerequisites.Error);
            if (!mutex.IsSuccess) return Result<RuntimeContentDefinition>.Failure(mutex.Error);
            return Result<RuntimeContentDefinition>.Success(new RuntimeRelicDefinition(
                id, name, description, path, tags, int0, outputs.Value, prerequisites.Value, mutex.Value, presentation));
        }

        private Result<RuntimeContentDefinition> ToStateGraph(string kind, ContentId packId, ContentId id, string name, string description, string path, ContentTag[] tags, ContentId presentation)
        {
            var anchors = ParseIds(references0, packId, id, path, "state graph anchor");
            var output = ParseOptionalId(referenceId0, packId, id, path, "state graph output");
            if (!anchors.IsSuccess) return Result<RuntimeContentDefinition>.Failure(anchors.Error);
            if (!output.IsSuccess) return Result<RuntimeContentDefinition>.Failure(output.Error);
            var source = stateTransitions ?? Array.Empty<QinglanStateTransitionDto>();
            var transitions = new ObjectiveStateTransition[source.Length];
            for (var index = 0; index < source.Length; index++)
            {
                if (source[index] == null) return Failure("State transition is null.", packId, id, path);
                transitions[index] = new ObjectiveStateTransition((ObjectiveState)source[index].from, (ObjectiveState)source[index].to);
            }
            if (kind == RuntimeContentKinds.MapObjective)
                return Result<RuntimeContentDefinition>.Success(new RuntimeMapObjectiveDefinition(id, name, description, path, tags, anchors.Value, transitions, output.Value, presentation));
            return Result<RuntimeContentDefinition>.Success(new RuntimeMapEventDefinition(id, name, description, path, tags, anchors.Value, transitions, value0, value1, output.Value, presentation));
        }

        private Result<RuntimeContentDefinition> ToLandmark(ContentId packId, ContentId id, string name, string description, string path, ContentTag[] tags, ContentId presentation)
        {
            var anchor = ParseRequiredId(referenceId0, packId, id, path, "landmark anchor");
            var reward = ParseOptionalId(referenceId1, packId, id, path, "landmark reward");
            var story = ParseOptionalId(referenceId2, packId, id, path, "landmark story");
            if (!anchor.IsSuccess) return Result<RuntimeContentDefinition>.Failure(anchor.Error);
            if (!reward.IsSuccess) return Result<RuntimeContentDefinition>.Failure(reward.Error);
            if (!story.IsSuccess) return Result<RuntimeContentDefinition>.Failure(story.Error);
            return Result<RuntimeContentDefinition>.Success(new RuntimeLandmarkDefinition(id, name, description, path, tags, anchor.Value, reward.Value, story.Value, bool0, presentation));
        }

        private Result<RuntimeContentDefinition> ToBoss(ContentId packId, ContentId id, string name, string description, string path, ContentTag[] tags, ContentId presentation)
        {
            var enemy = ParseRequiredId(referenceId0, packId, id, path, "boss enemy");
            var reward = ParseOptionalId(referenceId1, packId, id, path, "boss reward");
            if (!enemy.IsSuccess) return Result<RuntimeContentDefinition>.Failure(enemy.Error);
            if (!reward.IsSuccess) return Result<RuntimeContentDefinition>.Failure(reward.Error);
            var source = bossPhases ?? Array.Empty<QinglanBossPhaseDto>();
            var phases = new RuntimeBossPhase[source.Length];
            for (var index = 0; index < source.Length; index++)
            {
                if (source[index] == null) return Failure("Boss phase is null.", packId, id, path);
                var rules = ParseIds(source[index].acceptedRuleIds, packId, id, path, "boss phase rule");
                if (!rules.IsSuccess) return Result<RuntimeContentDefinition>.Failure(rules.Error);
                phases[index] = new RuntimeBossPhase(source[index].healthThreshold, rules.Value, (BossPhaseCleanupPolicy)source[index].cleanupPolicy);
            }
            return Result<RuntimeContentDefinition>.Success(new RuntimeBossDefinition(id, name, description, path, tags, enemy.Value, phases, reward.Value, value0, presentation));
        }

        private Result<RuntimeContentDefinition> ToEliteAffix(ContentId packId, ContentId id, string name, string description, string path, ContentTag[] tags, ContentId presentation)
        {
            var required = ParseTags(tags0, packId, id, path, "affix required tag");
            var excluded = ParseTags(tags1, packId, id, path, "affix excluded tag");
            var modifier = ParseOptionalId(referenceId0, packId, id, path, "affix modifier output");
            var skill = ParseOptionalId(referenceId1, packId, id, path, "affix skill");
            var reward = ParseOptionalId(referenceId2, packId, id, path, "affix death reward");
            if (!required.IsSuccess) return Result<RuntimeContentDefinition>.Failure(required.Error);
            if (!excluded.IsSuccess) return Result<RuntimeContentDefinition>.Failure(excluded.Error);
            if (!modifier.IsSuccess) return Result<RuntimeContentDefinition>.Failure(modifier.Error);
            if (!skill.IsSuccess) return Result<RuntimeContentDefinition>.Failure(skill.Error);
            if (!reward.IsSuccess) return Result<RuntimeContentDefinition>.Failure(reward.Error);
            return Result<RuntimeContentDefinition>.Success(new RuntimeEliteAffixDefinition(
                id, name, description, path, tags, required.Value, excluded.Value,
                modifier.Value, skill.Value, reward.Value, int0,
                value0 > 0f ? value0 : 1f, presentation));
        }

        private Result<RuntimeContentDefinition> ToMetaNode(ContentId packId, ContentId id, string name, string description, string path, ContentTag[] tags, ContentId presentation)
        {
            var prerequisites = ParseIds(references0, packId, id, path, "meta node prerequisite");
            var outputs = ParseIds(references1, packId, id, path, "meta node output");
            var mutex = ParseIds(references2, packId, id, path, "meta node mutex");
            if (!prerequisites.IsSuccess) return Result<RuntimeContentDefinition>.Failure(prerequisites.Error);
            if (!outputs.IsSuccess) return Result<RuntimeContentDefinition>.Failure(outputs.Error);
            if (!mutex.IsSuccess) return Result<RuntimeContentDefinition>.Failure(mutex.Error);
            return Result<RuntimeContentDefinition>.Success(new RuntimeMetaNodeDefinition(id, name, description, path, tags, (MetaNodeKind)enum0, int0, prerequisites.Value, mutex.Value, outputs.Value, presentation));
        }

        private Result<RuntimeContentDefinition> ToMetaInsert(ContentId packId, ContentId id, string name, string description, string path, ContentTag[] tags, ContentId presentation)
        {
            var outputs = ParseIds(references1, packId, id, path, "meta insert output");
            var slots = ParseTags(tags0, packId, id, path, "meta insert slot tag");
            if (!outputs.IsSuccess) return Result<RuntimeContentDefinition>.Failure(outputs.Error);
            if (!slots.IsSuccess) return Result<RuntimeContentDefinition>.Failure(slots.Error);
            return Result<RuntimeContentDefinition>.Success(new RuntimeMetaInsertDefinition(id, name, description, path, tags, int0, slots.Value, outputs.Value, presentation));
        }

        private Result<RuntimeContentDefinition> ToMetaFacility(ContentId packId, ContentId id, string name, string description, string path, ContentTag[] tags, ContentId presentation)
        {
            var unlock = ParseOptionalId(referenceId0, packId, id, path, "facility unlock condition");
            var page = ParseRequiredId(referenceId1, packId, id, path, "facility page profile");
            if (!unlock.IsSuccess) return Result<RuntimeContentDefinition>.Failure(unlock.Error);
            if (!page.IsSuccess) return Result<RuntimeContentDefinition>.Failure(page.Error);
            return Result<RuntimeContentDefinition>.Success(new RuntimeMetaFacilityDefinition(id, name, description, path, tags, unlock.Value, page.Value, presentation));
        }

        private Result<RuntimeContentDefinition> ToStory(ContentId packId, ContentId id, string name, string description, string path, ContentTag[] tags, ContentId presentation)
        {
            var unlock = ParseOptionalId(referenceId0, packId, id, path, "story unlock condition");
            if (!unlock.IsSuccess) return Result<RuntimeContentDefinition>.Failure(unlock.Error);
            return Result<RuntimeContentDefinition>.Success(new RuntimeStoryDefinition(id, name, description, path, tags, localizedSequenceKeys, unlock.Value, text0, presentation));
        }

        private Result<RuntimeContentDefinition> ToCollectible(ContentId packId, ContentId id, string name, string description, string path, ContentTag[] tags, ContentId presentation)
        {
            var topic = ParseRequiredId(referenceId0, packId, id, path, "collectible topic");
            var acquire = ParseOptionalId(referenceId1, packId, id, path, "collectible acquire rule");
            var fallback = ParseOptionalId(referenceId2, packId, id, path, "collectible fallback reward");
            if (!topic.IsSuccess) return Result<RuntimeContentDefinition>.Failure(topic.Error);
            if (!acquire.IsSuccess) return Result<RuntimeContentDefinition>.Failure(acquire.Error);
            if (!fallback.IsSuccess) return Result<RuntimeContentDefinition>.Failure(fallback.Error);
            return Result<RuntimeContentDefinition>.Success(new RuntimeCollectibleDefinition(id, name, description, path, tags, topic.Value, acquire.Value, text0, fallback.Value, presentation));
        }

        private static Result<ContentId> ParseRequiredId(string value, ContentId packId, ContentId ownerId, string path, string label)
        {
            if (string.IsNullOrEmpty(value)) return Result<ContentId>.Failure(new Error(ErrorCode.InvalidCatalog, label + " is required.", ownerId, packId, path));
            return CatalogDtoParsing.ParseCanonicalId(value, packId, path, label);
        }

        private static Result<ContentId> ParseOptionalId(string value, ContentId packId, ContentId ownerId, string path, string label)
        {
            return string.IsNullOrEmpty(value) ? Result<ContentId>.Success(default) : ParseRequiredId(value, packId, ownerId, path, label);
        }

        private static Result<ContentId[]> ParseIds(string[] values, ContentId packId, ContentId ownerId, string path, string label)
        {
            var source = values ?? Array.Empty<string>();
            var output = new ContentId[source.Length];
            for (var index = 0; index < source.Length; index++)
            {
                var parsed = ParseRequiredId(source[index], packId, ownerId, path, label);
                if (!parsed.IsSuccess) return Result<ContentId[]>.Failure(parsed.Error);
                output[index] = parsed.Value;
            }
            return Result<ContentId[]>.Success(output);
        }

        private static Result<ContentTag[]> ParseTags(string[] values, ContentId packId, ContentId ownerId, string path, string label)
        {
            return CatalogDtoParsing.ParseTags(values, packId, ownerId, path);
        }

        private static Result<ContentTag> ParseOptionalTag(string[] values, ContentId packId, ContentId ownerId, string path, string label)
        {
            var source = values ?? Array.Empty<string>();
            if (source.Length == 0) return Result<ContentTag>.Success(default);
            if (source.Length != 1) return Result<ContentTag>.Failure(new Error(ErrorCode.InvalidCatalog, label + " admits at most one tag.", ownerId, packId, path));
            return ParseOptionalTag(source[0], packId, ownerId, path, label);
        }

        private static Result<ContentTag> ParseOptionalTag(string value, ContentId packId, ContentId ownerId, string path, string label)
        {
            if (string.IsNullOrEmpty(value)) return Result<ContentTag>.Success(default);
            if (!ContentId.IsCanonical(value)) return Result<ContentTag>.Failure(new Error(ErrorCode.InvalidCatalog, label + " must be canonical.", ownerId, packId, path));
            return ContentTag.Create(value, packId, path);
        }

        private static Result<RuntimeContentDefinition> Failure(string message, ContentId packId, ContentId ownerId, string path)
        {
            return Result<RuntimeContentDefinition>.Failure(new Error(ErrorCode.InvalidCatalog, message, ownerId, packId, path));
        }

        private static string[] ToStringValues(System.Collections.Generic.IReadOnlyList<string> values)
        {
            var output = new string[values.Count];
            for (var index = 0; index < values.Count; index++) output[index] = values[index] ?? string.Empty;
            return output;
        }

        private static string[] ToStrings(System.Collections.Generic.IReadOnlyList<ContentId> values)
        {
            var output = new string[values.Count];
            for (var index = 0; index < values.Count; index++) output[index] = values[index].Value;
            return output;
        }

        private static string[] ToTagStrings(System.Collections.Generic.IReadOnlyList<ContentTag> values)
        {
            var output = new string[values.Count];
            for (var index = 0; index < values.Count; index++) output[index] = values[index].Value;
            return output;
        }
    }
}
