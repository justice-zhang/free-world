using System;
using System.Numerics;
using Game.Content.Runtime;
using Game.Core;

namespace Game.Simulation
{
    internal static class DeliveryExecutorUtility
    {
        public static bool TryGetOwnerPosition(
            SimulationWorld world,
            SkillInstance instance,
            out Vector2 position)
        {
            if (instance.Owner.Kind == EntityKind.Actor &&
                world.Actors.TryRead(instance.Owner.Handle, out var state))
            {
                position = state.Position;
                return true;
            }

            position = default;
            return false;
        }

        public static Vector2 Direction(Vector2 from, Vector2 to)
        {
            var direction = to - from;
            var lengthSquared = direction.LengthSquared();
            return lengthSquared <= 0.000001f
                ? Vector2.UnitX
                : direction / (float)Math.Sqrt(lengthSquared);
        }

        public static float Lifetime(float configured, float minimum)
        {
            return Math.Max(minimum, configured);
        }
    }

    internal sealed class InstantDeliveryExecutor : IDeliveryExecutor
    {
        public ContentId Id => SkillModuleIds.DeliveryInstant;

        public void Deliver(
            SkillRuntime runtime,
            SimulationWorld world,
            SkillInstance instance,
            RuntimeSkillLevel level,
            in SkillTriggerContext context,
            SkillTargetResultBuffer targets)
        {
            if (!DeliveryExecutorUtility.TryGetOwnerPosition(world, instance, out var source)) return;
            for (var index = 0; index < targets.Count; index++)
            {
                runtime.EnqueueEffects(instance, level, targets[index], source, context.ProcDepth);
            }
        }
    }

    internal sealed class ProjectileDeliveryExecutor : IDeliveryExecutor
    {
        public ContentId Id => SkillModuleIds.DeliveryProjectile;

        public void Deliver(
            SkillRuntime runtime,
            SimulationWorld world,
            SkillInstance instance,
            RuntimeSkillLevel level,
            in SkillTriggerContext context,
            SkillTargetResultBuffer targets)
        {
            if (!DeliveryExecutorUtility.TryGetOwnerPosition(world, instance, out var source)) return;
            var delivery = level.Delivery;
            for (var index = 0; index < targets.Count; index++)
            {
                var direction = DeliveryExecutorUtility.Direction(source, targets[index].Position);
                runtime.EnqueueSpawn(
                    new DeliverySpawnRequest
                    {
                        Kind = ActiveDeliveryKind.Projectile,
                        Instance = instance,
                        Level = level,
                        Position = source,
                        Velocity = direction * Math.Max(0f, delivery.Value0),
                        Lifetime = DeliveryExecutorUtility.Lifetime(
                            delivery.Value2,
                            world.DeltaTimeSeconds),
                        Radius = Math.Max(0f, delivery.Value1),
                        RemainingHits = Math.Max(1, delivery.Int0),
                        ProcDepth = context.ProcDepth
                    });
            }
        }
    }

    internal sealed class AreaDeliveryExecutor : IDeliveryExecutor
    {
        public ContentId Id => SkillModuleIds.DeliveryArea;

        public void Deliver(
            SkillRuntime runtime,
            SimulationWorld world,
            SkillInstance instance,
            RuntimeSkillLevel level,
            in SkillTriggerContext context,
            SkillTargetResultBuffer targets)
        {
            var delivery = level.Delivery;
            for (var index = 0; index < targets.Count; index++)
            {
                runtime.EnqueueSpawn(
                    new DeliverySpawnRequest
                    {
                        Kind = ActiveDeliveryKind.Area,
                        Instance = instance,
                        Level = level,
                        Position = targets[index].Position,
                        Lifetime = DeliveryExecutorUtility.Lifetime(
                            delivery.Value1,
                            world.DeltaTimeSeconds),
                        Radius = Math.Max(0f, delivery.Value0),
                        TickInterval = Math.Max(world.DeltaTimeSeconds, delivery.Value2),
                        RemainingHits = int.MaxValue,
                        ProcDepth = context.ProcDepth
                    });
            }
        }
    }

    internal sealed class AuraDeliveryExecutor : IDeliveryExecutor
    {
        public ContentId Id => SkillModuleIds.DeliveryAura;

        public void Deliver(
            SkillRuntime runtime,
            SimulationWorld world,
            SkillInstance instance,
            RuntimeSkillLevel level,
            in SkillTriggerContext context,
            SkillTargetResultBuffer targets)
        {
            if (!DeliveryExecutorUtility.TryGetOwnerPosition(world, instance, out var source)) return;
            var delivery = level.Delivery;
            runtime.EnqueueSpawn(
                new DeliverySpawnRequest
                {
                    Kind = ActiveDeliveryKind.Aura,
                    Instance = instance,
                    Level = level,
                    Position = source,
                    Lifetime = DeliveryExecutorUtility.Lifetime(
                        delivery.Value1,
                        world.DeltaTimeSeconds),
                    Radius = Math.Max(0f, delivery.Value0),
                    TickInterval = Math.Max(world.DeltaTimeSeconds, delivery.Value2),
                    RemainingHits = int.MaxValue,
                    ProcDepth = context.ProcDepth
                });
        }
    }

    internal sealed class OrbitDeliveryExecutor : IDeliveryExecutor
    {
        public ContentId Id => SkillModuleIds.DeliveryOrbit;

        public void Deliver(
            SkillRuntime runtime,
            SimulationWorld world,
            SkillInstance instance,
            RuntimeSkillLevel level,
            in SkillTriggerContext context,
            SkillTargetResultBuffer targets)
        {
            if (!DeliveryExecutorUtility.TryGetOwnerPosition(world, instance, out var source)) return;
            var delivery = level.Delivery;
            runtime.EnqueueSpawn(
                new DeliverySpawnRequest
                {
                    Kind = ActiveDeliveryKind.Orbit,
                    Instance = instance,
                    Level = level,
                    Position = source + new Vector2(Math.Max(0f, delivery.Value0), 0f),
                    Lifetime = DeliveryExecutorUtility.Lifetime(
                        delivery.Value2,
                        world.DeltaTimeSeconds),
                    Radius = Math.Max(0f, delivery.Value0),
                    SecondaryRadius = Math.Max(0f, delivery.Value1),
                    TickInterval = Math.Max(
                        world.DeltaTimeSeconds,
                        Math.Max(1, delivery.Int0) * world.DeltaTimeSeconds),
                    AngularSpeed = delivery.Value3,
                    RemainingHits = int.MaxValue,
                    ProcDepth = context.ProcDepth
                });
        }
    }
}
