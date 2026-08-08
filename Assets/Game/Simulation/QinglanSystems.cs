using System;

namespace Game.Simulation
{
    public abstract class QinglanOwnedSystem : ISimulationSystem
    {
        public abstract SimulationSystemId Id { get; }
        public virtual void Execute(SimulationWorld world)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
        }
    }

    public sealed class InputCommandSystem : QinglanOwnedSystem { public override SimulationSystemId Id => SimulationSystemId.InputCommand; }
    public sealed class MapObjectiveAndEventSystem : QinglanOwnedSystem
    {
        public override SimulationSystemId Id => SimulationSystemId.MapObjectiveAndEvent;

        public override void Execute(SimulationWorld world)
        {
            base.Execute(world);
            var runtime = world.Qinglan?.MapObjectives;
            if (runtime == null || !runtime.IsInitialized) return;
            runtime.AdvanceEvents(world.ExecutingTick * world.DeltaTimeSeconds);
            if (world.TryGetPlayerPosition(out var playerPosition))
                runtime.UpdateLandmarkDiscovery(playerPosition);
        }
    }
    public sealed class BossPhaseSystem : QinglanOwnedSystem
    {
        public override SimulationSystemId Id => SimulationSystemId.BossPhase;

        public override void Execute(SimulationWorld world)
        {
            base.Execute(world);
            world.Qinglan?.Bosses.Tick(world);
        }
    }
    public sealed class CharacterMechanicAccumulateSystem : QinglanOwnedSystem
    {
        public override SimulationSystemId Id => SimulationSystemId.CharacterMechanicAccumulate;

        public override void Execute(SimulationWorld world)
        {
            base.Execute(world);
            if (world.Qinglan != null)
            {
                for (var index = 0; index < world.ResolvedMovements.Count; index++)
                    world.Qinglan.Mechanics.Accumulate(world.ResolvedMovements.GetAt(index));
            }
            world.ResolvedMovements.Clear();
        }
    }
    public sealed class RewardResolutionSystem : QinglanOwnedSystem
    {
        public override SimulationSystemId Id => SimulationSystemId.RewardResolution;

        public override void Execute(SimulationWorld world)
        {
            base.Execute(world);
            var rewards = world.Qinglan?.Rewards;
            if (rewards == null || !rewards.IsInitialized) return;
            rewards.CaptureMapOutputs(world.Qinglan.MapObjectives);
            rewards.Resolve(world);
        }
    }
    public sealed class CharacterMechanicReactionSystem : QinglanOwnedSystem
    {
        public override SimulationSystemId Id => SimulationSystemId.CharacterMechanicReaction;

        public override void Execute(SimulationWorld world)
        {
            base.Execute(world);
            if (world.Qinglan == null) return;
            var events = world.CombatEvents;
            for (var index = 0; index < events.PendingDamageResolvedCount; index++)
            {
                var resolved = events.GetPendingDamageResolvedAt(index);
                if (resolved.Target.Kind != EntityKind.Actor ||
                    resolved.ShieldDamage + resolved.HealthDamage <= 0f)
                {
                    continue;
                }

                world.Qinglan.Mechanics.ReactToDamage(
                    resolved.Target.Handle,
                    world.ExecutingTick,
                    resolved.ShieldDamage,
                    resolved.HealthDamage);
            }
        }
    }
    public sealed class RegenerationSystem : QinglanOwnedSystem
    {
        public override SimulationSystemId Id => SimulationSystemId.Regeneration;

        public override void Execute(SimulationWorld world)
        {
            base.Execute(world);
            for (var index = 0; index < world.Actors.Count; index++)
            {
                var handle = world.Actors.GetHandleAt(index);
                if (!world.Actors.TryReadStat(
                        handle,
                        BuiltInStatIndices.Regeneration,
                        out var regeneration) ||
                    regeneration <= 0f)
                {
                    continue;
                }

                world.Actors.TryApplyHealing(handle, regeneration * world.DeltaTimeSeconds);
            }
        }
    }
    public sealed class LootAndRewardSystem : QinglanOwnedSystem
    {
        public override SimulationSystemId Id => SimulationSystemId.LootAndReward;
    }
}
