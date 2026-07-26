using System;
using System.Numerics;
using Game.Content.Runtime;
using Game.Core;

namespace Game.Simulation
{
    /// <summary>One command emitted by an effect executor and resolved centrally.</summary>
    public readonly struct SkillExecutionCommand
    {
        internal SkillExecutionCommand(
            in SkillEffectContext context,
            in ResolvedEffectOp effect)
        {
            Context = context;
            Effect = effect;
        }

        /// <summary>Gets source, target and proc-chain context.</summary>
        public SkillEffectContext Context { get; }
        /// <summary>Gets the resolved compact effect.</summary>
        public ResolvedEffectOp Effect { get; }
    }

    /// <summary>Reusable FIFO command storage for all M4 effects.</summary>
    public sealed class SkillExecutionCommandBuffer
    {
        private SkillExecutionCommand[] commands;

        /// <summary>Initializes an execution command buffer.</summary>
        public SkillExecutionCommandBuffer(int initialCapacity = 32)
        {
            if (initialCapacity <= 0) throw new ArgumentOutOfRangeException(nameof(initialCapacity));
            commands = new SkillExecutionCommand[initialCapacity];
        }

        /// <summary>Gets queued command count.</summary>
        public int Count { get; private set; }

        /// <summary>Gets one queued command.</summary>
        public SkillExecutionCommand GetAt(int index)
        {
            if (index < 0 || index >= Count) throw new ArgumentOutOfRangeException(nameof(index));
            return commands[index];
        }

        internal void Add(in SkillExecutionCommand command)
        {
            if (Count == commands.Length) Array.Resize(ref commands, commands.Length * 2);
            commands[Count++] = command;
        }

        internal void Clear()
        {
            Array.Clear(commands, 0, Count);
            Count = 0;
        }
    }

    internal enum ActiveDeliveryKind : byte
    {
        Projectile = 1,
        Area = 2,
        Aura = 3,
        Orbit = 4
    }

    internal struct DeliverySpawnRequest
    {
        public ActiveDeliveryKind Kind;
        public SkillInstance Instance;
        public RuntimeSkillLevel Level;
        public Vector2 Position;
        public Vector2 Velocity;
        public float Lifetime;
        public float Radius;
        public float SecondaryRadius;
        public float TickInterval;
        public float AngularSpeed;
        public int RemainingHits;
        public int ProcDepth;
    }

    internal sealed class DeliverySpawnBuffer
    {
        private DeliverySpawnRequest[] requests;

        public DeliverySpawnBuffer(int capacity)
        {
            requests = new DeliverySpawnRequest[capacity];
        }

        public int Count { get; private set; }
        public DeliverySpawnRequest GetAt(int index) => requests[index];

        public void Add(in DeliverySpawnRequest request)
        {
            if (Count == requests.Length) Array.Resize(ref requests, requests.Length * 2);
            requests[Count++] = request;
        }

        public void Clear()
        {
            Array.Clear(requests, 0, Count);
            Count = 0;
        }
    }

    internal struct ActiveDeliveryRecord
    {
        public ActiveDeliveryKind Kind;
        public SkillInstance Instance;
        public RuntimeSkillLevel Level;
        public float Radius;
        public float SecondaryRadius;
        public float TickInterval;
        public float PulseAccumulator;
        public float Angle;
        public float AngularSpeed;
        public int RemainingHits;
        public int ProcDepth;
        public SpatialEntity LastTarget;
        public Vector2 PreviousPosition;
        public bool HasPulsed;
    }

    internal sealed class ActiveDeliveryStorage
    {
        private ActiveDeliveryRecord[] projectileRecords;
        private ushort[] projectileGenerations;
        private ActiveDeliveryRecord[] areaRecords;
        private ushort[] areaGenerations;

        public ActiveDeliveryStorage(int capacity)
        {
            projectileRecords = new ActiveDeliveryRecord[capacity];
            projectileGenerations = new ushort[capacity];
            areaRecords = new ActiveDeliveryRecord[capacity];
            areaGenerations = new ushort[capacity];
        }

        public int Count { get; private set; }

        public void Attach(EntityKind kind, EntityHandle handle, in ActiveDeliveryRecord record)
        {
            Ensure(kind, handle.Index + 1);
            var generations = kind == EntityKind.Projectile
                ? projectileGenerations
                : areaGenerations;
            var records = kind == EntityKind.Projectile ? projectileRecords : areaRecords;
            if (generations[handle.Index] == 0) Count++;
            generations[handle.Index] = handle.Generation;
            records[handle.Index] = record;
        }

        public bool TryGet(EntityKind kind, EntityHandle handle, out ActiveDeliveryRecord record)
        {
            var generations = kind == EntityKind.Projectile
                ? projectileGenerations
                : areaGenerations;
            var records = kind == EntityKind.Projectile ? projectileRecords : areaRecords;
            if (handle.Index < 0 || handle.Index >= generations.Length ||
                generations[handle.Index] != handle.Generation)
            {
                record = default;
                return false;
            }

            record = records[handle.Index];
            return true;
        }

        public void Set(EntityKind kind, EntityHandle handle, in ActiveDeliveryRecord record)
        {
            if (kind == EntityKind.Projectile) projectileRecords[handle.Index] = record;
            else areaRecords[handle.Index] = record;
        }

        public void Detach(EntityKind kind, EntityHandle handle)
        {
            if (kind != EntityKind.Projectile && kind != EntityKind.Area) return;
            var generations = kind == EntityKind.Projectile
                ? projectileGenerations
                : areaGenerations;
            var records = kind == EntityKind.Projectile ? projectileRecords : areaRecords;
            if (handle.Index < 0 || handle.Index >= generations.Length ||
                generations[handle.Index] != handle.Generation)
            {
                return;
            }

            generations[handle.Index] = 0;
            records[handle.Index] = default;
            Count--;
        }

        private void Ensure(EntityKind kind, int required)
        {
            if (kind == EntityKind.Projectile)
            {
                Ensure(ref projectileRecords, ref projectileGenerations, required);
            }
            else if (kind == EntityKind.Area)
            {
                Ensure(ref areaRecords, ref areaGenerations, required);
            }
            else
            {
                throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }

        private static void Ensure(
            ref ActiveDeliveryRecord[] records,
            ref ushort[] generations,
            int required)
        {
            if (required <= records.Length) return;
            var capacity = records.Length * 2;
            while (capacity < required) capacity *= 2;
            Array.Resize(ref records, capacity);
            Array.Resize(ref generations, capacity);
        }
    }

    internal sealed class SkillResourceStorage
    {
        private float[] values;
        private ushort[] generations;

        public SkillResourceStorage(int capacity)
        {
            values = new float[capacity];
            generations = new ushort[capacity];
        }

        public void Set(SpatialEntity owner, float value)
        {
            Ensure(owner.Handle.Index + 1);
            generations[owner.Handle.Index] = owner.Handle.Generation;
            values[owner.Handle.Index] = Math.Max(0f, value);
        }

        public float Get(SpatialEntity owner)
        {
            return owner.Kind == EntityKind.Actor &&
                   owner.Handle.Index >= 0 &&
                   owner.Handle.Index < generations.Length &&
                   generations[owner.Handle.Index] == owner.Handle.Generation
                ? values[owner.Handle.Index]
                : 0f;
        }

        public bool TrySpend(SpatialEntity owner, float cost)
        {
            var current = Get(owner);
            if (current < cost) return false;
            Set(owner, current - cost);
            return true;
        }

        public void Gain(SpatialEntity owner, float value)
        {
            var next = Get(owner) + Math.Max(0f, value);
            Set(owner, float.IsInfinity(next) ? float.MaxValue : next);
        }

        private void Ensure(int required)
        {
            if (required <= values.Length) return;
            var capacity = values.Length * 2;
            while (capacity < required) capacity *= 2;
            Array.Resize(ref values, capacity);
            Array.Resize(ref generations, capacity);
        }
    }

    /// <summary>
    /// Owns actor skill instances, reusable query buffers, trigger events, delivery
    /// sidecars and the centralized effect command buffer.
    /// </summary>
    public sealed class SkillRuntime
    {
        private readonly SkillRuntimeCatalog catalog;
        private readonly SpatialQueryBuffer spatialResults;
        private readonly SkillTargetResultBuffer targets;
        private readonly DeliverySpawnBuffer spawnRequests;
        private readonly ActiveDeliveryStorage deliveries;
        private readonly SkillResourceStorage resources;
        private SkillInstance[] instances;
        private ushort[] instanceGenerations;
        private int[] freeInstanceSlots;
        private int firstFreeInstanceSlot = -1;
        private int instanceSlotCount;
        private SkillTriggerContext[] triggerEvents;
        private int triggerEventCount;
        private RandomStream random;

        /// <summary>Initializes an M4 skill runtime from a compiled catalog.</summary>
        public SkillRuntime(
            SkillRuntimeCatalog catalog,
            ulong seed = 1UL,
            int initialCapacity = 32)
        {
            if (initialCapacity <= 0) throw new ArgumentOutOfRangeException(nameof(initialCapacity));
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            instances = new SkillInstance[initialCapacity];
            instanceGenerations = new ushort[initialCapacity];
            freeInstanceSlots = new int[initialCapacity];
            triggerEvents = new SkillTriggerContext[initialCapacity];
            spatialResults = new SpatialQueryBuffer(initialCapacity);
            targets = new SkillTargetResultBuffer(initialCapacity);
            spawnRequests = new DeliverySpawnBuffer(initialCapacity);
            deliveries = new ActiveDeliveryStorage(initialCapacity);
            resources = new SkillResourceStorage(initialCapacity);
            Commands = new SkillExecutionCommandBuffer(initialCapacity);
            random = new RandomStream(seed).Derive(0x534B494C4CUL);
        }

        /// <summary>Gets effect commands waiting for centralized resolution.</summary>
        public SkillExecutionCommandBuffer Commands { get; }
        /// <summary>Gets the number of owned and secondary-only instances.</summary>
        public int InstanceCount { get; private set; }
        /// <summary>Gets the number of live projectile/area delivery sidecars.</summary>
        public int ActiveDeliveryCount => deliveries.Count;
        /// <summary>Gets cumulative successful activations.</summary>
        public long TriggerCount { get; private set; }
        /// <summary>Gets cumulative target impacts queued by deliveries.</summary>
        public long HitCount { get; private set; }

        /// <summary>Adds an actor-owned skill instance sharing its compiled definition.</summary>
        public Result<SkillInstanceHandle> AddInstance(
            SpatialEntity owner,
            RuntimeContentIndex skillIndex,
            int level = 1)
        {
            return AddInstanceInternal(owner, skillIndex, level, false, true);
        }

        /// <summary>Reads one live instance.</summary>
        public bool TryGetInstance(SkillInstanceHandle handle, out SkillInstance instance)
        {
            if (!handle.IsValid ||
                handle.Value >= instanceSlotCount ||
                instanceGenerations[handle.Value] != handle.Generation ||
                instances[handle.Value] == null)
            {
                instance = null;
                return false;
            }

            instance = instances[handle.Value];
            return instance != null;
        }

        /// <summary>Sets a validated one-based instance level.</summary>
        public bool SetLevel(SkillInstanceHandle handle, int level)
        {
            if (!TryGetInstance(handle, out var instance) ||
                level < 1 || level > instance.Definition.MaximumLevel)
            {
                return false;
            }

            instance.Level = level;
            return true;
        }

        /// <summary>Sets current owner resource used by skill costs.</summary>
        public bool SetResource(SpatialEntity owner, float value)
        {
            if (owner.Kind != EntityKind.Actor || !IsFinite(value) || value < 0f) return false;
            resources.Set(owner, value);
            return true;
        }

        /// <summary>Gets current owner skill resource.</summary>
        public float GetResource(SpatialEntity owner) => resources.Get(owner);

        /// <summary>Queues an external pickup or other trigger context for the next skill stage.</summary>
        public bool QueueTrigger(in SkillTriggerContext context)
        {
            if (context.ProcDepth < 0) return false;
            if (triggerEventCount == triggerEvents.Length)
            {
                Array.Resize(ref triggerEvents, triggerEvents.Length * 2);
            }

            triggerEvents[triggerEventCount++] = context;
            return true;
        }

        internal static SkillRuntime CreateEmpty(int capacity = 16)
        {
            return new SkillRuntime(SkillRuntimeCatalog.Empty(), 1UL, capacity);
        }

        internal void TickTriggers(SimulationWorld world)
        {
            var delta = world.DeltaTimeSeconds;
            for (var index = 0; index < instanceSlotCount; index++)
            {
                var instance = instances[index];
                if (instance == null) continue;
                if (instance.CooldownRemaining > 0f)
                {
                    instance.CooldownRemaining = Math.Max(0f, instance.CooldownRemaining - delta);
                }

                if (instance.SecondaryOnly ||
                    instance.Definition.Source.Trigger.ModuleId != SkillModuleIds.TriggerTimer)
                {
                    continue;
                }

                var timer = new SkillTriggerContext(
                    SkillTriggerEventType.Timer,
                    instance.Owner,
                    instance.Owner,
                    default,
                    default,
                    instance.Definition.Source.Id,
                    instance.Definition.Index,
                    0);
                TryActivate(world, instance, timer, false, false);
            }

            var eventCount = triggerEventCount;
            for (var eventIndex = 0; eventIndex < eventCount; eventIndex++)
            {
                var context = triggerEvents[eventIndex];
                if (context.EventType == SkillTriggerEventType.SecondarySkill)
                {
                    for (var instanceIndex = 0;
                         instanceIndex < instanceSlotCount;
                         instanceIndex++)
                    {
                        var instance = instances[instanceIndex];
                        if (instance == null) continue;
                        if (instance.Owner == context.Source &&
                            instance.Definition.Index == context.ReferenceIndex)
                        {
                            TryActivate(world, instance, context, true, true);
                            break;
                        }
                    }

                    continue;
                }

                for (var instanceIndex = 0;
                     instanceIndex < instanceSlotCount;
                     instanceIndex++)
                {
                    var instance = instances[instanceIndex];
                    if (instance == null) continue;
                    if (!instance.SecondaryOnly)
                    {
                        TryActivate(world, instance, context, false, false);
                    }
                }
            }

            Array.Clear(triggerEvents, 0, eventCount);
            triggerEventCount = 0;
        }

        internal void TickDeliveries(SimulationWorld world)
        {
            TickProjectiles(world);
            TickAreas(world);
        }

        internal void ApplyPendingSpawns(SimulationWorld world)
        {
            var count = spawnRequests.Count;
            for (var index = 0; index < count; index++)
            {
                var request = spawnRequests.GetAt(index);
                var entityKind = request.Kind == ActiveDeliveryKind.Projectile
                    ? EntityKind.Projectile
                    : EntityKind.Area;
                var state = SimulationEntityState.Create(
                    request.Position,
                    request.Velocity,
                    request.Velocity.LengthSquared() > 0.000001f
                        ? (float)Math.Atan2(request.Velocity.Y, request.Velocity.X)
                        : 0f,
                    request.Lifetime);
                var handle = world.CreateEntity(entityKind, state);
                deliveries.Attach(
                    entityKind,
                    handle,
                    new ActiveDeliveryRecord
                    {
                        Kind = request.Kind,
                        Instance = request.Instance,
                        Level = request.Level,
                        Radius = request.Radius,
                        SecondaryRadius = request.SecondaryRadius,
                        TickInterval = request.TickInterval,
                        AngularSpeed = request.AngularSpeed,
                        RemainingHits = request.RemainingHits,
                        ProcDepth = request.ProcDepth,
                        PreviousPosition = request.Position
                    });
                world.EmitEvent(
                    SimulationEventType.Created,
                    entityKind,
                    handle,
                    request.Position);
            }

            spawnRequests.Clear();
        }

        internal void OnEntityRemoved(EntityKind kind, EntityHandle handle)
        {
            deliveries.Detach(kind, handle);
            if (kind != EntityKind.Actor) return;

            var owner = new SpatialEntity(kind, handle);
            for (var index = 0; index < instanceSlotCount; index++)
            {
                var instance = instances[index];
                if (instance == null || instance.Owner != owner) continue;

                instances[index] = null;
                instanceGenerations[index] = NextGeneration(instanceGenerations[index]);
                freeInstanceSlots[index] = firstFreeInstanceSlot;
                firstFreeInstanceSlot = index;
                InstanceCount--;
            }
        }

        internal void EnqueueSpawn(in DeliverySpawnRequest request)
        {
            spawnRequests.Add(request);
        }

        internal void EnqueueEffects(
            SkillInstance instance,
            RuntimeSkillLevel level,
            in SkillTarget target,
            Vector2 sourcePosition,
            int procDepth)
        {
            var direction = target.Position - sourcePosition;
            var lengthSquared = direction.LengthSquared();
            if (lengthSquared > 0.000001f) direction /= (float)Math.Sqrt(lengthSquared);
            else direction = Vector2.UnitX;
            var context = new SkillEffectContext(
                instance.Owner,
                target.Entity,
                target.HasEntity,
                instance.Definition.Source.Id,
                target.Position,
                direction,
                procDepth);
            for (var index = 0; index < level.Effects.Count; index++)
            {
                instance.Definition.GetEffectExecutorAt(index).Queue(
                    Commands,
                    context,
                    level.GetEffectAt(index));
            }

            HitCount++;
        }

        internal void QueueSecondary(
            SpatialEntity owner,
            RuntimeContentIndex skillIndex,
            Vector2 position,
            Vector2 direction,
            ContentId sourceContentId,
            int procDepth)
        {
            QueueTrigger(
                new SkillTriggerContext(
                    SkillTriggerEventType.SecondarySkill,
                    owner,
                    owner,
                    position,
                    direction,
                    sourceContentId,
                    skillIndex,
                    procDepth));
        }

        internal void GainResource(SpatialEntity owner, float amount)
        {
            resources.Gain(owner, amount);
        }

        private Result<SkillInstanceHandle> AddInstanceInternal(
            SpatialEntity owner,
            RuntimeContentIndex skillIndex,
            int level,
            bool secondaryOnly,
            bool addReferencedSecondaries)
        {
            if (owner.Kind != EntityKind.Actor ||
                !catalog.TryGet(skillIndex, out var definition) ||
                level < 1 || level > definition.MaximumLevel)
            {
                return Result<SkillInstanceHandle>.Failure(
                    new Error(ErrorCode.InvalidAuthoringData, "Skill instance owner, definition, or level is invalid."));
            }

            int slot;
            if (firstFreeInstanceSlot >= 0)
            {
                slot = firstFreeInstanceSlot;
                firstFreeInstanceSlot = freeInstanceSlots[slot];
            }
            else
            {
                if (instanceSlotCount == instances.Length)
                {
                    var capacity = instances.Length * 2;
                    Array.Resize(ref instances, capacity);
                    Array.Resize(ref instanceGenerations, capacity);
                    Array.Resize(ref freeInstanceSlots, capacity);
                }

                slot = instanceSlotCount++;
                instanceGenerations[slot] = 1;
            }

            var handle = new SkillInstanceHandle(slot, instanceGenerations[slot]);
            instances[slot] = new SkillInstance(
                handle,
                owner,
                definition,
                level,
                secondaryOnly);
            InstanceCount++;

            if (addReferencedSecondaries)
            {
                var runtimeLevel = definition.GetLevel(level);
                for (var index = 0; index < runtimeLevel.Effects.Count; index++)
                {
                    var effect = runtimeLevel.GetEffectAt(index);
                    if (effect.Code == EffectOpCode.SpawnSecondarySkill &&
                        effect.Reference0.IsValid &&
                        !HasInstance(owner, effect.Reference0))
                    {
                        AddInstanceInternal(owner, effect.Reference0, 1, true, true);
                    }
                }
            }

            return Result<SkillInstanceHandle>.Success(handle);
        }

        private bool HasInstance(SpatialEntity owner, RuntimeContentIndex definition)
        {
            for (var index = 0; index < instanceSlotCount; index++)
            {
                var instance = instances[index];
                if (instance != null &&
                    instance.Owner == owner &&
                    instance.Definition.Index == definition)
                {
                    return true;
                }
            }

            return false;
        }

        private static ushort NextGeneration(ushort current)
        {
            var next = (ushort)(current + 1);
            return next == 0 ? (ushort)1 : next;
        }

        private bool TryActivate(
            SimulationWorld world,
            SkillInstance instance,
            in SkillTriggerContext context,
            bool bypassTrigger,
            bool bypassCooldownAndCost)
        {
            if (!world.Actors.Contains(instance.Owner.Handle) ||
                world.Actors.IsDeathPending(instance.Owner.Handle))
            {
                return false;
            }

            if (!bypassTrigger &&
                !instance.Definition.TriggerExecutor.Matches(instance.Owner, context))
            {
                return false;
            }

            if (context.ProcDepth > world.CombatRules.MaximumProcDepth)
            {
                world.Diagnostics.RecordTruncatedProcChain();
                return false;
            }

            var level = instance.Definition.GetLevel(instance.Level);
            if (!bypassCooldownAndCost && instance.CooldownRemaining > 0f) return false;
            if (!instance.Definition.ConditionEvaluator.Evaluate(world, instance, level, context)) return false;
            if (!bypassCooldownAndCost && level.ResourceCost > 0f &&
                resources.Get(instance.Owner) < level.ResourceCost)
            {
                return false;
            }

            instance.Definition.TargetingExecutor.Select(
                world,
                instance,
                level,
                context,
                spatialResults,
                targets,
                ref random);
            if (targets.Count == 0) return false;
            if (!bypassCooldownAndCost && level.ResourceCost > 0f &&
                !resources.TrySpend(instance.Owner, level.ResourceCost))
            {
                return false;
            }

            instance.Definition.DeliveryExecutor.Deliver(
                this,
                world,
                instance,
                level,
                context,
                targets);
            if (!bypassCooldownAndCost) instance.CooldownRemaining = level.CooldownSeconds;
            TriggerCount++;
            return true;
        }

        private void TickProjectiles(SimulationWorld world)
        {
            for (var dense = 0; dense < world.Projectiles.Count; dense++)
            {
                var handle = world.Projectiles.GetHandleAt(dense);
                if (!deliveries.TryGet(EntityKind.Projectile, handle, out var record)) continue;
                var state = world.Projectiles.GetStateAt(dense);
                var segment = state.Position - record.PreviousPosition;
                var broadCenter = (state.Position + record.PreviousPosition) * 0.5f;
                var broadRadius = (segment.Length() * 0.5f) + record.Radius;
                world.SpatialGrid.QueryRadius(broadCenter, broadRadius, spatialResults);
                var bestIndex = -1;
                var bestDistance = float.PositiveInfinity;
                for (var candidateIndex = 0; candidateIndex < spatialResults.Count; candidateIndex++)
                {
                    var candidate = spatialResults[candidateIndex];
                    if (candidate.Entity.Kind != EntityKind.Actor ||
                        candidate.Entity == record.Instance.Owner ||
                        candidate.Entity == record.LastTarget ||
                        !world.Actors.Contains(candidate.Entity.Handle) ||
                        world.Actors.IsDeathPending(candidate.Entity.Handle) ||
                        !world.IsHostileTarget(record.Instance.Owner, candidate.Entity))
                    {
                        continue;
                    }

                    var sweptDistance = DistanceToSegmentSquared(
                        candidate.Position,
                        record.PreviousPosition,
                        state.Position);
                    if (sweptDistance > record.Radius * record.Radius)
                    {
                        continue;
                    }

                    var travelDistance = Vector2.DistanceSquared(
                        record.PreviousPosition,
                        candidate.Position);
                    if (travelDistance < bestDistance ||
                        (travelDistance == bestDistance &&
                         (bestIndex < 0 || candidate.Entity.Handle.Index <
                          spatialResults[bestIndex].Entity.Handle.Index)))
                    {
                        bestIndex = candidateIndex;
                        bestDistance = travelDistance;
                    }
                }

                record.PreviousPosition = state.Position;
                if (bestIndex < 0)
                {
                    deliveries.Set(EntityKind.Projectile, handle, record);
                    continue;
                }
                var hit = spatialResults[bestIndex];
                var target = new SkillTarget(hit.Entity, hit.Position, true);
                EnqueueEffects(record.Instance, record.Level, target, state.Position, record.ProcDepth);
                record.LastTarget = hit.Entity;
                record.RemainingHits--;
                deliveries.Set(EntityKind.Projectile, handle, record);
                if (record.RemainingHits <= 0)
                {
                    world.Commands.Remove(EntityKind.Projectile, handle);
                }
            }
        }

        private void TickAreas(SimulationWorld world)
        {
            for (var dense = 0; dense < world.Areas.Count; dense++)
            {
                var handle = world.Areas.GetHandleAt(dense);
                if (!deliveries.TryGet(EntityKind.Area, handle, out var record)) continue;
                var state = world.Areas.GetStateAt(dense);
                if (record.Kind == ActiveDeliveryKind.Aura ||
                    record.Kind == ActiveDeliveryKind.Orbit)
                {
                    if (!world.Actors.TryRead(record.Instance.Owner.Handle, out var owner))
                    {
                        world.Commands.Remove(EntityKind.Area, handle);
                        continue;
                    }

                    if (record.Kind == ActiveDeliveryKind.Aura)
                    {
                        state.Position = owner.Position;
                    }
                    else
                    {
                        record.Angle += record.AngularSpeed * world.DeltaTimeSeconds;
                        state.Position = owner.Position + new Vector2(
                            (float)Math.Cos(record.Angle) * record.Radius,
                            (float)Math.Sin(record.Angle) * record.Radius);
                    }

                    world.Areas.SetStateAt(dense, state);
                    world.SpatialGrid.Update(new SpatialEntity(EntityKind.Area, handle), state.Position);
                }

                record.PulseAccumulator += world.DeltaTimeSeconds;
                if (record.HasPulsed && record.PulseAccumulator < record.TickInterval)
                {
                    deliveries.Set(EntityKind.Area, handle, record);
                    continue;
                }

                record.HasPulsed = true;
                record.PulseAccumulator = record.TickInterval > 0f
                    ? Math.Max(0f, record.PulseAccumulator - record.TickInterval)
                    : 0f;
                var hitRadius = record.Kind == ActiveDeliveryKind.Orbit
                    ? record.SecondaryRadius
                    : record.Radius;
                TargetingExecutorUtility.CollectActors(
                    world,
                    record.Instance.Owner,
                    state.Position,
                    hitRadius,
                    spatialResults,
                    targets);
                targets.SortStable();
                for (var targetIndex = 0; targetIndex < targets.Count; targetIndex++)
                {
                    var target = targets[targetIndex];
                    EnqueueEffects(record.Instance, record.Level, target, state.Position, record.ProcDepth);
                }

                deliveries.Set(EntityKind.Area, handle, record);
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static float DistanceToSegmentSquared(
            Vector2 point,
            Vector2 start,
            Vector2 end)
        {
            var segment = end - start;
            var lengthSquared = segment.LengthSquared();
            if (lengthSquared <= 0.000001f)
            {
                return Vector2.DistanceSquared(point, start);
            }

            var fraction = Vector2.Dot(point - start, segment) / lengthSquared;
            if (fraction < 0f) fraction = 0f;
            else if (fraction > 1f) fraction = 1f;
            var closest = start + (segment * fraction);
            return Vector2.DistanceSquared(point, closest);
        }
    }
}
