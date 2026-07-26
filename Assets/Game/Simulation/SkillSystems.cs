using System;
using System.Numerics;
using Game.Content.Runtime;
using Game.Core;

namespace Game.Simulation
{
    /// <summary>Advances cooldowns and executes timer/event triggers.</summary>
    public sealed class SkillTriggerSystem : ISimulationSystem
    {
        /// <inheritdoc />
        public SimulationSystemId Id => SimulationSystemId.SkillTrigger;

        /// <inheritdoc />
        public void Execute(SimulationWorld world)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            world.Skills.TickTriggers(world);
        }
    }

    /// <summary>Advances projectile, area, aura, and orbit delivery records.</summary>
    public sealed class SkillDeliverySystem : ISimulationSystem
    {
        /// <inheritdoc />
        public SimulationSystemId Id => SimulationSystemId.SkillDelivery;

        /// <inheritdoc />
        public void Execute(SimulationWorld world)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            world.Skills.TickDeliveries(world);
        }
    }

    /// <summary>
    /// Centralized resolver for effect commands. Skill instances only enqueue commands;
    /// this system is the sole M4 writer for healing, direct shield grants, and motion.
    /// Damage and status effects continue through the M3 request APIs.
    /// </summary>
    public sealed class SkillEffectResolutionSystem : ISimulationSystem
    {
        /// <inheritdoc />
        public SimulationSystemId Id => SimulationSystemId.SkillEffectResolution;

        /// <inheritdoc />
        public void Execute(SimulationWorld world)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            var commands = world.Skills.Commands;
            var count = commands.Count;
            for (var index = 0; index < count; index++)
            {
                Resolve(world, commands.GetAt(index));
            }

            commands.Clear();
        }

        private static void Resolve(SimulationWorld world, in SkillExecutionCommand command)
        {
            var context = command.Context;
            var effect = command.Effect;
            switch (effect.Code)
            {
                case EffectOpCode.Damage:
                    if (context.HasTarget)
                    {
                        world.QueueDamage(
                            new DamagePacket(
                                context.Owner,
                                context.Target,
                                context.SkillId,
                                (DamageType)effect.Int0,
                                (DamageTags)(uint)effect.Int1,
                                effect.Value0,
                                (effect.Flags & EffectOpFlags.CanCritical) != 0,
                                effect.Value1,
                                context.Direction * effect.Value2,
                                context.Position,
                                context.ProcDepth));
                    }
                    break;
                case EffectOpCode.Heal:
                    ApplyHealing(world, context, effect.Value0);
                    break;
                case EffectOpCode.ApplyStatus:
                    if (context.HasTarget)
                    {
                        world.QueueStatus(
                            new StatusApplicationRequest(
                                context.Owner,
                                context.Target,
                                context.SkillId,
                                effect.Reference0,
                                effect.Value0,
                                context.ProcDepth));
                    }
                    break;
                case EffectOpCode.RemoveStatus:
                    if (context.HasTarget)
                    {
                        world.QueueStatusDispel(
                            new StatusDispelRequest(context.Target, effect.Tag0));
                    }
                    break;
                case EffectOpCode.Knockback:
                    ApplyMotion(world, context, Math.Max(0f, effect.Value0));
                    break;
                case EffectOpCode.Pull:
                    ApplyMotion(world, context, -Math.Max(0f, effect.Value0));
                    break;
                case EffectOpCode.ModifyStat:
                    ApplyModifier(world, context, effect);
                    break;
                case EffectOpCode.SpawnSecondarySkill:
                    if (context.ProcDepth >= world.CombatRules.MaximumProcDepth)
                    {
                        world.Diagnostics.RecordTruncatedProcChain();
                    }
                    else
                    {
                        world.Skills.QueueSecondary(
                            context.Owner,
                            effect.Reference0,
                            context.Position,
                            context.Direction,
                            context.SkillId,
                            context.ProcDepth + 1);
                    }
                    break;
                case EffectOpCode.GrantShield:
                    ApplyShield(world, context, effect.Value0);
                    break;
                case EffectOpCode.GainResource:
                    world.Skills.GainResource(context.Owner, effect.Value0);
                    break;
            }
        }

        private static void ApplyHealing(
            SimulationWorld world,
            in SkillEffectContext context,
            float amount)
        {
            if (!context.HasTarget || !IsFinite(amount) || amount < 0f ||
                context.Target.Kind != EntityKind.Actor)
            {
                return;
            }

            world.Actors.TryApplyHealing(context.Target.Handle, amount);
        }

        private static void ApplyShield(
            SimulationWorld world,
            in SkillEffectContext context,
            float amount)
        {
            if (!context.HasTarget || !IsFinite(amount) || amount < 0f ||
                context.Target.Kind != EntityKind.Actor ||
                !world.Actors.TryGetCombat(context.Target.Handle, out var target) ||
                target.DeathPending || target.Dead)
            {
                return;
            }

            var nextMaximum = target.ShieldMaximum + amount;
            var nextCurrent = target.ShieldCurrent + amount;
            if (!IsFinite(nextMaximum) || !IsFinite(nextCurrent)) return;
            var previousMaximum = target.ShieldMaximum;
            var previousCurrent = target.ShieldCurrent;
            target.ShieldMaximum = nextMaximum;
            target.ShieldCurrent = nextCurrent;
            world.CombatEvents.Add(
                new ShieldChanged(
                    context.Target,
                    context.SkillId,
                    previousCurrent,
                    nextCurrent,
                    previousMaximum,
                    nextMaximum,
                    world.ExecutingTick));
        }

        private static void ApplyMotion(
            SimulationWorld world,
            in SkillEffectContext context,
            float signedMagnitude)
        {
            if (!context.HasTarget || context.Target.Kind != EntityKind.Actor ||
                !world.Actors.TryRead(context.Target.Handle, out var state))
            {
                return;
            }

            state.Velocity += context.Direction * signedMagnitude;
            world.Actors.TryWrite(context.Target.Handle, state);
        }

        private static void ApplyModifier(
            SimulationWorld world,
            in SkillEffectContext context,
            in ResolvedEffectOp effect)
        {
            if (!context.HasTarget || context.Target.Kind != EntityKind.Actor ||
                !effect.StatIndex0.IsValid)
            {
                return;
            }

            var duration = effect.Value1 <= 0f
                ? float.PositiveInfinity
                : effect.Value1;
            var modifier = new Modifier(
                context.SkillId,
                effect.StatId0,
                (ModifierOperation)effect.Int0,
                effect.Value0,
                effect.Int1,
                default,
                duration);
            world.Actors.TryAddModifier(context.Target.Handle, modifier, out _);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
