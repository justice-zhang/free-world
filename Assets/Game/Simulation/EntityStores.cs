using System;
using Game.Core;

namespace Game.Simulation
{
    internal sealed class EntityHandlePool
    {
        private ushort[] generations;
        private int[] denseIndexBySlot;
        private int[] freeSlots;
        private int slotCount;
        private int freeCount;

        public EntityHandlePool(int initialCapacity)
        {
            var capacity = Math.Max(1, initialCapacity);
            generations = new ushort[capacity];
            denseIndexBySlot = new int[capacity];
            freeSlots = new int[capacity];
            for (var index = 0; index < capacity; index++)
            {
                denseIndexBySlot[index] = -1;
            }
        }

        public EntityHandle Allocate(int denseIndex)
        {
            int slot;
            if (freeCount > 0)
            {
                slot = freeSlots[--freeCount];
            }
            else
            {
                slot = slotCount;
                EnsureCapacity(slot + 1);
                slotCount++;
                if (generations[slot] == 0)
                {
                    generations[slot] = 1;
                }
            }

            denseIndexBySlot[slot] = denseIndex;
            return new EntityHandle(slot, generations[slot]);
        }

        public bool TryResolve(EntityHandle handle, out int denseIndex)
        {
            if (handle.Index < 0 ||
                handle.Index >= slotCount ||
                handle.Generation == 0 ||
                generations[handle.Index] != handle.Generation)
            {
                denseIndex = -1;
                return false;
            }

            denseIndex = denseIndexBySlot[handle.Index];
            return denseIndex >= 0;
        }

        public EntityHandle GetHandle(int slot)
        {
            return new EntityHandle(slot, generations[slot]);
        }

        public void SetDenseIndex(int slot, int denseIndex)
        {
            denseIndexBySlot[slot] = denseIndex;
        }

        public void Release(EntityHandle handle)
        {
            var slot = handle.Index;
            denseIndexBySlot[slot] = -1;
            var nextGeneration = unchecked((ushort)(generations[slot] + 1));
            generations[slot] = nextGeneration == 0 ? (ushort)1 : nextGeneration;

            if (freeCount == freeSlots.Length)
            {
                Array.Resize(ref freeSlots, freeSlots.Length * 2);
            }

            freeSlots[freeCount++] = slot;
        }

        private void EnsureCapacity(int required)
        {
            if (required <= generations.Length)
            {
                return;
            }

            var previousLength = generations.Length;
            var newCapacity = previousLength * 2;
            while (newCapacity < required)
            {
                newCapacity *= 2;
            }

            Array.Resize(ref generations, newCapacity);
            Array.Resize(ref denseIndexBySlot, newCapacity);
            Array.Resize(ref freeSlots, newCapacity);
            for (var index = previousLength; index < newCapacity; index++)
            {
                denseIndexBySlot[index] = -1;
            }
        }
    }

    internal sealed class DenseBodyStorage
    {
        private readonly SimulationDiagnostics diagnostics;
        private readonly EntityHandlePool handles;
        private SimulationEntityState[] states;
        private int[] slotByDenseIndex;

        public DenseBodyStorage(int initialCapacity, SimulationDiagnostics diagnostics)
        {
            if (initialCapacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(initialCapacity));
            }

            this.diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
            handles = new EntityHandlePool(initialCapacity);
            states = new SimulationEntityState[initialCapacity];
            slotByDenseIndex = new int[initialCapacity];
        }

        public int Count { get; private set; }

        public EntityHandle Create(in SimulationEntityState initialState)
        {
            EnsureDenseCapacity(Count + 1);
            var handle = handles.Allocate(Count);
            states[Count] = initialState;
            slotByDenseIndex[Count] = handle.Index;
            Count++;
            diagnostics.RecordCreated();
            return handle;
        }

        public bool Contains(EntityHandle handle)
        {
            return handles.TryResolve(handle, out _);
        }

        public bool TryRead(EntityHandle handle, out SimulationEntityState state)
        {
            if (!handles.TryResolve(handle, out var denseIndex))
            {
                diagnostics.RecordInvalidHandleAccess();
                state = default;
                return false;
            }

            state = states[denseIndex];
            return true;
        }

        public bool TryWrite(EntityHandle handle, in SimulationEntityState state)
        {
            if (!handles.TryResolve(handle, out var denseIndex))
            {
                diagnostics.RecordInvalidHandleAccess();
                return false;
            }

            states[denseIndex] = state;
            return true;
        }

        public bool Remove(EntityHandle handle)
        {
            if (!handles.TryResolve(handle, out var denseIndex))
            {
                diagnostics.RecordInvalidHandleAccess();
                return false;
            }

            var lastDenseIndex = Count - 1;
            if (denseIndex != lastDenseIndex)
            {
                states[denseIndex] = states[lastDenseIndex];
                var movedSlot = slotByDenseIndex[lastDenseIndex];
                slotByDenseIndex[denseIndex] = movedSlot;
                handles.SetDenseIndex(movedSlot, denseIndex);
            }

            states[lastDenseIndex] = default;
            slotByDenseIndex[lastDenseIndex] = 0;
            Count--;
            handles.Release(handle);
            diagnostics.RecordRemoved();
            return true;
        }

        public EntityHandle GetHandleAt(int denseIndex)
        {
            ValidateDenseIndex(denseIndex);
            return handles.GetHandle(slotByDenseIndex[denseIndex]);
        }

        public SimulationEntityState GetStateAt(int denseIndex)
        {
            ValidateDenseIndex(denseIndex);
            return states[denseIndex];
        }

        public void SetStateAt(int denseIndex, in SimulationEntityState state)
        {
            ValidateDenseIndex(denseIndex);
            states[denseIndex] = state;
        }

        private void EnsureDenseCapacity(int required)
        {
            if (required <= states.Length)
            {
                return;
            }

            var newCapacity = states.Length * 2;
            while (newCapacity < required)
            {
                newCapacity *= 2;
            }

            Array.Resize(ref states, newCapacity);
            Array.Resize(ref slotByDenseIndex, newCapacity);
        }

        private void ValidateDenseIndex(int denseIndex)
        {
            if (denseIndex < 0 || denseIndex >= Count)
            {
                throw new ArgumentOutOfRangeException(nameof(denseIndex));
            }
        }
    }

    /// <summary>
    /// Dense actor store with generation-safe handles and swap-back removal.
    /// </summary>
    public sealed class ActorStore
    {
        private readonly DenseBodyStorage storage;
        private readonly ActorCombatStorage combat;

        /// <summary>Initializes an actor store.</summary>
        public ActorStore(int initialCapacity = 16, SimulationDiagnostics diagnostics = null)
        {
            storage = new DenseBodyStorage(
                initialCapacity,
                diagnostics ?? new SimulationDiagnostics());
            combat = new ActorCombatStorage(initialCapacity);
        }

        /// <summary>Gets the active actor count.</summary>
        public int Count => storage.Count;

        /// <summary>Creates an actor and returns its generation-safe handle.</summary>
        public EntityHandle Create(in SimulationEntityState initialState)
        {
            var combatState = ActorCombatInitialization.CreateDefault();
            return Create(initialState, combatState);
        }

        /// <summary>Creates an actor with explicitly initialized combat state.</summary>
        public EntityHandle Create(
            in SimulationEntityState initialState,
            in ActorCombatInitialization combatState)
        {
            if (float.IsNaN(combatState.BaseStats.Health) ||
                float.IsInfinity(combatState.BaseStats.Health) ||
                combatState.BaseStats.Health <= 0f ||
                float.IsNaN(combatState.CurrentHealth) ||
                float.IsInfinity(combatState.CurrentHealth) ||
                combatState.CurrentHealth <= 0f)
            {
                throw new ArgumentException(
                    "Actors must be created with positive finite maximum and current health.",
                    nameof(combatState));
            }

            var handle = storage.Create(initialState);
            combat.Attach(handle, combatState);
            return handle;
        }

        /// <summary>Checks whether a handle currently identifies a live actor.</summary>
        public bool Contains(EntityHandle handle)
        {
            return storage.Contains(handle);
        }

        /// <summary>Reads actor state if the handle is live.</summary>
        public bool TryRead(EntityHandle handle, out SimulationEntityState state)
        {
            return storage.TryRead(handle, out state);
        }

        /// <summary>Writes actor state if the handle is live.</summary>
        public bool TryWrite(EntityHandle handle, in SimulationEntityState state)
        {
            return storage.TryWrite(handle, state);
        }

        /// <summary>Removes an actor with swap-back compaction.</summary>
        public bool Remove(EntityHandle handle)
        {
            if (!storage.Contains(handle))
            {
                return storage.Remove(handle);
            }

            combat.Detach(handle);
            return storage.Remove(handle);
        }

        /// <summary>Gets the live handle at a dense iteration index.</summary>
        public EntityHandle GetHandleAt(int denseIndex)
        {
            return storage.GetHandleAt(denseIndex);
        }

        /// <summary>Gets state at a dense iteration index.</summary>
        public SimulationEntityState GetStateAt(int denseIndex)
        {
            return storage.GetStateAt(denseIndex);
        }

        /// <summary>Writes state at a dense iteration index without structural mutation.</summary>
        public void SetStateAt(int denseIndex, in SimulationEntityState state)
        {
            storage.SetStateAt(denseIndex, state);
        }

        /// <summary>Reads immutable health state if the actor is live.</summary>
        public bool TryReadHealth(EntityHandle handle, out Health health)
        {
            if (!storage.Contains(handle) ||
                !combat.TryGet(handle, out var record))
            {
                health = default;
                return false;
            }

            health = record.GetHealth();
            return true;
        }

        /// <summary>Reads immutable shield state if the actor is live.</summary>
        public bool TryReadShield(EntityHandle handle, out Shield shield)
        {
            if (!storage.Contains(handle) ||
                !combat.TryGet(handle, out var record))
            {
                shield = default;
                return false;
            }

            shield = record.GetShield();
            return true;
        }

        /// <summary>Reads one calculated statistic by compact index.</summary>
        public bool TryReadStat(
            EntityHandle handle,
            StatIndex statIndex,
            out float value)
        {
            if (!storage.Contains(handle) ||
                !combat.TryGet(handle, out var record))
            {
                value = 0f;
                return false;
            }

            value = record.Stats.Get(statIndex);
            return true;
        }

        /// <summary>Adds a source-attributed statistic modifier without writing Health directly.</summary>
        public bool TryAddModifier(
            EntityHandle handle,
            in Modifier modifier,
            out ModifierHandle modifierHandle)
        {
            if (!storage.Contains(handle) ||
                !combat.TryGet(handle, out var record))
            {
                modifierHandle = default;
                return false;
            }

            return record.Stats.Modifiers.TryAdd(modifier, out modifierHandle);
        }

        /// <summary>Removes a previously issued modifier handle.</summary>
        public bool TryRemoveModifier(
            EntityHandle handle,
            ModifierHandle modifierHandle)
        {
            return storage.Contains(handle) &&
                   combat.TryGet(handle, out var record) &&
                   record.Stats.Modifiers.Remove(modifierHandle);
        }

        /// <summary>Reads the first active instance for one runtime status index.</summary>
        public bool TryReadStatus(
            EntityHandle handle,
            RuntimeContentIndex statusIndex,
            out ActiveStatus status)
        {
            if (!storage.Contains(handle) ||
                !combat.TryGet(handle, out var record))
            {
                status = default;
                return false;
            }

            var index = record.Statuses.FindFirst(statusIndex);
            if (index < 0)
            {
                status = default;
                return false;
            }

            var instance = record.Statuses.GetAt(index);
            status = new ActiveStatus(
                instance.StatusIndex,
                instance.Stacks,
                instance.Strength,
                instance.RemainingDuration,
                instance.TickCount);
            return true;
        }

        /// <summary>Counts independent instances for one runtime status index.</summary>
        public int GetStatusInstanceCount(
            EntityHandle handle,
            RuntimeContentIndex statusIndex)
        {
            return storage.Contains(handle) &&
                   combat.TryGet(handle, out var record)
                ? record.Statuses.CountInstances(statusIndex)
                : 0;
        }

        internal bool TryApplyHealing(EntityHandle handle, float amount)
        {
            if (float.IsNaN(amount) || float.IsInfinity(amount) || amount < 0f ||
                !TryGetCombat(handle, out var record) ||
                record.DeathPending || record.Dead)
            {
                return false;
            }

            record.ReconcileHealthMaximum();
            var maximum = record.Stats.Get(BuiltInStatIndices.Health);
            record.HealthCurrent = Math.Min(maximum, record.HealthCurrent + amount);
            return true;
        }

        internal bool TryGetCombat(
            EntityHandle handle,
            out ActorCombatRecord record)
        {
            if (!storage.Contains(handle))
            {
                record = null;
                return false;
            }

            return combat.TryGet(handle, out record);
        }

        internal bool IsDeathPending(EntityHandle handle)
        {
            return TryGetCombat(handle, out var record) &&
                   (record.DeathPending || record.Dead);
        }
    }

    /// <summary>
    /// Dense projectile store with generation-safe handles and swap-back removal.
    /// </summary>
    public sealed class ProjectileStore
    {
        private readonly DenseBodyStorage storage;

        /// <summary>Initializes a projectile store.</summary>
        public ProjectileStore(int initialCapacity = 16, SimulationDiagnostics diagnostics = null)
        {
            storage = new DenseBodyStorage(
                initialCapacity,
                diagnostics ?? new SimulationDiagnostics());
        }

        /// <summary>Gets the active projectile count.</summary>
        public int Count => storage.Count;

        /// <summary>Creates a projectile.</summary>
        public EntityHandle Create(in SimulationEntityState initialState)
        {
            return storage.Create(initialState);
        }

        /// <summary>Checks whether a handle currently identifies a live projectile.</summary>
        public bool Contains(EntityHandle handle)
        {
            return storage.Contains(handle);
        }

        /// <summary>Reads projectile state if the handle is live.</summary>
        public bool TryRead(EntityHandle handle, out SimulationEntityState state)
        {
            return storage.TryRead(handle, out state);
        }

        /// <summary>Writes projectile state if the handle is live.</summary>
        public bool TryWrite(EntityHandle handle, in SimulationEntityState state)
        {
            return storage.TryWrite(handle, state);
        }

        /// <summary>Removes a projectile with swap-back compaction.</summary>
        public bool Remove(EntityHandle handle)
        {
            return storage.Remove(handle);
        }

        /// <summary>Gets the live handle at a dense iteration index.</summary>
        public EntityHandle GetHandleAt(int denseIndex)
        {
            return storage.GetHandleAt(denseIndex);
        }

        /// <summary>Gets state at a dense iteration index.</summary>
        public SimulationEntityState GetStateAt(int denseIndex)
        {
            return storage.GetStateAt(denseIndex);
        }

        /// <summary>Writes state at a dense iteration index without structural mutation.</summary>
        public void SetStateAt(int denseIndex, in SimulationEntityState state)
        {
            storage.SetStateAt(denseIndex, state);
        }
    }

    /// <summary>
    /// Dense area store with generation-safe handles and swap-back removal.
    /// </summary>
    public sealed class AreaStore
    {
        private readonly DenseBodyStorage storage;

        /// <summary>Initializes an area store.</summary>
        public AreaStore(int initialCapacity = 16, SimulationDiagnostics diagnostics = null)
        {
            storage = new DenseBodyStorage(
                initialCapacity,
                diagnostics ?? new SimulationDiagnostics());
        }

        /// <summary>Gets the active area count.</summary>
        public int Count => storage.Count;

        /// <summary>Creates an area.</summary>
        public EntityHandle Create(in SimulationEntityState initialState)
        {
            return storage.Create(initialState);
        }

        /// <summary>Checks whether a handle currently identifies a live area.</summary>
        public bool Contains(EntityHandle handle)
        {
            return storage.Contains(handle);
        }

        /// <summary>Reads area state if the handle is live.</summary>
        public bool TryRead(EntityHandle handle, out SimulationEntityState state)
        {
            return storage.TryRead(handle, out state);
        }

        /// <summary>Writes area state if the handle is live.</summary>
        public bool TryWrite(EntityHandle handle, in SimulationEntityState state)
        {
            return storage.TryWrite(handle, state);
        }

        /// <summary>Removes an area with swap-back compaction.</summary>
        public bool Remove(EntityHandle handle)
        {
            return storage.Remove(handle);
        }

        /// <summary>Gets the live handle at a dense iteration index.</summary>
        public EntityHandle GetHandleAt(int denseIndex)
        {
            return storage.GetHandleAt(denseIndex);
        }

        /// <summary>Gets state at a dense iteration index.</summary>
        public SimulationEntityState GetStateAt(int denseIndex)
        {
            return storage.GetStateAt(denseIndex);
        }

        /// <summary>Writes state at a dense iteration index without structural mutation.</summary>
        public void SetStateAt(int denseIndex, in SimulationEntityState state)
        {
            storage.SetStateAt(denseIndex, state);
        }
    }

    /// <summary>
    /// Dense pickup store with generation-safe handles and swap-back removal.
    /// </summary>
    public sealed class PickupStore
    {
        private readonly DenseBodyStorage storage;

        /// <summary>Initializes a pickup store.</summary>
        public PickupStore(int initialCapacity = 16, SimulationDiagnostics diagnostics = null)
        {
            storage = new DenseBodyStorage(
                initialCapacity,
                diagnostics ?? new SimulationDiagnostics());
        }

        /// <summary>Gets the active pickup count.</summary>
        public int Count => storage.Count;

        /// <summary>Creates a pickup.</summary>
        public EntityHandle Create(in SimulationEntityState initialState)
        {
            return storage.Create(initialState);
        }

        /// <summary>Checks whether a handle currently identifies a live pickup.</summary>
        public bool Contains(EntityHandle handle)
        {
            return storage.Contains(handle);
        }

        /// <summary>Reads pickup state if the handle is live.</summary>
        public bool TryRead(EntityHandle handle, out SimulationEntityState state)
        {
            return storage.TryRead(handle, out state);
        }

        /// <summary>Writes pickup state if the handle is live.</summary>
        public bool TryWrite(EntityHandle handle, in SimulationEntityState state)
        {
            return storage.TryWrite(handle, state);
        }

        /// <summary>Removes a pickup with swap-back compaction.</summary>
        public bool Remove(EntityHandle handle)
        {
            return storage.Remove(handle);
        }

        /// <summary>Gets the live handle at a dense iteration index.</summary>
        public EntityHandle GetHandleAt(int denseIndex)
        {
            return storage.GetHandleAt(denseIndex);
        }

        /// <summary>Gets state at a dense iteration index.</summary>
        public SimulationEntityState GetStateAt(int denseIndex)
        {
            return storage.GetStateAt(denseIndex);
        }

        /// <summary>Writes state at a dense iteration index without structural mutation.</summary>
        public void SetStateAt(int denseIndex, in SimulationEntityState state)
        {
            storage.SetStateAt(denseIndex, state);
        }
    }
}
