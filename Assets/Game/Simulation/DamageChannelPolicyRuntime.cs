using System;
using Game.Core;

namespace Game.Simulation
{
    /// <summary>Fixed-capacity target/channel policy sidecar for damage immunity, cooldown, and barriers.</summary>
    public sealed class DamageChannelPolicyRuntime
    {
        public const int MaximumChannelsPerActor = 8;
        private readonly ushort[] generations;
        private readonly DamageChannelId[] channels;
        private readonly long[] cooldownExpiry;
        private readonly float[] barriers;
        private readonly bool[] immunities;

        public DamageChannelPolicyRuntime(int actorCapacity)
        {
            if (actorCapacity < 1) throw new ArgumentOutOfRangeException(nameof(actorCapacity));
            generations = new ushort[actorCapacity];
            channels = new DamageChannelId[actorCapacity * MaximumChannelsPerActor];
            cooldownExpiry = new long[channels.Length];
            barriers = new float[channels.Length];
            immunities = new bool[channels.Length];
        }

        public int RejectedCapacity { get; private set; }
        public int StableEvictions { get; private set; }

        public bool SetImmune(EntityHandle target, DamageChannelId channel, bool immune)
        {
            var slot = GetOrCreateSlot(target, channel);
            if (slot < 0) return false;
            immunities[slot] = immune;
            return true;
        }

        public bool SetBarrier(EntityHandle target, DamageChannelId channel, float amount)
        {
            if (float.IsNaN(amount) || float.IsInfinity(amount) || amount < 0f) return false;
            var slot = GetOrCreateSlot(target, channel);
            if (slot < 0) return false;
            barriers[slot] = amount;
            return true;
        }

        internal DamageResolutionOutcome Evaluate(EntityHandle target, DamageChannelId channel, long tick)
        {
            var slot = GetOrCreateSlot(target, channel);
            if (slot < 0) return DamageResolutionOutcome.Invalid;
            if (immunities[slot]) return DamageResolutionOutcome.Immune;
            return cooldownExpiry[slot] > tick ? DamageResolutionOutcome.ChannelCooldown : DamageResolutionOutcome.Applied;
        }

        internal float AbsorbBarrier(EntityHandle target, DamageChannelId channel, float amount)
        {
            if (amount <= 0f) return 0f;
            var slot = GetOrCreateSlot(target, channel);
            if (slot < 0) return 0f;
            var absorbed = Math.Min(barriers[slot], amount);
            barriers[slot] -= absorbed;
            return absorbed;
        }

        internal void CommitCooldown(EntityHandle target, DamageChannelId channel, long tick, int cooldownTicks)
        {
            if (cooldownTicks <= 0) return;
            var slot = GetOrCreateSlot(target, channel);
            if (slot >= 0) cooldownExpiry[slot] = tick + cooldownTicks;
        }

        private int GetOrCreateSlot(EntityHandle target, DamageChannelId channel)
        {
            if (!target.IsValid || !channel.IsValid || target.Index >= generations.Length)
            {
                RejectedCapacity++;
                return -1;
            }
            var start = target.Index * MaximumChannelsPerActor;
            if (generations[target.Index] != target.Generation)
            {
                generations[target.Index] = target.Generation;
                for (var index = 0; index < MaximumChannelsPerActor; index++)
                {
                    var slot = start + index;
                    channels[slot] = default;
                    cooldownExpiry[slot] = 0;
                    barriers[slot] = 0f;
                    immunities[slot] = false;
                }
            }
            var empty = -1;
            var earliest = start;
            for (var index = 0; index < MaximumChannelsPerActor; index++)
            {
                var slot = start + index;
                if (channels[slot] == channel) return slot;
                if (!channels[slot].IsValid && empty < 0) empty = slot;
                if (cooldownExpiry[slot] < cooldownExpiry[earliest]) earliest = slot;
            }
            var selected = empty >= 0 ? empty : earliest;
            if (empty < 0) StableEvictions++;
            channels[selected] = channel;
            cooldownExpiry[selected] = 0;
            barriers[selected] = 0f;
            immunities[selected] = false;
            return selected;
        }
    }
}
