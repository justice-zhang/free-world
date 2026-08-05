using System;
using Game.Content.Runtime;
using Game.Core;

namespace Game.Simulation
{
    public enum MovementSource : byte
    {
        PlayerCommand = 1,
        Teleport = 2,
        Correction = 3,
        Knockback = 4,
        Pull = 5,
        Scripted = 6
    }

    public readonly struct ResolvedMovement
    {
        public ResolvedMovement(SpatialEntity entity, MovementSource source, float distance)
        {
            if (float.IsNaN(distance) || float.IsInfinity(distance) || distance < 0f)
                throw new ArgumentOutOfRangeException(nameof(distance));
            Entity = entity;
            Source = source;
            Distance = distance;
        }

        public SpatialEntity Entity { get; }
        public MovementSource Source { get; }
        public float Distance { get; }
    }

    /// <summary>Fixed actor-sidecar recording the source of the next resolved movement.</summary>
    public sealed class MovementSourceRuntime
    {
        private readonly ushort[] generations;
        private readonly MovementSource[] sources;

        public MovementSourceRuntime(int actorCapacity)
        {
            if (actorCapacity < 1) throw new ArgumentOutOfRangeException(nameof(actorCapacity));
            generations = new ushort[actorCapacity];
            sources = new MovementSource[actorCapacity];
        }

        public bool SetSource(EntityHandle actor, MovementSource source)
        {
            if (!actor.IsValid || actor.Index >= sources.Length ||
                source < MovementSource.PlayerCommand || source > MovementSource.Scripted)
                return false;
            generations[actor.Index] = actor.Generation;
            sources[actor.Index] = source;
            return true;
        }

        internal MovementSource ConsumeSource(EntityHandle actor)
        {
            if (!actor.IsValid || actor.Index >= sources.Length ||
                generations[actor.Index] != actor.Generation)
                return MovementSource.Scripted;
            var source = sources[actor.Index];
            sources[actor.Index] = MovementSource.Scripted;
            return source == 0 ? MovementSource.Scripted : source;
        }
    }

    /// <summary>Reusable per-tick buffer for map-resolved, source-attributed movement.</summary>
    public sealed class ResolvedMovementBuffer
    {
        private readonly ResolvedMovement[] entries;

        public ResolvedMovementBuffer(int capacity)
        {
            if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
            entries = new ResolvedMovement[capacity];
        }

        public int Count { get; private set; }
        public int RejectedCapacity { get; private set; }

        public ResolvedMovement GetAt(int index)
        {
            if (index < 0 || index >= Count) throw new ArgumentOutOfRangeException(nameof(index));
            return entries[index];
        }

        internal void Add(in ResolvedMovement movement)
        {
            if (Count >= entries.Length)
            {
                RejectedCapacity++;
                return;
            }
            entries[Count++] = movement;
        }

        internal void Clear()
        {
            Array.Clear(entries, 0, Count);
            Count = 0;
        }
    }

    public readonly struct RewardTransactionId : IEquatable<RewardTransactionId>
    {
        public RewardTransactionId(ulong runId, ContentId sourceStableId, int sequence)
        {
            if (!sourceStableId.IsValid) throw new ArgumentException("Source ID must be valid.", nameof(sourceStableId));
            if (sequence < 0) throw new ArgumentOutOfRangeException(nameof(sequence));
            RunId = runId;
            SourceStableId = sourceStableId;
            Sequence = sequence;
        }

        public ulong RunId { get; }
        public ContentId SourceStableId { get; }
        public int Sequence { get; }
        public bool Equals(RewardTransactionId other) => RunId == other.RunId && SourceStableId == other.SourceStableId && Sequence == other.Sequence;
        public override bool Equals(object obj) => obj is RewardTransactionId other && Equals(other);
        public override int GetHashCode() => unchecked(((RunId.GetHashCode() * 397) ^ SourceStableId.GetHashCode()) * 397 ^ Sequence);
    }

    public readonly struct CharacterMechanicSnapshot
    {
        internal CharacterMechanicSnapshot(float currentValue, int tier, long lastDamageTick)
        {
            CurrentValue = currentValue;
            Tier = tier;
            LastDamageTick = lastDamageTick;
        }
        public float CurrentValue { get; }
        public int Tier { get; }
        public long LastDamageTick { get; }
    }

    internal enum CharacterMechanicChangeReason : byte
    {
        ResolvedMovement = 1,
        ActualDamage = 2
    }

    internal readonly struct CharacterMechanicTierChanged
    {
        public CharacterMechanicTierChanged(
            EntityHandle owner,
            ContentId resourceId,
            int previousTier,
            int currentTier,
            float currentValue,
            ContentId outputId,
            CharacterMechanicChangeReason reason,
            long tick)
        {
            Owner = owner;
            ResourceId = resourceId;
            PreviousTier = previousTier;
            CurrentTier = currentTier;
            CurrentValue = currentValue;
            OutputId = outputId;
            Reason = reason;
            Tick = tick;
        }

        public EntityHandle Owner { get; }
        public ContentId ResourceId { get; }
        public int PreviousTier { get; }
        public int CurrentTier { get; }
        public float CurrentValue { get; }
        public ContentId OutputId { get; }
        public CharacterMechanicChangeReason Reason { get; }
        public long Tick { get; }
    }

    public sealed class CharacterMechanicRuntime
    {
        private struct Instance
        {
            public bool Active;
            public EntityHandle Owner;
            public RuntimeContentIndex DefinitionIndex;
            public RuntimeCharacterMechanicDefinition Definition;
            public float CurrentValue;
            public int Tier;
            public long LastDamageTick;
        }

        private readonly Instance[] instances;
        private readonly CharacterMechanicTierChanged[] pendingTierChanges;
        private readonly CharacterMechanicTierChanged[] tierChangeBatch;
        private int pendingTierChangeCount;
        private int tierChangeBatchCount;
        private long currentTick;

        public CharacterMechanicRuntime(int capacity = 4)
        {
            if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
            instances = new Instance[capacity];
            pendingTierChanges = new CharacterMechanicTierChanged[checked(capacity * 2)];
            tierChangeBatch = new CharacterMechanicTierChanged[checked(capacity * 16)];
        }

        internal int AvailableCapacity
        {
            get
            {
                var available = 0;
                for (var index = 0; index < instances.Length; index++)
                    if (!instances[index].Active) available++;
                return available;
            }
        }

        internal int TierChangeCount => tierChangeBatchCount;
        internal int RejectedTierChanges { get; private set; }
        internal int RejectedNonFiniteInputs { get; private set; }

        internal CharacterMechanicTierChanged GetTierChangeAt(int index)
        {
            if (index < 0 || index >= tierChangeBatchCount)
                throw new ArgumentOutOfRangeException(nameof(index));
            return tierChangeBatch[index];
        }

        public bool TryAttach(EntityHandle owner, RuntimeContentIndex definitionIndex, RuntimeCharacterMechanicDefinition definition)
        {
            if (!owner.IsValid || !definitionIndex.IsValid || definition == null) return false;
            for (var index = 0; index < instances.Length; index++)
            {
                if (instances[index].Active) continue;
                instances[index] = new Instance { Active = true, Owner = owner, DefinitionIndex = definitionIndex, Definition = definition, LastDamageTick = -1 };
                return true;
            }
            return false;
        }

        public void Accumulate(in ResolvedMovement movement)
        {
            if (movement.Source != MovementSource.PlayerCommand || movement.Entity.Kind != EntityKind.Actor) return;
            for (var index = 0; index < instances.Length; index++)
            {
                if (!instances[index].Active || instances[index].Owner != movement.Entity.Handle) continue;
                var instance = instances[index];
                var nextValue = instance.CurrentValue + movement.Distance * instance.Definition.GainPerUnit;
                if (float.IsNaN(nextValue) || float.IsInfinity(nextValue))
                {
                    RejectedNonFiniteInputs++;
                    continue;
                }

                var previousTier = instance.Tier;
                instance.CurrentValue = nextValue;
                ResolveTier(ref instance);
                EmitTierChange(
                    ref instance,
                    previousTier,
                    CharacterMechanicChangeReason.ResolvedMovement,
                    currentTick);
                instances[index] = instance;
            }
        }

        public void ReactToDamage(EntityHandle owner, long tick, float shieldDamage, float healthDamage)
        {
            if (shieldDamage + healthDamage <= 0f) return;
            for (var index = 0; index < instances.Length; index++)
            {
                if (!instances[index].Active || instances[index].Owner != owner || instances[index].LastDamageTick == tick) continue;
                var instance = instances[index];
                var previousTier = instance.Tier;
                instance.CurrentValue = ResolveDamageLoss(instance);
                instance.LastDamageTick = tick;
                ResolveTier(ref instance);
                EmitTierChange(
                    ref instance,
                    previousTier,
                    CharacterMechanicChangeReason.ActualDamage,
                    tick);
                instances[index] = instance;
            }
        }

        public bool TryGet(EntityHandle owner, out CharacterMechanicSnapshot snapshot)
        {
            for (var index = 0; index < instances.Length; index++)
            {
                if (!instances[index].Active || instances[index].Owner != owner) continue;
                snapshot = new CharacterMechanicSnapshot(instances[index].CurrentValue, instances[index].Tier, instances[index].LastDamageTick);
                return true;
            }
            snapshot = default;
            return false;
        }

        internal bool MatchesCurrentOutput(EntityHandle owner, ContentId outputId)
        {
            if (!outputId.IsValid) return true;
            for (var index = 0; index < instances.Length; index++)
            {
                var instance = instances[index];
                if (!instance.Active || instance.Owner != owner || instance.Tier <= 0) continue;
                if (instance.Definition.Tiers[instance.Tier - 1].OutputId == outputId) return true;
            }
            return false;
        }

        internal bool Detach(EntityHandle owner)
        {
            var detached = false;
            for (var index = 0; index < instances.Length; index++)
            {
                if (!instances[index].Active || instances[index].Owner != owner) continue;
                instances[index] = default;
                detached = true;
            }
            return detached;
        }

        internal void BeginBatch()
        {
            Array.Clear(tierChangeBatch, 0, tierChangeBatchCount);
            tierChangeBatchCount = 0;
            BeginTick(0);
        }

        internal void BeginTick(long tick)
        {
            Array.Clear(pendingTierChanges, 0, pendingTierChangeCount);
            pendingTierChangeCount = 0;
            currentTick = tick;
        }

        internal void FlushTick()
        {
            for (var index = 0; index < pendingTierChangeCount; index++)
            {
                if (tierChangeBatchCount >= tierChangeBatch.Length)
                {
                    RejectedTierChanges++;
                    continue;
                }
                tierChangeBatch[tierChangeBatchCount++] = pendingTierChanges[index];
            }
            BeginTick(0);
        }

        private static float ResolveDamageLoss(in Instance instance)
        {
            if (instance.Tier <= 0) return Math.Max(0f, instance.CurrentValue);
            var targetTier = instance.Tier - 1;
            var lowerBound = targetTier <= 0
                ? 0f
                : instance.Definition.Tiers[targetTier - 1].Threshold;
            var upperThreshold = instance.Definition.Tiers[targetTier].Threshold;
            var upperBound = PreviousFloat(upperThreshold);
            var reduced = Math.Max(0f, instance.CurrentValue - instance.Definition.LossOnDamage);
            if (reduced < lowerBound) reduced = lowerBound;
            if (reduced > upperBound) reduced = upperBound;
            return reduced;
        }

        private static float PreviousFloat(float value)
        {
            if (value <= 0f) return 0f;
            var bits = BitConverter.SingleToInt32Bits(value);
            return BitConverter.Int32BitsToSingle(bits - 1);
        }

        private void EmitTierChange(
            ref Instance instance,
            int previousTier,
            CharacterMechanicChangeReason reason,
            long tick)
        {
            if (previousTier == instance.Tier) return;
            if (pendingTierChangeCount >= pendingTierChanges.Length)
            {
                RejectedTierChanges++;
                return;
            }

            var outputId = instance.Tier > 0
                ? instance.Definition.Tiers[instance.Tier - 1].OutputId
                : default;
            pendingTierChanges[pendingTierChangeCount++] =
                new CharacterMechanicTierChanged(
                    instance.Owner,
                    instance.Definition.ResourceId,
                    previousTier,
                    instance.Tier,
                    instance.CurrentValue,
                    outputId,
                    reason,
                    tick);
        }

        private static void ResolveTier(ref Instance instance)
        {
            var tier = 0;
            for (var index = 0; index < instance.Definition.Tiers.Count; index++)
                if (instance.CurrentValue >= instance.Definition.Tiers[index].Threshold) tier = index + 1;
            instance.Tier = tier;
        }
    }

    public sealed class RewardRuntime
    {
        private readonly RewardTransactionId[] committed;
        private int count;

        public RewardRuntime(int transactionCapacity = 128)
        {
            if (transactionCapacity < 1) throw new ArgumentOutOfRangeException(nameof(transactionCapacity));
            committed = new RewardTransactionId[transactionCapacity];
        }

        public int CommittedCount => count;

        internal bool IsCommitted(in RewardTransactionId transaction)
        {
            for (var index = 0; index < count; index++)
                if (committed[index].Equals(transaction)) return true;
            return false;
        }

        internal bool CanCommit(in RewardTransactionId transaction) =>
            !IsCommitted(transaction) && count < committed.Length;

        public bool TryCommit(in RewardTransactionId transaction)
        {
            if (IsCommitted(transaction)) return false;
            if (count >= committed.Length) return false;
            committed[count++] = transaction;
            return true;
        }
    }

    public sealed class BossPhaseRuntime
    {
        public int ResolvePhase(RuntimeBossDefinition definition, int currentPhase, float healthFraction, bool lethal)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (lethal) return definition.Phases.Count;
            var phase = Math.Max(0, currentPhase);
            while (phase < definition.Phases.Count && healthFraction <= definition.Phases[phase].HealthThreshold) phase++;
            return phase;
        }
    }

    public sealed class EliteAffixRuntime
    {
        public bool IsEligible(RuntimeEliteAffixDefinition definition, ContentTag[] targetTags)
        {
            if (definition == null) return false;
            var tags = targetTags ?? Array.Empty<ContentTag>();
            for (var required = 0; required < definition.RequiredTags.Count; required++)
            {
                var found = false;
                for (var index = 0; index < tags.Length; index++) if (tags[index] == definition.RequiredTags[required]) { found = true; break; }
                if (!found) return false;
            }
            for (var excluded = 0; excluded < definition.ExcludedTags.Count; excluded++)
                for (var index = 0; index < tags.Length; index++) if (tags[index] == definition.ExcludedTags[excluded]) return false;
            return true;
        }
    }

    /// <summary>Composition-root owned general runtime bundle; no Scene or Unity object dependencies.</summary>
    public sealed class QinglanRuntimeHub
    {
        public QinglanRuntimeHub(
            CharacterMechanicRuntime mechanics = null,
            RewardRuntime rewards = null,
            MapObjectiveRuntime mapObjectives = null,
            BossPhaseRuntime bosses = null,
            EliteAffixRuntime affixes = null)
        {
            Mechanics = mechanics ?? new CharacterMechanicRuntime();
            Rewards = rewards ?? new RewardRuntime();
            MapObjectives = mapObjectives ?? new MapObjectiveRuntime();
            Bosses = bosses ?? new BossPhaseRuntime();
            Affixes = affixes ?? new EliteAffixRuntime();
        }

        public CharacterMechanicRuntime Mechanics { get; }
        public RewardRuntime Rewards { get; }
        public MapObjectiveRuntime MapObjectives { get; }
        public BossPhaseRuntime Bosses { get; }
        public EliteAffixRuntime Affixes { get; }
    }
}
