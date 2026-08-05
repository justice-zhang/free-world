using System;
using System.Numerics;
using Game.Content.Runtime;
using Game.Core;

namespace Game.Simulation
{
    public enum MapRuntimeEntryKind : byte
    {
        Objective = 1,
        Event = 2,
        LandmarkReward = 3,
        LandmarkStory = 4
    }

    public enum MapCommandStatus : byte
    {
        Applied = 1,
        AlreadyApplied = 2,
        NotFound = 3,
        InvalidState = 4,
        OutOfRange = 5,
        InvalidRequest = 6,
        CapacityExceeded = 7
    }

    public enum LandmarkState : byte
    {
        Undiscovered = 1,
        Discovered = 2,
        Claimed = 3
    }

    public readonly struct MapOutputRequest
    {
        internal MapOutputRequest(
            in RewardTransactionId transaction,
            ContentId outputId,
            ContentId anchorId,
            MapRuntimeEntryKind sourceKind)
        {
            Transaction = transaction;
            OutputId = outputId;
            AnchorId = anchorId;
            SourceKind = sourceKind;
        }

        public RewardTransactionId Transaction { get; }
        public ContentId SourceId => Transaction.SourceStableId;
        public ContentId OutputId { get; }
        public ContentId AnchorId { get; }
        public MapRuntimeEntryKind SourceKind { get; }
    }

    public readonly struct MapObjectiveSnapshot
    {
        internal MapObjectiveSnapshot(
            ContentId id,
            ObjectiveState state,
            float progress,
            ContentId activeAnchorId,
            SpatialEntity activator,
            ContentId outputId)
        {
            Id = id;
            State = state;
            Progress = progress;
            ActiveAnchorId = activeAnchorId;
            Activator = activator;
            OutputId = outputId;
        }

        public ContentId Id { get; }
        public ObjectiveState State { get; }
        public float Progress { get; }
        public ContentId ActiveAnchorId { get; }
        public SpatialEntity Activator { get; }
        public ContentId OutputId { get; }
    }

    public readonly struct MapEventSnapshot
    {
        internal MapEventSnapshot(
            ContentId id,
            ObjectiveState state,
            bool eligible,
            float progress,
            ContentId activeAnchorId,
            float triggerStartSeconds,
            float triggerEndSeconds,
            ContentId outputId)
        {
            Id = id;
            State = state;
            Eligible = eligible;
            Progress = progress;
            ActiveAnchorId = activeAnchorId;
            TriggerStartSeconds = triggerStartSeconds;
            TriggerEndSeconds = triggerEndSeconds;
            OutputId = outputId;
        }

        public ContentId Id { get; }
        public ObjectiveState State { get; }
        public bool Eligible { get; }
        public float Progress { get; }
        public ContentId ActiveAnchorId { get; }
        public float TriggerStartSeconds { get; }
        public float TriggerEndSeconds { get; }
        public ContentId OutputId { get; }
    }

    public readonly struct LandmarkSnapshot
    {
        internal LandmarkSnapshot(
            ContentId id,
            LandmarkState state,
            ContentId anchorId,
            ContentId rewardId,
            ContentId storyId,
            bool repeatable,
            int claimCount)
        {
            Id = id;
            State = state;
            AnchorId = anchorId;
            RewardId = rewardId;
            StoryId = storyId;
            Repeatable = repeatable;
            ClaimCount = claimCount;
        }

        public ContentId Id { get; }
        public LandmarkState State { get; }
        public ContentId AnchorId { get; }
        public ContentId RewardId { get; }
        public ContentId StoryId { get; }
        public bool Repeatable { get; }
        public int ClaimCount { get; }
    }

    /// <summary>
    /// Pure run-local owner of map objective, event, and landmark state. Scene bindings
    /// are presentation-only; all commands are validated against baked stable anchors.
    /// </summary>
    public sealed class MapObjectiveRuntime
    {
        private const ulong MapEventStreamId = 0x4D41504556454E54UL;
        private const float DefaultLandmarkDiscoveryRadius = 2.5f;

        private struct ObjectiveEntry
        {
            public RuntimeMapObjectiveDefinition Definition;
            public ContentId Id;
            public ObjectiveState State;
            public float Progress;
            public ContentId ActiveAnchorId;
            public SpatialEntity Activator;
            public int Sequence;
        }

        private struct EventEntry
        {
            public RuntimeMapEventDefinition Definition;
            public ObjectiveState State;
            public bool Eligible;
            public float Progress;
            public ContentId ActiveAnchorId;
            public int Sequence;
        }

        private struct LandmarkEntry
        {
            public RuntimeLandmarkDefinition Definition;
            public LandmarkState State;
            public int ClaimCount;
            public int Sequence;
        }

        private readonly ObjectiveEntry[] objectives;
        private readonly EventEntry[] events;
        private readonly LandmarkEntry[] landmarks;
        private readonly MapOutputRequest[] outputs;
        private readonly int[] eventCandidates;
        private RuntimeMapDefinition map;
        private RandomStream eventRandom;
        private ulong runId;
        private int objectiveCount;
        private int eventCount;
        private int landmarkCount;
        private int outputCount;

        public MapObjectiveRuntime(int capacity = 32)
            : this(capacity, 16, 32, 64)
        {
        }

        public MapObjectiveRuntime(
            int objectiveCapacity,
            int eventCapacity,
            int landmarkCapacity,
            int outputCapacity)
        {
            if (objectiveCapacity < 1 || eventCapacity < 1 || landmarkCapacity < 1 || outputCapacity < 1)
                throw new ArgumentOutOfRangeException(nameof(objectiveCapacity));
            objectives = new ObjectiveEntry[objectiveCapacity];
            events = new EventEntry[eventCapacity];
            landmarks = new LandmarkEntry[landmarkCapacity];
            outputs = new MapOutputRequest[outputCapacity];
            eventCandidates = new int[eventCapacity];
        }

        public bool IsInitialized => map != null;
        public ContentId MapId => map == null ? default : map.Id;
        public int ObjectiveCount => objectiveCount;
        public int EventCount => eventCount;
        public int LandmarkCount => landmarkCount;
        public int OutputCount => outputCount;
        public ulong EventStreamSeed => eventRandom.RootSeed;
        public ulong EventRandomCalls => eventRandom.Calls;
        public ulong CompletedObjectiveMask
        {
            get
            {
                var mask = 0UL;
                for (var index = 0; index < objectiveCount && index < 64; index++)
                    if (objectives[index].State == ObjectiveState.Completed) mask |= 1UL << index;
                return mask;
            }
        }

        public Result<bool> Initialize(ContentRegistry registry, ContentId mapId, ulong mapRunId)
        {
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            if (!mapId.IsValid || !registry.TryGet(mapId, out RuntimeMapDefinition definition) ||
                !definition.HasM5Data)
                return Failure("Map runtime requires a registered schema-4+ map.", mapId);
            if (definition.ObjectiveIds.Count > objectives.Length ||
                definition.EventIds.Count > events.Length ||
                definition.LandmarkIds.Count > landmarks.Length)
                return Failure("Map content exceeds configured objective, event, or landmark capacity.", mapId);

            Reset();
            map = definition;
            runId = mapRunId;
            eventRandom = new RandomStream(mapRunId).Derive(MapEventStreamId);

            for (var index = 0; index < definition.ObjectiveIds.Count; index++)
            {
                if (!registry.TryGet(definition.ObjectiveIds[index], out RuntimeMapObjectiveDefinition objective) ||
                    !HasValidAnchors(objective.AnchorIds))
                {
                    Reset();
                    return Failure("Map objective is missing or references an unavailable/non-walkable anchor.",
                        definition.ObjectiveIds[index]);
                }
                objectives[objectiveCount++] = new ObjectiveEntry
                {
                    Definition = objective,
                    Id = objective.Id,
                    State = ObjectiveState.Hidden
                };
            }

            for (var index = 0; index < definition.EventIds.Count; index++)
            {
                if (!registry.TryGet(definition.EventIds[index], out RuntimeMapEventDefinition mapEvent) ||
                    !HasValidAnchors(mapEvent.AnchorIds))
                {
                    Reset();
                    return Failure("Map event is missing or references an unavailable/non-walkable anchor.",
                        definition.EventIds[index]);
                }
                events[eventCount++] = new EventEntry
                {
                    Definition = mapEvent,
                    State = ObjectiveState.Hidden
                };
            }

            for (var index = 0; index < definition.LandmarkIds.Count; index++)
            {
                if (!registry.TryGet(definition.LandmarkIds[index], out RuntimeLandmarkDefinition landmark) ||
                    !TryGetAnchorPosition(landmark.AnchorId, out var position) || !IsWalkable(position))
                {
                    Reset();
                    return Failure("Map landmark is missing or references an unavailable/non-walkable anchor.",
                        definition.LandmarkIds[index]);
                }
                landmarks[landmarkCount++] = new LandmarkEntry
                {
                    Definition = landmark,
                    State = LandmarkState.Undiscovered
                };
            }

            return Result<bool>.Success(true);
        }

        public bool TryAdd(ContentId id, ObjectiveState initialState = ObjectiveState.Hidden)
        {
            if (IsInitialized || !id.IsValid || objectiveCount >= objectives.Length) return false;
            for (var index = 0; index < objectiveCount; index++)
                if (objectives[index].Id == id) return false;
            objectives[objectiveCount++] = new ObjectiveEntry { Id = id, State = initialState };
            return true;
        }

        public bool TryTransition(ContentId id, ObjectiveState expected, ObjectiveState next)
        {
            if (IsInitialized) return false;
            var index = FindObjective(id);
            if (index < 0 || objectives[index].State != expected ||
                !CanTransition(objectives[index].Definition, expected, next))
                return false;
            objectives[index].State = next;
            return true;
        }

        public bool TryGetState(ContentId id, out ObjectiveState state)
        {
            var index = FindObjective(id);
            if (index >= 0)
            {
                state = objectives[index].State;
                return true;
            }
            state = default;
            return false;
        }

        public MapObjectiveSnapshot GetObjectiveAt(int index)
        {
            if (index < 0 || index >= objectiveCount) throw new ArgumentOutOfRangeException(nameof(index));
            var entry = objectives[index];
            return new MapObjectiveSnapshot(
                entry.Id,
                entry.State,
                entry.Progress,
                entry.ActiveAnchorId,
                entry.Activator,
                entry.Definition == null ? default : entry.Definition.OutputId);
        }

        public MapEventSnapshot GetEventAt(int index)
        {
            if (index < 0 || index >= eventCount) throw new ArgumentOutOfRangeException(nameof(index));
            var entry = events[index];
            return new MapEventSnapshot(
                entry.Definition.Id,
                entry.State,
                entry.Eligible,
                entry.Progress,
                entry.ActiveAnchorId,
                entry.Definition.TriggerStartSeconds,
                entry.Definition.TriggerEndSeconds,
                entry.Definition.OutputId);
        }

        public LandmarkSnapshot GetLandmarkAt(int index)
        {
            if (index < 0 || index >= landmarkCount) throw new ArgumentOutOfRangeException(nameof(index));
            var entry = landmarks[index];
            return new LandmarkSnapshot(
                entry.Definition.Id,
                entry.State,
                entry.Definition.AnchorId,
                entry.Definition.RewardId,
                entry.Definition.StoryId,
                entry.Definition.Repeatable,
                entry.ClaimCount);
        }

        public MapOutputRequest GetOutputAt(int index)
        {
            if (index < 0 || index >= outputCount) throw new ArgumentOutOfRangeException(nameof(index));
            return outputs[index];
        }

        public void ClearOutputs()
        {
            Array.Clear(outputs, 0, outputCount);
            outputCount = 0;
        }

        public MapCommandStatus RevealObjective(ContentId id)
        {
            return TransitionObjective(id, ObjectiveState.Hidden, ObjectiveState.Revealed);
        }

        public MapCommandStatus MakeObjectiveAvailable(ContentId id)
        {
            return TransitionObjective(id, ObjectiveState.Revealed, ObjectiveState.Available);
        }

        public MapCommandStatus ActivateObjective(
            ContentId id,
            in SpatialEntity activator,
            Vector2 activatorPosition,
            float maximumDistance)
        {
            if (!activator.IsValid || !Finite(activatorPosition) || !Finite(maximumDistance) || maximumDistance < 0f)
                return MapCommandStatus.InvalidRequest;
            var index = FindObjective(id);
            if (index < 0) return MapCommandStatus.NotFound;
            ref var entry = ref objectives[index];
            if (entry.State != ObjectiveState.Available) return MapCommandStatus.InvalidState;
            if (!TryFindNearestAnchor(entry.Definition.AnchorIds, activatorPosition, maximumDistance,
                    out var anchorId))
                return MapCommandStatus.OutOfRange;
            if (!CanTransition(entry.Definition, ObjectiveState.Available, ObjectiveState.Activating))
                return MapCommandStatus.InvalidState;
            entry.State = ObjectiveState.Activating;
            entry.ActiveAnchorId = anchorId;
            entry.Activator = activator;
            entry.Progress = 0f;
            return MapCommandStatus.Applied;
        }

        public MapCommandStatus BeginObjectiveDefense(ContentId id)
        {
            return TransitionObjective(id, ObjectiveState.Activating, ObjectiveState.Defending);
        }

        public MapCommandStatus ReportObjectiveProgress(ContentId id, float normalizedDelta)
        {
            if (!Finite(normalizedDelta) || normalizedDelta <= 0f) return MapCommandStatus.InvalidRequest;
            var index = FindObjective(id);
            if (index < 0) return MapCommandStatus.NotFound;
            ref var entry = ref objectives[index];
            if (entry.State == ObjectiveState.Completed) return MapCommandStatus.AlreadyApplied;
            if (entry.State != ObjectiveState.Defending) return MapCommandStatus.InvalidState;
            var next = Math.Min(1f, entry.Progress + normalizedDelta);
            if (next < 1f)
            {
                entry.Progress = next;
                return MapCommandStatus.Applied;
            }
            if (!CanTransition(entry.Definition, ObjectiveState.Defending, ObjectiveState.Completed))
                return MapCommandStatus.InvalidState;
            if (!CanReserveOutputs(1)) return MapCommandStatus.CapacityExceeded;
            entry.Progress = 1f;
            entry.State = ObjectiveState.Completed;
            QueueOutput(
                entry.Id,
                entry.Sequence++,
                entry.Definition.OutputId,
                entry.ActiveAnchorId,
                MapRuntimeEntryKind.Objective);
            return MapCommandStatus.Applied;
        }

        public MapCommandStatus InterruptObjective(ContentId id)
        {
            var index = FindObjective(id);
            if (index < 0) return MapCommandStatus.NotFound;
            ref var entry = ref objectives[index];
            if ((entry.State != ObjectiveState.Activating && entry.State != ObjectiveState.Defending) ||
                !CanTransition(entry.Definition, entry.State, ObjectiveState.Available))
                return MapCommandStatus.InvalidState;
            entry.State = ObjectiveState.Available;
            entry.Progress = 0f;
            entry.ActiveAnchorId = default;
            entry.Activator = default;
            return MapCommandStatus.Applied;
        }

        public MapCommandStatus ArmEvent(ContentId id)
        {
            var index = FindEvent(id);
            if (index < 0) return MapCommandStatus.NotFound;
            ref var entry = ref events[index];
            if (entry.State == ObjectiveState.Completed || entry.Eligible)
                return MapCommandStatus.AlreadyApplied;
            if (entry.State != ObjectiveState.Hidden ||
                !CanTransition(entry.Definition, ObjectiveState.Hidden, ObjectiveState.Revealed) ||
                !CanTransition(entry.Definition, ObjectiveState.Revealed, ObjectiveState.Available))
                return MapCommandStatus.InvalidState;
            entry.State = ObjectiveState.Available;
            entry.Eligible = true;
            return MapCommandStatus.Applied;
        }

        public void AdvanceEvents(float elapsedSeconds)
        {
            if (!IsInitialized || !Finite(elapsedSeconds) || elapsedSeconds < 0f || HasActiveEvent()) return;
            var candidateCount = 0;
            for (var index = 0; index < eventCount; index++)
            {
                var entry = events[index];
                if (entry.Eligible && entry.State == ObjectiveState.Available &&
                    elapsedSeconds >= entry.Definition.TriggerStartSeconds &&
                    elapsedSeconds <= entry.Definition.TriggerEndSeconds)
                    eventCandidates[candidateCount++] = index;
            }
            if (candidateCount == 0) return;
            var selected = eventCandidates[eventRandom.NextInt(candidateCount)];
            ref var active = ref events[selected];
            if (!TransitionEvent(ref active, ObjectiveState.Available, ObjectiveState.Activating)) return;
            var anchorIndex = eventRandom.NextInt(active.Definition.AnchorIds.Count);
            active.ActiveAnchorId = active.Definition.AnchorIds[anchorIndex];
            active.Progress = 0f;
            TransitionEvent(ref active, ObjectiveState.Activating, ObjectiveState.Defending);
        }

        public MapCommandStatus ReportEventProgress(ContentId id, float normalizedDelta)
        {
            if (!Finite(normalizedDelta) || normalizedDelta <= 0f) return MapCommandStatus.InvalidRequest;
            var index = FindEvent(id);
            if (index < 0) return MapCommandStatus.NotFound;
            ref var entry = ref events[index];
            if (entry.State == ObjectiveState.Completed) return MapCommandStatus.AlreadyApplied;
            if (entry.State != ObjectiveState.Defending) return MapCommandStatus.InvalidState;
            var next = Math.Min(1f, entry.Progress + normalizedDelta);
            if (next < 1f)
            {
                entry.Progress = next;
                return MapCommandStatus.Applied;
            }

            var outputIsObjective = FindObjective(entry.Definition.OutputId) >= 0;
            if (!outputIsObjective && !CanReserveOutputs(1)) return MapCommandStatus.CapacityExceeded;
            if (!TransitionEvent(ref entry, ObjectiveState.Defending, ObjectiveState.Completed))
                return MapCommandStatus.InvalidState;
            entry.Progress = 1f;
            if (outputIsObjective)
            {
                UnlockObjective(entry.Definition.OutputId);
            }
            else
            {
                QueueOutput(
                    entry.Definition.Id,
                    entry.Sequence++,
                    entry.Definition.OutputId,
                    entry.ActiveAnchorId,
                    MapRuntimeEntryKind.Event);
            }
            return MapCommandStatus.Applied;
        }

        public MapCommandStatus InterruptEvent(ContentId id)
        {
            var index = FindEvent(id);
            if (index < 0) return MapCommandStatus.NotFound;
            ref var entry = ref events[index];
            if ((entry.State != ObjectiveState.Activating && entry.State != ObjectiveState.Defending) ||
                !TransitionEvent(ref entry, entry.State, ObjectiveState.Available))
                return MapCommandStatus.InvalidState;
            entry.Progress = 0f;
            entry.ActiveAnchorId = default;
            return MapCommandStatus.Applied;
        }

        public int UpdateLandmarkDiscovery(Vector2 playerPosition, float radius = DefaultLandmarkDiscoveryRadius)
        {
            if (!IsInitialized || !Finite(playerPosition) || !Finite(radius) || radius < 0f) return 0;
            var radiusSquared = radius * radius;
            var discovered = 0;
            for (var index = 0; index < landmarkCount; index++)
            {
                ref var entry = ref landmarks[index];
                if (entry.State != LandmarkState.Undiscovered ||
                    !TryGetAnchorPosition(entry.Definition.AnchorId, out var position) ||
                    Vector2.DistanceSquared(playerPosition, position) > radiusSquared)
                    continue;
                entry.State = LandmarkState.Discovered;
                discovered++;
            }
            return discovered;
        }

        public MapCommandStatus DiscoverLandmark(ContentId id)
        {
            var index = FindLandmark(id);
            if (index < 0) return MapCommandStatus.NotFound;
            ref var entry = ref landmarks[index];
            if (entry.State != LandmarkState.Undiscovered) return MapCommandStatus.AlreadyApplied;
            entry.State = LandmarkState.Discovered;
            return MapCommandStatus.Applied;
        }

        public MapCommandStatus ClaimLandmark(ContentId id)
        {
            var index = FindLandmark(id);
            if (index < 0) return MapCommandStatus.NotFound;
            ref var entry = ref landmarks[index];
            if (entry.State == LandmarkState.Claimed && !entry.Definition.Repeatable)
                return MapCommandStatus.AlreadyApplied;
            if (entry.State != LandmarkState.Discovered) return MapCommandStatus.InvalidState;
            var required = (entry.Definition.RewardId.IsValid ? 1 : 0) +
                           (entry.Definition.StoryId.IsValid ? 1 : 0);
            if (!CanReserveOutputs(required)) return MapCommandStatus.CapacityExceeded;
            if (entry.Definition.RewardId.IsValid)
                QueueOutput(
                    entry.Definition.Id,
                    entry.Sequence++,
                    entry.Definition.RewardId,
                    entry.Definition.AnchorId,
                    MapRuntimeEntryKind.LandmarkReward);
            if (entry.Definition.StoryId.IsValid)
                QueueOutput(
                    entry.Definition.Id,
                    entry.Sequence++,
                    entry.Definition.StoryId,
                    entry.Definition.AnchorId,
                    MapRuntimeEntryKind.LandmarkStory);
            entry.ClaimCount++;
            entry.State = entry.Definition.Repeatable
                ? LandmarkState.Discovered
                : LandmarkState.Claimed;
            return MapCommandStatus.Applied;
        }

        public bool IsObjectiveCompleted(ContentId id)
        {
            var index = FindObjective(id);
            return index >= 0 && objectives[index].State == ObjectiveState.Completed;
        }

        private MapCommandStatus TransitionObjective(ContentId id, ObjectiveState expected, ObjectiveState next)
        {
            var index = FindObjective(id);
            if (index < 0) return MapCommandStatus.NotFound;
            ref var entry = ref objectives[index];
            if (entry.State == next) return MapCommandStatus.AlreadyApplied;
            if (entry.State != expected || !CanTransition(entry.Definition, expected, next))
                return MapCommandStatus.InvalidState;
            entry.State = next;
            return MapCommandStatus.Applied;
        }

        private void UnlockObjective(ContentId id)
        {
            var index = FindObjective(id);
            if (index < 0) return;
            ref var entry = ref objectives[index];
            if (entry.State == ObjectiveState.Hidden &&
                CanTransition(entry.Definition, ObjectiveState.Hidden, ObjectiveState.Revealed))
                entry.State = ObjectiveState.Revealed;
            if (entry.State == ObjectiveState.Revealed &&
                CanTransition(entry.Definition, ObjectiveState.Revealed, ObjectiveState.Available))
                entry.State = ObjectiveState.Available;
        }

        private bool HasActiveEvent()
        {
            for (var index = 0; index < eventCount; index++)
                if (events[index].State == ObjectiveState.Activating ||
                    events[index].State == ObjectiveState.Defending)
                    return true;
            return false;
        }

        private int FindObjective(ContentId id)
        {
            for (var index = 0; index < objectiveCount; index++)
                if (objectives[index].Id == id) return index;
            return -1;
        }

        private int FindEvent(ContentId id)
        {
            for (var index = 0; index < eventCount; index++)
                if (events[index].Definition.Id == id) return index;
            return -1;
        }

        private int FindLandmark(ContentId id)
        {
            for (var index = 0; index < landmarkCount; index++)
                if (landmarks[index].Definition.Id == id) return index;
            return -1;
        }

        private bool HasValidAnchors(System.Collections.Generic.IReadOnlyList<ContentId> anchorIds)
        {
            if (anchorIds.Count == 0) return false;
            for (var index = 0; index < anchorIds.Count; index++)
                if (!TryGetAnchorPosition(anchorIds[index], out var position) || !IsWalkable(position))
                    return false;
            return true;
        }

        private bool TryFindNearestAnchor(
            System.Collections.Generic.IReadOnlyList<ContentId> anchorIds,
            Vector2 position,
            float maximumDistance,
            out ContentId anchorId)
        {
            var bestDistance = maximumDistance * maximumDistance;
            var found = false;
            anchorId = default;
            for (var index = 0; index < anchorIds.Count; index++)
            {
                if (!TryGetAnchorPosition(anchorIds[index], out var candidate)) continue;
                var distance = Vector2.DistanceSquared(position, candidate);
                if (distance > bestDistance) continue;
                if (!found || distance < bestDistance)
                {
                    found = true;
                    bestDistance = distance;
                    anchorId = anchorIds[index];
                }
            }
            return found;
        }

        private bool TryGetAnchorPosition(ContentId id, out Vector2 position)
        {
            if (map != null)
            {
                for (var index = 0; index < map.Anchors.Count; index++)
                {
                    if (map.Anchors[index].Id != id) continue;
                    position = map.Anchors[index].Position;
                    return true;
                }
            }
            position = default;
            return false;
        }

        private bool IsWalkable(Vector2 position)
        {
            if (map == null || !Finite(position) ||
                (map.BoundsMode == MapBoundsMode.Finite &&
                 (position.X < map.Minimum.X || position.X > map.Maximum.X ||
                  position.Y < map.Minimum.Y || position.Y > map.Maximum.Y)))
                return false;
            for (var index = 0; index < map.Obstacles.Count; index++)
            {
                var obstacle = map.Obstacles[index];
                if (position.X >= obstacle.Minimum.X && position.X <= obstacle.Maximum.X &&
                    position.Y >= obstacle.Minimum.Y && position.Y <= obstacle.Maximum.Y)
                    return false;
            }
            return true;
        }

        private bool CanReserveOutputs(int count) => count >= 0 && outputCount + count <= outputs.Length;

        private void QueueOutput(
            ContentId sourceId,
            int sequence,
            ContentId outputId,
            ContentId anchorId,
            MapRuntimeEntryKind sourceKind)
        {
            outputs[outputCount++] = new MapOutputRequest(
                new RewardTransactionId(runId, sourceId, sequence),
                outputId,
                anchorId,
                sourceKind);
        }

        private static bool TransitionEvent(
            ref EventEntry entry,
            ObjectiveState expected,
            ObjectiveState next)
        {
            if (entry.State != expected || !CanTransition(entry.Definition, expected, next)) return false;
            entry.State = next;
            return true;
        }

        private static bool CanTransition(
            RuntimeStateGraphDefinition definition,
            ObjectiveState from,
            ObjectiveState to)
        {
            if (!IsLegalTransition(from, to)) return false;
            if (definition == null) return true;
            for (var index = 0; index < definition.StateTransitions.Count; index++)
            {
                var transition = definition.StateTransitions[index];
                if (transition.From == from && transition.To == to) return true;
            }
            return false;
        }

        private static bool IsLegalTransition(ObjectiveState from, ObjectiveState to)
        {
            return (from == ObjectiveState.Hidden && to == ObjectiveState.Revealed) ||
                   (from == ObjectiveState.Revealed && to == ObjectiveState.Available) ||
                   (from == ObjectiveState.Available && to == ObjectiveState.Activating) ||
                   (from == ObjectiveState.Activating &&
                    (to == ObjectiveState.Defending || to == ObjectiveState.Available)) ||
                   (from == ObjectiveState.Defending &&
                    (to == ObjectiveState.Completed || to == ObjectiveState.Available));
        }

        private void Reset()
        {
            Array.Clear(objectives, 0, objectives.Length);
            Array.Clear(events, 0, events.Length);
            Array.Clear(landmarks, 0, landmarks.Length);
            Array.Clear(outputs, 0, outputs.Length);
            map = null;
            runId = 0UL;
            objectiveCount = 0;
            eventCount = 0;
            landmarkCount = 0;
            outputCount = 0;
            eventRandom = default;
        }

        private static Result<bool> Failure(string message, ContentId id)
        {
            return Result<bool>.Failure(new Error(ErrorCode.InvalidAuthoringData, message, id));
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool Finite(Vector2 value) => Finite(value.X) && Finite(value.Y);
    }
}
