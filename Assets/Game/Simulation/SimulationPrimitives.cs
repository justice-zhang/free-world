using System;
using System.Numerics;

namespace Game.Simulation
{
    /// <summary>
    /// Identifies one live slot in a simulation store.
    /// </summary>
    public readonly struct EntityHandle : IEquatable<EntityHandle>
    {
        /// <summary>
        /// Initializes a handle from a slot index and generation.
        /// </summary>
        public EntityHandle(int index, ushort generation)
        {
            Index = index;
            Generation = generation;
        }

        /// <summary>Gets the stable slot index while this handle is alive.</summary>
        public int Index { get; }

        /// <summary>Gets the generation used to reject stale handles.</summary>
        public ushort Generation { get; }

        /// <summary>Gets whether this handle can identify a generated store slot.</summary>
        public bool IsValid => Index >= 0 && Generation != 0;

        /// <inheritdoc />
        public bool Equals(EntityHandle other)
        {
            return Index == other.Index && Generation == other.Generation;
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            return obj is EntityHandle other && Equals(other);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                return (Index * 397) ^ Generation;
            }
        }

        /// <summary>Compares two handles.</summary>
        public static bool operator ==(EntityHandle left, EntityHandle right)
        {
            return left.Equals(right);
        }

        /// <summary>Compares two handles.</summary>
        public static bool operator !=(EntityHandle left, EntityHandle right)
        {
            return !left.Equals(right);
        }
    }

    /// <summary>
    /// Identifies the dedicated store that owns an entity handle.
    /// </summary>
    public enum EntityKind : byte
    {
        /// <summary>An actor entity.</summary>
        Actor = 1,

        /// <summary>A projectile entity.</summary>
        Projectile = 2,

        /// <summary>An area entity.</summary>
        Area = 3,

        /// <summary>A pickup entity.</summary>
        Pickup = 4
    }

    /// <summary>
    /// Minimal presentation-neutral state flags available in M2.
    /// </summary>
    [Flags]
    public enum SimulationStateFlags : uint
    {
        /// <summary>No state flags.</summary>
        None = 0,

        /// <summary>The entity is active.</summary>
        Active = 1U << 0,

        /// <summary>The entity moved during the current tick.</summary>
        Moving = 1U << 1,

        /// <summary>The entity should be hidden by a future view.</summary>
        Hidden = 1U << 2
    }

    /// <summary>
    /// Contains the common kinematic and lifetime columns stored by each M2 store.
    /// </summary>
    public struct SimulationEntityState
    {
        /// <summary>Gets or sets the world-space position.</summary>
        public Vector2 Position;

        /// <summary>Gets or sets velocity in world units per second.</summary>
        public Vector2 Velocity;

        /// <summary>Gets or sets clockwise-neutral facing in radians.</summary>
        public float FacingRadians;

        /// <summary>Gets or sets presentation-neutral state flags.</summary>
        public SimulationStateFlags StateFlags;

        /// <summary>
        /// Gets or sets remaining lifetime in seconds. Positive infinity means no expiry.
        /// </summary>
        public float RemainingLifetimeSeconds;

        /// <summary>
        /// Creates an active entity state with an optional finite lifetime.
        /// </summary>
        public static SimulationEntityState Create(
            Vector2 position,
            Vector2 velocity,
            float facingRadians = 0f,
            float lifetimeSeconds = float.PositiveInfinity,
            SimulationStateFlags stateFlags = SimulationStateFlags.Active)
        {
            if (float.IsNaN(lifetimeSeconds) || lifetimeSeconds < 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(lifetimeSeconds),
                    "Lifetime must be non-negative or positive infinity.");
            }

            return new SimulationEntityState
            {
                Position = position,
                Velocity = velocity,
                FacingRadians = facingRadians,
                StateFlags = stateFlags,
                RemainingLifetimeSeconds = lifetimeSeconds
            };
        }
    }

    /// <summary>
    /// Identifies an entity across all dedicated stores.
    /// </summary>
    public readonly struct SpatialEntity : IEquatable<SpatialEntity>
    {
        /// <summary>Initializes a store-qualified entity identifier.</summary>
        public SpatialEntity(EntityKind kind, EntityHandle handle)
        {
            Kind = kind;
            Handle = handle;
        }

        /// <summary>Gets the owning store kind.</summary>
        public EntityKind Kind { get; }

        /// <summary>Gets the store-local handle.</summary>
        public EntityHandle Handle { get; }

        /// <summary>Gets whether both the store kind and handle are valid.</summary>
        public bool IsValid =>
            Kind >= EntityKind.Actor &&
            Kind <= EntityKind.Pickup &&
            Handle.IsValid;

        /// <inheritdoc />
        public bool Equals(SpatialEntity other)
        {
            return Kind == other.Kind && Handle.Equals(other.Handle);
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            return obj is SpatialEntity other && Equals(other);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                return ((int)Kind * 397) ^ Handle.GetHashCode();
            }
        }

        /// <summary>Compares two store-qualified entity identifiers.</summary>
        public static bool operator ==(SpatialEntity left, SpatialEntity right)
        {
            return left.Equals(right);
        }

        /// <summary>Compares two store-qualified entity identifiers.</summary>
        public static bool operator !=(SpatialEntity left, SpatialEntity right)
        {
            return !left.Equals(right);
        }
    }

    /// <summary>
    /// Collects cumulative M2 simulation diagnostics without affecting simulation truth.
    /// </summary>
    public sealed class SimulationDiagnostics
    {
        /// <summary>Gets the number of currently active entities.</summary>
        public int ActiveEntities { get; private set; }

        /// <summary>Gets the cumulative number of created entities.</summary>
        public long CreatedEntities { get; private set; }

        /// <summary>Gets the cumulative number of removed entities.</summary>
        public long RemovedEntities { get; private set; }

        /// <summary>Gets the number of rejected stale or invalid handle accesses.</summary>
        public long InvalidHandleAccesses { get; private set; }

        /// <summary>Gets the duration of the most recently completed tick.</summary>
        public double LastTickMilliseconds { get; private set; }

        /// <summary>Gets cumulative measured tick time.</summary>
        public double TotalTickMilliseconds { get; private set; }

        /// <summary>Gets the number of timed ticks.</summary>
        public long CompletedTicks { get; private set; }

        /// <summary>Gets the number of trigger-chain requests rejected beyond the depth limit.</summary>
        public long TruncatedProcChains { get; private set; }

        /// <summary>Gets the number of damage packets rejected for invalid or inactive targets.</summary>
        public long RejectedDamagePackets { get; private set; }

        /// <summary>Gets the number of status applications rejected by validation or immunity.</summary>
        public long RejectedStatusApplications { get; private set; }

        internal void RecordCreated()
        {
            ActiveEntities++;
            CreatedEntities++;
        }

        internal void RecordRemoved()
        {
            ActiveEntities--;
            RemovedEntities++;
        }

        internal void RecordInvalidHandleAccess()
        {
            InvalidHandleAccesses++;
        }

        internal void RecordTick(double elapsedMilliseconds)
        {
            LastTickMilliseconds = elapsedMilliseconds;
            TotalTickMilliseconds += elapsedMilliseconds;
            CompletedTicks++;
        }

        internal void RecordTruncatedProcChain()
        {
            TruncatedProcChains++;
        }

        internal void RecordRejectedDamage()
        {
            RejectedDamagePackets++;
        }

        internal void RecordRejectedStatus()
        {
            RejectedStatusApplications++;
        }
    }

    /// <summary>
    /// Deterministic, value-type pseudo-random stream for simulation code.
    /// </summary>
    /// <remarks>
    /// Derived streams depend on the root seed and stream ID, not on parent call order.
    /// Callers must retain or pass the stream by reference; copying it intentionally forks
    /// the exact current sequence.
    /// </remarks>
    public struct RandomStream
    {
        private const ulong Increment = 0x9E3779B97F4A7C15UL;
        private readonly ulong rootSeed;
        private ulong state;
        private ulong calls;

        /// <summary>Initializes a deterministic stream from a root seed.</summary>
        public RandomStream(ulong seed)
        {
            rootSeed = seed;
            state = Mix(seed ^ 0xD1B54A32D192ED03UL);
            calls = 0UL;
        }

        /// <summary>Gets the root seed used for deterministic derivation.</summary>
        public ulong RootSeed => rootSeed;

        /// <summary>Gets the number of raw values consumed by this stream.</summary>
        public ulong Calls => calls;

        /// <summary>Returns the next uniformly distributed 32-bit value.</summary>
        public uint NextUInt()
        {
            state += Increment;
            calls++;
            return (uint)(Mix(state) >> 32);
        }

        /// <summary>Returns a value in the half-open interval [0, 1).</summary>
        public float NextFloat()
        {
            return (NextUInt() >> 8) * (1f / 16777216f);
        }

        /// <summary>Returns a value in the half-open interval [minimum, maximum).</summary>
        public float NextFloat(float minimum, float maximum)
        {
            if (!(maximum > minimum))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maximum),
                    "Maximum must be greater than minimum.");
            }

            return minimum + ((maximum - minimum) * NextFloat());
        }

        /// <summary>Returns an unbiased integer in the half-open interval [0, maximum).</summary>
        public int NextInt(int maximum)
        {
            if (maximum <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maximum));
            }

            var unsignedMaximum = (uint)maximum;
            var threshold = unchecked((uint)(0U - unsignedMaximum)) % unsignedMaximum;
            uint value;
            do
            {
                value = NextUInt();
            }
            while (value < threshold);

            return (int)(value % unsignedMaximum);
        }

        /// <summary>
        /// Creates a deterministic child stream independent of the parent's current state.
        /// </summary>
        public RandomStream Derive(ulong streamId)
        {
            return new RandomStream(Mix(rootSeed ^ Mix(streamId + Increment)));
        }

        private static ulong Mix(ulong value)
        {
            value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
            value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
            return value ^ (value >> 31);
        }
    }
}
