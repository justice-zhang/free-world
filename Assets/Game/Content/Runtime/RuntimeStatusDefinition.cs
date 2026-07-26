using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using Game.Core;

namespace Game.Content.Runtime
{
    /// <summary>Immutable statistic behavior owned by one runtime status definition.</summary>
    public readonly struct RuntimeStatusModifier
    {
        /// <summary>Initializes an enabled statistic modifier.</summary>
        public RuntimeStatusModifier(
            StatId statId,
            ModifierOperation operation,
            float value,
            int priority,
            ContentId stackingGroup)
        {
            Enabled = true;
            StatId = statId;
            Operation = operation;
            Value = value;
            Priority = priority;
            StackingGroup = stackingGroup;
        }

        /// <summary>Gets whether the behavior is enabled.</summary>
        public bool Enabled { get; }

        /// <summary>Gets the affected stable statistic ID.</summary>
        public StatId StatId { get; }

        /// <summary>Gets the deterministic modifier operation.</summary>
        public ModifierOperation Operation { get; }

        /// <summary>Gets the value at strength one and one stack.</summary>
        public float Value { get; }

        /// <summary>Gets the modifier priority.</summary>
        public int Priority { get; }

        /// <summary>Gets the optional stable stacking-group ID.</summary>
        public ContentId StackingGroup { get; }
    }

    /// <summary>Immutable periodic damage behavior owned by one runtime status definition.</summary>
    public readonly struct RuntimeStatusPeriodicDamage
    {
        /// <summary>Initializes an enabled periodic damage behavior.</summary>
        public RuntimeStatusPeriodicDamage(
            DamageType damageType,
            DamageTags tags,
            float baseValue,
            bool canCritical,
            float procCoefficient,
            Vector2 knockback)
        {
            Enabled = true;
            DamageType = damageType;
            Tags = tags;
            BaseValue = baseValue;
            CanCritical = canCritical;
            ProcCoefficient = procCoefficient;
            Knockback = knockback;
        }

        /// <summary>Gets whether periodic damage is enabled.</summary>
        public bool Enabled { get; }

        /// <summary>Gets the damage category.</summary>
        public DamageType DamageType { get; }

        /// <summary>Gets allocation-free damage tags.</summary>
        public DamageTags Tags { get; }

        /// <summary>Gets damage at strength one and one stack.</summary>
        public float BaseValue { get; }

        /// <summary>Gets critical eligibility.</summary>
        public bool CanCritical { get; }

        /// <summary>Gets the proc coefficient.</summary>
        public float ProcCoefficient { get; }

        /// <summary>Gets requested knockback.</summary>
        public Vector2 Knockback { get; }
    }

    /// <summary>
    /// Immutable, content-baked behavior for one runtime status definition.
    /// </summary>
    public readonly struct RuntimeStatusBehavior
    {
        /// <summary>Initializes composable status behavior.</summary>
        public RuntimeStatusBehavior(
            in RuntimeStatusModifier modifier,
            in RuntimeStatusPeriodicDamage periodicDamage,
            float shieldCapacity = 0f)
        {
            Modifier = modifier;
            PeriodicDamage = periodicDamage;
            ShieldCapacity = shieldCapacity;
        }

        /// <summary>Gets the optional statistic modifier.</summary>
        public RuntimeStatusModifier Modifier { get; }

        /// <summary>Gets the optional periodic damage behavior.</summary>
        public RuntimeStatusPeriodicDamage PeriodicDamage { get; }

        /// <summary>Gets temporary shield capacity granted per instance or stack.</summary>
        public float ShieldCapacity { get; }
    }

    /// <summary>
    /// Defines how repeated applications of the same status are combined.
    /// </summary>
    public enum StatusStackingPolicy : byte
    {
        /// <summary>No valid stacking policy was assigned.</summary>
        Invalid = 0,

        /// <summary>One instance remains active and a repeated application refreshes its duration.</summary>
        RefreshDuration = 1,

        /// <summary>One instance gains stacks up to its configured maximum.</summary>
        AddStacks = 2,

        /// <summary>A repeated application replaces the active instance only when stronger.</summary>
        ReplaceIfStronger = 3,

        /// <summary>Each application owns an independent lifetime up to the configured maximum.</summary>
        IndependentInstances = 4
    }

    /// <summary>
    /// Pure runtime status metadata consumed by the fixed-tick simulation.
    /// </summary>
    public sealed class RuntimeStatusDefinition : RuntimeContentDefinition
    {
        private readonly ContentTag[] dispelTags;
        private readonly IReadOnlyList<ContentTag> dispelTagsView;
        private readonly ContentTag[] immunityTags;
        private readonly IReadOnlyList<ContentTag> immunityTagsView;

        /// <summary>
        /// Initializes immutable runtime status metadata.
        /// </summary>
        public RuntimeStatusDefinition(
            ContentId id,
            string localizedNameKey,
            string localizedDescriptionKey,
            string sourceAssetPath,
            ContentTag[] tags,
            StatusStackingPolicy stackingPolicy,
            float durationSeconds,
            int maxStacks,
            float tickIntervalSeconds,
            ContentTag[] dispelTags,
            ContentTag[] immunityTags,
            RuntimeStatusBehavior behavior = default)
            : base(
                id,
                localizedNameKey,
                localizedDescriptionKey,
                sourceAssetPath,
                tags,
                Array.Empty<ContentId>())
        {
            StackingPolicy = stackingPolicy;
            DurationSeconds = durationSeconds;
            MaxStacks = maxStacks;
            TickIntervalSeconds = tickIntervalSeconds;
            this.dispelTags = dispelTags == null
                ? Array.Empty<ContentTag>()
                : (ContentTag[])dispelTags.Clone();
            dispelTagsView = Array.AsReadOnly(this.dispelTags);
            this.immunityTags = immunityTags == null
                ? Array.Empty<ContentTag>()
                : (ContentTag[])immunityTags.Clone();
            immunityTagsView = Array.AsReadOnly(this.immunityTags);
            Behavior = behavior;
        }

        /// <inheritdoc />
        public override string Kind => RuntimeContentKinds.Status;

        /// <summary>Gets the repeated-application policy.</summary>
        public StatusStackingPolicy StackingPolicy { get; }

        /// <summary>Gets the default lifetime in simulation seconds.</summary>
        public float DurationSeconds { get; }

        /// <summary>Gets the maximum stack or independent-instance count.</summary>
        public int MaxStacks { get; }

        /// <summary>Gets the periodic tick interval, or zero when no periodic tick is scheduled.</summary>
        public float TickIntervalSeconds { get; }

        /// <summary>Gets tags that may select this status for dispelling.</summary>
        public IReadOnlyList<ContentTag> DispelTags => dispelTagsView;

        /// <summary>Gets target immunity tags that block this status.</summary>
        public IReadOnlyList<ContentTag> ImmunityTags => immunityTagsView;

        /// <summary>Gets immutable content-baked simulation behavior.</summary>
        public RuntimeStatusBehavior Behavior { get; }

        protected override void AppendTypeSpecificDeterministicData(StringBuilder builder)
        {
            ContentHashUtility.AppendInt(builder, (int)StackingPolicy);
            ContentHashUtility.AppendFloat(builder, DurationSeconds);
            ContentHashUtility.AppendInt(builder, MaxStacks);
            ContentHashUtility.AppendFloat(builder, TickIntervalSeconds);
            ContentHashUtility.AppendInt(builder, dispelTags.Length);
            for (var index = 0; index < dispelTags.Length; index++)
            {
                ContentHashUtility.AppendToken(builder, dispelTags[index].Value);
            }

            ContentHashUtility.AppendInt(builder, immunityTags.Length);
            for (var index = 0; index < immunityTags.Length; index++)
            {
                ContentHashUtility.AppendToken(builder, immunityTags[index].Value);
            }

            var modifier = Behavior.Modifier;
            ContentHashUtility.AppendInt(builder, modifier.Enabled ? 1 : 0);
            if (modifier.Enabled)
            {
                ContentHashUtility.AppendToken(builder, modifier.StatId.Value);
                ContentHashUtility.AppendInt(builder, (int)modifier.Operation);
                ContentHashUtility.AppendFloat(builder, modifier.Value);
                ContentHashUtility.AppendInt(builder, modifier.Priority);
                ContentHashUtility.AppendToken(builder, modifier.StackingGroup.Value);
            }

            var periodic = Behavior.PeriodicDamage;
            ContentHashUtility.AppendInt(builder, periodic.Enabled ? 1 : 0);
            if (periodic.Enabled)
            {
                ContentHashUtility.AppendInt(builder, (int)periodic.DamageType);
                var tags = (ulong)periodic.Tags;
                ContentHashUtility.AppendInt(builder, unchecked((int)(tags >> 32)));
                ContentHashUtility.AppendInt(builder, unchecked((int)tags));
                ContentHashUtility.AppendFloat(builder, periodic.BaseValue);
                ContentHashUtility.AppendInt(builder, periodic.CanCritical ? 1 : 0);
                ContentHashUtility.AppendFloat(builder, periodic.ProcCoefficient);
                ContentHashUtility.AppendFloat(builder, periodic.Knockback.X);
                ContentHashUtility.AppendFloat(builder, periodic.Knockback.Y);
            }

            ContentHashUtility.AppendFloat(builder, Behavior.ShieldCapacity);
        }
    }

    internal static class StatusStackingPolicyCodec
    {
        public const string RefreshDuration = "refresh_duration";
        public const string AddStacks = "add_stacks";
        public const string ReplaceIfStronger = "replace_if_stronger";
        public const string IndependentInstances = "independent_instances";

        public static bool TryParse(string value, out StatusStackingPolicy policy)
        {
            switch (value)
            {
                case RefreshDuration:
                    policy = StatusStackingPolicy.RefreshDuration;
                    return true;
                case AddStacks:
                    policy = StatusStackingPolicy.AddStacks;
                    return true;
                case ReplaceIfStronger:
                    policy = StatusStackingPolicy.ReplaceIfStronger;
                    return true;
                case IndependentInstances:
                    policy = StatusStackingPolicy.IndependentInstances;
                    return true;
                default:
                    policy = StatusStackingPolicy.Invalid;
                    return false;
            }
        }

        public static string ToSerializedValue(StatusStackingPolicy policy)
        {
            switch (policy)
            {
                case StatusStackingPolicy.RefreshDuration:
                    return RefreshDuration;
                case StatusStackingPolicy.AddStacks:
                    return AddStacks;
                case StatusStackingPolicy.ReplaceIfStronger:
                    return ReplaceIfStronger;
                case StatusStackingPolicy.IndependentInstances:
                    return IndependentInstances;
                default:
                    throw new ArgumentOutOfRangeException(nameof(policy));
            }
        }
    }

    internal static class ModifierOperationCodec
    {
        public static bool TryParse(string value, out ModifierOperation operation)
        {
            switch (value)
            {
                case "add_flat":
                    operation = ModifierOperation.AddFlat;
                    return true;
                case "add_percent":
                    operation = ModifierOperation.AddPercent;
                    return true;
                case "multiply":
                    operation = ModifierOperation.Multiply;
                    return true;
                case "clamp_minimum":
                    operation = ModifierOperation.ClampMinimum;
                    return true;
                case "clamp_maximum":
                    operation = ModifierOperation.ClampMaximum;
                    return true;
                case "override":
                    operation = ModifierOperation.Override;
                    return true;
                default:
                    operation = default;
                    return false;
            }
        }

        public static string ToSerializedValue(ModifierOperation operation)
        {
            switch (operation)
            {
                case ModifierOperation.AddFlat:
                    return "add_flat";
                case ModifierOperation.AddPercent:
                    return "add_percent";
                case ModifierOperation.Multiply:
                    return "multiply";
                case ModifierOperation.ClampMinimum:
                    return "clamp_minimum";
                case ModifierOperation.ClampMaximum:
                    return "clamp_maximum";
                case ModifierOperation.Override:
                    return "override";
                default:
                    throw new ArgumentOutOfRangeException(nameof(operation));
            }
        }
    }

    internal static class DamageTypeCodec
    {
        public static bool TryParse(string value, out DamageType damageType)
        {
            switch (value)
            {
                case "physical":
                    damageType = DamageType.Physical;
                    return true;
                case "fire":
                    damageType = DamageType.Fire;
                    return true;
                case "cold":
                    damageType = DamageType.Cold;
                    return true;
                case "lightning":
                    damageType = DamageType.Lightning;
                    return true;
                case "poison":
                    damageType = DamageType.Poison;
                    return true;
                case "true":
                    damageType = DamageType.True;
                    return true;
                default:
                    damageType = default;
                    return false;
            }
        }

        public static string ToSerializedValue(DamageType damageType)
        {
            switch (damageType)
            {
                case DamageType.Physical:
                    return "physical";
                case DamageType.Fire:
                    return "fire";
                case DamageType.Cold:
                    return "cold";
                case DamageType.Lightning:
                    return "lightning";
                case DamageType.Poison:
                    return "poison";
                case DamageType.True:
                    return "true";
                default:
                    throw new ArgumentOutOfRangeException(nameof(damageType));
            }
        }
    }
}
