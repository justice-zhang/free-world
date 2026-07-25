using System;
using System.Numerics;

namespace Game.Simulation
{
    /// <summary>
    /// Structural command kinds supported by the M2 simulation kernel.
    /// </summary>
    public enum SimulationCommandType : byte
    {
        /// <summary>Create an entity in a dedicated store.</summary>
        Create = 1,

        /// <summary>Remove an entity from a dedicated store.</summary>
        Remove = 2
    }

    /// <summary>
    /// One buffered structural mutation applied by the cleanup system.
    /// </summary>
    public readonly struct SimulationCommand
    {
        internal SimulationCommand(
            SimulationCommandType type,
            EntityKind entityKind,
            EntityHandle target,
            SimulationEntityState initialState)
        {
            Type = type;
            EntityKind = entityKind;
            Target = target;
            InitialState = initialState;
        }

        /// <summary>Gets the structural operation.</summary>
        public SimulationCommandType Type { get; }

        /// <summary>Gets the affected dedicated store.</summary>
        public EntityKind EntityKind { get; }

        /// <summary>Gets the target handle for remove commands.</summary>
        public EntityHandle Target { get; }

        /// <summary>Gets initial state for create commands.</summary>
        public SimulationEntityState InitialState { get; }
    }

    /// <summary>
    /// Reusable FIFO buffer that defers structural mutations until cleanup.
    /// </summary>
    public sealed class SimulationCommandBuffer
    {
        private SimulationCommand[] commands;

        /// <summary>Initializes a command buffer.</summary>
        public SimulationCommandBuffer(int initialCapacity = 32)
        {
            if (initialCapacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(initialCapacity));
            }

            commands = new SimulationCommand[initialCapacity];
        }

        /// <summary>Gets the number of queued commands.</summary>
        public int Count { get; private set; }

        /// <summary>Queues one entity creation.</summary>
        public void Create(EntityKind kind, in SimulationEntityState initialState)
        {
            Add(new SimulationCommand(
                SimulationCommandType.Create,
                kind,
                default,
                initialState));
        }

        /// <summary>Queues one entity removal.</summary>
        public void Remove(EntityKind kind, EntityHandle target)
        {
            Add(new SimulationCommand(
                SimulationCommandType.Remove,
                kind,
                target,
                default));
        }

        /// <summary>Gets one queued command in FIFO order.</summary>
        public SimulationCommand GetAt(int index)
        {
            if (index < 0 || index >= Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return commands[index];
        }

        internal void Clear()
        {
            Array.Clear(commands, 0, Count);
            Count = 0;
        }

        private void Add(in SimulationCommand command)
        {
            if (Count == commands.Length)
            {
                Array.Resize(ref commands, commands.Length * 2);
            }

            commands[Count++] = command;
        }
    }

    /// <summary>
    /// Event kinds emitted by M2 structural processing.
    /// </summary>
    public enum SimulationEventType : byte
    {
        /// <summary>An entity became live.</summary>
        Created = 1,

        /// <summary>An entity was removed.</summary>
        Removed = 2
    }

    /// <summary>
    /// One presentation-neutral event emitted by a completed simulation tick.
    /// </summary>
    public readonly struct SimulationEvent
    {
        /// <summary>Initializes a simulation event.</summary>
        public SimulationEvent(
            SimulationEventType type,
            EntityKind entityKind,
            EntityHandle handle,
            Vector2 position,
            long tick)
        {
            Type = type;
            EntityKind = entityKind;
            Handle = handle;
            Position = position;
            Tick = tick;
        }

        /// <summary>Gets the event kind.</summary>
        public SimulationEventType Type { get; }

        /// <summary>Gets the dedicated store kind.</summary>
        public EntityKind EntityKind { get; }

        /// <summary>Gets the affected entity handle.</summary>
        public EntityHandle Handle { get; }

        /// <summary>Gets the entity position at event emission.</summary>
        public Vector2 Position { get; }

        /// <summary>Gets the fixed tick associated with this event.</summary>
        public long Tick { get; }
    }

    /// <summary>
    /// Reusable event buffer retained until the next runner batch starts.
    /// </summary>
    public sealed class SimulationEventBuffer
    {
        private SimulationEvent[] events;

        /// <summary>Initializes an event buffer.</summary>
        public SimulationEventBuffer(int initialCapacity = 32)
        {
            if (initialCapacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(initialCapacity));
            }

            events = new SimulationEvent[initialCapacity];
        }

        /// <summary>Gets the number of events emitted by the latest runner batch.</summary>
        public int Count { get; private set; }

        /// <summary>Gets one event in emission order.</summary>
        public SimulationEvent GetAt(int index)
        {
            if (index < 0 || index >= Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return events[index];
        }

        internal void BeginBatch()
        {
            Array.Clear(events, 0, Count);
            Count = 0;
        }

        internal void Add(in SimulationEvent simulationEvent)
        {
            if (Count == events.Length)
            {
                Array.Resize(ref events, events.Length * 2);
            }

            events[Count++] = simulationEvent;
        }
    }
}
