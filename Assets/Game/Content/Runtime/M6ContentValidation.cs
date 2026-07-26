using System;
using System.Collections.Generic;
using Game.Core;

namespace Game.Content.Runtime
{
    /// <summary>Schema-5 value and typed-reference validation shared by Baker and Registry.</summary>
    public static class M6ContentValidation
    {
        public static bool IsBuildProgressionDefinition(RuntimeContentDefinition definition)
        {
            return definition is RuntimePassiveDefinition ||
                   definition is RuntimeTraitDefinition ||
                   definition is RuntimeUpgradeOfferDefinition ||
                   definition is RuntimeSynergyDefinition ||
                   definition is RuntimeEvolutionDefinition;
        }

        internal static string ValidateDefinitionValues(RuntimeContentDefinition definition)
        {
            if (definition is RuntimePassiveDefinition passive)
            {
                if (passive.MaximumLevel < 1) return "Passive maximum level must be positive.";
                for (var index = 0; index < passive.LevelModifiers.Count; index++)
                {
                    var item = passive.LevelModifiers[index];
                    if (item.Level < 1 || item.Level > passive.MaximumLevel ||
                        !ValidModifier(item.Modifier))
                        return "Passive level modifiers must target a valid level and finite modifier.";
                }

                return null;
            }

            if (definition is RuntimeTraitDefinition trait)
            {
                for (var index = 0; index < trait.Modifiers.Count; index++)
                    if (!ValidModifier(trait.Modifiers[index]))
                        return "Trait modifiers must be finite and use a known modifier operation.";
                return null;
            }

            if (definition is RuntimeSynergyDefinition synergy)
            {
                if (synergy.Conditions.Count == 0 || synergy.Outputs.Count == 0)
                    return "Synergy requires at least one condition and one output.";
                for (var index = 0; index < synergy.Conditions.Count; index++)
                    if (!ValidCondition(synergy.Conditions[index]))
                        return "Synergy contains an invalid condition.";
                for (var index = 0; index < synergy.Outputs.Count; index++)
                    if (!ValidOutput(synergy.Outputs[index]))
                        return "Synergy contains an invalid output.";
                return null;
            }

            if (definition is RuntimeEvolutionDefinition evolution)
            {
                if (!evolution.RequiredSkillId.IsValid ||
                    evolution.RequiredSkillLevel < 1 ||
                    !evolution.ResultSkillId.IsValid ||
                    evolution.RequiredSkillId == evolution.ResultSkillId ||
                    (evolution.ConsumePolicy != EvolutionConsumePolicy.RetainRequiredPassives &&
                     evolution.ConsumePolicy != EvolutionConsumePolicy.ConsumeRequiredPassives))
                    return "Evolution skill requirements, result, or consume policy are invalid.";
                for (var index = 0; index < evolution.RequiredPassiveIds.Count; index++)
                {
                    if (!evolution.RequiredPassiveIds[index].IsValid)
                        return "Evolution required passive IDs must be valid.";
                    for (var other = index + 1; other < evolution.RequiredPassiveIds.Count; other++)
                        if (evolution.RequiredPassiveIds[index] == evolution.RequiredPassiveIds[other])
                            return "Evolution required passive IDs must be unique.";
                }
                for (var index = 0; index < evolution.AdditionalConditions.Count; index++)
                    if (!ValidCondition(evolution.AdditionalConditions[index]))
                        return "Evolution contains an invalid additional condition.";
                return null;
            }

            if (definition is RuntimeUpgradeOfferDefinition offer)
            {
                if (!offer.TargetContentId.IsValid || !Finite(offer.Weight) || offer.Weight <= 0f)
                    return "Upgrade offer requires a valid target and positive finite weight.";
                for (var index = 0; index < offer.Prerequisites.Count; index++)
                    if (!ValidCondition(offer.Prerequisites[index]))
                        return "Upgrade offer contains an invalid prerequisite.";
                for (var index = 0; index < offer.MutuallyExclusiveIds.Count; index++)
                    if (!offer.MutuallyExclusiveIds[index].IsValid)
                        return "Upgrade offer mutually-exclusive IDs must be valid.";
                return null;
            }

            return "Unsupported schema-5 definition.";
        }

        /// <summary>Runs the same value checks before an authoring definition enters a catalog.</summary>
        public static string ValidateDefinitionValuesForAuthoring(RuntimeContentDefinition definition)
        {
            return ValidateDefinitionValues(definition);
        }

        internal static void ValidateReferenceTypes(
            RuntimeContentDefinition definition,
            Dictionary<ContentId, RuntimeContentDefinition> definitions,
            ContentId packId,
            ContentValidationReport report)
        {
            if (definition is RuntimeSynergyDefinition synergy)
            {
                ValidateConditions(synergy, synergy.Conditions, definitions, packId, report);
                for (var index = 0; index < synergy.Outputs.Count; index++)
                {
                    var output = synergy.Outputs[index];
                    if (output.Type == SynergyOutputType.UnlockOffer)
                        ValidateType(synergy, output.TargetId, definitions, packId, report, value => value is RuntimeUpgradeOfferDefinition, "an Offer");
                    else if (output.Type == SynergyOutputType.GrantTrait)
                        ValidateType(synergy, output.TargetId, definitions, packId, report, value => value is RuntimeTraitDefinition, "a Trait");
                    else if (output.Type == SynergyOutputType.TransformSkill)
                    {
                        ValidateExecutableSkill(synergy, output.SourceId, definitions, packId, report);
                        ValidateExecutableSkill(synergy, output.TargetId, definitions, packId, report);
                    }
                    else if (output.Type == SynergyOutputType.AddEffectOp)
                    {
                        ValidateExecutableSkill(synergy, output.SourceId, definitions, packId, report);
                        ValidateEffectReference(synergy, output.Effect, definitions, packId, report);
                    }
                }
            }
            else if (definition is RuntimeEvolutionDefinition evolution)
            {
                ValidateExecutableSkill(evolution, evolution.RequiredSkillId, definitions, packId, report);
                ValidateExecutableSkill(evolution, evolution.ResultSkillId, definitions, packId, report);
                for (var index = 0; index < evolution.RequiredPassiveIds.Count; index++)
                    ValidateType(evolution, evolution.RequiredPassiveIds[index], definitions, packId, report, value => value is RuntimePassiveDefinition, "a Passive");
                ValidateConditions(evolution, evolution.AdditionalConditions, definitions, packId, report);
            }
            else if (definition is RuntimeUpgradeOfferDefinition offer)
            {
                ValidateType(
                    offer,
                    offer.TargetContentId,
                    definitions,
                    packId,
                    report,
                    value => value is RuntimeSkillDefinition skill && skill.IsExecutable ||
                             value is RuntimePassiveDefinition ||
                             value is RuntimeEvolutionDefinition,
                    "an executable Skill, Passive, or Evolution");
                ValidateConditions(offer, offer.Prerequisites, definitions, packId, report);
            }
        }

        private static void ValidateConditions(
            RuntimeContentDefinition owner,
            IReadOnlyList<BuildCondition> conditions,
            Dictionary<ContentId, RuntimeContentDefinition> definitions,
            ContentId packId,
            ContentValidationReport report)
        {
            for (var index = 0; index < conditions.Count; index++)
            {
                var condition = conditions[index];
                if (condition.Type == BuildConditionType.SkillLevelAtLeast)
                    ValidateExecutableSkill(owner, condition.ContentId, definitions, packId, report);
            }
        }

        private static void ValidateEffectReference(
            RuntimeContentDefinition owner,
            in EffectOp effect,
            Dictionary<ContentId, RuntimeContentDefinition> definitions,
            ContentId packId,
            ContentValidationReport report)
        {
            if (effect.Code == EffectOpCode.ApplyStatus)
                ValidateType(owner, effect.ReferenceId0, definitions, packId, report, value => value is RuntimeStatusDefinition, "a Status");
            else if (effect.Code == EffectOpCode.SpawnSecondarySkill)
                ValidateExecutableSkill(owner, effect.ReferenceId0, definitions, packId, report);
        }

        private static void ValidateExecutableSkill(
            RuntimeContentDefinition owner,
            ContentId id,
            Dictionary<ContentId, RuntimeContentDefinition> definitions,
            ContentId packId,
            ContentValidationReport report)
        {
            ValidateType(owner, id, definitions, packId, report, value => value is RuntimeSkillDefinition skill && skill.IsExecutable, "an executable Skill");
        }

        private static void ValidateType(
            RuntimeContentDefinition owner,
            ContentId id,
            Dictionary<ContentId, RuntimeContentDefinition> definitions,
            ContentId packId,
            ContentValidationReport report,
            Func<RuntimeContentDefinition, bool> predicate,
            string expected)
        {
            if (!id.IsValid || !definitions.TryGetValue(id, out var value) || predicate(value)) return;
            report.Add(
                new Error(
                    ErrorCode.InvalidAuthoringData,
                    "Content '" + owner.Id + "' requires '" + id + "' to reference " + expected + ".",
                    owner.Id,
                    packId,
                    owner.SourceAssetPath));
        }

        private static bool ValidCondition(in BuildCondition condition)
        {
            if (condition.Type == BuildConditionType.OwnsContent)
                return condition.ContentId.IsValid;
            if (condition.Type == BuildConditionType.HasTagCount)
                return condition.Tag.IsValid && condition.IntegerValue > 0;
            if (condition.Type == BuildConditionType.SkillLevelAtLeast)
                return condition.ContentId.IsValid && condition.IntegerValue > 0;
            if (condition.Type == BuildConditionType.StatAtLeast)
                return condition.StatId.IsValid && Finite(condition.FloatValue);
            return condition.Type == BuildConditionType.MapHasTag && condition.Tag.IsValid;
        }

        private static bool ValidOutput(in RuntimeSynergyOutput output)
        {
            if (output.Type == SynergyOutputType.AddModifier) return ValidModifier(output.Modifier);
            if (output.Type == SynergyOutputType.UnlockOffer || output.Type == SynergyOutputType.GrantTrait)
                return output.TargetId.IsValid;
            if (output.Type == SynergyOutputType.TransformSkill)
                return output.SourceId.IsValid && output.TargetId.IsValid && output.SourceId != output.TargetId;
            if (output.Type == SynergyOutputType.AddEffectOp)
                return output.SourceId.IsValid && output.Effect.Code >= EffectOpCode.Damage && output.Effect.Code <= EffectOpCode.GainResource;
            return false;
        }

        private static bool ValidModifier(in RuntimeBuildModifier modifier)
        {
            return modifier.StatId.IsValid &&
                   modifier.Operation >= ModifierOperation.AddFlat &&
                   modifier.Operation <= ModifierOperation.Override &&
                   Finite(modifier.Value);
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
