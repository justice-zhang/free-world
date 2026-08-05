using System;
using System.Numerics;
using Game.Core;

namespace Game.Simulation
{
    internal sealed class StructBuffer<T>
        where T : struct
    {
        private T[] items;

        public StructBuffer(int initialCapacity)
        {
            items = new T[Math.Max(1, initialCapacity)];
        }

        public int Count { get; private set; }

        public T GetAt(int index)
        {
            if (index < 0 || index >= Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return items[index];
        }

        public void Add(in T item)
        {
            if (Count == items.Length)
            {
                Array.Resize(ref items, items.Length * 2);
            }

            items[Count++] = item;
        }

        public void Append(StructBuffer<T> source)
        {
            for (var index = 0; index < source.Count; index++)
            {
                var item = source.items[index];
                Add(item);
            }
        }

        public void Clear()
        {
            Array.Clear(items, 0, Count);
            Count = 0;
        }
    }

    /// <summary>FIFO damage requests consumed only by DamageResolutionSystem.</summary>
    public sealed class DamageRequestBuffer
    {
        private readonly StructBuffer<DamagePacket> packets;

        internal DamageRequestBuffer(int initialCapacity)
        {
            packets = new StructBuffer<DamagePacket>(initialCapacity);
        }

        /// <summary>Gets pending request count for diagnostics.</summary>
        public int Count => packets.Count;

        internal DamagePacket GetAt(int index)
        {
            return packets.GetAt(index);
        }

        internal void Add(in DamagePacket packet)
        {
            packets.Add(packet);
        }

        internal void Clear()
        {
            packets.Clear();
        }
    }

    /// <summary>FIFO status applications consumed only by StatusTickSystem.</summary>
    public sealed class StatusApplicationBuffer
    {
        private readonly StructBuffer<StatusApplicationRequest> requests;

        internal StatusApplicationBuffer(int initialCapacity)
        {
            requests = new StructBuffer<StatusApplicationRequest>(initialCapacity);
        }

        /// <summary>Gets pending application count.</summary>
        public int Count => requests.Count;

        internal StatusApplicationRequest GetAt(int index)
        {
            return requests.GetAt(index);
        }

        internal void Add(in StatusApplicationRequest request)
        {
            requests.Add(request);
        }

        internal void Clear()
        {
            requests.Clear();
        }
    }

    /// <summary>FIFO status dispels consumed only by StatusTickSystem.</summary>
    public sealed class StatusDispelBuffer
    {
        private readonly StructBuffer<StatusDispelRequest> requests;

        internal StatusDispelBuffer(int initialCapacity)
        {
            requests = new StructBuffer<StatusDispelRequest>(initialCapacity);
        }

        /// <summary>Gets pending dispel count.</summary>
        public int Count => requests.Count;

        internal StatusDispelRequest GetAt(int index)
        {
            return requests.GetAt(index);
        }

        internal void Add(in StatusDispelRequest request)
        {
            requests.Add(request);
        }

        internal void Clear()
        {
            requests.Clear();
        }
    }

    internal readonly struct DeathRequest
    {
        public DeathRequest(
            SpatialEntity target,
            SpatialEntity source,
            ContentId sourceContentId,
            Vector2 position,
            int procDepth)
        {
            Target = target;
            Source = source;
            SourceContentId = sourceContentId;
            Position = position;
            ProcDepth = procDepth;
        }

        public SpatialEntity Target { get; }

        public SpatialEntity Source { get; }

        public ContentId SourceContentId { get; }

        public Vector2 Position { get; }

        public int ProcDepth { get; }
    }

    internal sealed class DeathRequestBuffer
    {
        private readonly StructBuffer<DeathRequest> requests;

        public DeathRequestBuffer(int initialCapacity)
        {
            requests = new StructBuffer<DeathRequest>(initialCapacity);
        }

        public int Count => requests.Count;

        public DeathRequest GetAt(int index)
        {
            return requests.GetAt(index);
        }

        public void Add(in DeathRequest request)
        {
            requests.Add(request);
        }

        public void Clear()
        {
            requests.Clear();
        }
    }

    /// <summary>Outcome represented by a successful StatusApplied event.</summary>
    public enum StatusApplicationOutcome : byte
    {
        /// <summary>A new aggregate or independent instance was added.</summary>
        Added = 1,

        /// <summary>An existing instance duration and definition-owned behavior were refreshed.</summary>
        Refreshed = 2,

        /// <summary>An aggregate status gained one stack.</summary>
        StackAdded = 3,

        /// <summary>A stronger application replaced the previous instance.</summary>
        Replaced = 4
    }

    /// <summary>One damage result emitted during a fixed tick.</summary>
    public readonly struct DamageApplied
    {
        internal DamageApplied(in DamageContext context, long tick)
        {
            Context = context;
            Tick = tick;
        }

        /// <summary>Gets the complete damage resolution context.</summary>
        public DamageContext Context { get; }

        /// <summary>Gets the fixed tick.</summary>
        public long Tick { get; }
    }

    /// <summary>One complete damage-policy outcome, including rejected and barrier-only packets.</summary>
    public readonly struct DamageResolved
    {
        internal DamageResolved(
            in DamagePacket packet,
            float requested,
            float mitigated,
            float barrierAbsorbed,
            float shieldDamage,
            float healthDamage,
            DamageResolutionOutcome outcome,
            long tick)
        {
            Packet = packet;
            Requested = requested;
            Mitigated = mitigated;
            BarrierAbsorbed = barrierAbsorbed;
            ShieldDamage = shieldDamage;
            HealthDamage = healthDamage;
            Outcome = outcome;
            Tick = tick;
        }

        public DamagePacket Packet { get; }
        public SpatialEntity Source => Packet.Source;
        public SpatialEntity Target => Packet.Target;
        public ContentId SourceContentId => Packet.SourceContentId;
        public DamageChannelId ChannelId => Packet.ChannelId;
        public float Requested { get; }
        public float Mitigated { get; }
        public float BarrierAbsorbed { get; }
        public float ShieldDamage { get; }
        public float HealthDamage { get; }
        public DamageResolutionOutcome Outcome { get; }
        public long Tick { get; }
    }

    /// <summary>One successful status application emitted during a fixed tick.</summary>
    public readonly struct StatusApplied
    {
        internal StatusApplied(
            SpatialEntity source,
            SpatialEntity target,
            ContentId statusId,
            RuntimeContentIndex statusIndex,
            StatusApplicationOutcome outcome,
            int stacks,
            float strength,
            float remainingDuration,
            long tick)
        {
            Source = source;
            Target = target;
            StatusId = statusId;
            StatusIndex = statusIndex;
            Outcome = outcome;
            Stacks = stacks;
            Strength = strength;
            RemainingDuration = remainingDuration;
            Tick = tick;
        }

        /// <summary>Gets the source entity.</summary>
        public SpatialEntity Source { get; }

        /// <summary>Gets the target actor.</summary>
        public SpatialEntity Target { get; }

        /// <summary>Gets the stable status ID.</summary>
        public ContentId StatusId { get; }

        /// <summary>Gets the runtime status index.</summary>
        public RuntimeContentIndex StatusIndex { get; }

        /// <summary>Gets the stacking outcome.</summary>
        public StatusApplicationOutcome Outcome { get; }

        /// <summary>Gets current stacks for the affected instance.</summary>
        public int Stacks { get; }

        /// <summary>Gets current strength.</summary>
        public float Strength { get; }

        /// <summary>Gets refreshed remaining duration.</summary>
        public float RemainingDuration { get; }

        /// <summary>Gets the fixed tick.</summary>
        public long Tick { get; }
    }

    /// <summary>One finalized actor death emitted exactly once.</summary>
    public readonly struct EntityDied
    {
        internal EntityDied(
            SpatialEntity target,
            SpatialEntity source,
            ContentId sourceContentId,
            Vector2 position,
            int procDepth,
            long tick)
        {
            Target = target;
            Source = source;
            SourceContentId = sourceContentId;
            Position = position;
            ProcDepth = procDepth;
            Tick = tick;
        }

        /// <summary>Gets the dead actor.</summary>
        public SpatialEntity Target { get; }

        /// <summary>Gets the killing source entity.</summary>
        public SpatialEntity Source { get; }

        /// <summary>Gets the stable killing content ID.</summary>
        public ContentId SourceContentId { get; }

        /// <summary>Gets the death position.</summary>
        public Vector2 Position { get; }

        /// <summary>Gets the killing proc depth.</summary>
        public int ProcDepth { get; }

        /// <summary>Gets the fixed tick.</summary>
        public long Tick { get; }
    }

    /// <summary>One shield current-value or capacity change emitted during a fixed tick.</summary>
    public readonly struct ShieldChanged
    {
        internal ShieldChanged(
            SpatialEntity target,
            ContentId sourceContentId,
            float previousValue,
            float currentValue,
            float previousMaximum,
            float currentMaximum,
            long tick)
        {
            Target = target;
            SourceContentId = sourceContentId;
            PreviousValue = previousValue;
            CurrentValue = currentValue;
            PreviousMaximum = previousMaximum;
            CurrentMaximum = currentMaximum;
            Tick = tick;
        }

        /// <summary>Gets the affected actor.</summary>
        public SpatialEntity Target { get; }

        /// <summary>Gets the stable cause ID.</summary>
        public ContentId SourceContentId { get; }

        /// <summary>Gets shield before the change.</summary>
        public float PreviousValue { get; }

        /// <summary>Gets shield after the change.</summary>
        public float CurrentValue { get; }

        /// <summary>Gets shield capacity before the change.</summary>
        public float PreviousMaximum { get; }

        /// <summary>Gets shield capacity after the change.</summary>
        public float CurrentMaximum { get; }

        /// <summary>Gets signed shield delta.</summary>
        public float Delta => CurrentValue - PreviousValue;

        /// <summary>Gets signed shield-capacity delta.</summary>
        public float MaximumDelta => CurrentMaximum - PreviousMaximum;

        /// <summary>Gets the fixed tick.</summary>
        public long Tick { get; }
    }

    /// <summary>
    /// Two-level M3 event storage: per-tick pending events and a public runner batch.
    /// </summary>
    public sealed class CombatEventBuffer
    {
        private readonly StructBuffer<DamageApplied> pendingDamage;
        private readonly StructBuffer<DamageResolved> pendingResolvedDamage;
        private readonly StructBuffer<StatusApplied> pendingStatuses;
        private readonly StructBuffer<EntityDied> pendingDeaths;
        private readonly StructBuffer<ShieldChanged> pendingShields;
        private readonly StructBuffer<DamageApplied> damageBatch;
        private readonly StructBuffer<DamageResolved> resolvedDamageBatch;
        private readonly StructBuffer<StatusApplied> statusBatch;
        private readonly StructBuffer<EntityDied> deathBatch;
        private readonly StructBuffer<ShieldChanged> shieldBatch;

        internal CombatEventBuffer(int initialCapacity)
        {
            pendingDamage = new StructBuffer<DamageApplied>(initialCapacity);
            pendingResolvedDamage = new StructBuffer<DamageResolved>(initialCapacity);
            pendingStatuses = new StructBuffer<StatusApplied>(initialCapacity);
            pendingDeaths = new StructBuffer<EntityDied>(initialCapacity);
            pendingShields = new StructBuffer<ShieldChanged>(initialCapacity);
            damageBatch = new StructBuffer<DamageApplied>(initialCapacity);
            resolvedDamageBatch = new StructBuffer<DamageResolved>(initialCapacity);
            statusBatch = new StructBuffer<StatusApplied>(initialCapacity);
            deathBatch = new StructBuffer<EntityDied>(initialCapacity);
            shieldBatch = new StructBuffer<ShieldChanged>(initialCapacity);
        }

        /// <summary>Gets damage events accumulated by the latest runner batch.</summary>
        public int DamageAppliedCount => damageBatch.Count;

        /// <summary>Gets all damage-policy outcomes accumulated by the latest runner batch.</summary>
        public int DamageResolvedCount => resolvedDamageBatch.Count;

        /// <summary>Gets status events accumulated by the latest runner batch.</summary>
        public int StatusAppliedCount => statusBatch.Count;

        /// <summary>Gets death events accumulated by the latest runner batch.</summary>
        public int EntityDiedCount => deathBatch.Count;

        /// <summary>Gets shield events accumulated by the latest runner batch.</summary>
        public int ShieldChangedCount => shieldBatch.Count;

        /// <summary>Gets one damage event.</summary>
        public DamageApplied GetDamageAppliedAt(int index)
        {
            return damageBatch.GetAt(index);
        }

        /// <summary>Gets one complete damage-policy outcome.</summary>
        public DamageResolved GetDamageResolvedAt(int index)
        {
            return resolvedDamageBatch.GetAt(index);
        }

        /// <summary>Gets one status event.</summary>
        public StatusApplied GetStatusAppliedAt(int index)
        {
            return statusBatch.GetAt(index);
        }

        /// <summary>Gets one death event.</summary>
        public EntityDied GetEntityDiedAt(int index)
        {
            return deathBatch.GetAt(index);
        }

        /// <summary>Gets one shield event.</summary>
        public ShieldChanged GetShieldChangedAt(int index)
        {
            return shieldBatch.GetAt(index);
        }

        internal int PendingDamageResolvedCount => pendingResolvedDamage.Count;

        internal DamageResolved GetPendingDamageResolvedAt(int index)
        {
            return pendingResolvedDamage.GetAt(index);
        }

        internal void BeginBatch()
        {
            damageBatch.Clear();
            resolvedDamageBatch.Clear();
            statusBatch.Clear();
            deathBatch.Clear();
            shieldBatch.Clear();
        }

        internal void BeginTick()
        {
            pendingDamage.Clear();
            pendingResolvedDamage.Clear();
            pendingStatuses.Clear();
            pendingDeaths.Clear();
            pendingShields.Clear();
        }

        internal void Add(in DamageApplied simulationEvent)
        {
            pendingDamage.Add(simulationEvent);
        }

        internal void Add(in DamageResolved simulationEvent)
        {
            pendingResolvedDamage.Add(simulationEvent);
        }

        internal void Add(in StatusApplied simulationEvent)
        {
            pendingStatuses.Add(simulationEvent);
        }

        internal void Add(in EntityDied simulationEvent)
        {
            pendingDeaths.Add(simulationEvent);
        }

        internal void Add(in ShieldChanged simulationEvent)
        {
            pendingShields.Add(simulationEvent);
        }

        internal void FlushTick()
        {
            damageBatch.Append(pendingDamage);
            resolvedDamageBatch.Append(pendingResolvedDamage);
            statusBatch.Append(pendingStatuses);
            deathBatch.Append(pendingDeaths);
            shieldBatch.Append(pendingShields);
            BeginTick();
        }
    }
}
