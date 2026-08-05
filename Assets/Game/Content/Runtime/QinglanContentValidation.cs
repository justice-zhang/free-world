using System;
using System.Collections.Generic;
using Game.Core;

namespace Game.Content.Runtime
{
    internal static class QinglanContentValidation
    {
        public static bool IsDefinition(RuntimeContentDefinition definition) => definition is RuntimeQinglanDefinition;

        public static string ValidateValues(RuntimeContentDefinition definition)
        {
            if (definition is RuntimeCharacterMechanicDefinition mechanic)
            {
                if (!mechanic.ResourceId.IsValid || !FiniteNonNegative(mechanic.GainPerUnit) ||
                    !FiniteNonNegative(mechanic.LossOnDamage) || mechanic.Tiers.Count == 0)
                    return "Character mechanic resource, gain, loss, and tiers must be valid.";
                var previous = -1f;
                for (var index = 0; index < mechanic.Tiers.Count; index++)
                {
                    if (!FiniteNonNegative(mechanic.Tiers[index].Threshold) ||
                        mechanic.Tiers[index].Threshold <= previous || !mechanic.Tiers[index].OutputId.IsValid)
                        return "Character mechanic thresholds must be finite, strictly increasing, and have outputs.";
                    previous = mechanic.Tiers[index].Threshold;
                }
            }
            else if (definition is RuntimeRewardDefinition reward)
            {
                if (reward.Operations.Count == 0 || reward.RepeatPolicy < RewardRepeatPolicy.OncePerTransaction ||
                    reward.RepeatPolicy > RewardRepeatPolicy.Repeatable)
                    return "Reward operations and repeat policy must be valid.";
                if (reward.RepeatPolicy == RewardRepeatPolicy.OncePerRun &&
                    string.IsNullOrWhiteSpace(reward.UniqueKey))
                    return "Once-per-run rewards require a unique key.";
                for (var index = 0; index < reward.Operations.Count; index++)
                {
                    var operation = reward.Operations[index];
                    if (operation.Code < RewardOperationCode.Heal || operation.Code > RewardOperationCode.TriggerStory ||
                        !Finite(operation.Value) || operation.IntegerValue < 0)
                        return "Reward operation code and operands must be valid.";
                    if (RequiresReference(operation.Code) && !operation.ReferenceId.IsValid)
                        return "Reward operation requires a stable content reference.";
                    if (operation.Code == RewardOperationCode.GrantUnique &&
                        string.IsNullOrWhiteSpace(reward.UniqueKey))
                        return "GrantUnique rewards require a unique key.";
                }
            }
            else if (definition is RuntimePickupDefinition pickup)
            {
                if (!pickup.RewardId.IsValid || !FinitePositive(pickup.Radius) || !FinitePositive(pickup.LifetimeSeconds))
                    return "Pickup reward, radius, and lifetime must be valid.";
            }
            else if (definition is RuntimeRelicDefinition relic)
            {
                if (relic.MaximumLevel < 1 || relic.OutputIds.Count == 0)
                    return "Relic maximum level and outputs must be valid.";
                if (Contains(relic.PrerequisiteIds, relic.Id) ||
                    Contains(relic.MutuallyExclusiveIds, relic.Id) ||
                    HasDuplicate(relic.PrerequisiteIds) ||
                    HasDuplicate(relic.MutuallyExclusiveIds))
                    return "Relic prerequisites and mutex IDs must be unique and cannot reference self.";
            }
            else if (definition is RuntimeStateGraphDefinition graph)
            {
                var message = ValidateStateGraph(graph);
                if (message != null) return message;
                if (definition is RuntimeMapEventDefinition mapEvent &&
                    (!FiniteNonNegative(mapEvent.TriggerStartSeconds) || !Finite(mapEvent.TriggerEndSeconds) ||
                     mapEvent.TriggerEndSeconds <= mapEvent.TriggerStartSeconds))
                    return "Map event trigger window must be finite and ordered.";
            }
            else if (definition is RuntimeLandmarkDefinition landmark)
            {
                if (!landmark.AnchorId.IsValid || (!landmark.RewardId.IsValid && !landmark.StoryId.IsValid))
                    return "Landmark anchor and at least one reward/story output are required.";
            }
            else if (definition is RuntimeBossDefinition boss)
            {
                if (!boss.EnemyId.IsValid || boss.Phases.Count == 0 || boss.Phases.Count > 8 ||
                    !FinitePositive(boss.ResistanceMultiplier))
                    return "Boss enemy, phase count, and resistance multiplier must be valid.";
                var previous = 1.0001f;
                for (var index = 0; index < boss.Phases.Count; index++)
                {
                    var phase = boss.Phases[index];
                    if (!FiniteNonNegative(phase.HealthThreshold) || phase.HealthThreshold >= previous ||
                        phase.HealthThreshold > 1f || phase.CleanupPolicy < BossPhaseCleanupPolicy.ExpireOnPhaseExit ||
                        phase.CleanupPolicy > BossPhaseCleanupPolicy.Persist)
                        return "Boss phase thresholds must strictly descend within [0,1] and cleanup policy must be valid.";
                    previous = phase.HealthThreshold;
                }
            }
            else if (definition is RuntimeEliteAffixDefinition affix)
            {
                if (!affix.ModifierOutputId.IsValid && !affix.SkillId.IsValid && !affix.DeathRewardId.IsValid)
                    return "Elite affix requires at least one modifier, skill, or death reward output.";
                for (var required = 0; required < affix.RequiredTags.Count; required++)
                    for (var excluded = 0; excluded < affix.ExcludedTags.Count; excluded++)
                        if (affix.RequiredTags[required] == affix.ExcludedTags[excluded])
                            return "Elite affix cannot require and exclude the same tag.";
            }
            else if (definition is RuntimeMetaNodeDefinition node)
            {
                if (node.NodeKind < MetaNodeKind.Branch || node.NodeKind > MetaNodeKind.Terminal || node.Cost < 0 || node.OutputIds.Count == 0)
                    return "Meta node kind, cost, and outputs must be valid.";
                if (Contains(node.PrerequisiteIds, node.Id) ||
                    Contains(node.MutuallyExclusiveIds, node.Id) ||
                    HasDuplicate(node.PrerequisiteIds) ||
                    HasDuplicate(node.MutuallyExclusiveIds))
                    return "Meta node prerequisites and mutex IDs must be unique and cannot reference self.";
            }
            else if (definition is RuntimeMetaInsertDefinition insert)
            {
                if (insert.Cost < 0 || insert.SlotTags.Count == 0 || insert.OutputIds.Count == 0)
                    return "Meta insert cost, slot tags, and outputs must be valid.";
            }
            else if (definition is RuntimeMetaFacilityDefinition facility)
            {
                if (!facility.UnlockConditionId.IsValid || !facility.PageProfileId.IsValid)
                    return "Meta facility unlock condition and page profile are required.";
            }
            else if (definition is RuntimeStoryDefinition story)
            {
                if (story.SequenceKeys.Count == 0 || !story.UnlockConditionId.IsValid ||
                    string.IsNullOrWhiteSpace(story.UniqueKey))
                    return "Story sequence, unlock condition, and unique key are required.";
                for (var index = 0; index < story.SequenceKeys.Count; index++)
                    if (string.IsNullOrWhiteSpace(story.SequenceKeys[index])) return "Story sequence contains an empty localization key.";
            }
            else if (definition is RuntimeCollectibleDefinition collectible)
            {
                if (!collectible.TopicId.IsValid || !collectible.AcquireRuleId.IsValid ||
                    string.IsNullOrWhiteSpace(collectible.BodyLocalizationKey))
                    return "Collectible topic, acquire rule, and body localization key are required.";
            }

            return null;
        }

        public static void ValidateReferenceTypes(
            RuntimeContentDefinition definition,
            IReadOnlyDictionary<ContentId, RuntimeContentDefinition> definitions,
            ContentId packId,
            ContentValidationReport report)
        {
            if (definition is RuntimeCharacterMechanicDefinition mechanic)
            {
                for (var index = 0; index < mechanic.Tiers.Count; index++)
                    ValidateType(
                        definition,
                        mechanic.Tiers[index].OutputId,
                        definitions,
                        packId,
                        report,
                        IsMechanicOutput,
                        "a Reward, Skill, Passive, or Trait output");
            }
            else if (definition is RuntimePickupDefinition pickup)
                ValidateType(definition, pickup.RewardId, definitions, packId, report, value => value is RuntimeRewardDefinition, "a Reward");
            else if (definition is RuntimeRewardDefinition reward)
            {
                if (reward.FallbackRewardId.IsValid)
                    ValidateType(definition, reward.FallbackRewardId, definitions, packId, report, value => value is RuntimeRewardDefinition, "a Reward");
                for (var index = 0; index < reward.Operations.Count; index++)
                {
                    var operation = reward.Operations[index];
                    if (!operation.ReferenceId.IsValid) continue;
                    if (operation.Code == RewardOperationCode.ApplyStatus)
                        ValidateType(definition, operation.ReferenceId, definitions, packId, report, value => value is RuntimeStatusDefinition, "a Status");
                    else if (operation.Code == RewardOperationCode.GrantRelicChoice)
                        ValidateType(definition, operation.ReferenceId, definitions, packId, report, value => value is RuntimeRelicDefinition, "a Relic");
                    else if (operation.Code == RewardOperationCode.GrantEvolutionChoice)
                        ValidateType(definition, operation.ReferenceId, definitions, packId, report, value => value is RuntimeEvolutionDefinition, "an Evolution");
                    else if (operation.Code == RewardOperationCode.TriggerStory)
                        ValidateType(definition, operation.ReferenceId, definitions, packId, report, value => value is RuntimeStoryDefinition, "a Story");
                }
            }
            else if (definition is RuntimeRelicDefinition relic)
            {
                ValidateList(
                    relic,
                    relic.OutputIds,
                    definitions,
                    packId,
                    report,
                    IsRelicOutput,
                    "a Reward, Skill, Passive, Trait, or Synergy output");
                ValidateList(
                    relic,
                    relic.PrerequisiteIds,
                    definitions,
                    packId,
                    report,
                    IsRelicPrerequisite,
                    "a Relic, Skill, Passive, Trait, or MetaNode prerequisite");
                ValidateList(
                    relic,
                    relic.MutuallyExclusiveIds,
                    definitions,
                    packId,
                    report,
                    value => value is RuntimeRelicDefinition,
                    "a Relic mutex");
            }
            else if (definition is RuntimeMapObjectiveDefinition objective)
            {
                if (objective.OutputId.IsValid)
                    ValidateType(definition, objective.OutputId, definitions, packId, report,
                        IsMapRuleOrReward, "a Reward or generic rule output");
            }
            else if (definition is RuntimeMapEventDefinition mapEvent)
            {
                if (mapEvent.OutputId.IsValid)
                    ValidateType(definition, mapEvent.OutputId, definitions, packId, report,
                        value => value is RuntimeMapObjectiveDefinition || IsMapRuleOrReward(value),
                        "a MapObjective, Reward, or generic rule output");
            }
            else if (definition is RuntimeBossDefinition boss)
            {
                ValidateType(definition, boss.EnemyId, definitions, packId, report,
                    value => value is RuntimeEnemyDefinition enemy && enemy.HasM5Data, "a schema-4 Enemy");
                if (boss.RewardId.IsValid)
                    ValidateType(definition, boss.RewardId, definitions, packId, report, value => value is RuntimeRewardDefinition, "a Reward");
                for (var phaseIndex = 0; phaseIndex < boss.Phases.Count; phaseIndex++)
                    ValidateList(
                        boss,
                        boss.Phases[phaseIndex].AcceptedRuleIds,
                        definitions,
                        packId,
                        report,
                        IsBossRule,
                        "a Skill, Status, Synergy, or MapObjective rule");
            }
            else if (definition is RuntimeLandmarkDefinition landmark)
            {
                if (landmark.RewardId.IsValid)
                    ValidateType(definition, landmark.RewardId, definitions, packId, report, value => value is RuntimeRewardDefinition, "a Reward");
                if (landmark.StoryId.IsValid)
                    ValidateType(definition, landmark.StoryId, definitions, packId, report, value => value is RuntimeStoryDefinition, "a Story");
            }
            else if (definition is RuntimeEliteAffixDefinition affix)
            {
                if (affix.ModifierOutputId.IsValid)
                    ValidateType(definition, affix.ModifierOutputId, definitions, packId, report,
                        IsModifierOutput, "a Passive, Trait, or Synergy modifier output");
                if (affix.SkillId.IsValid)
                    ValidateType(definition, affix.SkillId, definitions, packId, report,
                        value => value is RuntimeSkillDefinition skill && skill.IsExecutable,
                        "an executable Skill");
                if (affix.DeathRewardId.IsValid)
                    ValidateType(definition, affix.DeathRewardId, definitions, packId, report,
                        value => value is RuntimeRewardDefinition, "a Reward");
            }
            else if (definition is RuntimeMetaNodeDefinition node)
            {
                ValidateList(node, node.PrerequisiteIds, definitions, packId, report,
                    value => value is RuntimeMetaNodeDefinition, "a MetaNode prerequisite");
                ValidateList(node, node.MutuallyExclusiveIds, definitions, packId, report,
                    value => value is RuntimeMetaNodeDefinition, "a MetaNode mutex");
                ValidateList(node, node.OutputIds, definitions, packId, report,
                    IsMetaOutput, "a Trait, Synergy rule, or UpgradeOffer output");
            }
            else if (definition is RuntimeMetaInsertDefinition insert)
            {
                ValidateList(insert, insert.OutputIds, definitions, packId, report,
                    IsMetaOutput, "a Trait, Synergy rule, or UpgradeOffer output");
            }
            else if (definition is RuntimeMetaFacilityDefinition facility)
            {
                if (facility.UnlockConditionId.IsValid)
                    ValidateType(definition, facility.UnlockConditionId, definitions, packId, report,
                        IsMetaUnlockCondition, "a MetaNode or MapObjective unlock condition");
            }
            else if (definition is RuntimeStoryDefinition story)
            {
                if (story.UnlockConditionId.IsValid)
                    ValidateType(definition, story.UnlockConditionId, definitions, packId, report,
                        value => IsMetaUnlockCondition(value) || value is RuntimeMetaFacilityDefinition,
                        "a MetaNode, MapObjective, or MetaFacility unlock condition");
            }
            else if (definition is RuntimeCollectibleDefinition collectible)
            {
                if (collectible.AcquireRuleId.IsValid)
                    ValidateType(definition, collectible.AcquireRuleId, definitions, packId, report,
                        IsCollectibleAcquireRule,
                        "a Landmark, MapObjective, Story, or MetaNode acquire rule");
                if (collectible.FallbackRewardId.IsValid)
                    ValidateType(definition, collectible.FallbackRewardId, definitions, packId, report,
                        value => value is RuntimeRewardDefinition, "a Reward");
            }
        }

        private static bool IsMechanicOutput(RuntimeContentDefinition value) =>
            value is RuntimeRewardDefinition ||
            value is RuntimeSkillDefinition ||
            value is RuntimePassiveDefinition ||
            value is RuntimeTraitDefinition;

        private static bool IsModifierOutput(RuntimeContentDefinition value) =>
            value is RuntimePassiveDefinition ||
            value is RuntimeTraitDefinition ||
            value is RuntimeSynergyDefinition;

        private static bool IsRelicOutput(RuntimeContentDefinition value) =>
            value is RuntimeRewardDefinition ||
            value is RuntimeSkillDefinition ||
            IsModifierOutput(value);

        private static bool IsRelicPrerequisite(RuntimeContentDefinition value) =>
            value is RuntimeRelicDefinition ||
            value is RuntimeSkillDefinition ||
            value is RuntimePassiveDefinition ||
            value is RuntimeTraitDefinition ||
            value is RuntimeMetaNodeDefinition;

        private static bool IsMapRuleOrReward(RuntimeContentDefinition value) =>
            value is RuntimeRewardDefinition || value is RuntimeSynergyDefinition;

        private static bool IsBossRule(RuntimeContentDefinition value) =>
            value is RuntimeSkillDefinition ||
            value is RuntimeStatusDefinition ||
            value is RuntimeSynergyDefinition ||
            value is RuntimeMapObjectiveDefinition;

        private static bool IsMetaOutput(RuntimeContentDefinition value) =>
            value is RuntimeTraitDefinition ||
            value is RuntimeSynergyDefinition ||
            value is RuntimeUpgradeOfferDefinition;

        private static bool IsMetaUnlockCondition(RuntimeContentDefinition value) =>
            value is RuntimeMetaNodeDefinition || value is RuntimeMapObjectiveDefinition;

        private static bool IsCollectibleAcquireRule(RuntimeContentDefinition value) =>
            value is RuntimeLandmarkDefinition ||
            value is RuntimeMapObjectiveDefinition ||
            value is RuntimeStoryDefinition ||
            value is RuntimeMetaNodeDefinition;

        private static string ValidateStateGraph(RuntimeStateGraphDefinition graph)
        {
            if (graph.AnchorIds.Count == 0 || graph.StateTransitions.Count == 0)
                return "State graph requires anchors and transitions.";
            if (graph is RuntimeMapObjectiveDefinition && !graph.OutputId.IsValid)
                return "Map objective completion output is required.";
            var reachable = new bool[8];
            reachable[(int)ObjectiveState.Hidden] = true;
            for (var pass = 0; pass < 8; pass++)
            {
                for (var index = 0; index < graph.StateTransitions.Count; index++)
                {
                    var transition = graph.StateTransitions[index];
                    if (transition.From < ObjectiveState.Hidden || transition.From > ObjectiveState.DisabledWithError ||
                        transition.To < ObjectiveState.Hidden || transition.To > ObjectiveState.DisabledWithError || transition.From == transition.To)
                        return "State graph transition is invalid.";
                    if (!IsLegalObjectiveTransition(transition.From, transition.To))
                        return "State graph contains a transition outside the approved objective lifecycle.";
                    if (reachable[(int)transition.From]) reachable[(int)transition.To] = true;
                }
            }
            return reachable[(int)ObjectiveState.Completed] ? null : "State graph cannot reach Completed from Hidden.";
        }

        private static bool IsLegalObjectiveTransition(ObjectiveState from, ObjectiveState to) =>
            (from == ObjectiveState.Hidden && to == ObjectiveState.Revealed) ||
            (from == ObjectiveState.Revealed && to == ObjectiveState.Available) ||
            (from == ObjectiveState.Available && to == ObjectiveState.Activating) ||
            (from == ObjectiveState.Activating &&
             (to == ObjectiveState.Defending || to == ObjectiveState.Available)) ||
            (from == ObjectiveState.Defending &&
             (to == ObjectiveState.Completed || to == ObjectiveState.Available));

        private static bool RequiresReference(RewardOperationCode code) =>
            code == RewardOperationCode.ApplyStatus ||
            code == RewardOperationCode.GrantRelicChoice ||
            code == RewardOperationCode.GrantEvolutionChoice ||
            code == RewardOperationCode.UnlockContent ||
            code == RewardOperationCode.GrantUnique ||
            code == RewardOperationCode.TriggerStory;

        private static bool Contains(IReadOnlyList<ContentId> values, ContentId expected)
        {
            for (var index = 0; index < values.Count; index++)
                if (values[index] == expected) return true;
            return false;
        }

        private static bool HasDuplicate(IReadOnlyList<ContentId> values)
        {
            for (var left = 0; left < values.Count; left++)
                for (var right = left + 1; right < values.Count; right++)
                    if (values[left] == values[right]) return true;
            return false;
        }

        private static void ValidateType(
            RuntimeContentDefinition owner,
            ContentId reference,
            IReadOnlyDictionary<ContentId, RuntimeContentDefinition> definitions,
            ContentId packId,
            ContentValidationReport report,
            Func<RuntimeContentDefinition, bool> predicate,
            string expected)
        {
            if (!reference.IsValid || !definitions.TryGetValue(reference, out var resolved) || predicate(resolved)) return;
            report.Add(new Error(ErrorCode.InvalidAuthoringData,
                "Content '" + owner.Id + "' reference '" + reference + "' must resolve to " + expected + ".",
                owner.Id, packId, owner.SourceAssetPath));
        }

        private static void ValidateList(
            RuntimeContentDefinition owner,
            IReadOnlyList<ContentId> references,
            IReadOnlyDictionary<ContentId, RuntimeContentDefinition> definitions,
            ContentId packId,
            ContentValidationReport report,
            Func<RuntimeContentDefinition, bool> predicate,
            string expected)
        {
            for (var index = 0; index < references.Count; index++)
                ValidateType(
                    owner,
                    references[index],
                    definitions,
                    packId,
                    report,
                    predicate,
                    expected);
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool FiniteNonNegative(float value) => Finite(value) && value >= 0f;
        private static bool FinitePositive(float value) => Finite(value) && value > 0f;
    }
}
