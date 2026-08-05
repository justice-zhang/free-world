using System;
using System.Collections.Generic;
using System.Globalization;
using Game.Core;

namespace Game.Application
{
    /// <summary>Immutable candidate set exposed for one controlled reward transaction.</summary>
    public sealed class RewardChoice
    {
        private readonly ContentId[] candidates;
        private readonly IReadOnlyList<ContentId> candidatesView;

        public RewardChoice(ContentId transactionId, ContentId sourceId, ContentId[] candidateIds, ContentId fallbackId = default)
        {
            if (!transactionId.IsValid) throw new ArgumentException("Transaction ID must be valid.", nameof(transactionId));
            if (!sourceId.IsValid) throw new ArgumentException("Source ID must be valid.", nameof(sourceId));
            TransactionId = transactionId;
            SourceId = sourceId;
            candidates = CopyValid(candidateIds, nameof(candidateIds));
            candidatesView = Array.AsReadOnly(candidates);
            FallbackId = fallbackId;
        }

        public RewardChoice(
            ulong runId,
            ContentId sourceId,
            int sequence,
            ContentId[] candidateIds,
            ContentId fallbackId = default)
            : this(CreateProjectionId(runId, sourceId, sequence), sourceId, candidateIds, fallbackId)
        {
            RunId = runId;
            Sequence = sequence;
            HasReplayKey = true;
        }

        public ContentId TransactionId { get; }
        public ContentId SourceId { get; }
        public IReadOnlyList<ContentId> CandidateIds => candidatesView;
        public ContentId FallbackId { get; }
        public ulong RunId { get; }
        public int Sequence { get; }
        public bool HasReplayKey { get; }

        private static ContentId CreateProjectionId(ulong runId, ContentId sourceId, int sequence)
        {
            if (!sourceId.IsValid) throw new ArgumentException("Source ID must be valid.", nameof(sourceId));
            if (sequence < 0) throw new ArgumentOutOfRangeException(nameof(sequence));
            unchecked
            {
                var hash = 1469598103934665603UL;
                var text = sourceId.Value;
                for (var index = 0; index < text.Length; index++)
                {
                    hash ^= text[index];
                    hash *= 1099511628211UL;
                }
                return ContentId.Create(
                    "reward.transaction." + runId.ToString("x16", CultureInfo.InvariantCulture) + "." +
                    hash.ToString("x16", CultureInfo.InvariantCulture) + "." +
                    sequence.ToString(CultureInfo.InvariantCulture)).Value;
            }
        }

        private static ContentId[] CopyValid(ContentId[] source, string parameter)
        {
            source = source ?? Array.Empty<ContentId>();
            var result = (ContentId[])source.Clone();
            for (var index = 0; index < result.Length; index++)
                if (!result[index].IsValid) throw new ArgumentException("IDs must be valid.", parameter);
            return result;
        }
    }

    /// <summary>Validated stable-ID loadout passed from the hub to a new run.</summary>
    public sealed class MetaLoadout
    {
        private readonly ContentId[] nodeIds;
        private readonly ContentId[] insertIds;
        private readonly IReadOnlyList<ContentId> nodeIdsView;
        private readonly IReadOnlyList<ContentId> insertIdsView;

        public MetaLoadout(ContentId[] equippedNodeIds, ContentId terminalNodeId, ContentId[] equippedInsertIds)
        {
            nodeIds = CopyValid(equippedNodeIds, nameof(equippedNodeIds));
            insertIds = CopyValid(equippedInsertIds, nameof(equippedInsertIds));
            nodeIdsView = Array.AsReadOnly(nodeIds);
            insertIdsView = Array.AsReadOnly(insertIds);
            if (!terminalNodeId.IsValid) throw new ArgumentException("Terminal node ID must be valid.", nameof(terminalNodeId));
            TerminalNodeId = terminalNodeId;
        }

        public IReadOnlyList<ContentId> EquippedNodeIds => nodeIdsView;
        public ContentId TerminalNodeId { get; }
        public IReadOnlyList<ContentId> EquippedInsertIds => insertIdsView;

        private static ContentId[] CopyValid(ContentId[] source, string parameter)
        {
            source = source ?? Array.Empty<ContentId>();
            var result = (ContentId[])source.Clone();
            for (var index = 0; index < result.Length; index++)
                if (!result[index].IsValid) throw new ArgumentException("IDs must be valid.", parameter);
            return result;
        }
    }

    /// <summary>Immutable permanent delta generated from one frozen run result.</summary>
    public sealed class RunResultDelta
    {
        private readonly ContentId[] unlockIds;
        private readonly ContentId[] uniqueRewardIds;
        private readonly ContentId[] storyIds;
        private readonly ContentId[] collectibleIds;
        private readonly SavedCounter[] currencyDeltas;
        private readonly IReadOnlyList<ContentId> unlockIdsView;
        private readonly IReadOnlyList<ContentId> uniqueRewardIdsView;
        private readonly IReadOnlyList<ContentId> storyIdsView;
        private readonly IReadOnlyList<ContentId> collectibleIdsView;
        private readonly IReadOnlyList<SavedCounter> currencyDeltasView;

        public RunResultDelta(
            ContentId transactionId,
            ContentId[] unlockedContentIds = null,
            ContentId[] claimedUniqueRewardIds = null,
            ContentId[] completedStoryIds = null,
            ContentId[] collectedCollectibleIds = null,
            SavedCounter[] currencyChanges = null)
        {
            if (!transactionId.IsValid) throw new ArgumentException("Transaction ID must be valid.", nameof(transactionId));
            TransactionId = transactionId;
            unlockIds = Copy(unlockedContentIds);
            uniqueRewardIds = Copy(claimedUniqueRewardIds);
            storyIds = Copy(completedStoryIds);
            collectibleIds = Copy(collectedCollectibleIds);
            currencyDeltas = currencyChanges == null ? Array.Empty<SavedCounter>() : (SavedCounter[])currencyChanges.Clone();
            unlockIdsView = Array.AsReadOnly(unlockIds);
            uniqueRewardIdsView = Array.AsReadOnly(uniqueRewardIds);
            storyIdsView = Array.AsReadOnly(storyIds);
            collectibleIdsView = Array.AsReadOnly(collectibleIds);
            currencyDeltasView = Array.AsReadOnly(currencyDeltas);
        }

        public ContentId TransactionId { get; }
        public IReadOnlyList<ContentId> UnlockedContentIds => unlockIdsView;
        public IReadOnlyList<ContentId> ClaimedUniqueRewardIds => uniqueRewardIdsView;
        public IReadOnlyList<ContentId> CompletedStoryIds => storyIdsView;
        public IReadOnlyList<ContentId> CollectedCollectibleIds => collectibleIdsView;
        public IReadOnlyList<SavedCounter> CurrencyDeltas => currencyDeltasView;

        private static ContentId[] Copy(ContentId[] source)
        {
            source = source ?? Array.Empty<ContentId>();
            var result = (ContentId[])source.Clone();
            for (var index = 0; index < result.Length; index++)
                if (!result[index].IsValid) throw new ArgumentException("Run-result IDs must be valid.", nameof(source));
            return result;
        }
    }

    public enum CommitStatus : byte
    {
        Committed = 1,
        AlreadyCommitted = 2,
        ValidationFailed = 3,
        SaveFailed = 4
    }

    /// <summary>Result of validating and atomically persisting one run-result delta.</summary>
    public readonly struct CommitResult
    {
        public CommitResult(CommitStatus status, SaveDiagnostic diagnostic = default)
        {
            Status = status;
            Diagnostic = diagnostic;
        }

        public CommitStatus Status { get; }
        public SaveDiagnostic Diagnostic { get; }
        public bool IsSuccess => Status == CommitStatus.Committed || Status == CommitStatus.AlreadyCommitted;
    }
}
