using System;
using System.Collections.Generic;
using System.Numerics;
using Game.Content.Runtime;
using Game.Core;

namespace Game.Simulation
{
    /// <summary>Executes one registered trigger predicate.</summary>
    public interface ITriggerExecutor
    {
        /// <summary>Gets the stable module ID.</summary>
        ContentId Id { get; }
        /// <summary>Returns whether the context activates an owner instance.</summary>
        bool Matches(SpatialEntity owner, in SkillTriggerContext context);
    }

    /// <summary>Evaluates one registered pre-activation condition.</summary>
    public interface IConditionEvaluator
    {
        /// <summary>Gets the stable module ID.</summary>
        ContentId Id { get; }
        /// <summary>Returns whether activation may continue.</summary>
        bool Evaluate(
            SimulationWorld world,
            SkillInstance instance,
            RuntimeSkillLevel level,
            in SkillTriggerContext context);
    }

    /// <summary>Executes one registered allocation-conscious targeting strategy.</summary>
    public interface ITargetingExecutor
    {
        /// <summary>Gets the stable module ID.</summary>
        ContentId Id { get; }
        /// <summary>Writes selected targets to caller-owned storage.</summary>
        void Select(
            SimulationWorld world,
            SkillInstance instance,
            RuntimeSkillLevel level,
            in SkillTriggerContext context,
            SpatialQueryBuffer spatialResults,
            SkillTargetResultBuffer targets,
            ref RandomStream random);
    }

    /// <summary>Executes one registered delivery strategy.</summary>
    public interface IDeliveryExecutor
    {
        /// <summary>Gets the stable module ID.</summary>
        ContentId Id { get; }
        /// <summary>Queues immediate effects or a deferred delivery spawn.</summary>
        void Deliver(
            SkillRuntime runtime,
            SimulationWorld world,
            SkillInstance instance,
            RuntimeSkillLevel level,
            in SkillTriggerContext context,
            SkillTargetResultBuffer targets);
    }

    /// <summary>Executes one registered effect operation.</summary>
    public interface IEffectExecutor
    {
        /// <summary>Gets the compact operation code.</summary>
        EffectOpCode Code { get; }
        /// <summary>Queues one centralized execution command.</summary>
        void Queue(
            SkillExecutionCommandBuffer commands,
            in SkillEffectContext context,
            in ResolvedEffectOp effect);
    }

    /// <summary>Context shared by all effect executors.</summary>
    public readonly struct SkillEffectContext
    {
        /// <summary>Initializes one effect context.</summary>
        public SkillEffectContext(
            SpatialEntity owner,
            SpatialEntity target,
            bool hasTarget,
            ContentId skillId,
            Vector2 position,
            Vector2 direction,
            int procDepth)
        {
            Owner = owner;
            Target = target;
            HasTarget = hasTarget;
            SkillId = skillId;
            Position = position;
            Direction = direction;
            ProcDepth = procDepth;
        }

        /// <summary>Gets the skill owner.</summary>
        public SpatialEntity Owner { get; }
        /// <summary>Gets the selected target.</summary>
        public SpatialEntity Target { get; }
        /// <summary>Gets whether Target is valid for entity effects.</summary>
        public bool HasTarget { get; }
        /// <summary>Gets the stable source skill ID.</summary>
        public ContentId SkillId { get; }
        /// <summary>Gets the impact position.</summary>
        public Vector2 Position { get; }
        /// <summary>Gets delivery direction.</summary>
        public Vector2 Direction { get; }
        /// <summary>Gets propagated proc depth.</summary>
        public int ProcDepth { get; }
    }

    /// <summary>
    /// Explicit module registry. Registration is constructor-driven; no assembly or
    /// reflection scan is performed.
    /// </summary>
    public sealed class SkillModuleRegistry
    {
        private readonly Dictionary<ContentId, ITriggerExecutor> triggers =
            new Dictionary<ContentId, ITriggerExecutor>();
        private readonly Dictionary<ContentId, IConditionEvaluator> conditions =
            new Dictionary<ContentId, IConditionEvaluator>();
        private readonly Dictionary<ContentId, ITargetingExecutor> targetings =
            new Dictionary<ContentId, ITargetingExecutor>();
        private readonly Dictionary<ContentId, IDeliveryExecutor> deliveries =
            new Dictionary<ContentId, IDeliveryExecutor>();
        private readonly IEffectExecutor[] effects = new IEffectExecutor[13];

        /// <summary>Creates the built-in M4 module set through explicit calls.</summary>
        public static SkillModuleRegistry CreateDefault()
        {
            var registry = new SkillModuleRegistry();
            registry.RegisterTrigger(
                new EventTriggerExecutor(
                    SkillModuleIds.TriggerTimer,
                    SkillTriggerEventType.Timer,
                    TriggerOwnerMatch.None));
            registry.RegisterTrigger(
                new EventTriggerExecutor(
                    SkillModuleIds.TriggerOnHit,
                    SkillTriggerEventType.OnHit,
                    TriggerOwnerMatch.Source));
            registry.RegisterTrigger(
                new EventTriggerExecutor(
                    SkillModuleIds.TriggerOnKill,
                    SkillTriggerEventType.OnKill,
                    TriggerOwnerMatch.Source));
            registry.RegisterTrigger(
                new EventTriggerExecutor(
                    SkillModuleIds.TriggerOnDamageTaken,
                    SkillTriggerEventType.OnDamageTaken,
                    TriggerOwnerMatch.Target));
            registry.RegisterTrigger(
                new EventTriggerExecutor(
                    SkillModuleIds.TriggerOnPickup,
                    SkillTriggerEventType.OnPickup,
                    TriggerOwnerMatch.Target));
            registry.RegisterTrigger(
                new EventTriggerExecutor(
                    SkillModuleIds.TriggerOnStatusApplied,
                    SkillTriggerEventType.OnStatusApplied,
                    TriggerOwnerMatch.Target));
            registry.RegisterCondition(new AlwaysConditionEvaluator());
            registry.RegisterCondition(
                new StatusConditionEvaluator(
                    SkillModuleIds.ConditionStatusCountAtLeast,
                    true));
            registry.RegisterCondition(
                new StatusConditionEvaluator(
                    SkillModuleIds.ConditionTargetHasStatus,
                    false));
            registry.RegisterTargeting(new SelfTargetingExecutor());
            registry.RegisterTargeting(new NearestTargetingExecutor());
            registry.RegisterTargeting(new RandomTargetingExecutor());
            registry.RegisterTargeting(new CircleTargetingExecutor());
            registry.RegisterTargeting(new ConeTargetingExecutor());
            registry.RegisterTargeting(new LineTargetingExecutor());
            registry.RegisterTargeting(new RingTargetingExecutor());
            registry.RegisterTargeting(new RandomPointTargetingExecutor());
            registry.RegisterTargeting(new TriggerPositionTargetingExecutor());
            registry.RegisterDelivery(new InstantDeliveryExecutor());
            registry.RegisterDelivery(new ProjectileDeliveryExecutor());
            registry.RegisterDelivery(new AreaDeliveryExecutor());
            registry.RegisterDelivery(new AuraDeliveryExecutor());
            registry.RegisterDelivery(new OrbitDeliveryExecutor());
            registry.RegisterDelivery(new OutboundReturnDeliveryExecutor());
            registry.RegisterEffect(new BufferedEffectExecutor(EffectOpCode.Damage));
            registry.RegisterEffect(new BufferedEffectExecutor(EffectOpCode.Heal));
            registry.RegisterEffect(new BufferedEffectExecutor(EffectOpCode.ApplyStatus));
            registry.RegisterEffect(new BufferedEffectExecutor(EffectOpCode.RemoveStatus));
            registry.RegisterEffect(new BufferedEffectExecutor(EffectOpCode.Knockback));
            registry.RegisterEffect(new BufferedEffectExecutor(EffectOpCode.Pull));
            registry.RegisterEffect(new BufferedEffectExecutor(EffectOpCode.ModifyStat));
            registry.RegisterEffect(new BufferedEffectExecutor(EffectOpCode.SpawnSecondarySkill));
            registry.RegisterEffect(new BufferedEffectExecutor(EffectOpCode.GrantShield));
            registry.RegisterEffect(new BufferedEffectExecutor(EffectOpCode.GainResource));
            registry.RegisterEffect(new BufferedEffectExecutor(EffectOpCode.ConsumeStatus));
            registry.RegisterEffect(new BufferedEffectExecutor(EffectOpCode.DetonateStatus));
            return registry;
        }

        /// <summary>Registers a trigger executor.</summary>
        public void RegisterTrigger(ITriggerExecutor executor)
        {
            if (executor == null) throw new ArgumentNullException(nameof(executor));
            if (!triggers.TryAdd(executor.Id, executor)) throw Duplicate(executor.Id);
        }

        /// <summary>Registers a condition evaluator.</summary>
        public void RegisterCondition(IConditionEvaluator evaluator)
        {
            if (evaluator == null) throw new ArgumentNullException(nameof(evaluator));
            if (!conditions.TryAdd(evaluator.Id, evaluator)) throw Duplicate(evaluator.Id);
        }

        /// <summary>Registers a targeting executor.</summary>
        public void RegisterTargeting(ITargetingExecutor executor)
        {
            if (executor == null) throw new ArgumentNullException(nameof(executor));
            if (!targetings.TryAdd(executor.Id, executor)) throw Duplicate(executor.Id);
        }

        /// <summary>Registers a delivery executor.</summary>
        public void RegisterDelivery(IDeliveryExecutor executor)
        {
            if (executor == null) throw new ArgumentNullException(nameof(executor));
            if (!deliveries.TryAdd(executor.Id, executor)) throw Duplicate(executor.Id);
        }

        /// <summary>Registers an effect executor by compact operation code.</summary>
        public void RegisterEffect(IEffectExecutor executor)
        {
            if (executor == null) throw new ArgumentNullException(nameof(executor));
            var index = (int)executor.Code;
            if (index <= 0 || index >= effects.Length) throw new ArgumentOutOfRangeException(nameof(executor));
            if (effects[index] != null) throw Duplicate(SkillModuleIds.GetEffectId(executor.Code));
            effects[index] = executor;
        }

        /// <summary>Tries to resolve a trigger executor.</summary>
        public bool TryGetTrigger(ContentId id, out ITriggerExecutor executor) =>
            triggers.TryGetValue(id, out executor);
        /// <summary>Tries to resolve a condition evaluator.</summary>
        public bool TryGetCondition(ContentId id, out IConditionEvaluator evaluator) =>
            conditions.TryGetValue(id, out evaluator);
        /// <summary>Tries to resolve a targeting executor.</summary>
        public bool TryGetTargeting(ContentId id, out ITargetingExecutor executor) =>
            targetings.TryGetValue(id, out executor);
        /// <summary>Tries to resolve a delivery executor.</summary>
        public bool TryGetDelivery(ContentId id, out IDeliveryExecutor executor) =>
            deliveries.TryGetValue(id, out executor);
        /// <summary>Tries to resolve an effect executor.</summary>
        public bool TryGetEffect(EffectOpCode code, out IEffectExecutor executor)
        {
            var index = (int)code;
            executor = index > 0 && index < effects.Length ? effects[index] : null;
            return executor != null;
        }

        private static InvalidOperationException Duplicate(ContentId id)
        {
            return new InvalidOperationException("Skill module is already registered: " + id + ".");
        }
    }

    internal enum TriggerOwnerMatch : byte
    {
        None,
        Source,
        Target
    }

    internal sealed class EventTriggerExecutor : ITriggerExecutor
    {
        private readonly SkillTriggerEventType eventType;
        private readonly TriggerOwnerMatch ownerMatch;

        public EventTriggerExecutor(
            ContentId id,
            SkillTriggerEventType eventType,
            TriggerOwnerMatch ownerMatch)
        {
            Id = id;
            this.eventType = eventType;
            this.ownerMatch = ownerMatch;
        }

        public ContentId Id { get; }

        public bool Matches(SpatialEntity owner, in SkillTriggerContext context)
        {
            if (context.EventType != eventType) return false;
            if (ownerMatch == TriggerOwnerMatch.Source) return context.Source == owner;
            return ownerMatch != TriggerOwnerMatch.Target || context.Target == owner;
        }
    }

    internal sealed class AlwaysConditionEvaluator : IConditionEvaluator
    {
        public ContentId Id => SkillModuleIds.ConditionAlways;

        public bool Evaluate(
            SimulationWorld world,
            SkillInstance instance,
            RuntimeSkillLevel level,
            in SkillTriggerContext context)
        {
            return true;
        }
    }

    internal sealed class StatusConditionEvaluator : IConditionEvaluator
    {
        private readonly bool countStacks;

        public StatusConditionEvaluator(ContentId id, bool countStacks)
        {
            Id = id;
            this.countStacks = countStacks;
        }

        public ContentId Id { get; }

        public bool Evaluate(
            SimulationWorld world,
            SkillInstance instance,
            RuntimeSkillLevel level,
            in SkillTriggerContext context)
        {
            var module = instance.Definition.Source.Condition;
            var domain = countStacks ? module.Int1 : module.Int0;
            var target = ResolveTarget(instance.Owner, context, domain);
            var result = world.StatusTransactions.Query(
                world,
                target,
                module.Reference0,
                module.Tag0);
            return countStacks
                ? result.TotalStacks >= Math.Max(1, module.Int0)
                : result.MatchedInstances > 0;
        }

        private static SpatialEntity ResolveTarget(
            SpatialEntity owner,
            in SkillTriggerContext context,
            int domain)
        {
            switch ((StatusQueryTarget)domain)
            {
                case StatusQueryTarget.Source:
                    return context.Source;
                case StatusQueryTarget.Target:
                    return context.Target;
                default:
                    return owner;
            }
        }
    }

    internal sealed class BufferedEffectExecutor : IEffectExecutor
    {
        public BufferedEffectExecutor(EffectOpCode code)
        {
            Code = code;
        }

        public EffectOpCode Code { get; }

        public void Queue(
            SkillExecutionCommandBuffer commands,
            in SkillEffectContext context,
            in ResolvedEffectOp effect)
        {
            commands.Add(new SkillExecutionCommand(context, effect));
        }
    }
}
