using System;
using System.Collections.Generic;
using Game.Content.Runtime;
using Game.Core;
using UnityEngine;

namespace Game.Content.Authoring
{
    /// <summary>
    /// Authors status lifetime, stacking, dispel, and immunity metadata.
    /// </summary>
    [CreateAssetMenu(menuName = "Free World/Content/Status Effect", fileName = "StatusEffect")]
    public sealed class StatusEffectAuthoring : ContentAuthoringBase
    {
        [SerializeField] private StatusStackingPolicy stackingPolicy =
            StatusStackingPolicy.RefreshDuration;
        [SerializeField] private float durationSeconds = 1f;
        [SerializeField] private int maxStacks = 1;
        [SerializeField] private float tickIntervalSeconds;
        [SerializeField] private string[] dispelTags = Array.Empty<string>();
        [SerializeField] private string[] immunityTags = Array.Empty<string>();
        [SerializeField] private bool modifierEnabled;
        [SerializeField] private string modifierStatId = string.Empty;
        [SerializeField] private ModifierOperation modifierOperation = ModifierOperation.Multiply;
        [SerializeField] private float modifierValue = 1f;
        [SerializeField] private int modifierPriority;
        [SerializeField] private string modifierStackingGroup = string.Empty;
        [SerializeField] private bool periodicDamageEnabled;
        [SerializeField] private DamageType periodicDamageType = DamageType.Fire;
        [SerializeField] private int periodicDamageTagMask = (int)DamageTags.DamageOverTime;
        [SerializeField] private float periodicDamageValue;
        [SerializeField] private bool periodicCanCritical;
        [SerializeField] private float periodicProcCoefficient = 1f;
        [SerializeField] private Vector2 periodicKnockback;
        [SerializeField] private float shieldCapacity;

        /// <summary>
        /// Configures pure status metadata. Intended for editor tools and test fixtures.
        /// </summary>
        public void Configure(
            StatusStackingPolicy policy,
            float duration,
            int maximumStacks,
            float tickInterval,
            string[] statusDispelTags,
            string[] targetImmunityTags)
        {
            stackingPolicy = policy;
            durationSeconds = duration;
            maxStacks = maximumStacks;
            tickIntervalSeconds = tickInterval;
            dispelTags = statusDispelTags == null
                ? Array.Empty<string>()
                : (string[])statusDispelTags.Clone();
            immunityTags = targetImmunityTags == null
                ? Array.Empty<string>()
                : (string[])targetImmunityTags.Clone();
        }

        /// <summary>
        /// Configures content-baked behavior. Intended for editor tools and test fixtures.
        /// </summary>
        public void ConfigureBehavior(
            in RuntimeStatusModifier modifier,
            in RuntimeStatusPeriodicDamage periodicDamage,
            float temporaryShieldCapacity)
        {
            modifierEnabled = modifier.Enabled;
            modifierStatId = modifier.Enabled ? modifier.StatId.Value : string.Empty;
            modifierOperation = modifier.Operation;
            modifierValue = modifier.Value;
            modifierPriority = modifier.Priority;
            modifierStackingGroup = modifier.StackingGroup.IsValid
                ? modifier.StackingGroup.Value
                : string.Empty;
            periodicDamageEnabled = periodicDamage.Enabled;
            periodicDamageType = periodicDamage.DamageType;
            periodicDamageTagMask = unchecked((int)(ulong)periodicDamage.Tags);
            periodicDamageValue = periodicDamage.BaseValue;
            periodicCanCritical = periodicDamage.CanCritical;
            periodicProcCoefficient = periodicDamage.ProcCoefficient;
            periodicKnockback = periodicDamage.Enabled
                ? new Vector2(periodicDamage.Knockback.X, periodicDamage.Knockback.Y)
                : Vector2.zero;
            shieldCapacity = temporaryShieldCapacity;
        }

        internal override Result<RuntimeContentDefinition> Bake(
            ContentId packId,
            string authorAssetPath)
        {
            var commonResult = BakeCommon(packId, authorAssetPath);
            if (!commonResult.IsSuccess)
            {
                return Result<RuntimeContentDefinition>.Failure(commonResult.Error);
            }

            var common = commonResult.Value;
            var valueError = ValidateValues(
                stackingPolicy,
                durationSeconds,
                maxStacks,
                tickIntervalSeconds);
            if (valueError != null)
            {
                return Result<RuntimeContentDefinition>.Failure(
                    new Error(
                        ErrorCode.InvalidAuthoringData,
                        valueError,
                        common.Id,
                        packId,
                        authorAssetPath));
            }

            var dispelResult = BakeTags(
                dispelTags,
                "dispel",
                common.Id,
                packId,
                authorAssetPath);
            if (!dispelResult.IsSuccess)
            {
                return Result<RuntimeContentDefinition>.Failure(dispelResult.Error);
            }

            var immunityResult = BakeTags(
                immunityTags,
                "immunity",
                common.Id,
                packId,
                authorAssetPath);
            if (!immunityResult.IsSuccess)
            {
                return Result<RuntimeContentDefinition>.Failure(immunityResult.Error);
            }

            var behaviorResult = BakeBehavior(
                common.Id,
                packId,
                authorAssetPath);
            if (!behaviorResult.IsSuccess)
            {
                return Result<RuntimeContentDefinition>.Failure(behaviorResult.Error);
            }

            return Result<RuntimeContentDefinition>.Success(
                new RuntimeStatusDefinition(
                    common.Id,
                    common.LocalizedNameKey,
                    common.LocalizedDescriptionKey,
                    common.AuthorAssetPath,
                    common.Tags,
                    stackingPolicy,
                    durationSeconds,
                    maxStacks,
                    tickIntervalSeconds,
                    dispelResult.Value,
                    immunityResult.Value,
                    behaviorResult.Value));
        }

        private Result<RuntimeStatusBehavior> BakeBehavior(
            ContentId ownerId,
            ContentId packId,
            string authorAssetPath)
        {
            if (!IsFinite(shieldCapacity) || shieldCapacity < 0f)
            {
                return BehaviorFailure(
                    "Status shield capacity must be finite and non-negative.",
                    ownerId,
                    packId,
                    authorAssetPath);
            }

            var modifier = default(RuntimeStatusModifier);
            if (modifierEnabled)
            {
                if (!ContentId.IsCanonical(modifierStatId))
                {
                    return BehaviorFailure(
                        "Status modifier StatId must be lowercase canonical text.",
                        ownerId,
                        packId,
                        authorAssetPath);
                }

                var statResult = StatId.Create(modifierStatId, packId, authorAssetPath);
                if (!statResult.IsSuccess)
                {
                    return Result<RuntimeStatusBehavior>.Failure(statResult.Error);
                }

                if (!IsKnownOperation(modifierOperation) || !IsFinite(modifierValue))
                {
                    return BehaviorFailure(
                        "Status modifier operation and value must be valid.",
                        ownerId,
                        packId,
                        authorAssetPath);
                }

                var group = default(ContentId);
                if (!string.IsNullOrEmpty(modifierStackingGroup))
                {
                    if (!ContentId.IsCanonical(modifierStackingGroup))
                    {
                        return BehaviorFailure(
                            "Status modifier stacking group must be lowercase canonical text.",
                            ownerId,
                            packId,
                            authorAssetPath);
                    }

                    var groupResult = ContentId.Create(
                        modifierStackingGroup,
                        packId,
                        authorAssetPath);
                    if (!groupResult.IsSuccess)
                    {
                        return Result<RuntimeStatusBehavior>.Failure(groupResult.Error);
                    }

                    group = groupResult.Value;
                }

                modifier = new RuntimeStatusModifier(
                    statResult.Value,
                    modifierOperation,
                    modifierValue,
                    modifierPriority,
                    group);
            }

            var periodic = default(RuntimeStatusPeriodicDamage);
            if (periodicDamageEnabled)
            {
                var periodicTags = (DamageTags)unchecked((uint)periodicDamageTagMask);
                if (!IsKnownDamageType(periodicDamageType) ||
                    HasUnknownDamageTags(periodicTags) ||
                    !IsFinite(periodicDamageValue) ||
                    periodicDamageValue < 0f ||
                    !IsFinite(periodicProcCoefficient) ||
                    periodicProcCoefficient < 0f ||
                    periodicProcCoefficient > 1f ||
                    !IsFinite(periodicKnockback.x) ||
                    !IsFinite(periodicKnockback.y) ||
                    tickIntervalSeconds <= 0f)
                {
                    return BehaviorFailure(
                        "Status periodic damage fields must be valid and require a positive tick interval.",
                        ownerId,
                        packId,
                        authorAssetPath);
                }

                periodic = new RuntimeStatusPeriodicDamage(
                    periodicDamageType,
                    periodicTags,
                    periodicDamageValue,
                    periodicCanCritical,
                    periodicProcCoefficient,
                    new System.Numerics.Vector2(
                        periodicKnockback.x,
                        periodicKnockback.y));
            }

            return Result<RuntimeStatusBehavior>.Success(
                new RuntimeStatusBehavior(modifier, periodic, shieldCapacity));
        }

        private static Result<RuntimeStatusBehavior> BehaviorFailure(
            string message,
            ContentId ownerId,
            ContentId packId,
            string authorAssetPath)
        {
            return Result<RuntimeStatusBehavior>.Failure(
                new Error(
                    ErrorCode.InvalidAuthoringData,
                    message,
                    ownerId,
                    packId,
                    authorAssetPath));
        }

        private static Result<ContentTag[]> BakeTags(
            string[] rawTags,
            string fieldName,
            ContentId ownerId,
            ContentId packId,
            string authorAssetPath)
        {
            var source = rawTags ?? Array.Empty<string>();
            var output = new ContentTag[source.Length];
            var seen = new HashSet<ContentTag>();
            for (var index = 0; index < source.Length; index++)
            {
                if (!ContentId.IsCanonical(source[index]))
                {
                    return Result<ContentTag[]>.Failure(
                        new Error(
                            ErrorCode.InvalidContentTag,
                            "Authoring status " + fieldName +
                            " tag must already be lowercase canonical text: '" +
                            (source[index] ?? string.Empty) + "'.",
                            ownerId,
                            packId,
                            authorAssetPath));
                }

                var result = ContentTag.Create(source[index], packId, authorAssetPath);
                if (!result.IsSuccess)
                {
                    return Result<ContentTag[]>.Failure(result.Error);
                }

                if (!seen.Add(result.Value))
                {
                    return Result<ContentTag[]>.Failure(
                        new Error(
                            ErrorCode.InvalidAuthoringData,
                            "Authoring status " + fieldName + " tag '" +
                            result.Value + "' is duplicated.",
                            ownerId,
                            packId,
                            authorAssetPath));
                }

                output[index] = result.Value;
            }

            return Result<ContentTag[]>.Success(output);
        }

        private static string ValidateValues(
            StatusStackingPolicy policy,
            float duration,
            int maximumStacks,
            float tickInterval)
        {
            if (!IsKnownPolicy(policy))
            {
                return "Status stacking policy is invalid.";
            }

            if (!IsFinite(duration) || duration <= 0f)
            {
                return "Status duration must be finite and positive.";
            }

            if (maximumStacks < 1)
            {
                return "Status max stacks must be at least one.";
            }

            if ((policy == StatusStackingPolicy.RefreshDuration ||
                 policy == StatusStackingPolicy.ReplaceIfStronger) &&
                maximumStacks != 1)
            {
                return "Refresh-duration and replace-if-stronger statuses must use one stack.";
            }

            if (!IsFinite(tickInterval) || tickInterval < 0f)
            {
                return "Status tick interval must be finite and cannot be negative.";
            }

            return null;
        }

        private static bool IsKnownPolicy(StatusStackingPolicy policy)
        {
            return policy == StatusStackingPolicy.RefreshDuration ||
                   policy == StatusStackingPolicy.AddStacks ||
                   policy == StatusStackingPolicy.ReplaceIfStronger ||
                   policy == StatusStackingPolicy.IndependentInstances;
        }

        private static bool IsKnownOperation(ModifierOperation operation)
        {
            return operation >= ModifierOperation.AddFlat &&
                   operation <= ModifierOperation.Override;
        }

        private static bool IsKnownDamageType(DamageType damageType)
        {
            return damageType >= DamageType.Physical && damageType <= DamageType.True;
        }

        private static bool HasUnknownDamageTags(DamageTags tags)
        {
            const DamageTags known =
                DamageTags.Direct |
                DamageTags.DamageOverTime |
                DamageTags.Status |
                DamageTags.Secondary;
            return (tags & ~known) != 0;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
