using System;
using System.Collections.Generic;
using System.Text;
using Game.Core;

namespace Game.Content.Runtime
{
    /// <summary>Stable effect operation codes admitted by content schema 3.</summary>
    public enum EffectOpCode : byte
    {
        /// <summary>Queues an M3 damage packet.</summary>
        Damage = 1,
        /// <summary>Queues centralized healing.</summary>
        Heal = 2,
        /// <summary>Queues an M3 status application.</summary>
        ApplyStatus = 3,
        /// <summary>Queues an M3 tag-based status removal.</summary>
        RemoveStatus = 4,
        /// <summary>Pushes the target away from the source.</summary>
        Knockback = 5,
        /// <summary>Pulls the target toward the source.</summary>
        Pull = 6,
        /// <summary>Adds a statistic modifier through the combat store API.</summary>
        ModifyStat = 7,
        /// <summary>Invokes another registered skill with propagated proc depth.</summary>
        SpawnSecondarySkill = 8,
        /// <summary>Queues a centralized shield grant.</summary>
        GrantShield = 9,
        /// <summary>Adds owner skill resource.</summary>
        GainResource = 10,
        /// <summary>Atomically consumes matching status stacks.</summary>
        ConsumeStatus = 11,
        /// <summary>Consumes matching stacks and queues one scaled damage request.</summary>
        DetonateStatus = 12
    }

    /// <summary>Entity domain used by status-query condition modules.</summary>
    public enum StatusQueryTarget : byte
    {
        /// <summary>The actor that owns the skill instance.</summary>
        Owner = 0,
        /// <summary>The source carried by the trigger context.</summary>
        Source = 1,
        /// <summary>The target carried by the trigger context.</summary>
        Target = 2
    }

    /// <summary>Behavior used when a consume request cannot find every requested stack.</summary>
    public enum StatusConsumeMissingPolicy : byte
    {
        /// <summary>Reject the transaction without consuming any stack.</summary>
        RequireExact = 0,
        /// <summary>Consume all available stacks up to the requested count.</summary>
        ConsumeAvailable = 1
    }

    /// <summary>Optional compact flags interpreted by an effect executor.</summary>
    [Flags]
    public enum EffectOpFlags : ushort
    {
        /// <summary>No flags.</summary>
        None = 0,
        /// <summary>The damage operation may critically strike.</summary>
        CanCritical = 1 << 0
    }

    /// <summary>One explicitly identified module and its compact numeric parameters.</summary>
    public readonly struct SkillModuleDefinition
    {
        /// <summary>Initializes one baked module definition.</summary>
        public SkillModuleDefinition(
            ContentId moduleId,
            float value0 = 0f,
            float value1 = 0f,
            float value2 = 0f,
            float value3 = 0f,
            int int0 = 0,
            int int1 = 0,
            ContentId presentationId = default)
            : this(
                moduleId, value0, value1, value2, value3, int0, int1,
                presentationId, default, default, default, default, default, default)
        {
        }

        private SkillModuleDefinition(
            ContentId moduleId,
            float value0,
            float value1,
            float value2,
            float value3,
            int int0,
            int int1,
            ContentId presentationId,
            ContentId referenceId0 = default,
            ContentId referenceId1 = default,
            ContentTag tag0 = default,
            ContentTag tag1 = default,
            RuntimeContentIndex reference0 = default,
            RuntimeContentIndex reference1 = default)
        {
            ModuleId = moduleId;
            Value0 = value0;
            Value1 = value1;
            Value2 = value2;
            Value3 = value3;
            Int0 = int0;
            Int1 = int1;
            PresentationId = presentationId;
            ReferenceId0 = referenceId0;
            ReferenceId1 = referenceId1;
            Tag0 = tag0;
            Tag1 = tag1;
            Reference0 = reference0;
            Reference1 = reference1;
        }

        /// <summary>Creates a schema-6 module carrying stable reference and tag operands.</summary>
        public static SkillModuleDefinition CreateReferenced(
            ContentId moduleId,
            float value0 = 0f,
            float value1 = 0f,
            float value2 = 0f,
            float value3 = 0f,
            int int0 = 0,
            int int1 = 0,
            ContentId presentationId = default,
            ContentId referenceId0 = default,
            ContentId referenceId1 = default,
            ContentTag tag0 = default,
            ContentTag tag1 = default,
            RuntimeContentIndex reference0 = default,
            RuntimeContentIndex reference1 = default)
        {
            return new SkillModuleDefinition(
                moduleId, value0, value1, value2, value3, int0, int1,
                presentationId, referenceId0, referenceId1, tag0, tag1,
                reference0, reference1);
        }

        /// <summary>Gets the stable executor ID.</summary>
        public ContentId ModuleId { get; }
        /// <summary>Gets the first floating-point parameter.</summary>
        public float Value0 { get; }
        /// <summary>Gets the second floating-point parameter.</summary>
        public float Value1 { get; }
        /// <summary>Gets the third floating-point parameter.</summary>
        public float Value2 { get; }
        /// <summary>Gets the fourth floating-point parameter.</summary>
        public float Value3 { get; }
        /// <summary>Gets the first integer parameter.</summary>
        public int Int0 { get; }
        /// <summary>Gets the second integer parameter.</summary>
        public int Int1 { get; }
        /// <summary>Gets the stable presentation placeholder/profile ID.</summary>
        public ContentId PresentationId { get; }
        public ContentId ReferenceId0 { get; }
        public ContentId ReferenceId1 { get; }
        public ContentTag Tag0 { get; }
        public ContentTag Tag1 { get; }
        public RuntimeContentIndex Reference0 { get; }
        public RuntimeContentIndex Reference1 { get; }

        internal SkillModuleDefinition BindReferences(
            RuntimeContentIndex reference0,
            RuntimeContentIndex reference1)
        {
            return new SkillModuleDefinition(
                ModuleId, Value0, Value1, Value2, Value3, Int0, Int1, PresentationId,
                ReferenceId0, ReferenceId1, Tag0, Tag1, reference0, reference1);
        }

        internal void AppendDeterministicData(StringBuilder builder)
        {
            ContentHashUtility.AppendToken(builder, ModuleId.Value);
            ContentHashUtility.AppendFloat(builder, Value0);
            ContentHashUtility.AppendFloat(builder, Value1);
            ContentHashUtility.AppendFloat(builder, Value2);
            ContentHashUtility.AppendFloat(builder, Value3);
            ContentHashUtility.AppendInt(builder, Int0);
            ContentHashUtility.AppendInt(builder, Int1);
            ContentHashUtility.AppendToken(builder, PresentationId.Value);
            if (ReferenceId0.IsValid || ReferenceId1.IsValid || Tag0.IsValid || Tag1.IsValid)
            {
                ContentHashUtility.AppendToken(builder, ReferenceId0.Value);
                ContentHashUtility.AppendToken(builder, ReferenceId1.Value);
                ContentHashUtility.AppendToken(builder, Tag0.Value);
                ContentHashUtility.AppendToken(builder, Tag1.Value);
            }
        }
    }

    /// <summary>A baked, presentation-independent effect instruction.</summary>
    public readonly struct EffectOp
    {
        /// <summary>Initializes one baked effect instruction.</summary>
        public EffectOp(
            EffectOpCode code,
            float value0 = 0f,
            float value1 = 0f,
            float value2 = 0f,
            int int0 = 0,
            int int1 = 0,
            ContentId referenceId0 = default,
            ContentId referenceId1 = default,
            ContentTag tag0 = default,
            StatId statId0 = default,
            EffectOpFlags flags = EffectOpFlags.None,
            RuntimeContentIndex reference0 = default,
            RuntimeContentIndex reference1 = default)
        {
            Code = code;
            Value0 = value0;
            Value1 = value1;
            Value2 = value2;
            Int0 = int0;
            Int1 = int1;
            ReferenceId0 = referenceId0;
            ReferenceId1 = referenceId1;
            Tag0 = tag0;
            StatId0 = statId0;
            Flags = flags;
            Reference0 = reference0;
            Reference1 = reference1;
        }

        /// <summary>Gets the compact operation code.</summary>
        public EffectOpCode Code { get; }
        /// <summary>Gets the first numeric operand.</summary>
        public float Value0 { get; }
        /// <summary>Gets the second numeric operand.</summary>
        public float Value1 { get; }
        /// <summary>Gets the third numeric operand.</summary>
        public float Value2 { get; }
        /// <summary>Gets the first integer operand.</summary>
        public int Int0 { get; }
        /// <summary>Gets the second integer operand.</summary>
        public int Int1 { get; }
        /// <summary>Gets the stable first content reference preserved on disk.</summary>
        public ContentId ReferenceId0 { get; }
        /// <summary>Gets the stable second content reference preserved on disk.</summary>
        public ContentId ReferenceId1 { get; }
        /// <summary>Gets the optional canonical tag operand.</summary>
        public ContentTag Tag0 { get; }
        /// <summary>Gets the optional stable statistic operand.</summary>
        public StatId StatId0 { get; }
        /// <summary>Gets compact operation flags.</summary>
        public EffectOpFlags Flags { get; }
        /// <summary>Gets the load-local first reference after registry binding.</summary>
        public RuntimeContentIndex Reference0 { get; }
        /// <summary>Gets the load-local second reference after registry binding.</summary>
        public RuntimeContentIndex Reference1 { get; }

        internal EffectOp BindReferences(
            RuntimeContentIndex reference0,
            RuntimeContentIndex reference1)
        {
            return new EffectOp(
                Code,
                Value0,
                Value1,
                Value2,
                Int0,
                Int1,
                ReferenceId0,
                ReferenceId1,
                Tag0,
                StatId0,
                Flags,
                reference0,
                reference1);
        }

        internal void AppendDeterministicData(StringBuilder builder)
        {
            ContentHashUtility.AppendInt(builder, (int)Code);
            ContentHashUtility.AppendFloat(builder, Value0);
            ContentHashUtility.AppendFloat(builder, Value1);
            ContentHashUtility.AppendFloat(builder, Value2);
            ContentHashUtility.AppendInt(builder, Int0);
            ContentHashUtility.AppendInt(builder, Int1);
            ContentHashUtility.AppendToken(builder, ReferenceId0.Value);
            ContentHashUtility.AppendToken(builder, ReferenceId1.Value);
            ContentHashUtility.AppendToken(builder, Tag0.Value);
            ContentHashUtility.AppendToken(builder, StatId0.Value);
            ContentHashUtility.AppendInt(builder, (int)Flags);
        }
    }

    /// <summary>Numeric slots that a baked level patch may modify.</summary>
    public enum SkillPatchTarget : byte
    {
        /// <summary>Skill cooldown.</summary>
        Cooldown = 1,
        /// <summary>Skill resource cost.</summary>
        ResourceCost = 2,
        /// <summary>Trigger floating-point slot zero.</summary>
        TriggerValue0 = 10,
        /// <summary>Trigger floating-point slot one.</summary>
        TriggerValue1 = 11,
        /// <summary>Trigger integer slot zero.</summary>
        TriggerInt0 = 12,
        /// <summary>Targeting floating-point slot zero.</summary>
        TargetingValue0 = 20,
        /// <summary>Targeting floating-point slot one.</summary>
        TargetingValue1 = 21,
        /// <summary>Targeting integer slot zero.</summary>
        TargetingInt0 = 22,
        /// <summary>Delivery floating-point slot zero.</summary>
        DeliveryValue0 = 30,
        /// <summary>Delivery floating-point slot one.</summary>
        DeliveryValue1 = 31,
        /// <summary>Delivery floating-point slot two.</summary>
        DeliveryValue2 = 32,
        /// <summary>Delivery floating-point slot three.</summary>
        DeliveryValue3 = 33,
        /// <summary>Delivery integer slot zero.</summary>
        DeliveryInt0 = 34,
        /// <summary>Effect floating-point slot zero.</summary>
        EffectValue0 = 40,
        /// <summary>Effect floating-point slot one.</summary>
        EffectValue1 = 41,
        /// <summary>Effect floating-point slot two.</summary>
        EffectValue2 = 42,
        /// <summary>Effect integer slot zero.</summary>
        EffectInt0 = 43,
        /// <summary>Effect integer slot one.</summary>
        EffectInt1 = 44
    }

    /// <summary>Type of the numeric slot addressed by a level patch.</summary>
    public enum SkillPatchValueType : byte
    {
        /// <summary>IEEE single-precision value.</summary>
        Float = 1,
        /// <summary>Signed 32-bit integer value.</summary>
        Integer = 2
    }

    /// <summary>Arithmetic applied by a level patch.</summary>
    public enum SkillPatchOperation : byte
    {
        /// <summary>Adds the patch operand.</summary>
        Add = 1,
        /// <summary>Multiplies by the patch operand.</summary>
        Multiply = 2,
        /// <summary>Replaces with the patch operand.</summary>
        Override = 3
    }

    /// <summary>One path-resolved, type-checked level patch instruction.</summary>
    public readonly struct SkillLevelPatch
    {
        /// <summary>Initializes one baked level patch.</summary>
        public SkillLevelPatch(
            int level,
            SkillPatchTarget target,
            int targetIndex,
            SkillPatchValueType valueType,
            SkillPatchOperation operation,
            float floatValue,
            int integerValue)
        {
            Level = level;
            Target = target;
            TargetIndex = targetIndex;
            ValueType = valueType;
            Operation = operation;
            FloatValue = floatValue;
            IntegerValue = integerValue;
        }

        /// <summary>Gets the first level that applies the patch.</summary>
        public int Level { get; }
        /// <summary>Gets the pre-resolved numeric field.</summary>
        public SkillPatchTarget Target { get; }
        /// <summary>Gets the effect index, or zero for non-effect targets.</summary>
        public int TargetIndex { get; }
        /// <summary>Gets the validated numeric value type.</summary>
        public SkillPatchValueType ValueType { get; }
        /// <summary>Gets the arithmetic operation.</summary>
        public SkillPatchOperation Operation { get; }
        /// <summary>Gets the floating-point operand.</summary>
        public float FloatValue { get; }
        /// <summary>Gets the integer operand.</summary>
        public int IntegerValue { get; }

        internal void AppendDeterministicData(StringBuilder builder)
        {
            ContentHashUtility.AppendInt(builder, Level);
            ContentHashUtility.AppendInt(builder, (int)Target);
            ContentHashUtility.AppendInt(builder, TargetIndex);
            ContentHashUtility.AppendInt(builder, (int)ValueType);
            ContentHashUtility.AppendInt(builder, (int)Operation);
            ContentHashUtility.AppendFloat(builder, FloatValue);
            ContentHashUtility.AppendInt(builder, IntegerValue);
        }
    }

    /// <summary>
    /// Explicit schema-3 patch-path table used only while authoring or decoding.
    /// Runtime execution receives resolved enum slots and never parses these strings.
    /// </summary>
    public static class SkillLevelPatchPath
    {
        /// <summary>Resolves one supported path and its required numeric type.</summary>
        public static bool TryResolve(
            string path,
            int effectCount,
            out SkillPatchTarget target,
            out int targetIndex,
            out SkillPatchValueType valueType)
        {
            targetIndex = 0;
            valueType = SkillPatchValueType.Float;
            switch (path)
            {
                case "cooldown": target = SkillPatchTarget.Cooldown; return true;
                case "resource_cost": target = SkillPatchTarget.ResourceCost; return true;
                case "trigger.value0": target = SkillPatchTarget.TriggerValue0; return true;
                case "trigger.value1": target = SkillPatchTarget.TriggerValue1; return true;
                case "trigger.int0":
                    target = SkillPatchTarget.TriggerInt0;
                    valueType = SkillPatchValueType.Integer;
                    return true;
                case "targeting.value0": target = SkillPatchTarget.TargetingValue0; return true;
                case "targeting.value1": target = SkillPatchTarget.TargetingValue1; return true;
                case "targeting.int0":
                    target = SkillPatchTarget.TargetingInt0;
                    valueType = SkillPatchValueType.Integer;
                    return true;
                case "delivery.value0": target = SkillPatchTarget.DeliveryValue0; return true;
                case "delivery.value1": target = SkillPatchTarget.DeliveryValue1; return true;
                case "delivery.value2": target = SkillPatchTarget.DeliveryValue2; return true;
                case "delivery.value3": target = SkillPatchTarget.DeliveryValue3; return true;
                case "delivery.int0":
                    target = SkillPatchTarget.DeliveryInt0;
                    valueType = SkillPatchValueType.Integer;
                    return true;
            }

            const string prefix = "effects[";
            if (string.IsNullOrEmpty(path) || !path.StartsWith(prefix, StringComparison.Ordinal))
            {
                target = default;
                return false;
            }

            var close = path.IndexOf(']', prefix.Length);
            if (close <= prefix.Length || close + 2 >= path.Length || path[close + 1] != '.')
            {
                target = default;
                return false;
            }

            var parsedIndex = 0;
            for (var index = prefix.Length; index < close; index++)
            {
                var character = path[index];
                if (character < '0' || character > '9')
                {
                    target = default;
                    return false;
                }

                var digit = character - '0';
                if (parsedIndex > (int.MaxValue - digit) / 10)
                {
                    target = default;
                    return false;
                }

                parsedIndex = (parsedIndex * 10) + digit;
            }

            if (parsedIndex < 0 || parsedIndex >= effectCount)
            {
                target = default;
                return false;
            }

            var member = path.Substring(close + 2);
            switch (member)
            {
                case "value0": target = SkillPatchTarget.EffectValue0; break;
                case "value1": target = SkillPatchTarget.EffectValue1; break;
                case "value2": target = SkillPatchTarget.EffectValue2; break;
                case "int0":
                    target = SkillPatchTarget.EffectInt0;
                    valueType = SkillPatchValueType.Integer;
                    break;
                case "int1":
                    target = SkillPatchTarget.EffectInt1;
                    valueType = SkillPatchValueType.Integer;
                    break;
                default:
                    target = default;
                    return false;
            }

            targetIndex = parsedIndex;
            return true;
        }

        /// <summary>Returns the canonical schema path for one resolved target.</summary>
        public static string GetPath(SkillPatchTarget target, int targetIndex)
        {
            switch (target)
            {
                case SkillPatchTarget.Cooldown: return "cooldown";
                case SkillPatchTarget.ResourceCost: return "resource_cost";
                case SkillPatchTarget.TriggerValue0: return "trigger.value0";
                case SkillPatchTarget.TriggerValue1: return "trigger.value1";
                case SkillPatchTarget.TriggerInt0: return "trigger.int0";
                case SkillPatchTarget.TargetingValue0: return "targeting.value0";
                case SkillPatchTarget.TargetingValue1: return "targeting.value1";
                case SkillPatchTarget.TargetingInt0: return "targeting.int0";
                case SkillPatchTarget.DeliveryValue0: return "delivery.value0";
                case SkillPatchTarget.DeliveryValue1: return "delivery.value1";
                case SkillPatchTarget.DeliveryValue2: return "delivery.value2";
                case SkillPatchTarget.DeliveryValue3: return "delivery.value3";
                case SkillPatchTarget.DeliveryInt0: return "delivery.int0";
                case SkillPatchTarget.EffectValue0: return "effects[" + targetIndex + "].value0";
                case SkillPatchTarget.EffectValue1: return "effects[" + targetIndex + "].value1";
                case SkillPatchTarget.EffectValue2: return "effects[" + targetIndex + "].value2";
                case SkillPatchTarget.EffectInt0: return "effects[" + targetIndex + "].int0";
                case SkillPatchTarget.EffectInt1: return "effects[" + targetIndex + "].int1";
                default: return string.Empty;
            }
        }
    }

    /// <summary>Stable module IDs admitted by the schema-3 skill contract.</summary>
    public static class SkillModuleIds
    {
        /// <summary>Timer trigger.</summary>
        public static readonly ContentId TriggerTimer = Id("base.trigger.timer");
        /// <summary>On-hit trigger.</summary>
        public static readonly ContentId TriggerOnHit = Id("base.trigger.on_hit");
        /// <summary>On-kill trigger.</summary>
        public static readonly ContentId TriggerOnKill = Id("base.trigger.on_kill");
        /// <summary>On-damage-taken trigger.</summary>
        public static readonly ContentId TriggerOnDamageTaken = Id("base.trigger.on_damage_taken");
        /// <summary>On-pickup trigger.</summary>
        public static readonly ContentId TriggerOnPickup = Id("base.trigger.on_pickup");
        /// <summary>On-status-applied trigger.</summary>
        public static readonly ContentId TriggerOnStatusApplied = Id("base.trigger.on_status_applied");

        /// <summary>Always-true condition.</summary>
        public static readonly ContentId ConditionAlways = Id("base.condition.always");
        public static readonly ContentId ConditionStatusCountAtLeast = Id("base.condition.status_count_at_least");
        public static readonly ContentId ConditionTargetHasStatus = Id("base.condition.target_has_status");

        /// <summary>Self targeting.</summary>
        public static readonly ContentId TargetingSelf = Id("base.targeting.self");
        /// <summary>Nearest-actor targeting.</summary>
        public static readonly ContentId TargetingNearest = Id("base.targeting.nearest");
        /// <summary>Random-actor targeting.</summary>
        public static readonly ContentId TargetingRandom = Id("base.targeting.random");
        /// <summary>Circle targeting.</summary>
        public static readonly ContentId TargetingCircle = Id("base.targeting.circle");
        /// <summary>Cone targeting.</summary>
        public static readonly ContentId TargetingCone = Id("base.targeting.cone");
        /// <summary>Line targeting.</summary>
        public static readonly ContentId TargetingLine = Id("base.targeting.line");
        /// <summary>Ring targeting.</summary>
        public static readonly ContentId TargetingRing = Id("base.targeting.ring");
        /// <summary>Random point around owner targeting.</summary>
        public static readonly ContentId TargetingRandomPointAroundPlayer =
            Id("base.targeting.random_point_around_player");
        public static readonly ContentId TargetingTriggerPosition = Id("base.targeting.trigger_position");

        /// <summary>Immediate delivery.</summary>
        public static readonly ContentId DeliveryInstant = Id("base.delivery.instant");
        /// <summary>Projectile delivery.</summary>
        public static readonly ContentId DeliveryProjectile = Id("base.delivery.projectile");
        /// <summary>Ground-area delivery.</summary>
        public static readonly ContentId DeliveryArea = Id("base.delivery.area");
        /// <summary>Owner-following aura delivery.</summary>
        public static readonly ContentId DeliveryAura = Id("base.delivery.aura");
        /// <summary>Owner-orbiting delivery.</summary>
        public static readonly ContentId DeliveryOrbit = Id("base.delivery.orbit");
        public static readonly ContentId DeliveryOutboundReturn = Id("base.delivery.outbound_return");

        /// <summary>Damage effect.</summary>
        public static readonly ContentId EffectDamage = Id("base.effect.damage");
        /// <summary>Heal effect.</summary>
        public static readonly ContentId EffectHeal = Id("base.effect.heal");
        /// <summary>Apply-status effect.</summary>
        public static readonly ContentId EffectApplyStatus = Id("base.effect.apply_status");
        /// <summary>Remove-status effect.</summary>
        public static readonly ContentId EffectRemoveStatus = Id("base.effect.remove_status");
        /// <summary>Knockback effect.</summary>
        public static readonly ContentId EffectKnockback = Id("base.effect.knockback");
        /// <summary>Pull effect.</summary>
        public static readonly ContentId EffectPull = Id("base.effect.pull");
        /// <summary>Statistic-modifier effect.</summary>
        public static readonly ContentId EffectModifyStat = Id("base.effect.modify_stat");
        /// <summary>Secondary-skill effect.</summary>
        public static readonly ContentId EffectSpawnSecondarySkill =
            Id("base.effect.spawn_secondary_skill");
        /// <summary>Shield-grant effect.</summary>
        public static readonly ContentId EffectGrantShield = Id("base.effect.grant_shield");
        /// <summary>Resource-gain effect.</summary>
        public static readonly ContentId EffectGainResource = Id("base.effect.gain_resource");
        public static readonly ContentId EffectConsumeStatus = Id("base.effect.consume_status");
        public static readonly ContentId EffectDetonateStatus = Id("base.effect.detonate_status");

        /// <summary>Returns whether an ID is a built-in trigger module.</summary>
        public static bool IsTrigger(ContentId id)
        {
            return id == TriggerTimer || id == TriggerOnHit || id == TriggerOnKill ||
                   id == TriggerOnDamageTaken || id == TriggerOnPickup ||
                   id == TriggerOnStatusApplied;
        }

        /// <summary>Returns whether an ID is a built-in condition module.</summary>
        public static bool IsCondition(ContentId id)
        {
            return id == ConditionAlways || id == ConditionStatusCountAtLeast || id == ConditionTargetHasStatus;
        }

        /// <summary>Returns whether an ID is a built-in targeting module.</summary>
        public static bool IsTargeting(ContentId id)
        {
            return id == TargetingSelf || id == TargetingNearest ||
                   id == TargetingRandom || id == TargetingCircle ||
                   id == TargetingCone || id == TargetingLine ||
                   id == TargetingRing || id == TargetingRandomPointAroundPlayer ||
                   id == TargetingTriggerPosition;
        }

        /// <summary>Returns whether an ID is a built-in delivery module.</summary>
        public static bool IsDelivery(ContentId id)
        {
            return id == DeliveryInstant || id == DeliveryProjectile ||
                   id == DeliveryArea || id == DeliveryAura || id == DeliveryOrbit ||
                   id == DeliveryOutboundReturn;
        }

        /// <summary>Maps a stable effect module ID to its compact operation code.</summary>
        public static bool TryGetEffectCode(ContentId id, out EffectOpCode code)
        {
            if (id == EffectDamage) code = EffectOpCode.Damage;
            else if (id == EffectHeal) code = EffectOpCode.Heal;
            else if (id == EffectApplyStatus) code = EffectOpCode.ApplyStatus;
            else if (id == EffectRemoveStatus) code = EffectOpCode.RemoveStatus;
            else if (id == EffectKnockback) code = EffectOpCode.Knockback;
            else if (id == EffectPull) code = EffectOpCode.Pull;
            else if (id == EffectModifyStat) code = EffectOpCode.ModifyStat;
            else if (id == EffectSpawnSecondarySkill) code = EffectOpCode.SpawnSecondarySkill;
            else if (id == EffectGrantShield) code = EffectOpCode.GrantShield;
            else if (id == EffectGainResource) code = EffectOpCode.GainResource;
            else if (id == EffectConsumeStatus) code = EffectOpCode.ConsumeStatus;
            else if (id == EffectDetonateStatus) code = EffectOpCode.DetonateStatus;
            else
            {
                code = default;
                return false;
            }

            return true;
        }

        /// <summary>Maps a compact effect operation code to its stable module ID.</summary>
        public static ContentId GetEffectId(EffectOpCode code)
        {
            switch (code)
            {
                case EffectOpCode.Damage: return EffectDamage;
                case EffectOpCode.Heal: return EffectHeal;
                case EffectOpCode.ApplyStatus: return EffectApplyStatus;
                case EffectOpCode.RemoveStatus: return EffectRemoveStatus;
                case EffectOpCode.Knockback: return EffectKnockback;
                case EffectOpCode.Pull: return EffectPull;
                case EffectOpCode.ModifyStat: return EffectModifyStat;
                case EffectOpCode.SpawnSecondarySkill: return EffectSpawnSecondarySkill;
                case EffectOpCode.GrantShield: return EffectGrantShield;
                case EffectOpCode.GainResource: return EffectGainResource;
                case EffectOpCode.ConsumeStatus: return EffectConsumeStatus;
                case EffectOpCode.DetonateStatus: return EffectDetonateStatus;
                default: return default;
            }
        }

        private static ContentId Id(string value)
        {
            var result = ContentId.Create(value);
            if (!result.IsSuccess)
            {
                throw new InvalidOperationException(result.Error.Message);
            }

            return result.Value;
        }
    }

    /// <summary>Pure schema-3 skill definition shared by any number of skill instances.</summary>
    public sealed class RuntimeSkillDefinition : RuntimeContentDefinition
    {
        private readonly EffectOp[] effects;
        private readonly IReadOnlyList<EffectOp> effectsView;
        private readonly SkillLevelPatch[] levelPatches;
        private readonly IReadOnlyList<SkillLevelPatch> levelPatchesView;

        /// <summary>Initializes legacy schema-1/2 skill metadata.</summary>
        public RuntimeSkillDefinition(
            ContentId id,
            string localizedNameKey,
            string localizedDescriptionKey,
            string sourceAssetPath,
            ContentTag[] tags,
            float cooldownSeconds)
            : this(
                id,
                localizedNameKey,
                localizedDescriptionKey,
                sourceAssetPath,
                tags,
                cooldownSeconds,
                0f,
                default,
                default,
                default,
                default,
                Array.Empty<EffectOp>(),
                Array.Empty<SkillLevelPatch>(),
                false)
        {
        }

        /// <summary>Initializes one executable schema-3 modular skill.</summary>
        public RuntimeSkillDefinition(
            ContentId id,
            string localizedNameKey,
            string localizedDescriptionKey,
            string sourceAssetPath,
            ContentTag[] tags,
            float cooldownSeconds,
            float resourceCost,
            SkillModuleDefinition trigger,
            SkillModuleDefinition condition,
            SkillModuleDefinition targeting,
            SkillModuleDefinition delivery,
            EffectOp[] effects,
            SkillLevelPatch[] levelPatches)
            : this(
                id,
                localizedNameKey,
                localizedDescriptionKey,
                sourceAssetPath,
                tags,
                cooldownSeconds,
                resourceCost,
                trigger,
                condition,
                targeting,
                delivery,
                effects,
                levelPatches,
                true)
        {
        }

        private RuntimeSkillDefinition(
            ContentId id,
            string localizedNameKey,
            string localizedDescriptionKey,
            string sourceAssetPath,
            ContentTag[] tags,
            float cooldownSeconds,
            float resourceCost,
            SkillModuleDefinition trigger,
            SkillModuleDefinition condition,
            SkillModuleDefinition targeting,
            SkillModuleDefinition delivery,
            EffectOp[] effects,
            SkillLevelPatch[] levelPatches,
            bool executable)
            : base(
                id,
                localizedNameKey,
                localizedDescriptionKey,
                sourceAssetPath,
                tags,
                CollectReferences(trigger, condition, targeting, delivery, effects))
        {
            CooldownSeconds = cooldownSeconds;
            ResourceCost = resourceCost;
            Trigger = trigger;
            Condition = condition;
            Targeting = targeting;
            Delivery = delivery;
            this.effects = effects == null ? Array.Empty<EffectOp>() : (EffectOp[])effects.Clone();
            effectsView = Array.AsReadOnly(this.effects);
            this.levelPatches = levelPatches == null
                ? Array.Empty<SkillLevelPatch>()
                : (SkillLevelPatch[])levelPatches.Clone();
            levelPatchesView = Array.AsReadOnly(this.levelPatches);
            IsExecutable = executable;
        }

        /// <inheritdoc />
        public override string Kind => RuntimeContentKinds.Skill;
        /// <summary>Gets whether this definition contains schema-3 execution data.</summary>
        public bool IsExecutable { get; }
        /// <summary>Gets cooldown in seconds.</summary>
        public float CooldownSeconds { get; }
        /// <summary>Gets resource consumed by a successful activation.</summary>
        public float ResourceCost { get; }
        /// <summary>Gets the trigger module.</summary>
        public SkillModuleDefinition Trigger { get; }
        /// <summary>Gets the condition module.</summary>
        public SkillModuleDefinition Condition { get; }
        /// <summary>Gets the targeting module.</summary>
        public SkillModuleDefinition Targeting { get; }
        /// <summary>Gets the delivery module.</summary>
        public SkillModuleDefinition Delivery { get; }
        /// <summary>Gets baked effect operations in author order.</summary>
        public IReadOnlyList<EffectOp> Effects => effectsView;
        /// <summary>Gets path-resolved level patches in author order.</summary>
        public IReadOnlyList<SkillLevelPatch> LevelPatches => levelPatchesView;

        /// <summary>Gets the highest authored one-based skill level.</summary>
        public int MaximumLevel => levelPatches.Length == 0
            ? 1
            : levelPatches[levelPatches.Length - 1].Level;

        internal RuntimeSkillDefinition BindReferences(
            Func<ContentId, RuntimeContentIndex> resolver)
        {
            if (!IsExecutable)
            {
                return this;
            }

            var bound = new EffectOp[effects.Length];
            for (var index = 0; index < effects.Length; index++)
            {
                var source = effects[index];
                var first = source.ReferenceId0.IsValid
                    ? resolver(source.ReferenceId0)
                    : default;
                var second = source.ReferenceId1.IsValid
                    ? resolver(source.ReferenceId1)
                    : default;
                bound[index] = source.BindReferences(first, second);
            }

            var boundTrigger = BindModule(Trigger, resolver);
            var boundCondition = BindModule(Condition, resolver);
            var boundTargeting = BindModule(Targeting, resolver);
            var boundDelivery = BindModule(Delivery, resolver);

            return new RuntimeSkillDefinition(
                Id,
                LocalizedNameKey,
                LocalizedDescriptionKey,
                SourceAssetPath,
                CopyTags(Tags),
                CooldownSeconds,
                ResourceCost,
                boundTrigger,
                boundCondition,
                boundTargeting,
                boundDelivery,
                bound,
                levelPatches,
                true);
        }

        protected override void AppendTypeSpecificDeterministicData(StringBuilder builder)
        {
            ContentHashUtility.AppendFloat(builder, CooldownSeconds);
            if (!IsExecutable)
            {
                return;
            }

            ContentHashUtility.AppendFloat(builder, ResourceCost);
            Trigger.AppendDeterministicData(builder);
            Condition.AppendDeterministicData(builder);
            Targeting.AppendDeterministicData(builder);
            Delivery.AppendDeterministicData(builder);
            ContentHashUtility.AppendInt(builder, effects.Length);
            for (var index = 0; index < effects.Length; index++)
            {
                effects[index].AppendDeterministicData(builder);
            }

            ContentHashUtility.AppendInt(builder, levelPatches.Length);
            for (var index = 0; index < levelPatches.Length; index++)
            {
                levelPatches[index].AppendDeterministicData(builder);
            }
        }

        private static ContentId[] CollectReferences(
            SkillModuleDefinition trigger,
            SkillModuleDefinition condition,
            SkillModuleDefinition targeting,
            SkillModuleDefinition delivery,
            EffectOp[] source)
        {
            source = source ?? Array.Empty<EffectOp>();
            var count = 0;
            CountModuleReferences(trigger, ref count);
            CountModuleReferences(condition, ref count);
            CountModuleReferences(targeting, ref count);
            CountModuleReferences(delivery, ref count);
            for (var index = 0; index < source.Length; index++)
            {
                if (source[index].ReferenceId0.IsValid) count++;
                if (source[index].ReferenceId1.IsValid) count++;
            }

            var output = new ContentId[count];
            var write = 0;
            WriteModuleReferences(trigger, output, ref write);
            WriteModuleReferences(condition, output, ref write);
            WriteModuleReferences(targeting, output, ref write);
            WriteModuleReferences(delivery, output, ref write);
            for (var index = 0; index < source.Length; index++)
            {
                if (source[index].ReferenceId0.IsValid) output[write++] = source[index].ReferenceId0;
                if (source[index].ReferenceId1.IsValid) output[write++] = source[index].ReferenceId1;
            }

            return output;
        }

        private static SkillModuleDefinition BindModule(
            SkillModuleDefinition module,
            Func<ContentId, RuntimeContentIndex> resolver)
        {
            return module.BindReferences(
                module.ReferenceId0.IsValid ? resolver(module.ReferenceId0) : default,
                module.ReferenceId1.IsValid ? resolver(module.ReferenceId1) : default);
        }

        private static void CountModuleReferences(SkillModuleDefinition module, ref int count)
        {
            if (module.ReferenceId0.IsValid) count++;
            if (module.ReferenceId1.IsValid) count++;
        }

        private static void WriteModuleReferences(SkillModuleDefinition module, ContentId[] output, ref int write)
        {
            if (module.ReferenceId0.IsValid) output[write++] = module.ReferenceId0;
            if (module.ReferenceId1.IsValid) output[write++] = module.ReferenceId1;
        }

        private static ContentTag[] CopyTags(IReadOnlyList<ContentTag> source)
        {
            var output = new ContentTag[source.Count];
            for (var index = 0; index < source.Count; index++) output[index] = source[index];
            return output;
        }
    }
}
