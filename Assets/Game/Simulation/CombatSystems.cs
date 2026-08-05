using System;
using System.Collections.Generic;
using Game.Content.Runtime;
using Game.Core;

namespace Game.Simulation
{
    /// <summary>Centralized resolver and sole M3 writer of damage-driven Health changes.</summary>
    public sealed class DamageResolutionSystem : ISimulationSystem
    {
        /// <inheritdoc />
        public SimulationSystemId Id => SimulationSystemId.DamageResolution;

        /// <inheritdoc />
        public void Execute(SimulationWorld world)
        {
            if (world == null)
            {
                throw new ArgumentNullException(nameof(world));
            }

            var requestCount = world.DamageRequests.Count;
            for (var index = 0; index < requestCount; index++)
            {
                var packet = world.DamageRequests.GetAt(index);
                Resolve(world, packet);
            }

            world.DamageRequests.Clear();
        }

        private static void Resolve(
            SimulationWorld world,
            in DamagePacket packet)
        {
            if (packet.Target.Kind != EntityKind.Actor ||
                packet.DamageType < DamageType.Physical ||
                packet.DamageType > DamageType.True ||
                !world.Actors.TryGetCombat(packet.Target.Handle, out var target) ||
                target.DeathPending ||
                target.Dead ||
                target.HealthCurrent <= 0f)
            {
                world.CombatEvents.Add(
                    new DamageResolved(packet, packet.BaseValue, 0f, 0f, 0f, 0f,
                        DamageResolutionOutcome.Invalid, world.ExecutingTick));
                world.Diagnostics.RecordRejectedDamage();
                return;
            }

            var policyOutcome = StatusDamagePolicy.IsImmune(target, packet.ChannelId)
                ? DamageResolutionOutcome.Immune
                : world.DamageChannels.Evaluate(
                    packet.Target.Handle,
                    packet.ChannelId,
                    world.ExecutingTick);
            if (policyOutcome != DamageResolutionOutcome.Applied)
            {
                world.CombatEvents.Add(
                    new DamageResolved(packet, packet.BaseValue, 0f, 0f, 0f, 0f,
                        policyOutcome, world.ExecutingTick));
                return;
            }

            var rules = world.CombatRules;
            var normalizedBase = NormalizeDamage(packet.BaseValue, rules);
            var sourceMultiplier = 1f;
            var criticalChance = 0f;
            var criticalMultiplier = rules.CriticalMultiplier;
            if (packet.Source.Kind == EntityKind.Actor &&
                world.Actors.TryGetCombat(packet.Source.Handle, out var source))
            {
                sourceMultiplier = source.Stats.Get(BuiltInStatIndices.Damage);
                criticalChance = source.Stats.Get(BuiltInStatIndices.CriticalChance);
                criticalMultiplier = source.Stats.Get(BuiltInStatIndices.CriticalMultiplier);
            }

            var sourceModified = ClampFinite(
                normalizedBase * sourceMultiplier,
                rules.MinimumDamage,
                rules.MaximumDamage);
            var wasCritical = false;
            var criticalModified = sourceModified;
            if (packet.CanCritical)
            {
                criticalChance = ClampFinite(criticalChance, 0f, 1f);
                wasCritical = world.DamageRandom.NextFloat() < criticalChance;
                if (wasCritical)
                {
                    criticalModified = ClampFinite(
                        sourceModified * criticalMultiplier,
                        rules.MinimumDamage,
                        rules.MaximumDamage);
                }
            }

            var mitigated = ApplyMitigation(target, packet.DamageType, criticalModified, rules);
            var finalDamage = ClampFinite(
                mitigated,
                rules.MinimumDamage,
                rules.MaximumDamage);

            if (finalDamage <= 0f)
            {
                world.CombatEvents.Add(
                    new DamageResolved(packet, packet.BaseValue, mitigated, 0f, 0f, 0f,
                        DamageResolutionOutcome.Zero, world.ExecutingTick));
                world.CombatEvents.Add(
                    new DamageApplied(
                        new DamageContext(
                            packet,
                            normalizedBase,
                            sourceModified,
                            criticalModified,
                            mitigated,
                            0f,
                            0f,
                            0f,
                            wasCritical),
                        world.ExecutingTick));
                return;
            }

            var barrierAbsorbed = world.DamageChannels.AbsorbBarrier(
                packet.Target.Handle,
                packet.ChannelId,
                finalDamage);
            var damageAfterBarrier = Math.Max(0f, finalDamage - barrierAbsorbed);
            world.DamageChannels.CommitCooldown(
                packet.Target.Handle,
                packet.ChannelId,
                world.ExecutingTick,
                packet.ChannelCooldownTicks);

            var previousShield = target.ShieldCurrent;
            var shieldAbsorbed = Math.Min(previousShield, damageAfterBarrier);
            if (shieldAbsorbed > 0f)
            {
                target.ShieldCurrent = previousShield - shieldAbsorbed;
                var shieldEvent = new ShieldChanged(
                    packet.Target,
                    packet.SourceContentId,
                    previousShield,
                    target.ShieldCurrent,
                    target.ShieldMaximum,
                    target.ShieldMaximum,
                    world.ExecutingTick);
                world.CombatEvents.Add(shieldEvent);
            }

            target.ReconcileHealthMaximum();
            var remaining = damageAfterBarrier - shieldAbsorbed;
            var healthDamage = Math.Min(target.HealthCurrent, Math.Max(0f, remaining));
            target.HealthCurrent -= healthDamage;
            if (target.HealthCurrent < 0f)
            {
                target.HealthCurrent = 0f;
            }

            var context = new DamageContext(
                packet,
                normalizedBase,
                sourceModified,
                criticalModified,
                mitigated,
                finalDamage,
                shieldAbsorbed,
                healthDamage,
                wasCritical);
            world.CombatEvents.Add(
                new DamageResolved(
                    packet,
                    packet.BaseValue,
                    mitigated,
                    barrierAbsorbed,
                    shieldAbsorbed,
                    healthDamage,
                    DamageResolutionOutcome.Applied,
                    world.ExecutingTick));
            if (shieldAbsorbed + healthDamage > 0f)
            {
                var damageEvent = new DamageApplied(context, world.ExecutingTick);
                world.CombatEvents.Add(damageEvent);
                world.Skills.QueueTrigger(
                    new SkillTriggerContext(
                        SkillTriggerEventType.OnHit,
                        packet.Source,
                        packet.Target,
                        packet.Position,
                        default,
                        packet.SourceContentId,
                        default,
                        packet.ProcDepth + 1));
                world.Skills.QueueTrigger(
                    new SkillTriggerContext(
                        SkillTriggerEventType.OnDamageTaken,
                        packet.Source,
                        packet.Target,
                        packet.Position,
                        default,
                        packet.SourceContentId,
                        default,
                        packet.ProcDepth + 1));
            }

            if (target.HealthCurrent <= 0f && !target.DeathPending)
            {
                target.DeathPending = true;
                var request = new DeathRequest(
                    packet.Target,
                    packet.Source,
                    packet.SourceContentId,
                    packet.Position,
                    packet.ProcDepth);
                world.DeathRequests.Add(request);
            }
        }

        private static float ApplyMitigation(
            ActorCombatRecord target,
            DamageType damageType,
            float damage,
            in CombatRules rules)
        {
            if (damageType == DamageType.True)
            {
                return damage;
            }

            if (damageType == DamageType.Physical)
            {
                var armor = Math.Max(0f, target.Stats.Get(BuiltInStatIndices.Armor));
                return damage * (rules.ArmorScale / (rules.ArmorScale + armor));
            }

            var resistance = target.Resistances.Get(damageType);
            resistance = ClampFinite(resistance, 0f, rules.MaximumResistance);
            return damage * (1f - resistance);
        }

        private static float NormalizeDamage(float damage, in CombatRules rules)
        {
            if (float.IsNaN(damage) || float.IsNegativeInfinity(damage))
            {
                return rules.MinimumDamage;
            }

            if (float.IsPositiveInfinity(damage))
            {
                return rules.MaximumDamage;
            }

            return ClampFinite(damage, rules.MinimumDamage, rules.MaximumDamage);
        }

        private static float ClampFinite(float value, float minimum, float maximum)
        {
            if (float.IsNaN(value) || float.IsNegativeInfinity(value))
            {
                return minimum;
            }

            if (float.IsPositiveInfinity(value))
            {
                return maximum;
            }

            if (value < minimum)
            {
                return minimum;
            }

            return value > maximum ? maximum : value;
        }
    }

    /// <summary>
    /// Ticks existing statuses, expires and dispels them, then applies buffered requests.
    /// </summary>
    public sealed class StatusTickSystem : ISimulationSystem
    {
        private const float IntervalEpsilon = 0.000001f;
        private const int MaximumTicksPerExecution = 1024;

        /// <inheritdoc />
        public SimulationSystemId Id => SimulationSystemId.StatusTick;

        /// <inheritdoc />
        public void Execute(SimulationWorld world)
        {
            if (world == null)
            {
                throw new ArgumentNullException(nameof(world));
            }

            ProcessDispels(world);
            TickExisting(world);
            ProcessApplications(world);
        }

        private static void ProcessDispels(SimulationWorld world)
        {
            var requestCount = world.StatusDispels.Count;
            for (var requestIndex = 0; requestIndex < requestCount; requestIndex++)
            {
                var request = world.StatusDispels.GetAt(requestIndex);
                if (request.Target.Kind != EntityKind.Actor ||
                    !world.Actors.TryGetCombat(request.Target.Handle, out var actor) ||
                    actor.DeathPending ||
                    actor.Dead)
                {
                    world.Diagnostics.RecordRejectedStatus();
                    continue;
                }

                var statusIndex = 0;
                while (statusIndex < actor.Statuses.Count)
                {
                    var instance = actor.Statuses.GetAt(statusIndex);
                    if (ContainsTag(instance.Definition.DispelTags, request.DispelTag))
                    {
                        RemoveStatusAt(
                            world,
                            request.Target,
                            actor,
                            statusIndex);
                    }
                    else
                    {
                        statusIndex++;
                    }
                }
            }

            world.StatusDispels.Clear();
        }

        private static void TickExisting(SimulationWorld world)
        {
            var deltaTime = world.DeltaTimeSeconds;
            for (var actorIndex = 0; actorIndex < world.Actors.Count; actorIndex++)
            {
                var handle = world.Actors.GetHandleAt(actorIndex);
                if (!world.Actors.TryGetCombat(handle, out var actor) ||
                    actor.DeathPending ||
                    actor.Dead)
                {
                    continue;
                }

                actor.Stats.Modifiers.Tick(deltaTime);
                var target = new SpatialEntity(EntityKind.Actor, handle);
                var statusIndex = 0;
                while (statusIndex < actor.Statuses.Count)
                {
                    var instance = actor.Statuses.GetAt(statusIndex);
                    var activeDelta = deltaTime;
                    if (!float.IsPositiveInfinity(instance.RemainingDuration))
                    {
                        activeDelta = Math.Min(
                            deltaTime,
                            Math.Max(0f, instance.RemainingDuration));
                    }

                    var interval = instance.Definition.TickIntervalSeconds;
                    if (interval > 0f && !float.IsInfinity(interval))
                    {
                        instance.TickAccumulator += activeDelta;
                        var ticksThisExecution = 0;
                        while (instance.TickAccumulator + IntervalEpsilon >= interval &&
                               ticksThisExecution < MaximumTicksPerExecution)
                        {
                            instance.TickAccumulator -= interval;
                            instance.TickCount++;
                            ticksThisExecution++;
                            QueuePeriodicDamage(world, target, actorIndex, instance);
                        }
                    }

                    if (!float.IsPositiveInfinity(instance.RemainingDuration))
                    {
                        instance.RemainingDuration -= activeDelta;
                    }

                    if (instance.RemainingDuration <= IntervalEpsilon)
                    {
                        RemoveStatusAt(world, target, actor, statusIndex);
                    }
                    else
                    {
                        actor.Statuses.SetAt(statusIndex, instance);
                        statusIndex++;
                    }
                }

                actor.ReconcileHealthMaximum();
            }
        }

        private static void QueuePeriodicDamage(
            SimulationWorld world,
            SpatialEntity target,
            int actorDenseIndex,
            in StatusInstance instance)
        {
            var payload = instance.Definition.Behavior.PeriodicDamage;
            if (!payload.Enabled)
            {
                return;
            }

            if (instance.ProcDepth >= world.CombatRules.MaximumProcDepth)
            {
                world.Diagnostics.RecordTruncatedProcChain();
                return;
            }

            var body = world.Actors.GetStateAt(actorDenseIndex);
            var value = payload.BaseValue * instance.Strength * instance.Stacks;
            var packet = new DamagePacket(
                instance.Source,
                target,
                instance.SourceContentId.IsValid
                    ? instance.SourceContentId
                    : instance.Definition.Id,
                payload.DamageType,
                payload.Tags | DamageTags.Status | DamageTags.DamageOverTime,
                value,
                payload.CanCritical,
                payload.ProcCoefficient,
                payload.Knockback,
                body.Position,
                instance.ProcDepth + 1,
                BuiltInDamageChannels.Periodic,
                0);
            world.QueueDamage(packet);
        }

        private static void ProcessApplications(SimulationWorld world)
        {
            var requestCount = world.StatusApplications.Count;
            for (var requestIndex = 0; requestIndex < requestCount; requestIndex++)
            {
                var request = world.StatusApplications.GetAt(requestIndex);
                Apply(world, request);
            }

            world.StatusApplications.Clear();
        }

        private static void Apply(
            SimulationWorld world,
            in StatusApplicationRequest request)
        {
            if (request.Target.Kind != EntityKind.Actor ||
                !world.Actors.TryGetCombat(request.Target.Handle, out var actor) ||
                actor.DeathPending ||
                actor.Dead ||
                !world.StatusCatalog.TryGet(request.StatusIndex, out var definition) ||
                !IsDefinitionValid(definition) ||
                IsImmune(actor.Statuses, definition.Tags) ||
                !IsBehaviorValid(definition.Behavior))
            {
                world.Diagnostics.RecordRejectedStatus();
                return;
            }

            var existingIndex = actor.Statuses.FindFirst(request.StatusIndex);
            var outcome = StatusApplicationOutcome.Added;
            StatusInstance instance;
            var isNew = existingIndex < 0 ||
                        definition.StackingPolicy == StatusStackingPolicy.IndependentInstances;

            if (definition.StackingPolicy == StatusStackingPolicy.IndependentInstances)
            {
                if (actor.Statuses.CountInstances(request.StatusIndex) >= definition.MaxStacks)
                {
                    world.Diagnostics.RecordRejectedStatus();
                    return;
                }

                instance = CreateInstance(definition, request);
                if (!InstallModifier(actor, ref instance))
                {
                    world.Diagnostics.RecordRejectedStatus();
                    return;
                }

                if (!TrySetShieldContribution(
                        world,
                        request.Target,
                        actor,
                        definition.Id,
                        ref instance))
                {
                    RemoveModifier(actor, instance.ModifierHandle);
                    world.Diagnostics.RecordRejectedStatus();
                    return;
                }

                actor.Statuses.Add(instance);
            }
            else if (isNew)
            {
                instance = CreateInstance(definition, request);
                if (!InstallModifier(actor, ref instance))
                {
                    world.Diagnostics.RecordRejectedStatus();
                    return;
                }

                if (!TrySetShieldContribution(
                        world,
                        request.Target,
                        actor,
                        definition.Id,
                        ref instance))
                {
                    RemoveModifier(actor, instance.ModifierHandle);
                    world.Diagnostics.RecordRejectedStatus();
                    return;
                }

                actor.Statuses.Add(instance);
            }
            else
            {
                instance = actor.Statuses.GetAt(existingIndex);
                var previousModifierHandle = instance.ModifierHandle;
                switch (definition.StackingPolicy)
                {
                    case StatusStackingPolicy.RefreshDuration:
                        RefreshInstance(ref instance, request, definition, true);
                        outcome = StatusApplicationOutcome.Refreshed;
                        break;

                    case StatusStackingPolicy.AddStacks:
                        if (instance.Stacks < definition.MaxStacks)
                        {
                            instance.Stacks++;
                            outcome = StatusApplicationOutcome.StackAdded;
                        }
                        else
                        {
                            outcome = StatusApplicationOutcome.Refreshed;
                        }

                        instance.RemainingDuration = definition.DurationSeconds;
                        instance.Source = request.Source;
                        instance.SourceContentId = request.SourceContentId;
                        instance.Strength = Math.Max(instance.Strength, request.Strength);
                        instance.ProcDepth = request.ProcDepth;
                        break;

                    case StatusStackingPolicy.ReplaceIfStronger:
                        if (request.Strength < instance.Strength)
                        {
                            world.Diagnostics.RecordRejectedStatus();
                            return;
                        }

                        if (request.Strength > instance.Strength)
                        {
                            RefreshInstance(ref instance, request, definition, true);
                            instance.TickCount = 0;
                            outcome = StatusApplicationOutcome.Replaced;
                        }
                        else
                        {
                            RefreshInstance(ref instance, request, definition, false);
                            outcome = StatusApplicationOutcome.Refreshed;
                        }

                        break;

                    default:
                        world.Diagnostics.RecordRejectedStatus();
                        return;
                }

                if (!InstallModifier(actor, ref instance))
                {
                    world.Diagnostics.RecordRejectedStatus();
                    return;
                }

                if (!TrySetShieldContribution(
                        world,
                        request.Target,
                        actor,
                        definition.Id,
                        ref instance))
                {
                    RemoveModifier(actor, instance.ModifierHandle);
                    world.Diagnostics.RecordRejectedStatus();
                    return;
                }

                RemoveModifier(actor, previousModifierHandle);
                actor.Statuses.SetAt(existingIndex, instance);
            }

            actor.ReconcileHealthMaximum();
            var appliedEvent = new StatusApplied(
                request.Source,
                request.Target,
                definition.Id,
                request.StatusIndex,
                outcome,
                instance.Stacks,
                instance.Strength,
                instance.RemainingDuration,
                world.ExecutingTick);
            world.CombatEvents.Add(appliedEvent);
            var appliedPosition = default(System.Numerics.Vector2);
            if (world.Actors.TryRead(request.Target.Handle, out var appliedBody))
            {
                appliedPosition = appliedBody.Position;
            }
            world.Skills.QueueTrigger(
                new SkillTriggerContext(
                    SkillTriggerEventType.OnStatusApplied,
                    request.Source,
                    request.Target,
                    appliedPosition,
                    default,
                    request.SourceContentId,
                    request.StatusIndex,
                    request.ProcDepth + 1));
        }

        private static StatusInstance CreateInstance(
            RuntimeStatusDefinition definition,
            in StatusApplicationRequest request)
        {
            return new StatusInstance
            {
                StatusIndex = request.StatusIndex,
                Definition = definition,
                Source = request.Source,
                SourceContentId = request.SourceContentId,
                Strength = request.Strength,
                Stacks = 1,
                RemainingDuration = definition.DurationSeconds,
                TickAccumulator = 0f,
                TickCount = 0,
                ProcDepth = request.ProcDepth,
                ModifierHandle = default,
                ShieldContribution = 0f
            };
        }

        private static void RefreshInstance(
            ref StatusInstance instance,
            in StatusApplicationRequest request,
            RuntimeStatusDefinition definition,
            bool resetTickAccumulator)
        {
            instance.Definition = definition;
            instance.Source = request.Source;
            instance.SourceContentId = request.SourceContentId;
            instance.Strength = request.Strength;
            instance.Stacks = 1;
            instance.RemainingDuration = definition.DurationSeconds;
            if (resetTickAccumulator)
            {
                instance.TickAccumulator = 0f;
            }

            instance.ProcDepth = request.ProcDepth;
            instance.ModifierHandle = default;
        }

        private static bool InstallModifier(
            ActorCombatRecord actor,
            ref StatusInstance instance)
        {
            var payload = instance.Definition.Behavior.Modifier;
            if (!payload.Enabled)
            {
                instance.ModifierHandle = default;
                return true;
            }

            var value = ScaleModifier(payload, instance.Strength, instance.Stacks);
            if (!IsFinite(value))
            {
                instance.ModifierHandle = default;
                return false;
            }

            var modifier = new Modifier(
                instance.Definition.Id,
                payload.StatId,
                payload.Operation,
                value,
                payload.Priority,
                payload.StackingGroup,
                float.PositiveInfinity);
            return actor.Stats.Modifiers.TryAdd(modifier, out instance.ModifierHandle);
        }

        private static float ScaleModifier(
            in RuntimeStatusModifier payload,
            float strength,
            int stacks)
        {
            var scale = strength * stacks;
            if (payload.Operation == ModifierOperation.Multiply)
            {
                return 1f + ((payload.Value - 1f) * scale);
            }

            if (payload.Operation == ModifierOperation.ClampMinimum ||
                payload.Operation == ModifierOperation.ClampMaximum ||
                payload.Operation == ModifierOperation.Override)
            {
                return payload.Value * strength;
            }

            return payload.Value * scale;
        }

        private static bool IsBehaviorValid(in RuntimeStatusBehavior behavior)
        {
            if (!IsFiniteNonNegative(behavior.ShieldCapacity))
            {
                return false;
            }

            if (behavior.Modifier.Enabled &&
                (!behavior.Modifier.StatId.IsValid ||
                 !StatCatalog.Default.TryGetIndex(
                     behavior.Modifier.StatId,
                     out _) ||
                 behavior.Modifier.Operation < ModifierOperation.AddFlat ||
                 behavior.Modifier.Operation > ModifierOperation.Override ||
                 float.IsNaN(behavior.Modifier.Value) ||
                 float.IsInfinity(behavior.Modifier.Value)))
            {
                return false;
            }

            if (!behavior.PeriodicDamage.Enabled)
            {
                return true;
            }

            var periodic = behavior.PeriodicDamage;
            const DamageTags knownDamageTags =
                DamageTags.Direct |
                DamageTags.DamageOverTime |
                DamageTags.Status |
                DamageTags.Secondary;
            return periodic.DamageType >= DamageType.Physical &&
                   periodic.DamageType <= DamageType.True &&
                   (periodic.Tags & ~knownDamageTags) == 0 &&
                   IsFiniteNonNegative(periodic.BaseValue) &&
                   IsFiniteNonNegative(periodic.ProcCoefficient) &&
                   periodic.ProcCoefficient <= 1f &&
                   IsFinite(periodic.Knockback.X) &&
                   IsFinite(periodic.Knockback.Y);
        }

        private static bool IsDefinitionValid(RuntimeStatusDefinition definition)
        {
            if (definition == null ||
                definition.StackingPolicy < StatusStackingPolicy.RefreshDuration ||
                definition.StackingPolicy > StatusStackingPolicy.IndependentInstances ||
                !IsFinite(definition.DurationSeconds) ||
                definition.DurationSeconds <= 0f ||
                definition.MaxStacks <= 0 ||
                !IsFinite(definition.TickIntervalSeconds) ||
                definition.TickIntervalSeconds < 0f ||
                (definition.Behavior.PeriodicDamage.Enabled &&
                 definition.TickIntervalSeconds <= 0f))
            {
                return false;
            }

            return (definition.StackingPolicy != StatusStackingPolicy.RefreshDuration &&
                    definition.StackingPolicy != StatusStackingPolicy.ReplaceIfStronger) ||
                   definition.MaxStacks == 1;
        }

        private static bool IsFiniteNonNegative(float value)
        {
            return value >= 0f && IsFinite(value);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool TrySetShieldContribution(
            SimulationWorld world,
            SpatialEntity targetEntity,
            ActorCombatRecord target,
            ContentId sourceId,
            ref StatusInstance instance)
        {
            var desired = instance.Definition.Behavior.ShieldCapacity *
                          instance.Strength *
                          instance.Stacks;
            if (!IsFiniteNonNegative(desired))
            {
                return false;
            }

            var delta = desired - instance.ShieldContribution;
            if (delta == 0f)
            {
                return true;
            }

            var previous = target.ShieldCurrent;
            var previousMaximum = target.ShieldMaximum;
            var nextMaximum = previousMaximum + delta;
            if (!IsFiniteNonNegative(nextMaximum))
            {
                return false;
            }

            target.ShieldMaximum = nextMaximum;
            if (delta > 0f)
            {
                target.ShieldCurrent = Math.Min(
                    target.ShieldMaximum,
                    target.ShieldCurrent + delta);
            }
            else if (target.ShieldCurrent > target.ShieldMaximum)
            {
                target.ShieldCurrent = target.ShieldMaximum;
            }

            instance.ShieldContribution = desired;
            EmitShieldChangedIfNeeded(
                world,
                targetEntity,
                sourceId,
                previous,
                target.ShieldCurrent,
                previousMaximum,
                target.ShieldMaximum);
            return true;
        }

        internal static void RemoveStatusAt(
            SimulationWorld world,
            SpatialEntity targetEntity,
            ActorCombatRecord actor,
            int statusIndex)
        {
            var instance = actor.Statuses.GetAt(statusIndex);
            RemoveModifier(actor, instance.ModifierHandle);
            if (instance.ShieldContribution > 0f)
            {
                var previous = actor.ShieldCurrent;
                var previousMaximum = actor.ShieldMaximum;
                actor.ShieldMaximum = Math.Max(
                    0f,
                    actor.ShieldMaximum - instance.ShieldContribution);
                if (actor.ShieldCurrent > actor.ShieldMaximum)
                {
                    actor.ShieldCurrent = actor.ShieldMaximum;
                }

                EmitShieldChangedIfNeeded(
                    world,
                    targetEntity,
                    instance.Definition.Id,
                    previous,
                    actor.ShieldCurrent,
                    previousMaximum,
                    actor.ShieldMaximum);
            }

            actor.Statuses.RemoveAt(statusIndex);
        }

        private static void EmitShieldChangedIfNeeded(
            SimulationWorld world,
            SpatialEntity targetEntity,
            ContentId sourceId,
            float previous,
            float current,
            float previousMaximum,
            float currentMaximum)
        {
            if (previous == current && previousMaximum == currentMaximum)
            {
                return;
            }

            var shieldEvent = new ShieldChanged(
                targetEntity,
                sourceId,
                previous,
                current,
                previousMaximum,
                currentMaximum,
                world.ExecutingTick);
            world.CombatEvents.Add(shieldEvent);
        }

        private static void RemoveModifier(
            ActorCombatRecord actor,
            ModifierHandle handle)
        {
            if (handle.IsValid)
            {
                actor.Stats.Modifiers.Remove(handle);
            }
        }

        private static bool IsImmune(
            StatusCollection activeStatuses,
            IReadOnlyList<ContentTag> incomingTags)
        {
            for (var statusIndex = 0;
                 statusIndex < activeStatuses.Count;
                 statusIndex++)
            {
                var active = activeStatuses.GetAt(statusIndex);
                var immunityTags = active.Definition.ImmunityTags;
                for (var immunityIndex = 0;
                     immunityIndex < immunityTags.Count;
                     immunityIndex++)
                {
                    if (ContainsTag(incomingTags, immunityTags[immunityIndex]))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool ContainsTag(
            IReadOnlyList<ContentTag> tags,
            ContentTag expected)
        {
            for (var index = 0; index < tags.Count; index++)
            {
                if (tags[index] == expected)
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>Finalizes buffered actor deaths once and requests deferred cleanup.</summary>
    public sealed class DeathSystem : ISimulationSystem
    {
        /// <inheritdoc />
        public SimulationSystemId Id => SimulationSystemId.Death;

        /// <inheritdoc />
        public void Execute(SimulationWorld world)
        {
            if (world == null)
            {
                throw new ArgumentNullException(nameof(world));
            }

            var requestCount = world.DeathRequests.Count;
            for (var index = 0; index < requestCount; index++)
            {
                var request = world.DeathRequests.GetAt(index);
                if (request.Target.Kind != EntityKind.Actor ||
                    !world.Actors.TryGetCombat(request.Target.Handle, out var target) ||
                    !target.DeathPending ||
                    target.Dead)
                {
                    continue;
                }

                target.Dead = true;
                var bodyFound = world.Actors.TryRead(
                    request.Target.Handle,
                    out var body);
                var position = bodyFound ? body.Position : request.Position;
                if (world.Enemies.TryGetSnapshot(request.Target.Handle, out var enemy))
                {
                    world.Progression?.RecordEnemyDefeat(enemy.ExperienceReward, position);
                    world.Enemies.ProcessDeathOutputs(world, request.Target.Handle, position);
                }
                var diedEvent = new EntityDied(
                    request.Target,
                    request.Source,
                    request.SourceContentId,
                    position,
                    request.ProcDepth,
                    world.ExecutingTick);
                world.CombatEvents.Add(diedEvent);
                world.Skills.QueueTrigger(
                    new SkillTriggerContext(
                        SkillTriggerEventType.OnKill,
                        request.Source,
                        request.Target,
                        position,
                        default,
                        request.SourceContentId,
                        default,
                        request.ProcDepth + 1));
                world.Commands.Remove(EntityKind.Actor, request.Target.Handle);
            }

            world.DeathRequests.Clear();
        }
    }

    /// <summary>Appends current-tick M3 events to the observable runner batch.</summary>
    public sealed class EventFlushSystem : ISimulationSystem
    {
        /// <inheritdoc />
        public SimulationSystemId Id => SimulationSystemId.EventFlush;

        /// <inheritdoc />
        public void Execute(SimulationWorld world)
        {
            if (world == null)
            {
                throw new ArgumentNullException(nameof(world));
            }

            world.CombatEvents.FlushTick();
        }
    }
}
