using System;
using Game.Content.Runtime;
using Game.Core;
using UnityEngine;

namespace Game.Content.Authoring
{
    /// <summary>Serializable numeric module parameters used by modular skill authoring.</summary>
    [Serializable]
    public sealed class SkillModuleAuthoringData
    {
        public string moduleId = string.Empty;
        public float value0;
        public float value1;
        public float value2;
        public float value3;
        public int int0;
        public int int1;
        public string presentationId = string.Empty;
    }

    /// <summary>Serializable authoring form of one effect operation.</summary>
    [Serializable]
    public sealed class SkillEffectAuthoringData
    {
        public string moduleId = string.Empty;
        public float value0;
        public float value1;
        public float value2;
        public int int0;
        public int int1;
        public string referenceId0 = string.Empty;
        public string referenceId1 = string.Empty;
        public string tag0 = string.Empty;
        public string statId0 = string.Empty;
        public EffectOpFlags flags;
    }

    /// <summary>Serializable authoring form of one level patch.</summary>
    [Serializable]
    public sealed class SkillLevelPatchAuthoringData
    {
        public int level = 2;
        public string path = string.Empty;
        public SkillPatchValueType valueType = SkillPatchValueType.Float;
        public SkillPatchOperation operation = SkillPatchOperation.Add;
        public float floatValue;
        public int integerValue;
    }

    /// <summary>
    /// Skill authoring data. Schema 1/2 may retain cooldown-only metadata; schema 3
    /// bakes a complete modular execution definition.
    /// </summary>
    [CreateAssetMenu(menuName = "Free World/Content/Skill", fileName = "Skill")]
    public sealed class SkillAuthoring : ContentAuthoringBase
    {
        private enum ModuleKind : byte
        {
            Trigger,
            Condition,
            Targeting,
            Delivery
        }

        [SerializeField] private float cooldownSeconds = 1f;
        [SerializeField] private float resourceCost;
        [SerializeField] private bool modularRuntimeEnabled;
        [SerializeField] private SkillModuleAuthoringData trigger = new SkillModuleAuthoringData();
        [SerializeField] private SkillModuleAuthoringData condition = new SkillModuleAuthoringData();
        [SerializeField] private SkillModuleAuthoringData targeting = new SkillModuleAuthoringData();
        [SerializeField] private SkillModuleAuthoringData delivery = new SkillModuleAuthoringData();
        [SerializeField] private SkillEffectAuthoringData[] effects = Array.Empty<SkillEffectAuthoringData>();
        [SerializeField] private SkillLevelPatchAuthoringData[] levelPatches = Array.Empty<SkillLevelPatchAuthoringData>();

        /// <summary>Gets whether this asset opts into schema-3 execution data.</summary>
        public bool ModularRuntimeEnabled => modularRuntimeEnabled;

        /// <summary>Configures legacy cooldown-only metadata.</summary>
        public void Configure(float cooldown)
        {
            cooldownSeconds = cooldown;
            modularRuntimeEnabled = false;
        }

        /// <summary>Configures a complete schema-3 modular skill fixture or editor asset.</summary>
        public void ConfigureRuntime(
            float cooldown,
            float cost,
            SkillModuleAuthoringData triggerModule,
            SkillModuleAuthoringData conditionModule,
            SkillModuleAuthoringData targetingModule,
            SkillModuleAuthoringData deliveryModule,
            SkillEffectAuthoringData[] effectOperations,
            SkillLevelPatchAuthoringData[] patches)
        {
            cooldownSeconds = cooldown;
            resourceCost = cost;
            trigger = triggerModule ?? new SkillModuleAuthoringData();
            condition = conditionModule ?? new SkillModuleAuthoringData();
            targeting = targetingModule ?? new SkillModuleAuthoringData();
            delivery = deliveryModule ?? new SkillModuleAuthoringData();
            effects = effectOperations == null ? Array.Empty<SkillEffectAuthoringData>() : (SkillEffectAuthoringData[])effectOperations.Clone();
            levelPatches = patches == null ? Array.Empty<SkillLevelPatchAuthoringData>() : (SkillLevelPatchAuthoringData[])patches.Clone();
            modularRuntimeEnabled = true;
        }

        internal override Result<RuntimeContentDefinition> Bake(ContentId packId, string authorAssetPath)
        {
            var commonResult = BakeCommon(packId, authorAssetPath);
            if (!commonResult.IsSuccess)
            {
                return Result<RuntimeContentDefinition>.Failure(commonResult.Error);
            }

            var common = commonResult.Value;
            if (!IsFinite(cooldownSeconds) || cooldownSeconds < 0f)
            {
                return Failure("Skill cooldown must be finite and non-negative.", common, packId);
            }

            if (!modularRuntimeEnabled)
            {
                return Result<RuntimeContentDefinition>.Success(
                    new RuntimeSkillDefinition(
                        common.Id,
                        common.LocalizedNameKey,
                        common.LocalizedDescriptionKey,
                        common.AuthorAssetPath,
                        common.Tags,
                        cooldownSeconds));
            }

            if (!IsFinite(resourceCost) || resourceCost < 0f)
            {
                return Failure("Skill resource cost must be finite and non-negative.", common, packId);
            }

            var triggerResult = BakeModule(trigger, ModuleKind.Trigger, "trigger", common, packId);
            if (!triggerResult.IsSuccess) return Result<RuntimeContentDefinition>.Failure(triggerResult.Error);
            var conditionResult = BakeModule(condition, ModuleKind.Condition, "condition", common, packId);
            if (!conditionResult.IsSuccess) return Result<RuntimeContentDefinition>.Failure(conditionResult.Error);
            var targetingResult = BakeModule(targeting, ModuleKind.Targeting, "targeting", common, packId);
            if (!targetingResult.IsSuccess) return Result<RuntimeContentDefinition>.Failure(targetingResult.Error);
            var deliveryResult = BakeModule(delivery, ModuleKind.Delivery, "delivery", common, packId);
            if (!deliveryResult.IsSuccess) return Result<RuntimeContentDefinition>.Failure(deliveryResult.Error);

            if (effects == null || effects.Length == 0)
            {
                return Failure("Executable skills require at least one effect.", common, packId);
            }

            var runtimeEffects = new EffectOp[effects.Length];
            for (var index = 0; index < effects.Length; index++)
            {
                var effectResult = BakeEffect(effects[index], index, common, packId);
                if (!effectResult.IsSuccess) return Result<RuntimeContentDefinition>.Failure(effectResult.Error);
                runtimeEffects[index] = effectResult.Value;
            }

            var patchResult = BakeLevelPatches(runtimeEffects.Length, common, packId);
            if (!patchResult.IsSuccess) return Result<RuntimeContentDefinition>.Failure(patchResult.Error);

            return Result<RuntimeContentDefinition>.Success(
                new RuntimeSkillDefinition(
                    common.Id,
                    common.LocalizedNameKey,
                    common.LocalizedDescriptionKey,
                    common.AuthorAssetPath,
                    common.Tags,
                    cooldownSeconds,
                    resourceCost,
                    triggerResult.Value,
                    conditionResult.Value,
                    targetingResult.Value,
                    deliveryResult.Value,
                    runtimeEffects,
                    patchResult.Value));
        }

        private static Result<SkillModuleDefinition> BakeModule(
            SkillModuleAuthoringData source,
            ModuleKind kind,
            string label,
            in AuthoringCommonData common,
            ContentId packId)
        {
            if (source == null)
            {
                return Result<SkillModuleDefinition>.Failure(ErrorFor("Skill " + label + " module is missing.", common, packId));
            }

            var idResult = ParseRequiredId(source.moduleId, label + " module ID", common, packId);
            if (!idResult.IsSuccess) return Result<SkillModuleDefinition>.Failure(idResult.Error);
            var known = kind == ModuleKind.Trigger
                ? SkillModuleIds.IsTrigger(idResult.Value)
                : kind == ModuleKind.Condition
                    ? SkillModuleIds.IsCondition(idResult.Value)
                    : kind == ModuleKind.Targeting
                        ? SkillModuleIds.IsTargeting(idResult.Value)
                        : SkillModuleIds.IsDelivery(idResult.Value);
            if (!known)
            {
                return Result<SkillModuleDefinition>.Failure(
                    ErrorFor("Skill " + label + " module ID '" + idResult.Value + "' is not explicitly registered.", common, packId));
            }

            if (!IsFinite(source.value0) || !IsFinite(source.value1) || !IsFinite(source.value2) || !IsFinite(source.value3))
            {
                return Result<SkillModuleDefinition>.Failure(ErrorFor("Skill " + label + " numeric parameters must be finite.", common, packId));
            }

            var presentationResult = ParseOptionalId(source.presentationId, label + " presentation ID", common, packId);
            if (!presentationResult.IsSuccess) return Result<SkillModuleDefinition>.Failure(presentationResult.Error);
            if (kind == ModuleKind.Delivery && idResult.Value != SkillModuleIds.DeliveryInstant && !presentationResult.Value.IsValid)
            {
                return Result<SkillModuleDefinition>.Failure(
                    ErrorFor("Non-instant delivery requires a canonical placeholder/profile presentation ID.", common, packId));
            }

            return Result<SkillModuleDefinition>.Success(
                new SkillModuleDefinition(
                    idResult.Value,
                    source.value0,
                    source.value1,
                    source.value2,
                    source.value3,
                    source.int0,
                    source.int1,
                    presentationResult.Value));
        }

        private static Result<EffectOp> BakeEffect(
            SkillEffectAuthoringData source,
            int index,
            in AuthoringCommonData common,
            ContentId packId)
        {
            if (source == null)
            {
                return Result<EffectOp>.Failure(ErrorFor("Skill effect is null at index " + index + ".", common, packId));
            }

            var idResult = ParseRequiredId(source.moduleId, "effect module ID", common, packId);
            if (!idResult.IsSuccess) return Result<EffectOp>.Failure(idResult.Error);
            if (!SkillModuleIds.TryGetEffectCode(idResult.Value, out var code))
            {
                return Result<EffectOp>.Failure(
                    ErrorFor("Skill effect module ID '" + idResult.Value + "' is not explicitly registered.", common, packId));
            }

            if (!IsFinite(source.value0) || !IsFinite(source.value1) || !IsFinite(source.value2))
            {
                return Result<EffectOp>.Failure(ErrorFor("Skill effect numeric operands must be finite.", common, packId));
            }

            var firstResult = ParseOptionalId(source.referenceId0, "effect reference 0", common, packId);
            if (!firstResult.IsSuccess) return Result<EffectOp>.Failure(firstResult.Error);
            var secondResult = ParseOptionalId(source.referenceId1, "effect reference 1", common, packId);
            if (!secondResult.IsSuccess) return Result<EffectOp>.Failure(secondResult.Error);

            ContentTag tag = default;
            if (!string.IsNullOrEmpty(source.tag0))
            {
                if (!ContentId.IsCanonical(source.tag0)) return Result<EffectOp>.Failure(ErrorFor("Effect tag must be canonical lowercase text.", common, packId));
                var tagResult = ContentTag.Create(source.tag0, packId, common.AuthorAssetPath);
                if (!tagResult.IsSuccess) return Result<EffectOp>.Failure(tagResult.Error);
                tag = tagResult.Value;
            }

            StatId stat = default;
            if (!string.IsNullOrEmpty(source.statId0))
            {
                if (!ContentId.IsCanonical(source.statId0)) return Result<EffectOp>.Failure(ErrorFor("Effect StatId must be canonical lowercase text.", common, packId));
                var statResult = StatId.Create(source.statId0, packId, common.AuthorAssetPath);
                if (!statResult.IsSuccess) return Result<EffectOp>.Failure(statResult.Error);
                stat = statResult.Value;
            }

            var op = new EffectOp(
                code,
                source.value0,
                source.value1,
                source.value2,
                source.int0,
                source.int1,
                firstResult.Value,
                secondResult.Value,
                tag,
                stat,
                source.flags);
            var validationMessage = ValidateEffect(op);
            return validationMessage == null
                ? Result<EffectOp>.Success(op)
                : Result<EffectOp>.Failure(ErrorFor(validationMessage, common, packId));
        }

        private Result<SkillLevelPatch[]> BakeLevelPatches(
            int effectCount,
            in AuthoringCommonData common,
            ContentId packId)
        {
            var source = levelPatches ?? Array.Empty<SkillLevelPatchAuthoringData>();
            var output = new SkillLevelPatch[source.Length];
            var previousLevel = 1;
            for (var index = 0; index < source.Length; index++)
            {
                var patch = source[index];
                if (patch == null)
                {
                    return Result<SkillLevelPatch[]>.Failure(ErrorFor("Skill level patch is null at index " + index + ".", common, packId));
                }

                if (patch.level < 2 || patch.level < previousLevel || patch.level > previousLevel + 1)
                {
                    return Result<SkillLevelPatch[]>.Failure(
                        ErrorFor("Skill level patches must begin at level 2 and use continuous non-decreasing levels.", common, packId));
                }

                if (!SkillLevelPatchPath.TryResolve(patch.path, effectCount, out var target, out var targetIndex, out var requiredType))
                {
                    return Result<SkillLevelPatch[]>.Failure(ErrorFor("Invalid Skill LevelPatch path '" + patch.path + "'.", common, packId));
                }

                if (patch.valueType != requiredType)
                {
                    return Result<SkillLevelPatch[]>.Failure(
                        ErrorFor("Skill LevelPatch path '" + patch.path + "' requires value type " + requiredType + ".", common, packId));
                }

                if (patch.operation < SkillPatchOperation.Add || patch.operation > SkillPatchOperation.Override ||
                    (requiredType == SkillPatchValueType.Float && !IsFinite(patch.floatValue)))
                {
                    return Result<SkillLevelPatch[]>.Failure(ErrorFor("Skill LevelPatch operation or value is invalid.", common, packId));
                }

                output[index] = new SkillLevelPatch(
                    patch.level,
                    target,
                    targetIndex,
                    requiredType,
                    patch.operation,
                    patch.floatValue,
                    patch.integerValue);
                previousLevel = patch.level;
            }

            return Result<SkillLevelPatch[]>.Success(output);
        }

        private static string ValidateEffect(in EffectOp op)
        {
            switch (op.Code)
            {
                case EffectOpCode.Damage:
                    if (op.Value0 < 0f || op.Value1 < 0f || op.Value1 > 1f ||
                        op.Int0 < (int)DamageType.Physical || op.Int0 > (int)DamageType.True ||
                        (op.Flags & ~EffectOpFlags.CanCritical) != 0)
                    {
                        return "Damage effect operands are invalid.";
                    }
                    break;
                case EffectOpCode.Heal:
                case EffectOpCode.Knockback:
                case EffectOpCode.Pull:
                case EffectOpCode.GrantShield:
                case EffectOpCode.GainResource:
                    if (op.Value0 < 0f) return op.Code + " effect value cannot be negative.";
                    break;
                case EffectOpCode.ApplyStatus:
                case EffectOpCode.SpawnSecondarySkill:
                    if (!op.ReferenceId0.IsValid) return op.Code + " requires content reference 0.";
                    break;
                case EffectOpCode.RemoveStatus:
                    if (!op.Tag0.IsValid) return "RemoveStatus requires a canonical dispel tag.";
                    break;
                case EffectOpCode.ModifyStat:
                    if (!op.StatId0.IsValid ||
                        op.Int0 < (int)ModifierOperation.AddFlat || op.Int0 > (int)ModifierOperation.Override ||
                        op.Value1 < 0f)
                    {
                        return "ModifyStat effect operands are invalid.";
                    }
                    break;
                default:
                    return "Effect operation code is invalid.";
            }

            return null;
        }

        private static Result<ContentId> ParseRequiredId(
            string value,
            string label,
            in AuthoringCommonData common,
            ContentId packId)
        {
            if (!ContentId.IsCanonical(value))
            {
                return Result<ContentId>.Failure(ErrorFor("Skill " + label + " must be canonical lowercase text.", common, packId));
            }

            return ContentId.Create(value, packId, common.AuthorAssetPath);
        }

        private static Result<ContentId> ParseOptionalId(
            string value,
            string label,
            in AuthoringCommonData common,
            ContentId packId)
        {
            return string.IsNullOrEmpty(value)
                ? Result<ContentId>.Success(default)
                : ParseRequiredId(value, label, common, packId);
        }

        private static Result<RuntimeContentDefinition> Failure(
            string message,
            in AuthoringCommonData common,
            ContentId packId)
        {
            return Result<RuntimeContentDefinition>.Failure(ErrorFor(message, common, packId));
        }

        private static Error ErrorFor(string message, in AuthoringCommonData common, ContentId packId)
        {
            return new Error(ErrorCode.InvalidAuthoringData, message, common.Id, packId, common.AuthorAssetPath);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
