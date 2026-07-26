using System;
using System.Numerics;
using Game.Content.Runtime;
using Game.Core;

namespace Game.Simulation
{
    internal static class TargetingExecutorUtility
    {
        public static bool TryGetOwnerState(
            SimulationWorld world,
            SkillInstance instance,
            out SimulationEntityState state)
        {
            state = default;
            return instance.Owner.Kind == EntityKind.Actor &&
                   world.Actors.TryRead(instance.Owner.Handle, out state);
        }

        public static Vector2 GetDirection(
            in SkillTriggerContext context,
            in SimulationEntityState owner)
        {
            var direction = context.Direction;
            if (direction.LengthSquared() <= 0.000001f)
            {
                direction = new Vector2(
                    (float)Math.Cos(owner.FacingRadians),
                    (float)Math.Sin(owner.FacingRadians));
            }

            var lengthSquared = direction.LengthSquared();
            return lengthSquared <= 0.000001f
                ? Vector2.UnitX
                : direction / (float)Math.Sqrt(lengthSquared);
        }

        public static void CollectActors(
            SimulationWorld world,
            SpatialEntity owner,
            Vector2 center,
            float radius,
            SpatialQueryBuffer spatialResults,
            SkillTargetResultBuffer targets)
        {
            targets.Reset();
            world.SpatialGrid.QueryRadius(center, Math.Max(0f, radius), spatialResults);
            for (var index = 0; index < spatialResults.Count; index++)
            {
                var candidate = spatialResults[index];
                if (candidate.Entity.Kind != EntityKind.Actor ||
                    candidate.Entity == owner ||
                    !world.Actors.Contains(candidate.Entity.Handle) ||
                    world.Actors.IsDeathPending(candidate.Entity.Handle) ||
                    !world.IsHostileTarget(owner, candidate.Entity))
                {
                    continue;
                }

                targets.Add(new SkillTarget(candidate.Entity, candidate.Position, true));
            }
        }

        public static int GetLimit(int configured, int available, int defaultValue)
        {
            var limit = configured > 0 ? configured : defaultValue;
            return Math.Min(Math.Max(0, limit), available);
        }
    }

    internal sealed class SelfTargetingExecutor : ITargetingExecutor
    {
        public ContentId Id => SkillModuleIds.TargetingSelf;

        public void Select(
            SimulationWorld world,
            SkillInstance instance,
            RuntimeSkillLevel level,
            in SkillTriggerContext context,
            SpatialQueryBuffer spatialResults,
            SkillTargetResultBuffer targets,
            ref RandomStream random)
        {
            targets.Reset();
            if (TargetingExecutorUtility.TryGetOwnerState(world, instance, out var state))
            {
                targets.Add(new SkillTarget(instance.Owner, state.Position, true));
            }
        }
    }

    internal sealed class NearestTargetingExecutor : ITargetingExecutor
    {
        public ContentId Id => SkillModuleIds.TargetingNearest;

        public void Select(
            SimulationWorld world,
            SkillInstance instance,
            RuntimeSkillLevel level,
            in SkillTriggerContext context,
            SpatialQueryBuffer spatialResults,
            SkillTargetResultBuffer targets,
            ref RandomStream random)
        {
            if (!TargetingExecutorUtility.TryGetOwnerState(world, instance, out var owner))
            {
                targets.Reset();
                return;
            }

            TargetingExecutorUtility.CollectActors(
                world,
                instance.Owner,
                owner.Position,
                level.Targeting.Value0,
                spatialResults,
                targets);
            targets.SortByDistance(owner.Position);
            targets.Truncate(
                TargetingExecutorUtility.GetLimit(level.Targeting.Int0, targets.Count, 1));
        }
    }

    internal sealed class RandomTargetingExecutor : ITargetingExecutor
    {
        public ContentId Id => SkillModuleIds.TargetingRandom;

        public void Select(
            SimulationWorld world,
            SkillInstance instance,
            RuntimeSkillLevel level,
            in SkillTriggerContext context,
            SpatialQueryBuffer spatialResults,
            SkillTargetResultBuffer targets,
            ref RandomStream random)
        {
            if (!TargetingExecutorUtility.TryGetOwnerState(world, instance, out var owner))
            {
                targets.Reset();
                return;
            }

            TargetingExecutorUtility.CollectActors(
                world,
                instance.Owner,
                owner.Position,
                level.Targeting.Value0,
                spatialResults,
                targets);
            targets.SortStable();
            var selected = TargetingExecutorUtility.GetLimit(
                level.Targeting.Int0,
                targets.Count,
                1);
            for (var index = 0; index < selected; index++)
            {
                var swap = index + random.NextInt(targets.Count - index);
                targets.Swap(index, swap);
            }

            targets.Truncate(selected);
        }
    }

    internal sealed class CircleTargetingExecutor : ITargetingExecutor
    {
        public ContentId Id => SkillModuleIds.TargetingCircle;

        public void Select(
            SimulationWorld world,
            SkillInstance instance,
            RuntimeSkillLevel level,
            in SkillTriggerContext context,
            SpatialQueryBuffer spatialResults,
            SkillTargetResultBuffer targets,
            ref RandomStream random)
        {
            if (!TargetingExecutorUtility.TryGetOwnerState(world, instance, out var owner))
            {
                targets.Reset();
                return;
            }

            TargetingExecutorUtility.CollectActors(
                world,
                instance.Owner,
                owner.Position,
                level.Targeting.Value0,
                spatialResults,
                targets);
            targets.SortStable();
            targets.Truncate(
                TargetingExecutorUtility.GetLimit(level.Targeting.Int0, targets.Count, targets.Count));
        }
    }

    internal sealed class ConeTargetingExecutor : ITargetingExecutor
    {
        public ContentId Id => SkillModuleIds.TargetingCone;

        public void Select(
            SimulationWorld world,
            SkillInstance instance,
            RuntimeSkillLevel level,
            in SkillTriggerContext context,
            SpatialQueryBuffer spatialResults,
            SkillTargetResultBuffer targets,
            ref RandomStream random)
        {
            if (!TargetingExecutorUtility.TryGetOwnerState(world, instance, out var owner))
            {
                targets.Reset();
                return;
            }

            var direction = TargetingExecutorUtility.GetDirection(context, owner);
            var range = Math.Max(0f, level.Targeting.Value0);
            var halfAngle = Math.Max(0f, Math.Min(180f, level.Targeting.Value1)) *
                            (float)Math.PI / 360f;
            var minimumDot = (float)Math.Cos(halfAngle);
            TargetingExecutorUtility.CollectActors(
                world,
                instance.Owner,
                owner.Position,
                range,
                spatialResults,
                targets);
            for (var index = targets.Count - 1; index >= 0; index--)
            {
                var offset = targets[index].Position - owner.Position;
                var lengthSquared = offset.LengthSquared();
                var dot = lengthSquared <= 0.000001f
                    ? 1f
                    : Vector2.Dot(direction, offset / (float)Math.Sqrt(lengthSquared));
                if (dot < minimumDot) RemoveAt(targets, index);
            }

            targets.SortStable();
            targets.Truncate(
                TargetingExecutorUtility.GetLimit(level.Targeting.Int0, targets.Count, targets.Count));
        }

        private static void RemoveAt(SkillTargetResultBuffer targets, int index)
        {
            for (var move = index + 1; move < targets.Count; move++) targets.Swap(move - 1, move);
            targets.Truncate(targets.Count - 1);
        }
    }

    internal sealed class LineTargetingExecutor : ITargetingExecutor
    {
        public ContentId Id => SkillModuleIds.TargetingLine;

        public void Select(
            SimulationWorld world,
            SkillInstance instance,
            RuntimeSkillLevel level,
            in SkillTriggerContext context,
            SpatialQueryBuffer spatialResults,
            SkillTargetResultBuffer targets,
            ref RandomStream random)
        {
            if (!TargetingExecutorUtility.TryGetOwnerState(world, instance, out var owner))
            {
                targets.Reset();
                return;
            }

            var direction = TargetingExecutorUtility.GetDirection(context, owner);
            var length = Math.Max(0f, level.Targeting.Value0);
            var halfWidth = Math.Max(0f, level.Targeting.Value1);
            TargetingExecutorUtility.CollectActors(
                world,
                instance.Owner,
                owner.Position,
                length + halfWidth,
                spatialResults,
                targets);
            for (var index = targets.Count - 1; index >= 0; index--)
            {
                var offset = targets[index].Position - owner.Position;
                var forward = Vector2.Dot(offset, direction);
                var perpendicular = offset - (direction * forward);
                if (forward < 0f || forward > length || perpendicular.LengthSquared() > halfWidth * halfWidth)
                {
                    RemoveAt(targets, index);
                }
            }

            targets.SortByDistance(owner.Position);
            targets.Truncate(
                TargetingExecutorUtility.GetLimit(level.Targeting.Int0, targets.Count, targets.Count));
        }

        private static void RemoveAt(SkillTargetResultBuffer targets, int index)
        {
            for (var move = index + 1; move < targets.Count; move++) targets.Swap(move - 1, move);
            targets.Truncate(targets.Count - 1);
        }
    }

    internal sealed class RingTargetingExecutor : ITargetingExecutor
    {
        public ContentId Id => SkillModuleIds.TargetingRing;

        public void Select(
            SimulationWorld world,
            SkillInstance instance,
            RuntimeSkillLevel level,
            in SkillTriggerContext context,
            SpatialQueryBuffer spatialResults,
            SkillTargetResultBuffer targets,
            ref RandomStream random)
        {
            if (!TargetingExecutorUtility.TryGetOwnerState(world, instance, out var owner))
            {
                targets.Reset();
                return;
            }

            var inner = Math.Max(0f, level.Targeting.Value0);
            var outer = Math.Max(inner, level.Targeting.Value1);
            TargetingExecutorUtility.CollectActors(
                world,
                instance.Owner,
                owner.Position,
                outer,
                spatialResults,
                targets);
            var innerSquared = inner * inner;
            for (var index = targets.Count - 1; index >= 0; index--)
            {
                if (Vector2.DistanceSquared(owner.Position, targets[index].Position) < innerSquared)
                {
                    RemoveAt(targets, index);
                }
            }

            targets.SortStable();
            targets.Truncate(
                TargetingExecutorUtility.GetLimit(level.Targeting.Int0, targets.Count, targets.Count));
        }

        private static void RemoveAt(SkillTargetResultBuffer targets, int index)
        {
            for (var move = index + 1; move < targets.Count; move++) targets.Swap(move - 1, move);
            targets.Truncate(targets.Count - 1);
        }
    }

    internal sealed class RandomPointTargetingExecutor : ITargetingExecutor
    {
        public ContentId Id => SkillModuleIds.TargetingRandomPointAroundPlayer;

        public void Select(
            SimulationWorld world,
            SkillInstance instance,
            RuntimeSkillLevel level,
            in SkillTriggerContext context,
            SpatialQueryBuffer spatialResults,
            SkillTargetResultBuffer targets,
            ref RandomStream random)
        {
            targets.Reset();
            if (!TargetingExecutorUtility.TryGetOwnerState(world, instance, out var owner)) return;
            var minimum = Math.Max(0f, level.Targeting.Value0);
            var maximum = Math.Max(minimum, level.Targeting.Value1);
            var radiusSquared = (minimum * minimum) +
                                ((maximum * maximum) - (minimum * minimum)) * random.NextFloat();
            var radius = (float)Math.Sqrt(radiusSquared);
            var angle = random.NextFloat() * 2f * (float)Math.PI;
            var point = owner.Position + new Vector2(
                (float)Math.Cos(angle) * radius,
                (float)Math.Sin(angle) * radius);
            targets.Add(new SkillTarget(default, point, false));
        }
    }
}
