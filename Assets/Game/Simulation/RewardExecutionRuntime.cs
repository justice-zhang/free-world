using System;
using System.Collections.Generic;
using System.Numerics;
using Game.Content.Runtime;
using Game.Core;

namespace Game.Simulation
{
    public enum RewardDeltaKind : byte
    {
        Currency = 1,
        UnlockContent = 2,
        Unique = 3,
        Story = 4
    }

    /// <summary>One immutable run-result contribution produced by RewardResolution.</summary>
    public readonly struct RewardResultEntry
    {
        internal RewardResultEntry(
            in RewardTransactionId transaction,
            RewardDeltaKind kind,
            ContentId contentId,
            int amount)
        {
            Transaction = transaction;
            Kind = kind;
            ContentId = contentId;
            Amount = amount;
        }

        public RewardTransactionId Transaction { get; }
        public RewardDeltaKind Kind { get; }
        public ContentId ContentId { get; }
        public int Amount { get; }
    }

    public enum RelicChoiceRequestStatus : byte
    {
        ChoiceRequested = 1,
        FallbackCommitted = 2,
        AlreadyPending = 3,
        AlreadyCommitted = 4,
        Busy = 5,
        CapacityExceeded = 6,
        InvalidRequest = 7
    }

    public enum RelicChoiceResolutionStatus : byte
    {
        Committed = 1,
        NoPendingChoice = 2,
        InvalidSelection = 3,
        NoLongerEligible = 4,
        CapacityExceeded = 5
    }

    public readonly struct RelicInventoryEntry
    {
        internal RelicInventoryEntry(ContentId relicId, int level, int maximumLevel)
        {
            RelicId = relicId;
            Level = level;
            MaximumLevel = maximumLevel;
        }

        public ContentId RelicId { get; }
        public int Level { get; }
        public int MaximumLevel { get; }
        public bool IsMaximumLevel => Level >= MaximumLevel;
    }

    /// <summary>Fixed three-slot battle-relic inventory owned by the reward runtime.</summary>
    public sealed class RelicInventory
    {
        private struct Slot
        {
            public RuntimeRelicDefinition Definition;
            public int Level;
        }

        private readonly Slot[] slots;

        internal RelicInventory(int slotCount)
        {
            if (slotCount < 1) throw new ArgumentOutOfRangeException(nameof(slotCount));
            slots = new Slot[slotCount];
        }

        public int SlotCount => slots.Length;
        public int Count { get; private set; }

        public RelicInventoryEntry GetAt(int index)
        {
            if (index < 0 || index >= Count) throw new ArgumentOutOfRangeException(nameof(index));
            var slot = slots[index];
            return new RelicInventoryEntry(slot.Definition.Id, slot.Level, slot.Definition.MaximumLevel);
        }

        public bool TryGet(ContentId id, out RelicInventoryEntry entry)
        {
            var index = Find(id);
            if (index >= 0)
            {
                var slot = slots[index];
                entry = new RelicInventoryEntry(slot.Definition.Id, slot.Level, slot.Definition.MaximumLevel);
                return true;
            }
            entry = default;
            return false;
        }

        internal bool CanAcquire(RuntimeRelicDefinition definition)
        {
            if (definition == null) return false;
            var index = Find(definition.Id);
            return index >= 0 ? slots[index].Level < definition.MaximumLevel : Count < slots.Length;
        }

        internal bool Acquire(RuntimeRelicDefinition definition)
        {
            if (!CanAcquire(definition)) return false;
            var index = Find(definition.Id);
            if (index >= 0)
            {
                var slot = slots[index];
                slot.Level++;
                slots[index] = slot;
                return true;
            }
            slots[Count++] = new Slot { Definition = definition, Level = 1 };
            return true;
        }

        internal bool HasTag(ContentTag tag)
        {
            if (!tag.IsValid) return false;
            for (var slot = 0; slot < Count; slot++)
            {
                var tags = slots[slot].Definition.Tags;
                for (var index = 0; index < tags.Count; index++)
                    if (tags[index] == tag) return true;
            }
            return false;
        }

        internal int LevelForTag(ContentTag tag)
        {
            var level = 0;
            if (!tag.IsValid) return level;
            for (var slot = 0; slot < Count; slot++)
            {
                var tags = slots[slot].Definition.Tags;
                for (var index = 0; index < tags.Count; index++)
                {
                    if (tags[index] != tag) continue;
                    level += slots[slot].Level;
                    break;
                }
            }
            return level;
        }

        private int Find(ContentId id)
        {
            for (var index = 0; index < Count; index++)
                if (slots[index].Definition.Id == id) return index;
            return -1;
        }
    }

    /// <summary>Frozen candidate projection for one elite-core relic selection.</summary>
    public sealed class RelicChoiceSnapshot
    {
        private readonly ContentId[] candidates;

        internal RelicChoiceSnapshot(
            in RewardTransactionId transaction,
            ContentId fallbackId,
            ContentId[] candidateIds)
        {
            Transaction = transaction;
            FallbackId = fallbackId;
            candidates = candidateIds == null ? Array.Empty<ContentId>() : (ContentId[])candidateIds.Clone();
        }

        public RewardTransactionId Transaction { get; }
        public ContentId FallbackId { get; }
        public int CandidateCount => candidates.Length;

        public ContentId GetCandidateAt(int index)
        {
            if (index < 0 || index >= candidates.Length) throw new ArgumentOutOfRangeException(nameof(index));
            return candidates[index];
        }
    }

    /// <summary>Immutable non-XP pickup projection for presentation and tests.</summary>
    public readonly struct RewardPickupSnapshot
    {
        internal RewardPickupSnapshot(
            EntityHandle handle,
            ContentId pickupId,
            ContentId rewardId,
            in RewardTransactionId transaction,
            ContentTag eligibilityTag,
            bool choice)
        {
            Handle = handle;
            PickupId = pickupId;
            RewardId = rewardId;
            Transaction = transaction;
            EligibilityTag = eligibilityTag;
            IsChoice = choice;
        }

        public EntityHandle Handle { get; }
        public ContentId PickupId { get; }
        public ContentId RewardId { get; }
        public RewardTransactionId Transaction { get; }
        public ContentTag EligibilityTag { get; }
        public bool IsChoice { get; }
    }

    public sealed partial class RewardRuntime
    {
        private enum RequestMode : byte
        {
            Direct = 1,
            Ground = 2
        }

        private struct PendingRequest
        {
            public RuntimeRewardDefinition Definition;
            public RewardTransactionId Transaction;
            public SpatialEntity Source;
            public Vector2 Position;
            public RequestMode Mode;
        }

        private struct PendingPickup
        {
            public RuntimePickupDefinition Pickup;
            public RuntimeRewardDefinition Reward;
            public RewardTransactionId Transaction;
            public SpatialEntity Source;
            public Vector2 Position;
            public bool Choice;
        }

        private struct PickupRecord
        {
            public ushort Generation;
            public RuntimePickupDefinition Pickup;
            public RuntimeRewardDefinition Reward;
            public RewardTransactionId Transaction;
            public SpatialEntity Source;
            public bool ForceCollect;
            public bool Choice;
        }

        private const ulong RewardStreamId = 0x524557415244UL;
        private const float DefaultGroundRadius = 0.75f;
        private const float DefaultGroundLifetime = 120f;
        private const float PickupAttractionSpeed = 15f;
        private static readonly ContentTag HostileAreaTag = Tag("reward.target.hostile_area");
        private static readonly ContentTag UniqueTag = Tag("reward.unique");
        private static readonly ContentTag ObjectiveLockedTag = Tag("reward.objective_locked");
        private static readonly ContentTag ChoiceTag = Tag("reward.choice");
        private static readonly ContentTag OverhealBarrierTag = Tag("relic.rule.overheal_barrier");
        private static readonly ContentTag BossDamageTag = Tag("relic.rule.boss_damage");
        private static readonly ContentTag IncomingRiskTag = Tag("relic.rule.incoming_risk");

        private BuildRuntimeCatalog catalog;
        private ProgressionRuntime progression;
        private SpatialEntity player;
        private RuntimePickupDefinition[] pickupDefinitions = Array.Empty<RuntimePickupDefinition>();
        private RuntimeRelicDefinition[] relicDefinitions = Array.Empty<RuntimeRelicDefinition>();
        private RuntimeRelicDefinition[] relicScratch = Array.Empty<RuntimeRelicDefinition>();
        private PendingRequest[] pendingRequests;
        private PendingPickup[] pendingPickups;
        private PickupRecord[] pickupRecords;
        private RewardResultEntry[] resultEntries;
        private ContentId[] ownedUniqueRewards = Array.Empty<ContentId>();
        private string[] appliedOncePerRunKeys;
        private Dictionary<RewardTransactionId, byte> activePickupTransactions;
        private readonly SpatialQueryBuffer spatialResults;
        private RandomStream rewardRandom;
        private int pendingRequestCount;
        private int pendingPickupCount;
        private int resultEntryCount;
        private int appliedOncePerRunCount;
        private bool initialized;

        private void EnsureExecutionStorage(int capacity)
        {
            if (pendingRequests != null) return;
            var baseCapacity = Math.Max(16, capacity);
            pendingRequests = new PendingRequest[baseCapacity];
            pendingPickups = new PendingPickup[baseCapacity];
            pickupRecords = new PickupRecord[checked(baseCapacity * 4)];
            resultEntries = new RewardResultEntry[checked(baseCapacity * 4)];
            appliedOncePerRunKeys = new string[baseCapacity];
            activePickupTransactions = new Dictionary<RewardTransactionId, byte>(checked(baseCapacity * 4));
        }

        internal void Initialize(
            BuildRuntimeCatalog runtimeCatalog,
            ProgressionRuntime progressionRuntime,
            EntityHandle playerHandle,
            ulong runSeed)
        {
            if (runtimeCatalog == null) throw new ArgumentNullException(nameof(runtimeCatalog));
            if (progressionRuntime == null) throw new ArgumentNullException(nameof(progressionRuntime));
            if (!playerHandle.IsValid) throw new ArgumentException("Reward player must be valid.", nameof(playerHandle));
            if (initialized)
            {
                if (catalog != runtimeCatalog || player.Handle != playerHandle)
                    throw new InvalidOperationException("Reward runtime is already initialized for another run.");
                return;
            }

            EnsureExecutionStorage(committed.Length);
            catalog = runtimeCatalog;
            progression = progressionRuntime;
            player = new SpatialEntity(EntityKind.Actor, playerHandle);
            rewardRandom = new RandomStream(runSeed).Derive(RewardStreamId);
            RunId = runSeed;
            var pickupCount = 0;
            var relicCount = 0;
            for (var index = 0; index < catalog.DefinitionCount; index++)
            {
                var definition = catalog.GetDefinitionAt(index);
                if (definition is RuntimePickupDefinition) pickupCount++;
                else if (definition is RuntimeRelicDefinition) relicCount++;
            }
            pickupDefinitions = new RuntimePickupDefinition[pickupCount];
            relicDefinitions = new RuntimeRelicDefinition[relicCount];
            relicScratch = new RuntimeRelicDefinition[relicCount];
            pickupCount = 0;
            relicCount = 0;
            for (var index = 0; index < catalog.DefinitionCount; index++)
            {
                var definition = catalog.GetDefinitionAt(index);
                if (definition is RuntimePickupDefinition pickup) pickupDefinitions[pickupCount++] = pickup;
                else if (definition is RuntimeRelicDefinition relic) relicDefinitions[relicCount++] = relic;
            }
            Relics = new RelicInventory(3);
            initialized = true;
        }

        public bool IsInitialized => initialized;
        public ulong RunId { get; private set; }
        public RelicInventory Relics { get; private set; }
        public RelicChoiceSnapshot CurrentRelicChoice { get; private set; }
        public bool HasPendingRelicChoice => CurrentRelicChoice != null;
        public bool PauseRequested => HasPendingRelicChoice;
        public ulong RandomCalls => rewardRandom.Calls;
        public int ActivePickupCount { get; private set; }
        public int ResultEntryCount => resultEntryCount;
        public int RejectedCapacity { get; private set; }
        public int PickupCapacityGrowthCount { get; private set; }

        public RewardResultEntry GetResultEntryAt(int index)
        {
            if (index < 0 || index >= resultEntryCount) throw new ArgumentOutOfRangeException(nameof(index));
            return resultEntries[index];
        }

        public bool TryGetPickup(EntityHandle handle, out RewardPickupSnapshot snapshot)
        {
            if (TryGetPickupRecord(handle, out var record))
            {
                snapshot = new RewardPickupSnapshot(
                    handle,
                    record.Pickup == null ? record.Reward.Id : record.Pickup.Id,
                    record.Reward.Id,
                    record.Transaction,
                    record.Pickup == null ? default : record.Pickup.EligibilityTag,
                    record.Choice);
                return true;
            }
            snapshot = default;
            return false;
        }

        public void SetOwnedUniqueRewards(ContentId[] ownedIds)
        {
            var source = ownedIds ?? Array.Empty<ContentId>();
            ownedUniqueRewards = (ContentId[])source.Clone();
            Array.Sort(ownedUniqueRewards);
            for (var index = 0; index < ownedUniqueRewards.Length; index++)
            {
                if (!ownedUniqueRewards[index].IsValid)
                    throw new ArgumentException("Owned unique reward IDs must be valid.", nameof(ownedIds));
                if (index > 0 && ownedUniqueRewards[index] == ownedUniqueRewards[index - 1])
                    throw new ArgumentException("Owned unique reward IDs must be unique.", nameof(ownedIds));
            }
        }

        internal bool TryQueueDirect(
            ContentId rewardId,
            in RewardTransactionId transaction,
            Vector2 position,
            SpatialEntity source = default)
        {
            return TryQueue(rewardId, transaction, position, source, RequestMode.Direct);
        }

        internal bool TryQueueGroundReward(
            ContentId rewardId,
            in RewardTransactionId transaction,
            Vector2 position,
            SpatialEntity source = default)
        {
            return TryQueue(rewardId, transaction, position, source, RequestMode.Ground);
        }

        internal bool TryQueuePickup(
            ContentId pickupId,
            in RewardTransactionId transaction,
            Vector2 position,
            SpatialEntity source = default)
        {
            if (!initialized || IsCommitted(transaction) || HasPending(transaction)) return false;
            if (!catalog.TryGetDefinition(pickupId, out var definition) ||
                !(definition is RuntimePickupDefinition pickup) ||
                !catalog.TryGetDefinition(pickup.RewardId, out var rewardDefinition) ||
                !(rewardDefinition is RuntimeRewardDefinition reward))
                return false;
            return QueuePendingPickup(pickup, reward, transaction, position, source, false);
        }

        internal bool TryQueueRandomPickup(
            float chance,
            ContentId sourceStableId,
            EntityHandle defeated,
            Vector2 position,
            SpatialEntity source = default)
        {
            if (!initialized || pickupDefinitions.Length == 0 || !sourceStableId.IsValid || !defeated.IsValid)
                return false;
            chance = Math.Max(0f, Math.Min(1f, chance));
            if (chance <= 0f || rewardRandom.NextFloat() >= chance) return false;
            var pickup = pickupDefinitions[rewardRandom.NextInt(pickupDefinitions.Length)];
            var sequence = ComposeEntitySequence(defeated, 0);
            return TryQueuePickup(pickup.Id, new RewardTransactionId(RunId, sourceStableId, sequence), position, source);
        }

        internal bool CaptureMapOutputs(MapObjectiveRuntime map)
        {
            if (!initialized || map == null || map.OutputCount == 0) return true;
            var position = Vector2.Zero;
            for (var index = 0; index < map.OutputCount; index++)
            {
                var output = map.GetOutputAt(index);
                if (!catalog.TryGetDefinition(output.OutputId, out var definition) ||
                    !(definition is RuntimeRewardDefinition))
                    continue;
                if (!TryQueueDirect(output.OutputId, output.Transaction, position, player)) return false;
            }
            map.ClearOutputs();
            return true;
        }

        internal void Resolve(SimulationWorld world)
        {
            if (!initialized || world == null) return;
            var write = 0;
            for (var index = 0; index < pendingRequestCount; index++)
            {
                var request = pendingRequests[index];
                if (request.Mode == RequestMode.Ground)
                {
                    if (!QueuePendingPickup(null, request.Definition, request.Transaction,
                            request.Position, request.Source, true))
                        pendingRequests[write++] = request;
                    continue;
                }

                if (!ResolveDefinition(world, request.Definition, request.Transaction,
                        request.Position, request.Source, 0))
                    pendingRequests[write++] = request;
            }
            Array.Clear(pendingRequests, write, pendingRequestCount - write);
            pendingRequestCount = write;
        }

        internal void TickPickups(SimulationWorld world)
        {
            if (!initialized || !world.Actors.TryRead(player.Handle, out var playerState)) return;
            var pickupRange = 0f;
            world.Actors.TryReadStat(player.Handle, BuiltInStatIndices.PickupRange, out pickupRange);
            var attractionSquared = pickupRange * pickupRange;
            for (var dense = 0; dense < world.Pickups.Count; dense++)
            {
                var handle = world.Pickups.GetHandleAt(dense);
                if (!TryGetPickupRecord(handle, out var record)) continue;
                var state = world.Pickups.GetStateAt(dense);
                var offset = playerState.Position - state.Position;
                var distanceSquared = offset.LengthSquared();
                var radius = record.Pickup == null ? DefaultGroundRadius : record.Pickup.Radius;
                if ((record.ForceCollect || distanceSquared <= radius * radius) && CanCollect(world, record))
                {
                    if (!TryQueue(record.Reward.Id, record.Transaction, state.Position, record.Source,
                            RequestMode.Direct, true) &&
                        !IsCommitted(record.Transaction))
                        continue;
                    activePickupTransactions.Remove(record.Transaction);
                    pickupRecords[handle.Index] = default;
                    ActivePickupCount--;
                    world.Commands.Remove(EntityKind.Pickup, handle);
                    world.QueueSkillTrigger(new SkillTriggerContext(
                        SkillTriggerEventType.OnPickup,
                        player,
                        new SpatialEntity(EntityKind.Pickup, handle),
                        state.Position,
                        Vector2.Zero,
                        record.Reward.Id,
                        default,
                        0));
                }
                else if ((record.ForceCollect || distanceSquared <= attractionSquared) && distanceSquared > 0f)
                {
                    state.Velocity = Vector2.Normalize(offset) * PickupAttractionSpeed;
                    world.Pickups.SetStateAt(dense, state);
                }
                else
                {
                    state.Velocity = Vector2.Zero;
                    world.Pickups.SetStateAt(dense, state);
                }
            }
        }

        internal void ApplyPendingPickups(SimulationWorld world)
        {
            for (var index = 0; index < pendingPickupCount; index++)
            {
                var pending = pendingPickups[index];
                if (!activePickupTransactions.TryAdd(pending.Transaction, 0))
                {
                    RejectedCapacity++;
                    continue;
                }
                var lifetime = pending.Pickup == null ? DefaultGroundLifetime : pending.Pickup.LifetimeSeconds;
                var handle = world.CreatePickup(SimulationEntityState.Create(
                    pending.Position,
                    Vector2.Zero,
                    0f,
                    lifetime));
                EnsurePickupRecordCapacity(handle.Index + 1);
                pickupRecords[handle.Index] = new PickupRecord
                {
                    Generation = handle.Generation,
                    Pickup = pending.Pickup,
                    Reward = pending.Reward,
                    Transaction = pending.Transaction,
                    Source = pending.Source,
                    Choice = pending.Choice
                };
                ActivePickupCount++;
                world.EmitEvent(SimulationEventType.Created, EntityKind.Pickup, handle, pending.Position);
            }
            Array.Clear(pendingPickups, 0, pendingPickupCount);
            pendingPickupCount = 0;
        }

        internal void OnPickupRemoved(EntityHandle handle)
        {
            if (!handle.IsValid || handle.Index >= pickupRecords.Length ||
                pickupRecords[handle.Index].Generation != handle.Generation)
                return;
            activePickupTransactions.Remove(pickupRecords[handle.Index].Transaction);
            pickupRecords[handle.Index] = default;
            if (ActivePickupCount > 0) ActivePickupCount--;
        }

        public RelicChoiceResolutionStatus SelectRelic(SimulationWorld world, ContentId relicId)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            var choice = CurrentRelicChoice;
            if (choice == null) return RelicChoiceResolutionStatus.NoPendingChoice;
            var candidate = false;
            for (var index = 0; index < choice.CandidateCount; index++)
                if (choice.GetCandidateAt(index) == relicId) { candidate = true; break; }
            if (!candidate || !TryGetRelic(relicId, out var relic))
                return RelicChoiceResolutionStatus.InvalidSelection;
            if (!IsRelicEligible(relic) || !CanCommit(choice.Transaction))
                return IsRelicEligible(relic)
                    ? RelicChoiceResolutionStatus.CapacityExceeded
                    : RelicChoiceResolutionStatus.NoLongerEligible;
            if (!ApplyRelicOutputs(world, relic, choice.Transaction) || !Relics.Acquire(relic) ||
                !TryCommit(choice.Transaction))
                return RelicChoiceResolutionStatus.NoLongerEligible;
            CurrentRelicChoice = null;
            return RelicChoiceResolutionStatus.Committed;
        }

        internal float ResolveDamageMultiplier(SpatialEntity source, SpatialEntity target, bool targetIsBoss)
        {
            if (!initialized || Relics == null) return 1f;
            var multiplier = 1f;
            if (source == player && targetIsBoss)
                multiplier += 0.25f * Relics.LevelForTag(BossDamageTag);
            if (target == player)
                multiplier += 0.15f * Relics.LevelForTag(IncomingRiskTag);
            return multiplier;
        }

        internal static int ComposeEntitySequence(EntityHandle handle, int salt)
        {
            unchecked
            {
                return ((handle.Generation & 0x7fff) << 16) |
                       ((handle.Index * 8 + Math.Max(0, salt)) & 0xffff);
            }
        }

        private bool TryQueue(
            ContentId rewardId,
            in RewardTransactionId transaction,
            Vector2 position,
            SpatialEntity source,
            RequestMode mode,
            bool allowActivePickup = false)
        {
            if (!initialized || !Finite(position) || IsCommitted(transaction) ||
                (!allowActivePickup && activePickupTransactions.ContainsKey(transaction))) return false;
            if (!catalog.TryGetDefinition(rewardId, out var definition) ||
                !(definition is RuntimeRewardDefinition reward)) return false;
            for (var index = 0; index < pendingRequestCount; index++)
            {
                if (!pendingRequests[index].Transaction.Equals(transaction)) continue;
                return pendingRequests[index].Definition.Id == rewardId;
            }
            if (pendingRequestCount >= pendingRequests.Length)
            {
                RejectedCapacity++;
                return false;
            }
            pendingRequests[pendingRequestCount++] = new PendingRequest
            {
                Definition = reward,
                Transaction = transaction,
                Position = position,
                Source = source,
                Mode = mode
            };
            return true;
        }

        private bool QueuePendingPickup(
            RuntimePickupDefinition pickup,
            RuntimeRewardDefinition reward,
            in RewardTransactionId transaction,
            Vector2 position,
            SpatialEntity source,
            bool choice)
        {
            if (pendingPickupCount >= pendingPickups.Length)
            {
                RejectedCapacity++;
                return false;
            }
            pendingPickups[pendingPickupCount++] = new PendingPickup
            {
                Pickup = pickup,
                Reward = reward,
                Transaction = transaction,
                Source = source,
                Position = position,
                Choice = choice || HasChoiceOperation(reward)
            };
            return true;
        }

        private bool ResolveDefinition(
            SimulationWorld world,
            RuntimeRewardDefinition definition,
            in RewardTransactionId transaction,
            Vector2 position,
            SpatialEntity source,
            int fallbackDepth)
        {
            if (IsCommitted(transaction)) return true;
            if (definition.RepeatPolicy == RewardRepeatPolicy.OncePerRun &&
                ContainsKey(definition.UniqueKey))
                return TryCommit(transaction);

            var hasRelicChoice = false;
            var hasEvolutionChoice = false;
            for (var index = 0; index < definition.Operations.Count; index++)
            {
                hasRelicChoice |= definition.Operations[index].Code == RewardOperationCode.GrantRelicChoice;
                hasEvolutionChoice |= definition.Operations[index].Code == RewardOperationCode.GrantEvolutionChoice;
            }
            if (hasRelicChoice)
            {
                var status = RequestRelicChoice(world, definition, transaction, position, source, fallbackDepth);
                return status != RelicChoiceRequestStatus.Busy && status != RelicChoiceRequestStatus.CapacityExceeded;
            }
            if (hasEvolutionChoice)
            {
                if (progression == null || !definition.FallbackRewardId.IsValid) return false;
                var status = progression.RewardChoices.RequestEvolutionChoice(
                    transaction,
                    definition.FallbackRewardId,
                    3);
                if (status == RewardChoiceRequestStatus.FallbackCommitted)
                    ExecuteFallback(world, definition.FallbackRewardId, transaction, position, source, fallbackDepth + 1);
                return status != RewardChoiceRequestStatus.Busy && status != RewardChoiceRequestStatus.CapacityExceeded;
            }

            var resultStart = resultEntryCount;
            if (!ExecuteOperations(world, definition, transaction, position, source, fallbackDepth))
            {
                resultEntryCount = resultStart;
                return false;
            }
            if (!TryCommit(transaction))
            {
                resultEntryCount = resultStart;
                return false;
            }
            if (definition.RepeatPolicy == RewardRepeatPolicy.OncePerRun)
                RecordKey(definition.UniqueKey);
            return true;
        }

        private bool ExecuteOperations(
            SimulationWorld world,
            RuntimeRewardDefinition definition,
            in RewardTransactionId transaction,
            Vector2 position,
            SpatialEntity source,
            int fallbackDepth)
        {
            for (var index = 0; index < definition.Operations.Count; index++)
            {
                var operation = definition.Operations[index];
                switch (operation.Code)
                {
                    case RewardOperationCode.Heal:
                        ApplyHealing(world, operation);
                        break;
                    case RewardOperationCode.ApplyStatus:
                        ApplyStatus(world, definition.Id, operation, position, source);
                        break;
                    case RewardOperationCode.DamageArea:
                        ApplyAreaDamage(world, definition.Id, operation, position, source);
                        break;
                    case RewardOperationCode.CollectEligiblePickups:
                        CollectEligiblePickups();
                        break;
                    case RewardOperationCode.AddCurrency:
                        if (!RecordResult(transaction, RewardDeltaKind.Currency,
                                ResultId(operation), Math.Max(1, operation.IntegerValue))) return false;
                        break;
                    case RewardOperationCode.UnlockContent:
                        if (!RecordResult(transaction, RewardDeltaKind.UnlockContent,
                                operation.ReferenceId, 1)) return false;
                        break;
                    case RewardOperationCode.GrantUnique:
                        if (HasUnique(operation.ReferenceId))
                        {
                            ExecuteFallback(world, definition.FallbackRewardId, transaction,
                                position, source, fallbackDepth + 1);
                            break;
                        }
                        if (!RecordResult(transaction, RewardDeltaKind.Unique,
                                operation.ReferenceId, 1)) return false;
                        break;
                    case RewardOperationCode.TriggerStory:
                        if (!RecordResult(transaction, RewardDeltaKind.Story,
                                operation.ReferenceId, 1)) return false;
                        break;
                    case RewardOperationCode.SpawnEnemy:
                        // EnemyRuntime owns the bounded structural SpawnEnemy operation.
                        break;
                }
            }
            return true;
        }

        private RelicChoiceRequestStatus RequestRelicChoice(
            SimulationWorld world,
            RuntimeRewardDefinition definition,
            in RewardTransactionId transaction,
            Vector2 position,
            SpatialEntity source,
            int fallbackDepth)
        {
            if (IsCommitted(transaction)) return RelicChoiceRequestStatus.AlreadyCommitted;
            if (CurrentRelicChoice != null)
                return CurrentRelicChoice.Transaction.Equals(transaction)
                    ? RelicChoiceRequestStatus.AlreadyPending
                    : RelicChoiceRequestStatus.Busy;
            if (!CanCommit(transaction)) return RelicChoiceRequestStatus.CapacityExceeded;

            var eligible = 0;
            for (var operationIndex = 0; operationIndex < definition.Operations.Count; operationIndex++)
            {
                var operation = definition.Operations[operationIndex];
                if (operation.Code != RewardOperationCode.GrantRelicChoice ||
                    !TryGetRelic(operation.ReferenceId, out var relic) ||
                    !IsRelicEligible(relic) || ContainsRelic(relicScratch, eligible, relic.Id))
                    continue;
                relicScratch[eligible++] = relic;
            }
            var count = Math.Min(3, eligible);
            if (count == 0)
            {
                if (definition.FallbackRewardId.IsValid)
                    ExecuteFallback(world, definition.FallbackRewardId, transaction,
                        position, source, fallbackDepth + 1);
                return TryCommit(transaction)
                    ? RelicChoiceRequestStatus.FallbackCommitted
                    : RelicChoiceRequestStatus.CapacityExceeded;
            }

            var candidates = new ContentId[count];
            for (var output = 0; output < count; output++)
            {
                var selected = rewardRandom.NextInt(eligible);
                candidates[output] = relicScratch[selected].Id;
                eligible--;
                relicScratch[selected] = relicScratch[eligible];
                relicScratch[eligible] = null;
            }
            CurrentRelicChoice = new RelicChoiceSnapshot(transaction, definition.FallbackRewardId, candidates);
            return RelicChoiceRequestStatus.ChoiceRequested;
        }

        private bool IsRelicEligible(RuntimeRelicDefinition relic)
        {
            if (relic == null || !Relics.CanAcquire(relic)) return false;
            for (var index = 0; index < relic.PrerequisiteIds.Count; index++)
            {
                var id = relic.PrerequisiteIds[index];
                if (!progression.Build.OwnsContent(id) && !Relics.TryGet(id, out _)) return false;
            }
            for (var index = 0; index < relic.MutuallyExclusiveIds.Count; index++)
                if (Relics.TryGet(relic.MutuallyExclusiveIds[index], out _)) return false;
            return true;
        }

        private bool ApplyRelicOutputs(
            SimulationWorld world,
            RuntimeRelicDefinition relic,
            in RewardTransactionId transaction)
        {
            for (var index = 0; index < relic.OutputIds.Count; index++)
            {
                var outputId = relic.OutputIds[index];
                if (!catalog.TryGetDefinition(outputId, out var output)) return false;
                if (output is RuntimeTraitDefinition)
                {
                    if (!progression.Build.GrantTrait(outputId)) return false;
                }
                else if (output is RuntimePassiveDefinition)
                {
                    if (!progression.Build.TryAcquirePassive(outputId)) return false;
                }
                else if (output is RuntimeSkillDefinition skill)
                {
                    if (!skill.IsExecutable || !catalog.TryGetIndex(outputId, out var indexValue) ||
                        !world.Skills.AddInstance(player, indexValue).IsSuccess) return false;
                }
                else if (output is RuntimeRewardDefinition)
                {
                    var sequence = checked(transaction.Sequence + index + 1);
                    if (!TryQueueDirect(outputId,
                            new RewardTransactionId(transaction.RunId, relic.Id, sequence),
                            Vector2.Zero,
                            player)) return false;
                }
                else
                {
                    return false;
                }
            }
            return true;
        }

        private void ApplyHealing(SimulationWorld world, in RewardOperation operation)
        {
            if (!world.Actors.TryGetCombat(player.Handle, out var actor) || actor.Dead || actor.DeathPending) return;
            actor.ReconcileHealthMaximum();
            var maximum = actor.Stats.Get(BuiltInStatIndices.Health);
            var amount = Math.Max(0f, operation.Value) +
                         maximum * Math.Max(0, operation.IntegerValue) / 10000f;
            var missing = Math.Max(0f, maximum - actor.HealthCurrent);
            world.Actors.TryApplyHealing(player.Handle, amount);
            var overflow = Math.Max(0f, amount - missing);
            if (overflow <= 0f || !Relics.HasTag(OverhealBarrierTag)) return;
            var cap = maximum * 0.20f * Math.Max(1, Relics.LevelForTag(OverhealBarrierTag));
            var grant = Math.Min(overflow, Math.Max(0f, cap - actor.ShieldCurrent));
            if (grant <= 0f) return;
            actor.ShieldMaximum = Math.Max(actor.ShieldMaximum, cap);
            actor.ShieldCurrent += grant;
        }

        private void ApplyStatus(
            SimulationWorld world,
            ContentId rewardId,
            in RewardOperation operation,
            Vector2 position,
            SpatialEntity source)
        {
            if (!catalog.TryGetIndex(operation.ReferenceId, out var statusIndex)) return;
            var strength = Math.Max(0f, operation.Value);
            if (operation.EligibilityTag == HostileAreaTag)
            {
                var radius = operation.IntegerValue > 0 ? operation.IntegerValue / 10f : 6f;
                world.SpatialGrid.QueryRadius(position, radius, spatialResults);
                for (var index = 0; index < spatialResults.Count; index++)
                {
                    var target = spatialResults[index].Entity;
                    if (target.Kind != EntityKind.Actor || !world.IsHostileTarget(player, target)) continue;
                    world.QueueStatus(new StatusApplicationRequest(
                        source.IsValid ? source : player,
                        target,
                        rewardId,
                        statusIndex,
                        strength,
                        0));
                }
                return;
            }
            world.QueueStatus(new StatusApplicationRequest(
                source.IsValid ? source : player,
                player,
                rewardId,
                statusIndex,
                strength,
                0));
        }

        private void ApplyAreaDamage(
            SimulationWorld world,
            ContentId rewardId,
            in RewardOperation operation,
            Vector2 position,
            SpatialEntity source)
        {
            var radius = operation.IntegerValue > 0 ? operation.IntegerValue / 10f : 6f;
            world.SpatialGrid.QueryRadius(position, radius, spatialResults);
            for (var index = 0; index < spatialResults.Count; index++)
            {
                var target = spatialResults[index].Entity;
                if (target.Kind != EntityKind.Actor || !world.IsHostileTarget(player, target)) continue;
                var direction = spatialResults[index].Position - position;
                if (direction.LengthSquared() > 0f) direction = Vector2.Normalize(direction) * 5f;
                world.QueueDamage(new DamagePacket(
                    source.IsValid ? source : player,
                    target,
                    rewardId,
                    DamageType.Lightning,
                    DamageTags.Direct | DamageTags.Secondary,
                    Math.Max(0f, operation.Value),
                    false,
                    0f,
                    direction,
                    position,
                    1,
                    BuiltInDamageChannels.Direct,
                    0));
            }
        }

        private void CollectEligiblePickups()
        {
            for (var index = 0; index < pickupRecords.Length; index++)
            {
                var record = pickupRecords[index];
                if (record.Generation == 0 || record.Choice ||
                    HasTag(record.Pickup, UniqueTag) ||
                    HasTag(record.Pickup, ObjectiveLockedTag) ||
                    HasTag(record.Pickup, ChoiceTag))
                    continue;
                record.ForceCollect = true;
                pickupRecords[index] = record;
            }
        }

        private bool CanCollect(SimulationWorld world, in PickupRecord record)
        {
            var onlyHeal = true;
            var hasHeal = false;
            for (var index = 0; index < record.Reward.Operations.Count; index++)
            {
                var code = record.Reward.Operations[index].Code;
                hasHeal |= code == RewardOperationCode.Heal;
                onlyHeal &= code == RewardOperationCode.Heal;
            }
            if (!hasHeal || !onlyHeal) return true;
            if (!world.Actors.TryReadHealth(player.Handle, out var health)) return false;
            if (health.Current < health.Maximum) return true;
            if (!Relics.HasTag(OverhealBarrierTag) ||
                !world.Actors.TryReadShield(player.Handle, out var shield)) return false;
            var cap = health.Maximum * 0.20f * Math.Max(1, Relics.LevelForTag(OverhealBarrierTag));
            return shield.Current < cap;
        }

        private void ExecuteFallback(
            SimulationWorld world,
            ContentId fallbackId,
            in RewardTransactionId transaction,
            Vector2 position,
            SpatialEntity source,
            int depth)
        {
            if (depth > 3 || !fallbackId.IsValid ||
                !catalog.TryGetDefinition(fallbackId, out var fallback) ||
                !(fallback is RuntimeRewardDefinition reward)) return;
            ExecuteOperations(world, reward, transaction, position, source, depth);
        }

        private bool RecordResult(
            in RewardTransactionId transaction,
            RewardDeltaKind kind,
            ContentId contentId,
            int amount)
        {
            if (!contentId.IsValid || resultEntryCount >= resultEntries.Length)
            {
                RejectedCapacity++;
                return false;
            }
            resultEntries[resultEntryCount++] = new RewardResultEntry(transaction, kind, contentId, amount);
            return true;
        }

        private bool HasUnique(ContentId id)
        {
            if (!id.IsValid) return false;
            if (Array.BinarySearch(ownedUniqueRewards, id) >= 0) return true;
            for (var index = 0; index < resultEntryCount; index++)
                if (resultEntries[index].Kind == RewardDeltaKind.Unique &&
                    resultEntries[index].ContentId == id) return true;
            return false;
        }

        private bool ContainsKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return false;
            for (var index = 0; index < appliedOncePerRunCount; index++)
                if (string.Equals(appliedOncePerRunKeys[index], key, StringComparison.Ordinal)) return true;
            return false;
        }

        private void RecordKey(string key)
        {
            if (ContainsKey(key) || appliedOncePerRunCount >= appliedOncePerRunKeys.Length) return;
            appliedOncePerRunKeys[appliedOncePerRunCount++] = key;
        }

        private bool TryGetRelic(ContentId id, out RuntimeRelicDefinition relic)
        {
            for (var index = 0; index < relicDefinitions.Length; index++)
            {
                if (relicDefinitions[index].Id != id) continue;
                relic = relicDefinitions[index];
                return true;
            }
            relic = null;
            return false;
        }

        private bool HasPending(in RewardTransactionId transaction)
        {
            for (var index = 0; index < pendingRequestCount; index++)
                if (pendingRequests[index].Transaction.Equals(transaction)) return true;
            for (var index = 0; index < pendingPickupCount; index++)
                if (pendingPickups[index].Transaction.Equals(transaction)) return true;
            return activePickupTransactions.ContainsKey(transaction);
        }

        private bool TryGetPickupRecord(EntityHandle handle, out PickupRecord record)
        {
            if (handle.IsValid && handle.Index < pickupRecords.Length)
            {
                record = pickupRecords[handle.Index];
                if (record.Generation == handle.Generation && record.Reward != null) return true;
            }
            record = default;
            return false;
        }

        private void EnsurePickupRecordCapacity(int required)
        {
            if (required <= pickupRecords.Length) return;
            var capacity = pickupRecords.Length;
            while (capacity < required) capacity *= 2;
            Array.Resize(ref pickupRecords, capacity);
            PickupCapacityGrowthCount++;
        }

        private static bool HasChoiceOperation(RuntimeRewardDefinition definition)
        {
            for (var index = 0; index < definition.Operations.Count; index++)
                if (definition.Operations[index].Code == RewardOperationCode.GrantRelicChoice ||
                    definition.Operations[index].Code == RewardOperationCode.GrantEvolutionChoice)
                    return true;
            return false;
        }

        private static bool ContainsRelic(RuntimeRelicDefinition[] values, int count, ContentId id)
        {
            for (var index = 0; index < count; index++)
                if (values[index].Id == id) return true;
            return false;
        }

        private static bool HasTag(RuntimePickupDefinition definition, ContentTag tag)
        {
            if (definition == null) return false;
            if (definition.EligibilityTag == tag) return true;
            for (var index = 0; index < definition.Tags.Count; index++)
                if (definition.Tags[index] == tag) return true;
            return false;
        }

        private static ContentId ResultId(in RewardOperation operation)
        {
            if (operation.ReferenceId.IsValid) return operation.ReferenceId;
            return operation.EligibilityTag.IsValid
                ? ContentId.Create(operation.EligibilityTag.Value).Value
                : default;
        }

        private static ContentTag Tag(string value) => ContentTag.Create(value).Value;

        private static bool Finite(Vector2 value) =>
            !float.IsNaN(value.X) && !float.IsInfinity(value.X) &&
            !float.IsNaN(value.Y) && !float.IsInfinity(value.Y);
    }
}
