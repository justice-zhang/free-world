using System;
using Game.Content.Runtime;
using Game.Core;

namespace Game.Simulation
{
    /// <summary>Read-only aggregate returned by a status selector.</summary>
    public readonly struct StatusQueryResult
    {
        internal StatusQueryResult(int matchedInstances, int totalStacks)
        {
            MatchedInstances = matchedInstances;
            TotalStacks = totalStacks;
        }

        /// <summary>Gets the number of matching runtime instances.</summary>
        public int MatchedInstances { get; }

        /// <summary>Gets the total stack count across matching instances.</summary>
        public int TotalStacks { get; }
    }

    /// <summary>Outcome of one validate-then-commit status consumption.</summary>
    public readonly struct StatusConsumeResult
    {
        internal StatusConsumeResult(bool committed, int matchedInstances, int consumedStacks)
        {
            Committed = committed;
            MatchedInstances = matchedInstances;
            ConsumedStacks = consumedStacks;
        }

        /// <summary>Gets whether validation succeeded and the transaction committed.</summary>
        public bool Committed { get; }

        /// <summary>Gets the number of instances included by the selector.</summary>
        public int MatchedInstances { get; }

        /// <summary>Gets the number of stacks actually consumed.</summary>
        public int ConsumedStacks { get; }
    }

    /// <summary>
    /// Fixed-capacity query and consume owner. A consume operation builds its complete plan
    /// before mutating status storage, so a failed exact request never removes partial stacks.
    /// </summary>
    public sealed class StatusTransactionRuntime
    {
        private readonly long[] plannedInstanceIds;
        private readonly int[] plannedStackCounts;

        /// <summary>Creates a transaction runtime with an audited maximum matching-instance count.</summary>
        public StatusTransactionRuntime(int maximumPlannedInstances = 32)
        {
            if (maximumPlannedInstances <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumPlannedInstances));
            }

            plannedInstanceIds = new long[maximumPlannedInstances];
            plannedStackCounts = new int[maximumPlannedInstances];
        }

        /// <summary>Gets the maximum number of instances one atomic plan may contain.</summary>
        public int MaximumPlannedInstances => plannedInstanceIds.Length;

        /// <summary>Queries a live actor by bound status index or canonical status tag.</summary>
        public StatusQueryResult Query(
            SimulationWorld world,
            SpatialEntity target,
            RuntimeContentIndex statusIndex,
            ContentTag statusTag)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            if (!TryGetTarget(world, target, out var actor)) return default;

            var instances = 0;
            var stacks = 0;
            for (var index = 0; index < actor.Statuses.Count; index++)
            {
                var instance = actor.Statuses.GetAt(index);
                if (!Matches(instance, statusIndex, statusTag)) continue;
                instances++;
                stacks += Math.Max(0, instance.Stacks);
            }

            return new StatusQueryResult(instances, stacks);
        }

        /// <summary>
        /// Atomically consumes matching stacks. When requireExact is true, insufficient stacks or
        /// plan overflow rejects the operation without mutation; otherwise all available stacks up
        /// to requestedStacks are committed.
        /// </summary>
        public StatusConsumeResult Consume(
            SimulationWorld world,
            SpatialEntity target,
            RuntimeContentIndex statusIndex,
            ContentTag statusTag,
            int requestedStacks,
            bool requireExact)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            if (requestedStacks <= 0 || !TryGetTarget(world, target, out var actor))
            {
                return default;
            }

            var remaining = requestedStacks;
            var matchedInstances = 0;
            var planCount = 0;
            for (var index = 0; index < actor.Statuses.Count; index++)
            {
                var instance = actor.Statuses.GetAt(index);
                if (!Matches(instance, statusIndex, statusTag)) continue;
                matchedInstances++;
                if (remaining <= 0) continue;
                if (planCount >= plannedInstanceIds.Length)
                {
                    ClearPlan(planCount);
                    return default;
                }

                var consume = Math.Min(Math.Max(0, instance.Stacks), remaining);
                if (consume <= 0) continue;
                plannedInstanceIds[planCount] = instance.InstanceId;
                plannedStackCounts[planCount] = consume;
                planCount++;
                remaining -= consume;
            }

            if ((requireExact && remaining > 0) || planCount == 0)
            {
                ClearPlan(planCount);
                return new StatusConsumeResult(false, matchedInstances, 0);
            }

            var consumed = 0;
            for (var planIndex = 0; planIndex < planCount; planIndex++)
            {
                var storageIndex = FindInstance(actor.Statuses, plannedInstanceIds[planIndex]);
                if (storageIndex < 0)
                {
                    // No mutation can occur outside this synchronous owner. This guard preserves
                    // deterministic failure behavior if that invariant is violated in the future.
                    ClearPlan(planCount);
                    return new StatusConsumeResult(false, matchedInstances, consumed);
                }

                var instance = actor.Statuses.GetAt(storageIndex);
                var amount = plannedStackCounts[planIndex];
                consumed += amount;
                if (amount >= instance.Stacks)
                {
                    StatusTickSystem.RemoveStatusAt(world, target, actor, storageIndex);
                }
                else
                {
                    instance.Stacks -= amount;
                    actor.Statuses.SetAt(storageIndex, instance);
                }
            }

            ClearPlan(planCount);
            return new StatusConsumeResult(true, matchedInstances, consumed);
        }

        private static bool TryGetTarget(
            SimulationWorld world,
            SpatialEntity target,
            out ActorCombatRecord actor)
        {
            if (target.Kind != EntityKind.Actor ||
                !world.Actors.TryGetCombat(target.Handle, out actor) ||
                actor.DeathPending ||
                actor.Dead)
            {
                actor = null;
                return false;
            }

            return true;
        }

        private static bool Matches(
            in StatusInstance instance,
            RuntimeContentIndex statusIndex,
            ContentTag statusTag)
        {
            if (statusIndex.IsValid && instance.StatusIndex == statusIndex) return true;
            if (!statusTag.IsValid || instance.Definition == null) return false;
            var tags = instance.Definition.Tags;
            for (var index = 0; index < tags.Count; index++)
            {
                if (tags[index] == statusTag) return true;
            }

            return false;
        }

        private static int FindInstance(StatusCollection statuses, long instanceId)
        {
            for (var index = 0; index < statuses.Count; index++)
            {
                if (statuses.GetAt(index).InstanceId == instanceId) return index;
            }

            return -1;
        }

        private void ClearPlan(int count)
        {
            Array.Clear(plannedInstanceIds, 0, count);
            Array.Clear(plannedStackCounts, 0, count);
        }
    }
}
