using System;
using Game.Content.Runtime;
using Game.Core;

namespace Game.Simulation
{
    public enum RewardChoiceRequestStatus : byte
    {
        ChoiceRequested = 1,
        FallbackCommitted = 2,
        AlreadyPending = 3,
        AlreadyCommitted = 4,
        Busy = 5,
        InvalidRequest = 6,
        CapacityExceeded = 7
    }

    public enum RewardChoiceResolutionStatus : byte
    {
        Committed = 1,
        NoPendingChoice = 2,
        InvalidSelection = 3,
        NoLongerEligible = 4,
        CapacityExceeded = 5
    }

    internal enum RewardChoiceHistoryAction : byte
    {
        Requested = 1,
        Selected = 2,
        Fallback = 3,
        ReplayRejected = 4
    }

    /// <summary>Immutable replay evidence for one controlled reward-choice action.</summary>
    internal readonly struct RewardChoiceHistoryEntry
    {
        internal RewardChoiceHistoryEntry(
            int sequence,
            RewardChoiceHistoryAction action,
            in RewardTransactionId transaction,
            ContentId resolvedId,
            ulong callsBefore,
            ulong callsAfter,
            int candidateCount)
        {
            Sequence = sequence;
            Action = action;
            Transaction = transaction;
            ResolvedId = resolvedId;
            RandomCallsBefore = callsBefore;
            RandomCallsAfter = callsAfter;
            CandidateCount = candidateCount;
        }

        public int Sequence { get; }
        public RewardChoiceHistoryAction Action { get; }
        public RewardTransactionId Transaction { get; }
        public ContentId ResolvedId { get; }
        public ulong RandomCallsBefore { get; }
        public ulong RandomCallsAfter { get; }
        public int CandidateCount { get; }
    }

    /// <summary>Read-only candidate projection for one pending reward transaction.</summary>
    public sealed class RewardChoiceSnapshot
    {
        private readonly ContentId[] candidateIds;

        internal RewardChoiceSnapshot(
            in RewardTransactionId transaction,
            ContentId fallbackId,
            ContentId[] candidates)
        {
            Transaction = transaction;
            FallbackId = fallbackId;
            candidateIds = candidates == null ? Array.Empty<ContentId>() : (ContentId[])candidates.Clone();
        }

        public RewardTransactionId Transaction { get; }
        public ContentId SourceId => Transaction.SourceStableId;
        public ContentId FallbackId { get; }
        public int CandidateCount => candidateIds.Length;

        public ContentId GetCandidateAt(int index)
        {
            if (index < 0 || index >= candidateIds.Length)
                throw new ArgumentOutOfRangeException(nameof(index));
            return candidateIds[index];
        }
    }

    /// <summary>
    /// Run-local owner of controlled Evolution choices. It uses an isolated Reward
    /// random stream and never participates in the fixed-tick hot path unless requested.
    /// </summary>
    public sealed class RewardChoiceRuntime
    {
        private const ulong RewardStreamId = 0x524557415244UL;
        private readonly BuildRuntimeCatalog catalog;
        private readonly BuildState build;
        private readonly RewardRuntime transactions;
        private readonly CompiledUpgradeOfferDefinition[] scratch;
        private RewardChoiceHistoryEntry[] history;
        private RandomStream random;

        internal RewardChoiceRuntime(
            BuildRuntimeCatalog runtimeCatalog,
            BuildState buildState,
            RewardRuntime transactionRuntime,
            ulong runSeed,
            int initialHistoryCapacity)
        {
            catalog = runtimeCatalog ?? throw new ArgumentNullException(nameof(runtimeCatalog));
            build = buildState ?? throw new ArgumentNullException(nameof(buildState));
            transactions = transactionRuntime ?? throw new ArgumentNullException(nameof(transactionRuntime));
            if (initialHistoryCapacity < 1)
                throw new ArgumentOutOfRangeException(nameof(initialHistoryCapacity));
            scratch = new CompiledUpgradeOfferDefinition[catalog.Offers.Count];
            history = new RewardChoiceHistoryEntry[initialHistoryCapacity];
            random = new RandomStream(runSeed).Derive(RewardStreamId);
        }

        public RewardChoiceSnapshot CurrentChoice { get; private set; }
        public bool HasPendingChoice => CurrentChoice != null;
        public bool PauseRequested => HasPendingChoice;
        internal int HistoryCount { get; private set; }
        public ulong StreamSeed => random.RootSeed;
        public ulong RandomCalls => random.Calls;
        public ContentId LastResolvedId { get; private set; }

        internal RewardChoiceHistoryEntry GetHistoryAt(int index)
        {
            if (index < 0 || index >= HistoryCount) throw new ArgumentOutOfRangeException(nameof(index));
            return history[index];
        }

        public RewardChoiceRequestStatus RequestEvolutionChoice(
            in RewardTransactionId transaction,
            ContentId fallbackId,
            int candidateCount = 3)
        {
            if (!transaction.SourceStableId.IsValid || !fallbackId.IsValid ||
                candidateCount < 1 || candidateCount > 3)
                return RewardChoiceRequestStatus.InvalidRequest;
            if (transactions.IsCommitted(transaction))
            {
                Record(RewardChoiceHistoryAction.ReplayRejected, transaction, default, random.Calls, random.Calls, 0);
                return RewardChoiceRequestStatus.AlreadyCommitted;
            }
            if (CurrentChoice != null)
                return CurrentChoice.Transaction.Equals(transaction)
                    ? RewardChoiceRequestStatus.AlreadyPending
                    : RewardChoiceRequestStatus.Busy;

            var eligibleCount = 0;
            for (var index = 0; index < catalog.Offers.Count; index++)
            {
                var offer = catalog.Offers[index];
                if (offer.TargetKind == UpgradeTargetKind.Evolution &&
                    !offer.Source.InitiallyUnlocked &&
                    build.CanAcceptControlledEvolutionOffer(offer))
                    scratch[eligibleCount++] = offer;
            }

            var callsBefore = random.Calls;
            var outputCount = Math.Min(candidateCount, eligibleCount);
            if (outputCount == 0)
            {
                if (!transactions.CanCommit(transaction) || !transactions.TryCommit(transaction))
                    return RewardChoiceRequestStatus.CapacityExceeded;
                LastResolvedId = fallbackId;
                Record(
                    RewardChoiceHistoryAction.Fallback,
                    transaction,
                    fallbackId,
                    callsBefore,
                    random.Calls,
                    0);
                return RewardChoiceRequestStatus.FallbackCommitted;
            }

            var candidates = new ContentId[outputCount];
            for (var outputIndex = 0; outputIndex < outputCount; outputIndex++)
            {
                var totalWeight = 0f;
                for (var index = 0; index < eligibleCount; index++)
                    totalWeight += scratch[index].Source.Weight;
                var roll = random.NextFloat() * totalWeight;
                var selected = eligibleCount - 1;
                var cursor = 0f;
                for (var index = 0; index < eligibleCount; index++)
                {
                    cursor += scratch[index].Source.Weight;
                    if (roll < cursor)
                    {
                        selected = index;
                        break;
                    }
                }
                candidates[outputIndex] = scratch[selected].Source.Id;
                eligibleCount--;
                scratch[selected] = scratch[eligibleCount];
                scratch[eligibleCount] = null;
            }

            CurrentChoice = new RewardChoiceSnapshot(transaction, fallbackId, candidates);
            Record(
                RewardChoiceHistoryAction.Requested,
                transaction,
                default,
                callsBefore,
                random.Calls,
                outputCount);
            return RewardChoiceRequestStatus.ChoiceRequested;
        }

        public RewardChoiceResolutionStatus Select(ContentId offerId)
        {
            var choice = CurrentChoice;
            if (choice == null) return RewardChoiceResolutionStatus.NoPendingChoice;
            var found = false;
            for (var index = 0; index < choice.CandidateCount; index++)
            {
                if (choice.GetCandidateAt(index) == offerId)
                {
                    found = true;
                    break;
                }
            }
            if (!found || !catalog.TryGetOffer(offerId, out var offer))
                return RewardChoiceResolutionStatus.InvalidSelection;
            if (!build.CanAcceptControlledEvolutionOffer(offer))
                return RewardChoiceResolutionStatus.NoLongerEligible;
            if (!transactions.CanCommit(choice.Transaction))
                return RewardChoiceResolutionStatus.CapacityExceeded;
            if (!build.ApplyOffer(offer) || !transactions.TryCommit(choice.Transaction))
                return RewardChoiceResolutionStatus.NoLongerEligible;

            LastResolvedId = offer.Source.TargetContentId;
            CurrentChoice = null;
            Record(
                RewardChoiceHistoryAction.Selected,
                choice.Transaction,
                offerId,
                random.Calls,
                random.Calls,
                choice.CandidateCount);
            return RewardChoiceResolutionStatus.Committed;
        }

        private void Record(
            RewardChoiceHistoryAction action,
            in RewardTransactionId transaction,
            ContentId resolvedId,
            ulong callsBefore,
            ulong callsAfter,
            int candidateCount)
        {
            if (HistoryCount >= history.Length) Array.Resize(ref history, history.Length * 2);
            history[HistoryCount] = new RewardChoiceHistoryEntry(
                HistoryCount,
                action,
                transaction,
                resolvedId,
                callsBefore,
                callsAfter,
                candidateCount);
            HistoryCount++;
        }
    }
}
