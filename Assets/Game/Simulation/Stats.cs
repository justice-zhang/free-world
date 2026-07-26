using System;
using System.Collections.Generic;
using Game.Core;

namespace Game.Simulation
{
    /// <summary>
    /// Immutable mapping from stable statistic IDs to compact runtime indices.
    /// </summary>
    public sealed class StatCatalog
    {
        private readonly Dictionary<StatId, StatIndex> indexById;
        private readonly StatId[] ids;
        private readonly float[] minimums;
        private readonly float[] maximums;

        private StatCatalog(
            StatId[] ids,
            float[] minimums,
            float[] maximums)
        {
            this.ids = ids;
            this.minimums = minimums;
            this.maximums = maximums;
            indexById = new Dictionary<StatId, StatIndex>(ids.Length);
            for (var index = 0; index < ids.Length; index++)
            {
                indexById.Add(ids[index], new StatIndex(index));
            }
        }

        /// <summary>Gets the built-in M3 statistic catalog.</summary>
        public static StatCatalog Default { get; } = CreateDefault();

        /// <summary>Gets the number of indexed statistics.</summary>
        public int Count => ids.Length;

        /// <summary>Resolves a stable ID without string comparison in later reads.</summary>
        public bool TryGetIndex(StatId id, out StatIndex index)
        {
            return indexById.TryGetValue(id, out index);
        }

        /// <summary>Gets the stable ID assigned to an index.</summary>
        public StatId GetId(StatIndex index)
        {
            Validate(index);
            return ids[index.Value];
        }

        /// <summary>Applies the hard domain bounds for one statistic.</summary>
        public float ClampToDomain(StatIndex index, float value)
        {
            Validate(index);
            if (float.IsNaN(value))
            {
                return minimums[index.Value];
            }

            if (value < minimums[index.Value])
            {
                return minimums[index.Value];
            }

            return value > maximums[index.Value]
                ? maximums[index.Value]
                : value;
        }

        private static StatCatalog CreateDefault()
        {
            var ids = new[]
            {
                BuiltInStatIds.Health,
                BuiltInStatIds.MoveSpeed,
                BuiltInStatIds.Damage,
                BuiltInStatIds.AttackSpeed,
                BuiltInStatIds.Cooldown,
                BuiltInStatIds.Range,
                BuiltInStatIds.Duration,
                BuiltInStatIds.ProjectileCount,
                BuiltInStatIds.Pierce,
                BuiltInStatIds.CriticalChance,
                BuiltInStatIds.Armor,
                BuiltInStatIds.PickupRange,
                BuiltInStatIds.Luck,
                BuiltInStatIds.Regeneration
            };
            var minimums = new[]
            {
                1f,
                0f,
                0f,
                0f,
                0f,
                0f,
                0f,
                0f,
                0f,
                0f,
                0f,
                0f,
                0f,
                0f
            };
            var maximums = new[]
            {
                1_000_000_000f,
                1_000_000f,
                1_000_000f,
                1_000_000f,
                1_000_000f,
                1_000_000f,
                1_000_000f,
                1_000_000f,
                1_000_000f,
                1f,
                1_000_000f,
                1_000_000f,
                1_000_000f,
                1_000_000f
            };
            return new StatCatalog(ids, minimums, maximums);
        }

        private void Validate(StatIndex index)
        {
            if (!index.IsValid || index.Value >= ids.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }
        }
    }

    /// <summary>
    /// Compact indices assigned by the built-in M3 statistic catalog.
    /// </summary>
    public static class BuiltInStatIndices
    {
        /// <summary>Maximum health.</summary>
        public static readonly StatIndex Health = new StatIndex(0);

        /// <summary>Movement speed.</summary>
        public static readonly StatIndex MoveSpeed = new StatIndex(1);

        /// <summary>Outgoing damage multiplier.</summary>
        public static readonly StatIndex Damage = new StatIndex(2);

        /// <summary>Attack-speed multiplier.</summary>
        public static readonly StatIndex AttackSpeed = new StatIndex(3);

        /// <summary>Cooldown multiplier.</summary>
        public static readonly StatIndex Cooldown = new StatIndex(4);

        /// <summary>Range multiplier.</summary>
        public static readonly StatIndex Range = new StatIndex(5);

        /// <summary>Duration multiplier.</summary>
        public static readonly StatIndex Duration = new StatIndex(6);

        /// <summary>Projectile count.</summary>
        public static readonly StatIndex ProjectileCount = new StatIndex(7);

        /// <summary>Penetration count.</summary>
        public static readonly StatIndex Pierce = new StatIndex(8);

        /// <summary>Critical-hit probability.</summary>
        public static readonly StatIndex CriticalChance = new StatIndex(9);

        /// <summary>Physical armor.</summary>
        public static readonly StatIndex Armor = new StatIndex(10);

        /// <summary>Pickup attraction range.</summary>
        public static readonly StatIndex PickupRange = new StatIndex(11);

        /// <summary>Luck.</summary>
        public static readonly StatIndex Luck = new StatIndex(12);

        /// <summary>Health regeneration per second.</summary>
        public static readonly StatIndex Regeneration = new StatIndex(13);
    }

    /// <summary>
    /// Base values used to initialize one actor statistic block.
    /// </summary>
    public struct StatBaseValues
    {
        /// <summary>Maximum health.</summary>
        public float Health;

        /// <summary>Movement speed.</summary>
        public float MoveSpeed;

        /// <summary>Outgoing damage multiplier.</summary>
        public float Damage;

        /// <summary>Attack-speed multiplier.</summary>
        public float AttackSpeed;

        /// <summary>Cooldown multiplier.</summary>
        public float Cooldown;

        /// <summary>Range multiplier.</summary>
        public float Range;

        /// <summary>Duration multiplier.</summary>
        public float Duration;

        /// <summary>Projectile count.</summary>
        public float ProjectileCount;

        /// <summary>Penetration count.</summary>
        public float Pierce;

        /// <summary>Critical-hit probability.</summary>
        public float CriticalChance;

        /// <summary>Physical armor.</summary>
        public float Armor;

        /// <summary>Pickup attraction range.</summary>
        public float PickupRange;

        /// <summary>Luck.</summary>
        public float Luck;

        /// <summary>Health regeneration per second.</summary>
        public float Regeneration;

        /// <summary>Creates conventional M3 defaults.</summary>
        public static StatBaseValues CreateDefault(
            float health = 100f,
            float moveSpeed = 5f)
        {
            return new StatBaseValues
            {
                Health = health,
                MoveSpeed = moveSpeed,
                Damage = 1f,
                AttackSpeed = 1f,
                Cooldown = 1f,
                Range = 1f,
                Duration = 1f,
                ProjectileCount = 1f,
                Pierce = 0f,
                CriticalChance = 0f,
                Armor = 0f,
                PickupRange = 1f,
                Luck = 0f,
                Regeneration = 0f
            };
        }
    }

    /// <summary>
    /// One stable, source-attributed statistic modifier.
    /// </summary>
    public readonly struct Modifier
    {
        /// <summary>Initializes a modifier.</summary>
        public Modifier(
            ContentId sourceId,
            StatId statId,
            ModifierOperation operation,
            float value,
            int priority,
            ContentId stackingGroup,
            float duration)
        {
            if (!sourceId.IsValid)
            {
                throw new ArgumentException("Modifier source ID must be valid.", nameof(sourceId));
            }

            if (!statId.IsValid)
            {
                throw new ArgumentException("Modifier statistic ID must be valid.", nameof(statId));
            }

            if (operation < ModifierOperation.AddFlat ||
                operation > ModifierOperation.Override)
            {
                throw new ArgumentOutOfRangeException(nameof(operation));
            }

            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            if (float.IsNaN(duration) ||
                duration < 0f ||
                float.IsNegativeInfinity(duration))
            {
                throw new ArgumentOutOfRangeException(nameof(duration));
            }

            SourceId = sourceId;
            StatId = statId;
            Operation = operation;
            Value = value;
            Priority = priority;
            StackingGroup = stackingGroup;
            Duration = duration;
        }

        /// <summary>Gets the stable source content ID.</summary>
        public ContentId SourceId { get; }

        /// <summary>Gets the stable target statistic ID.</summary>
        public StatId StatId { get; }

        /// <summary>Gets the calculation operation.</summary>
        public ModifierOperation Operation { get; }

        /// <summary>Gets the operation value.</summary>
        public float Value { get; }

        /// <summary>Gets deterministic priority within an operation stage.</summary>
        public int Priority { get; }

        /// <summary>Gets the optional mutually-exclusive stacking group.</summary>
        public ContentId StackingGroup { get; }

        /// <summary>Gets remaining duration in seconds; positive infinity is permanent.</summary>
        public float Duration { get; }
    }

    /// <summary>
    /// Identifies one modifier entry so its owner can remove it without source-wide scans.
    /// </summary>
    public readonly struct ModifierHandle : IEquatable<ModifierHandle>
    {
        internal ModifierHandle(long value)
        {
            Value = value;
        }

        /// <summary>Gets the monotonically assigned handle value.</summary>
        public long Value { get; }

        /// <summary>Gets whether this handle identifies an entry.</summary>
        public bool IsValid => Value > 0;

        /// <inheritdoc />
        public bool Equals(ModifierHandle other)
        {
            return Value == other.Value;
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            return obj is ModifierHandle other && Equals(other);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }
    }

    /// <summary>
    /// Reusable, allocation-free-on-read collection for deterministic modifiers.
    /// </summary>
    public sealed class ModifierCollection
    {
        private struct Entry
        {
            public ModifierHandle Handle;
            public Modifier Modifier;
            public StatIndex StatIndex;
            public int StackingGroupKey;
            public float RemainingDuration;
            public long Sequence;
        }

        private readonly StatCatalog catalog;
        private readonly Dictionary<ContentId, int> stackingGroupKeys;
        private Entry[] entries;
        private long nextSequence = 1;
        private int nextStackingGroupKey = 1;

        /// <summary>Initializes a reusable modifier collection.</summary>
        public ModifierCollection(
            StatCatalog catalog = null,
            int initialCapacity = 4)
        {
            if (initialCapacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(initialCapacity));
            }

            this.catalog = catalog ?? StatCatalog.Default;
            entries = new Entry[initialCapacity];
            stackingGroupKeys = new Dictionary<ContentId, int>(initialCapacity);
        }

        /// <summary>Gets the number of retained modifier entries.</summary>
        public int Count { get; private set; }

        /// <summary>Gets a revision that changes whenever calculated values may change.</summary>
        public int Revision { get; private set; }

        /// <summary>Adds a modifier after resolving its stable StatId once.</summary>
        public bool TryAdd(in Modifier modifier, out ModifierHandle handle)
        {
            if (!catalog.TryGetIndex(modifier.StatId, out var statIndex))
            {
                handle = default;
                return false;
            }

            if (Count == entries.Length)
            {
                Array.Resize(ref entries, entries.Length * 2);
            }

            var stackingGroupKey = ResolveStackingGroupKey(modifier.StackingGroup);
            var sequence = nextSequence++;
            handle = new ModifierHandle(sequence);
            entries[Count++] = new Entry
            {
                Handle = handle,
                Modifier = modifier,
                StatIndex = statIndex,
                StackingGroupKey = stackingGroupKey,
                RemainingDuration = modifier.Duration,
                Sequence = sequence
            };
            Revision++;
            return true;
        }

        /// <summary>
        /// Removes every modifier while retaining the allocated entry storage for reuse.
        /// Previously issued handles remain stale and cannot identify entries added later.
        /// </summary>
        public void Clear()
        {
            Array.Clear(entries, 0, Count);
            Count = 0;
            stackingGroupKeys.Clear();
            nextStackingGroupKey = 1;
            Revision++;
        }

        /// <summary>Prepares this collection for a new owner without replacing its arrays.</summary>
        public void Reset()
        {
            Clear();
        }

        /// <summary>Removes one modifier by its owner handle.</summary>
        public bool Remove(ModifierHandle handle)
        {
            if (!handle.IsValid)
            {
                return false;
            }

            for (var index = 0; index < Count; index++)
            {
                if (entries[index].Handle.Equals(handle))
                {
                    RemoveAt(index);
                    return true;
                }
            }

            return false;
        }

        /// <summary>Removes all modifiers attributed to one stable source.</summary>
        public int RemoveBySource(ContentId sourceId)
        {
            var removed = 0;
            var index = 0;
            while (index < Count)
            {
                if (entries[index].Modifier.SourceId == sourceId)
                {
                    RemoveAt(index);
                    removed++;
                }
                else
                {
                    index++;
                }
            }

            return removed;
        }

        /// <summary>Advances finite durations and removes expired entries.</summary>
        public void Tick(float deltaTimeSeconds)
        {
            if (float.IsNaN(deltaTimeSeconds) ||
                float.IsInfinity(deltaTimeSeconds) ||
                deltaTimeSeconds < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(deltaTimeSeconds));
            }

            var index = 0;
            while (index < Count)
            {
                if (float.IsPositiveInfinity(entries[index].RemainingDuration))
                {
                    index++;
                    continue;
                }

                entries[index].RemainingDuration -= deltaTimeSeconds;
                if (entries[index].RemainingDuration <= 0f)
                {
                    RemoveAt(index);
                }
                else
                {
                    index++;
                }
            }
        }

        /// <summary>
        /// Calculates one value in Base → AddFlat → AddPercent → Multiply → Clamp → Override order.
        /// </summary>
        public float Evaluate(StatIndex statIndex, float baseValue)
        {
            if (!statIndex.IsValid || statIndex.Value >= catalog.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(statIndex));
            }

            var value = baseValue;
            value += SumOperation(statIndex, ModifierOperation.AddFlat);
            value *= 1f + SumOperation(statIndex, ModifierOperation.AddPercent);
            value = ApplyOrdered(statIndex, value, ModifierOperation.Multiply, default);
            value = ApplyOrdered(
                statIndex,
                value,
                ModifierOperation.ClampMinimum,
                ModifierOperation.ClampMaximum);
            value = ApplyOrdered(statIndex, value, ModifierOperation.Override, default);
            return value;
        }

        private float SumOperation(StatIndex statIndex, ModifierOperation operation)
        {
            var sum = 0f;
            var hasPrevious = false;
            var previousPriority = 0;
            var previousSequence = 0L;
            while (TryFindNext(
                       statIndex,
                       operation,
                       default,
                       hasPrevious,
                       previousPriority,
                       previousSequence,
                       out var entryIndex))
            {
                ref var entry = ref entries[entryIndex];
                sum += entry.Modifier.Value;
                previousPriority = entry.Modifier.Priority;
                previousSequence = entry.Sequence;
                hasPrevious = true;
            }

            return sum;
        }

        private float ApplyOrdered(
            StatIndex statIndex,
            float value,
            ModifierOperation first,
            ModifierOperation second)
        {
            var hasPrevious = false;
            var previousPriority = 0;
            var previousSequence = 0L;
            while (TryFindNext(
                       statIndex,
                       first,
                       second,
                       hasPrevious,
                       previousPriority,
                       previousSequence,
                       out var entryIndex))
            {
                ref var entry = ref entries[entryIndex];
                switch (entry.Modifier.Operation)
                {
                    case ModifierOperation.Multiply:
                        value *= entry.Modifier.Value;
                        break;
                    case ModifierOperation.ClampMinimum:
                        if (value < entry.Modifier.Value)
                        {
                            value = entry.Modifier.Value;
                        }

                        break;
                    case ModifierOperation.ClampMaximum:
                        if (value > entry.Modifier.Value)
                        {
                            value = entry.Modifier.Value;
                        }

                        break;
                    case ModifierOperation.Override:
                        value = entry.Modifier.Value;
                        break;
                }

                previousPriority = entry.Modifier.Priority;
                previousSequence = entry.Sequence;
                hasPrevious = true;
            }

            return value;
        }

        private bool TryFindNext(
            StatIndex statIndex,
            ModifierOperation first,
            ModifierOperation second,
            bool hasPrevious,
            int previousPriority,
            long previousSequence,
            out int foundIndex)
        {
            foundIndex = -1;
            var foundPriority = int.MaxValue;
            var foundSequence = long.MaxValue;
            for (var index = 0; index < Count; index++)
            {
                ref var entry = ref entries[index];
                if (entry.StatIndex != statIndex ||
                    (entry.Modifier.Operation != first &&
                     (second == default || entry.Modifier.Operation != second)) ||
                    IsSuppressedByStackingGroup(index))
                {
                    continue;
                }

                var priority = entry.Modifier.Priority;
                var sequence = entry.Sequence;
                if (hasPrevious &&
                    (priority < previousPriority ||
                     (priority == previousPriority && sequence <= previousSequence)))
                {
                    continue;
                }

                if (priority < foundPriority ||
                    (priority == foundPriority && sequence < foundSequence))
                {
                    foundIndex = index;
                    foundPriority = priority;
                    foundSequence = sequence;
                }
            }

            return foundIndex >= 0;
        }

        private bool IsSuppressedByStackingGroup(int candidateIndex)
        {
            ref var candidate = ref entries[candidateIndex];
            var groupKey = candidate.StackingGroupKey;
            if (groupKey == 0)
            {
                return false;
            }

            for (var index = 0; index < Count; index++)
            {
                if (index == candidateIndex)
                {
                    continue;
                }

                ref var other = ref entries[index];
                if (other.StatIndex != candidate.StatIndex ||
                    other.Modifier.Operation != candidate.Modifier.Operation ||
                    other.StackingGroupKey != groupKey)
                {
                    continue;
                }

                if (other.Modifier.Priority > candidate.Modifier.Priority ||
                    (other.Modifier.Priority == candidate.Modifier.Priority &&
                     other.Sequence > candidate.Sequence))
                {
                    return true;
                }
            }

            return false;
        }

        private int ResolveStackingGroupKey(ContentId stackingGroup)
        {
            if (!stackingGroup.IsValid)
            {
                return 0;
            }

            if (stackingGroupKeys.TryGetValue(stackingGroup, out var key))
            {
                return key;
            }

            key = nextStackingGroupKey;
            nextStackingGroupKey = checked(nextStackingGroupKey + 1);
            stackingGroupKeys.Add(stackingGroup, key);
            return key;
        }

        private void RemoveAt(int index)
        {
            var lastIndex = Count - 1;
            if (index != lastIndex)
            {
                entries[index] = entries[lastIndex];
            }

            entries[lastIndex] = default;
            Count--;
            Revision++;
        }
    }

    internal sealed class ActorStatBlock
    {
        private readonly StatCatalog catalog;
        private readonly float[] baseValues;
        private readonly float[] cachedValues;
        private readonly int[] cachedModifierRevisions;
        private readonly int[] baseRevisions;
        private readonly int[] cachedBaseRevisions;

        public ActorStatBlock(
            in StatBaseValues values,
            StatCatalog catalog = null)
        {
            this.catalog = catalog ?? StatCatalog.Default;
            baseValues = new float[this.catalog.Count];
            cachedValues = new float[this.catalog.Count];
            cachedModifierRevisions = new int[this.catalog.Count];
            baseRevisions = new int[this.catalog.Count];
            cachedBaseRevisions = new int[this.catalog.Count];
            for (var index = 0; index < this.catalog.Count; index++)
            {
                cachedModifierRevisions[index] = -1;
                cachedBaseRevisions[index] = -1;
            }

            Modifiers = new ModifierCollection(this.catalog);
            WriteBaseValues(values);
        }

        public ModifierCollection Modifiers { get; }

        public void Reset(in StatBaseValues values)
        {
            Modifiers.Reset();
            Array.Clear(baseValues, 0, baseValues.Length);
            Array.Clear(cachedValues, 0, cachedValues.Length);
            Array.Clear(baseRevisions, 0, baseRevisions.Length);
            WriteBaseValues(values);
            for (var index = 0; index < catalog.Count; index++)
            {
                cachedModifierRevisions[index] = -1;
                cachedBaseRevisions[index] = -1;
            }
        }

        private void WriteBaseValues(in StatBaseValues values)
        {
            baseValues[BuiltInStatIndices.Health.Value] = values.Health;
            baseValues[BuiltInStatIndices.MoveSpeed.Value] = values.MoveSpeed;
            baseValues[BuiltInStatIndices.Damage.Value] = values.Damage;
            baseValues[BuiltInStatIndices.AttackSpeed.Value] = values.AttackSpeed;
            baseValues[BuiltInStatIndices.Cooldown.Value] = values.Cooldown;
            baseValues[BuiltInStatIndices.Range.Value] = values.Range;
            baseValues[BuiltInStatIndices.Duration.Value] = values.Duration;
            baseValues[BuiltInStatIndices.ProjectileCount.Value] = values.ProjectileCount;
            baseValues[BuiltInStatIndices.Pierce.Value] = values.Pierce;
            baseValues[BuiltInStatIndices.CriticalChance.Value] = values.CriticalChance;
            baseValues[BuiltInStatIndices.Armor.Value] = values.Armor;
            baseValues[BuiltInStatIndices.PickupRange.Value] = values.PickupRange;
            baseValues[BuiltInStatIndices.Luck.Value] = values.Luck;
            baseValues[BuiltInStatIndices.Regeneration.Value] = values.Regeneration;
        }

        public float Get(StatIndex index)
        {
            if (!index.IsValid || index.Value >= catalog.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            var valueIndex = index.Value;
            if (cachedModifierRevisions[valueIndex] != Modifiers.Revision ||
                cachedBaseRevisions[valueIndex] != baseRevisions[valueIndex])
            {
                var evaluated = Modifiers.Evaluate(index, baseValues[valueIndex]);
                cachedValues[valueIndex] = catalog.ClampToDomain(index, evaluated);
                cachedModifierRevisions[valueIndex] = Modifiers.Revision;
                cachedBaseRevisions[valueIndex] = baseRevisions[valueIndex];
            }

            return cachedValues[valueIndex];
        }

        public void SetBase(StatIndex index, float value)
        {
            if (!index.IsValid || index.Value >= catalog.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            baseValues[index.Value] = value;
            baseRevisions[index.Value]++;
        }
    }
}
