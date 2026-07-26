using System;
using System.Collections.Generic;
using System.Numerics;
using Game.Content.Runtime;
using Game.Core;

namespace Game.Simulation
{
    /// <summary>Trigger events understood by the initial M4 trigger executors.</summary>
    public enum SkillTriggerEventType : byte
    {
        /// <summary>Cooldown-driven timer event.</summary>
        Timer = 1,
        /// <summary>Owner dealt resolved damage.</summary>
        OnHit = 2,
        /// <summary>Owner caused a finalized death.</summary>
        OnKill = 3,
        /// <summary>Owner received resolved damage.</summary>
        OnDamageTaken = 4,
        /// <summary>Owner collected a pickup.</summary>
        OnPickup = 5,
        /// <summary>Owner received a status.</summary>
        OnStatusApplied = 6,
        /// <summary>Explicit secondary-skill invocation.</summary>
        SecondarySkill = 7
    }

    /// <summary>Pure event context passed from simulation events into skill triggers.</summary>
    public readonly struct SkillTriggerContext
    {
        /// <summary>Initializes one trigger context.</summary>
        public SkillTriggerContext(
            SkillTriggerEventType eventType,
            SpatialEntity source,
            SpatialEntity target,
            Vector2 position,
            Vector2 direction,
            ContentId sourceContentId,
            RuntimeContentIndex referenceIndex,
            int procDepth)
        {
            EventType = eventType;
            Source = source;
            Target = target;
            Position = position;
            Direction = direction;
            SourceContentId = sourceContentId;
            ReferenceIndex = referenceIndex;
            ProcDepth = procDepth;
        }

        /// <summary>Gets the event category.</summary>
        public SkillTriggerEventType EventType { get; }
        /// <summary>Gets the event source.</summary>
        public SpatialEntity Source { get; }
        /// <summary>Gets the event target.</summary>
        public SpatialEntity Target { get; }
        /// <summary>Gets the event position.</summary>
        public Vector2 Position { get; }
        /// <summary>Gets an optional normalized or aim direction.</summary>
        public Vector2 Direction { get; }
        /// <summary>Gets the stable content source.</summary>
        public ContentId SourceContentId { get; }
        /// <summary>Gets an optional load-local content reference.</summary>
        public RuntimeContentIndex ReferenceIndex { get; }
        /// <summary>Gets the propagated proc-chain depth.</summary>
        public int ProcDepth { get; }
    }

    /// <summary>One entity or world-point selected by a targeting executor.</summary>
    public readonly struct SkillTarget
    {
        /// <summary>Initializes one target.</summary>
        public SkillTarget(SpatialEntity entity, Vector2 position, bool hasEntity)
        {
            Entity = entity;
            Position = position;
            HasEntity = hasEntity;
        }

        /// <summary>Gets the selected entity when present.</summary>
        public SpatialEntity Entity { get; }
        /// <summary>Gets the captured target position.</summary>
        public Vector2 Position { get; }
        /// <summary>Gets whether Entity is meaningful.</summary>
        public bool HasEntity { get; }
    }

    /// <summary>Caller-owned reusable target output storage.</summary>
    public sealed class SkillTargetResultBuffer
    {
        private SkillTarget[] targets;

        /// <summary>Initializes a reusable target buffer.</summary>
        public SkillTargetResultBuffer(int initialCapacity = 16)
        {
            if (initialCapacity <= 0) throw new ArgumentOutOfRangeException(nameof(initialCapacity));
            targets = new SkillTarget[initialCapacity];
        }

        /// <summary>Gets the target count.</summary>
        public int Count { get; private set; }

        /// <summary>Gets one selected target.</summary>
        public SkillTarget this[int index]
        {
            get
            {
                if (index < 0 || index >= Count) throw new ArgumentOutOfRangeException(nameof(index));
                return targets[index];
            }
        }

        internal void Reset()
        {
            Count = 0;
        }

        internal void Add(in SkillTarget target)
        {
            if (Count == targets.Length) Array.Resize(ref targets, targets.Length * 2);
            targets[Count++] = target;
        }

        internal void Swap(int first, int second)
        {
            var temporary = targets[first];
            targets[first] = targets[second];
            targets[second] = temporary;
        }

        internal void Truncate(int count)
        {
            if (count < 0 || count > Count) throw new ArgumentOutOfRangeException(nameof(count));
            for (var index = count; index < Count; index++) targets[index] = default;
            Count = count;
        }

        internal void SortStable()
        {
            for (var index = 1; index < Count; index++)
            {
                var value = targets[index];
                var destination = index - 1;
                while (destination >= 0 && Compare(value, targets[destination]) < 0)
                {
                    targets[destination + 1] = targets[destination];
                    destination--;
                }

                targets[destination + 1] = value;
            }
        }

        internal void SortByDistance(Vector2 origin)
        {
            for (var index = 1; index < Count; index++)
            {
                var value = targets[index];
                var valueDistance = Vector2.DistanceSquared(origin, value.Position);
                var destination = index - 1;
                while (destination >= 0)
                {
                    var candidate = targets[destination];
                    var candidateDistance = Vector2.DistanceSquared(origin, candidate.Position);
                    if (candidateDistance < valueDistance ||
                        (candidateDistance == valueDistance && Compare(value, candidate) >= 0))
                    {
                        break;
                    }

                    targets[destination + 1] = candidate;
                    destination--;
                }

                targets[destination + 1] = value;
            }
        }

        private static int Compare(in SkillTarget left, in SkillTarget right)
        {
            if (left.HasEntity != right.HasEntity) return left.HasEntity ? -1 : 1;
            if (left.HasEntity)
            {
                var kind = left.Entity.Kind.CompareTo(right.Entity.Kind);
                if (kind != 0) return kind;
                var slot = left.Entity.Handle.Index.CompareTo(right.Entity.Handle.Index);
                if (slot != 0) return slot;
                return left.Entity.Handle.Generation.CompareTo(right.Entity.Handle.Generation);
            }

            var x = left.Position.X.CompareTo(right.Position.X);
            return x != 0 ? x : left.Position.Y.CompareTo(right.Position.Y);
        }
    }

    /// <summary>High-frequency effect instruction with content and statistic indices resolved.</summary>
    public readonly struct ResolvedEffectOp
    {
        internal ResolvedEffectOp(in EffectOp source, StatIndex statIndex)
        {
            Code = source.Code;
            Value0 = source.Value0;
            Value1 = source.Value1;
            Value2 = source.Value2;
            Int0 = source.Int0;
            Int1 = source.Int1;
            Reference0 = source.Reference0;
            Reference1 = source.Reference1;
            Tag0 = source.Tag0;
            StatId0 = source.StatId0;
            StatIndex0 = statIndex;
            Flags = source.Flags;
        }

        internal ResolvedEffectOp(
            EffectOpCode code,
            float value0,
            float value1,
            float value2,
            int int0,
            int int1,
            RuntimeContentIndex reference0,
            RuntimeContentIndex reference1,
            ContentTag tag0,
            StatId statId0,
            StatIndex statIndex0,
            EffectOpFlags flags)
        {
            Code = code;
            Value0 = value0;
            Value1 = value1;
            Value2 = value2;
            Int0 = int0;
            Int1 = int1;
            Reference0 = reference0;
            Reference1 = reference1;
            Tag0 = tag0;
            StatId0 = statId0;
            StatIndex0 = statIndex0;
            Flags = flags;
        }

        /// <summary>Gets the effect code.</summary>
        public EffectOpCode Code { get; }
        /// <summary>Gets numeric operand zero.</summary>
        public float Value0 { get; }
        /// <summary>Gets numeric operand one.</summary>
        public float Value1 { get; }
        /// <summary>Gets numeric operand two.</summary>
        public float Value2 { get; }
        /// <summary>Gets integer operand zero.</summary>
        public int Int0 { get; }
        /// <summary>Gets integer operand one.</summary>
        public int Int1 { get; }
        /// <summary>Gets load-local content reference zero.</summary>
        public RuntimeContentIndex Reference0 { get; }
        /// <summary>Gets load-local content reference one.</summary>
        public RuntimeContentIndex Reference1 { get; }
        /// <summary>Gets canonical tag operand zero.</summary>
        public ContentTag Tag0 { get; }
        /// <summary>Gets stable statistic ID zero.</summary>
        public StatId StatId0 { get; }
        /// <summary>Gets compact statistic index zero.</summary>
        public StatIndex StatIndex0 { get; }
        /// <summary>Gets effect flags.</summary>
        public EffectOpFlags Flags { get; }
    }

    /// <summary>Fully patched immutable execution values for one skill level.</summary>
    public sealed class RuntimeSkillLevel
    {
        private readonly ResolvedEffectOp[] effects;
        private readonly IReadOnlyList<ResolvedEffectOp> effectsView;

        internal RuntimeSkillLevel(
            int level,
            float cooldown,
            float resourceCost,
            in SkillModuleDefinition trigger,
            in SkillModuleDefinition targeting,
            in SkillModuleDefinition delivery,
            ResolvedEffectOp[] effects)
        {
            Level = level;
            CooldownSeconds = cooldown;
            ResourceCost = resourceCost;
            Trigger = trigger;
            Targeting = targeting;
            Delivery = delivery;
            this.effects = effects;
            effectsView = Array.AsReadOnly(effects);
        }

        /// <summary>Gets the one-based level.</summary>
        public int Level { get; }
        /// <summary>Gets patched cooldown.</summary>
        public float CooldownSeconds { get; }
        /// <summary>Gets patched resource cost.</summary>
        public float ResourceCost { get; }
        /// <summary>Gets patched trigger numeric data.</summary>
        public SkillModuleDefinition Trigger { get; }
        /// <summary>Gets patched targeting numeric data.</summary>
        public SkillModuleDefinition Targeting { get; }
        /// <summary>Gets patched delivery numeric data.</summary>
        public SkillModuleDefinition Delivery { get; }
        /// <summary>Gets patched effects.</summary>
        public IReadOnlyList<ResolvedEffectOp> Effects => effectsView;

        internal ResolvedEffectOp GetEffectAt(int index)
        {
            return effects[index];
        }
    }

    /// <summary>Compiled definition shared by all instances of one loaded skill.</summary>
    public sealed class CompiledSkillDefinition
    {
        private readonly RuntimeSkillLevel[] levels;
        private readonly IEffectExecutor[] effects;

        internal CompiledSkillDefinition(
            RuntimeContentIndex index,
            RuntimeSkillDefinition source,
            ITriggerExecutor trigger,
            IConditionEvaluator condition,
            ITargetingExecutor targeting,
            IDeliveryExecutor delivery,
            IEffectExecutor[] effects,
            RuntimeSkillLevel[] levels)
        {
            Index = index;
            Source = source;
            TriggerExecutor = trigger;
            ConditionEvaluator = condition;
            TargetingExecutor = targeting;
            DeliveryExecutor = delivery;
            this.effects = effects;
            this.levels = levels;
        }

        /// <summary>Gets the load-local skill index.</summary>
        public RuntimeContentIndex Index { get; }
        /// <summary>Gets the shared pure content definition.</summary>
        public RuntimeSkillDefinition Source { get; }
        /// <summary>Gets maximum authored level.</summary>
        public int MaximumLevel => levels.Length;
        internal ITriggerExecutor TriggerExecutor { get; }
        internal IConditionEvaluator ConditionEvaluator { get; }
        internal ITargetingExecutor TargetingExecutor { get; }
        internal IDeliveryExecutor DeliveryExecutor { get; }

        /// <summary>Gets immutable patched values for a valid level.</summary>
        public RuntimeSkillLevel GetLevel(int level)
        {
            if (level < 1 || level > levels.Length) throw new ArgumentOutOfRangeException(nameof(level));
            return levels[level - 1];
        }

        internal IEffectExecutor GetEffectExecutorAt(int index)
        {
            return effects[index];
        }
    }

    /// <summary>Load-local collection of compiled executable skills.</summary>
    public sealed class SkillRuntimeCatalog
    {
        private readonly CompiledSkillDefinition[] definitions;

        private SkillRuntimeCatalog(CompiledSkillDefinition[] definitions)
        {
            this.definitions = definitions;
        }

        /// <summary>Compiles all executable skill definitions from a validated content registry.</summary>
        public static Result<SkillRuntimeCatalog> Build(
            ContentRegistry content,
            SkillModuleRegistry modules,
            StatCatalog stats = null)
        {
            if (content == null) throw new ArgumentNullException(nameof(content));
            if (modules == null) throw new ArgumentNullException(nameof(modules));
            var statCatalog = stats ?? StatCatalog.Default;
            var compiled = new CompiledSkillDefinition[content.Count];
            for (var index = 0; index < content.Count; index++)
            {
                var entryResult = content.Get(new RuntimeContentIndex(index));
                if (!entryResult.IsSuccess)
                {
                    return Result<SkillRuntimeCatalog>.Failure(entryResult.Error);
                }

                if (!(entryResult.Value.Definition is RuntimeSkillDefinition skill) ||
                    !skill.IsExecutable)
                {
                    continue;
                }

                var compileResult = Compile(
                    entryResult.Value.Index,
                    skill,
                    modules,
                    statCatalog);
                if (!compileResult.IsSuccess)
                {
                    return Result<SkillRuntimeCatalog>.Failure(compileResult.Error);
                }

                compiled[index] = compileResult.Value;
            }

            return Result<SkillRuntimeCatalog>.Success(new SkillRuntimeCatalog(compiled));
        }

        /// <summary>Tries to resolve a compiled executable skill.</summary>
        public bool TryGet(RuntimeContentIndex index, out CompiledSkillDefinition definition)
        {
            if (!index.IsValid || index.Value >= definitions.Length)
            {
                definition = null;
                return false;
            }

            definition = definitions[index.Value];
            return definition != null;
        }

        internal static SkillRuntimeCatalog Empty()
        {
            return new SkillRuntimeCatalog(Array.Empty<CompiledSkillDefinition>());
        }

        private static Result<CompiledSkillDefinition> Compile(
            RuntimeContentIndex index,
            RuntimeSkillDefinition skill,
            SkillModuleRegistry modules,
            StatCatalog stats)
        {
            if (!modules.TryGetTrigger(skill.Trigger.ModuleId, out var trigger) ||
                !modules.TryGetCondition(skill.Condition.ModuleId, out var condition) ||
                !modules.TryGetTargeting(skill.Targeting.ModuleId, out var targeting) ||
                !modules.TryGetDelivery(skill.Delivery.ModuleId, out var delivery))
            {
                return Failure(skill, "Skill references a module that is not present in the runtime registry.");
            }

            var effectExecutors = new IEffectExecutor[skill.Effects.Count];
            var baseEffects = new ResolvedEffectOp[skill.Effects.Count];
            for (var effectIndex = 0; effectIndex < skill.Effects.Count; effectIndex++)
            {
                var source = skill.Effects[effectIndex];
                if (!modules.TryGetEffect(source.Code, out effectExecutors[effectIndex]))
                {
                    return Failure(skill, "Skill effect executor is not present in the runtime registry.");
                }

                var statIndex = default(StatIndex);
                if (source.StatId0.IsValid && !stats.TryGetIndex(source.StatId0, out statIndex))
                {
                    return Failure(skill, "Skill effect references an unknown StatId.");
                }

                if ((source.Code == EffectOpCode.ApplyStatus ||
                     source.Code == EffectOpCode.SpawnSecondarySkill) &&
                    !source.Reference0.IsValid)
                {
                    return Failure(skill, "Skill effect content reference was not bound by ContentRegistry.");
                }

                baseEffects[effectIndex] = new ResolvedEffectOp(source, statIndex);
            }

            var maximumLevel = skill.LevelPatches.Count == 0
                ? 1
                : skill.LevelPatches[skill.LevelPatches.Count - 1].Level;
            var levels = new RuntimeSkillLevel[maximumLevel];
            var currentCooldown = skill.CooldownSeconds;
            var currentCost = skill.ResourceCost;
            var currentTrigger = skill.Trigger;
            var currentTargeting = skill.Targeting;
            var currentDelivery = skill.Delivery;
            var currentEffects = baseEffects;
            var patchIndex = 0;
            for (var level = 1; level <= maximumLevel; level++)
            {
                if (level > 1)
                {
                    currentEffects = (ResolvedEffectOp[])currentEffects.Clone();
                    while (patchIndex < skill.LevelPatches.Count &&
                           skill.LevelPatches[patchIndex].Level == level)
                    {
                        var patch = skill.LevelPatches[patchIndex++];
                        ApplyPatch(
                            patch,
                            ref currentCooldown,
                            ref currentCost,
                            ref currentTrigger,
                            ref currentTargeting,
                            ref currentDelivery,
                            currentEffects);
                    }
                }

                if (!IsFinite(currentCooldown) || currentCooldown < 0f ||
                    !IsFinite(currentCost) || currentCost < 0f)
                {
                    return Failure(skill, "LevelPatch produced an invalid cooldown or resource cost.");
                }

                levels[level - 1] = new RuntimeSkillLevel(
                    level,
                    currentCooldown,
                    currentCost,
                    currentTrigger,
                    currentTargeting,
                    currentDelivery,
                    (ResolvedEffectOp[])currentEffects.Clone());
            }

            return Result<CompiledSkillDefinition>.Success(
                new CompiledSkillDefinition(
                    index,
                    skill,
                    trigger,
                    condition,
                    targeting,
                    delivery,
                    effectExecutors,
                    levels));
        }

        private static void ApplyPatch(
            in SkillLevelPatch patch,
            ref float cooldown,
            ref float resourceCost,
            ref SkillModuleDefinition trigger,
            ref SkillModuleDefinition targeting,
            ref SkillModuleDefinition delivery,
            ResolvedEffectOp[] effects)
        {
            if (patch.Target == SkillPatchTarget.Cooldown)
            {
                cooldown = Patch(cooldown, patch.Operation, patch.FloatValue);
                return;
            }

            if (patch.Target == SkillPatchTarget.ResourceCost)
            {
                resourceCost = Patch(resourceCost, patch.Operation, patch.FloatValue);
                return;
            }

            if (patch.Target >= SkillPatchTarget.TriggerValue0 &&
                patch.Target <= SkillPatchTarget.TriggerInt0)
            {
                trigger = PatchModule(trigger, patch);
                return;
            }

            if (patch.Target >= SkillPatchTarget.TargetingValue0 &&
                patch.Target <= SkillPatchTarget.TargetingInt0)
            {
                targeting = PatchModule(targeting, patch);
                return;
            }

            if (patch.Target >= SkillPatchTarget.DeliveryValue0 &&
                patch.Target <= SkillPatchTarget.DeliveryInt0)
            {
                delivery = PatchModule(delivery, patch);
                return;
            }

            var source = effects[patch.TargetIndex];
            var value0 = source.Value0;
            var value1 = source.Value1;
            var value2 = source.Value2;
            var int0 = source.Int0;
            var int1 = source.Int1;
            switch (patch.Target)
            {
                case SkillPatchTarget.EffectValue0: value0 = Patch(value0, patch.Operation, patch.FloatValue); break;
                case SkillPatchTarget.EffectValue1: value1 = Patch(value1, patch.Operation, patch.FloatValue); break;
                case SkillPatchTarget.EffectValue2: value2 = Patch(value2, patch.Operation, patch.FloatValue); break;
                case SkillPatchTarget.EffectInt0: int0 = Patch(int0, patch.Operation, patch.IntegerValue); break;
                case SkillPatchTarget.EffectInt1: int1 = Patch(int1, patch.Operation, patch.IntegerValue); break;
            }

            effects[patch.TargetIndex] = new ResolvedEffectOp(
                source.Code,
                value0,
                value1,
                value2,
                int0,
                int1,
                source.Reference0,
                source.Reference1,
                source.Tag0,
                source.StatId0,
                source.StatIndex0,
                source.Flags);
        }

        private static SkillModuleDefinition PatchModule(
            in SkillModuleDefinition source,
            in SkillLevelPatch patch)
        {
            var value0 = source.Value0;
            var value1 = source.Value1;
            var value2 = source.Value2;
            var value3 = source.Value3;
            var int0 = source.Int0;
            switch (patch.Target)
            {
                case SkillPatchTarget.TriggerValue0:
                case SkillPatchTarget.TargetingValue0:
                case SkillPatchTarget.DeliveryValue0:
                    value0 = Patch(value0, patch.Operation, patch.FloatValue);
                    break;
                case SkillPatchTarget.TriggerValue1:
                case SkillPatchTarget.TargetingValue1:
                case SkillPatchTarget.DeliveryValue1:
                    value1 = Patch(value1, patch.Operation, patch.FloatValue);
                    break;
                case SkillPatchTarget.DeliveryValue2:
                    value2 = Patch(value2, patch.Operation, patch.FloatValue);
                    break;
                case SkillPatchTarget.DeliveryValue3:
                    value3 = Patch(value3, patch.Operation, patch.FloatValue);
                    break;
                case SkillPatchTarget.TriggerInt0:
                case SkillPatchTarget.TargetingInt0:
                case SkillPatchTarget.DeliveryInt0:
                    int0 = Patch(int0, patch.Operation, patch.IntegerValue);
                    break;
            }

            return new SkillModuleDefinition(
                source.ModuleId,
                value0,
                value1,
                value2,
                value3,
                int0,
                source.Int1,
                source.PresentationId);
        }

        private static float Patch(float current, SkillPatchOperation operation, float operand)
        {
            if (operation == SkillPatchOperation.Add) return current + operand;
            return operation == SkillPatchOperation.Multiply ? current * operand : operand;
        }

        private static int Patch(int current, SkillPatchOperation operation, int operand)
        {
            if (operation == SkillPatchOperation.Add) return checked(current + operand);
            return operation == SkillPatchOperation.Multiply ? checked(current * operand) : operand;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static Result<CompiledSkillDefinition> Failure(
            RuntimeSkillDefinition skill,
            string message)
        {
            return Result<CompiledSkillDefinition>.Failure(
                new Error(
                    ErrorCode.InvalidAuthoringData,
                    message,
                    skill.Id,
                    default,
                    skill.SourceAssetPath));
        }
    }

    /// <summary>Opaque handle for one actor-owned skill instance.</summary>
    public readonly struct SkillInstanceHandle : IEquatable<SkillInstanceHandle>
    {
        private readonly int encodedIndex;
        private readonly ushort generation;

        internal SkillInstanceHandle(int value, ushort generation)
        {
            if (generation == 0) throw new ArgumentOutOfRangeException(nameof(generation));
            encodedIndex = checked(value + 1);
            this.generation = generation;
        }

        /// <summary>Gets the zero-based instance value, or -1 when invalid.</summary>
        public int Value => encodedIndex - 1;
        /// <summary>Gets whether the handle was assigned.</summary>
        public bool IsValid => encodedIndex > 0 && generation > 0;
        internal ushort Generation => generation;
        /// <inheritdoc />
        public bool Equals(SkillInstanceHandle other) =>
            encodedIndex == other.encodedIndex && generation == other.generation;
        /// <inheritdoc />
        public override bool Equals(object obj) => obj is SkillInstanceHandle other && Equals(other);
        /// <inheritdoc />
        public override int GetHashCode() => (encodedIndex * 397) ^ generation;
        /// <summary>Compares two handles.</summary>
        public static bool operator ==(SkillInstanceHandle left, SkillInstanceHandle right) => left.Equals(right);
        /// <summary>Compares two handles.</summary>
        public static bool operator !=(SkillInstanceHandle left, SkillInstanceHandle right) => !left.Equals(right);
    }

    /// <summary>Mutable per-owner state referencing a shared compiled skill definition.</summary>
    public sealed class SkillInstance
    {
        internal SkillInstance(
            SkillInstanceHandle handle,
            SpatialEntity owner,
            CompiledSkillDefinition definition,
            int level,
            bool secondaryOnly)
        {
            Handle = handle;
            Owner = owner;
            Definition = definition;
            Level = level;
            SecondaryOnly = secondaryOnly;
        }

        /// <summary>Gets the instance handle.</summary>
        public SkillInstanceHandle Handle { get; }
        /// <summary>Gets the owning actor.</summary>
        public SpatialEntity Owner { get; }
        /// <summary>Gets the shared compiled definition.</summary>
        public CompiledSkillDefinition Definition { get; }
        /// <summary>Gets the current one-based level.</summary>
        public int Level { get; internal set; }
        /// <summary>Gets remaining cooldown seconds.</summary>
        public float CooldownRemaining { get; internal set; }
        internal bool SecondaryOnly { get; }
    }
}
