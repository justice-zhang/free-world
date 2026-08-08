using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Game.Content.Runtime;
using Game.Core;

namespace Game.Application
{
    public enum RecoveryMarkerStatus : byte
    {
        None = 1,
        PromptRequired = 2
    }

    public readonly struct RecoveryMarkerInspection
    {
        internal RecoveryMarkerInspection(
            RecoveryMarkerStatus status,
            RunRecoverySaveData marker,
            SaveDiagnostic diagnostic)
        {
            Status = status;
            Marker = marker;
            Diagnostic = diagnostic;
        }

        public RecoveryMarkerStatus Status { get; }
        public RunRecoverySaveData Marker { get; }
        public SaveDiagnostic Diagnostic { get; }
        public bool RequiresPrompt => Status == RecoveryMarkerStatus.PromptRequired;
        public bool HasValidMarker => Marker != null;
    }

    /// <summary>
    /// Single Profile 3 owner. It serializes meta writes and the one permitted run-result
    /// transaction order: validate, merge, atomic save, clear Recovery, then publish.
    /// </summary>
    public sealed class QinglanProfileCoordinator
    {
        private const string VictoryOnlyTag = "story.victory_only";
        private const string UniqueProgressTag = "progress.unique";
        private const string RecoveryRejectedPromptKey = "save.prompt.recovery_rejected";
        private readonly SaveCoordinator saves;
        private readonly ContentRegistry content;
        private readonly ApplicationEventStream events;
        private readonly Func<string> utcNow;
        private readonly SemaphoreSlim gate = new SemaphoreSlim(1, 1);
        private ContentId pendingPublicationTransaction;
        private RunResult pendingPublicationResult;

        public QinglanProfileCoordinator(
            SaveCoordinator saveCoordinator,
            ContentRegistry contentRegistry,
            ApplicationEventStream applicationEvents,
            ProfileSaveData initialProfile,
            Func<string> utcNowProvider = null)
        {
            saves = saveCoordinator ?? throw new ArgumentNullException(nameof(saveCoordinator));
            content = contentRegistry ?? throw new ArgumentNullException(nameof(contentRegistry));
            events = applicationEvents ?? throw new ArgumentNullException(nameof(applicationEvents));
            Profile = initialProfile ?? throw new ArgumentNullException(nameof(initialProfile));
            utcNow = utcNowProvider ?? (() => DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
            Meta = new QinglanMetaProgression(content);
        }

        public ProfileSaveData Profile { get; private set; }
        public QinglanMetaProgression Meta { get; }
        public SaveDiagnostic LastDiagnostic { get; private set; }

        public async ValueTask<MetaOperationResult> PurchaseAsync(
            ContentId contentId,
            CancellationToken cancellationToken = default)
        {
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var mutation = Meta.Purchase(Profile, contentId, utcNow());
                return await SaveMetaMutationAsync(mutation, cancellationToken).ConfigureAwait(false);
            }
            finally { gate.Release(); }
        }

        public async ValueTask<MetaOperationResult> ResetLoadoutAsync(
            MetaLoadout loadout,
            CancellationToken cancellationToken = default)
        {
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var mutation = Meta.ResetLoadout(Profile, loadout, utcNow());
                return await SaveMetaMutationAsync(mutation, cancellationToken).ConfigureAwait(false);
            }
            finally { gate.Release(); }
        }

        public ValueTask<CommitResult> CommitRunResultAsync(
            RunResult result,
            CancellationToken cancellationToken = default) =>
            CommitRunResultAsync(result, null, cancellationToken);

        public async ValueTask<CommitResult> CommitRunResultAsync(
            RunResult result,
            DemoRunCoordinator flow,
            CancellationToken cancellationToken = default)
        {
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (result.Descriptor == null || result.Delta == null ||
                    result.Outcome == RunOutcome.RecoveryRejected)
                    return ValidationFailure("save.error.invalid_run_result");

                var transactionId = result.Delta.TransactionId;
                if (QinglanMetaProgression.Contains(Profile.CommittedTransactionIds, transactionId))
                {
                    var cleared = await saves.ClearRunRecoveryAsync(cancellationToken).ConfigureAwait(false);
                    if (!cleared.IsSuccess) return SaveFailure(cleared.Diagnostic);
                    if (pendingPublicationTransaction == transactionId)
                    {
                        events.Publish(ApplicationEvent.RunResultCommitted(pendingPublicationResult));
                        pendingPublicationTransaction = default;
                        pendingPublicationResult = default;
                    }
                    flow?.ConfirmResultCommitted(transactionId);
                    LastDiagnostic = default;
                    return new CommitResult(CommitStatus.AlreadyCommitted);
                }

                var validation = ValidateDelta(result);
                if (validation.IsError) return new CommitResult(CommitStatus.ValidationFailed, validation);
                var candidate = Merge(result);
                var saved = await saves.SaveProfileAsync(candidate, cancellationToken).ConfigureAwait(false);
                if (!saved.IsSuccess) return SaveFailure(saved.Diagnostic);

                Profile = candidate;
                pendingPublicationTransaction = transactionId;
                pendingPublicationResult = result;
                var clear = await saves.ClearRunRecoveryAsync(cancellationToken).ConfigureAwait(false);
                if (!clear.IsSuccess) return SaveFailure(clear.Diagnostic);

                events.Publish(ApplicationEvent.RunResultCommitted(result));
                pendingPublicationTransaction = default;
                pendingPublicationResult = default;
                flow?.ConfirmResultCommitted(transactionId);
                LastDiagnostic = default;
                return new CommitResult(CommitStatus.Committed);
            }
            finally { gate.Release(); }
        }

        /// <summary>Conservatively treats any non-not-found recovery failure as a prompt.</summary>
        public async ValueTask<RecoveryMarkerInspection> InspectRecoveryAsync(
            CancellationToken cancellationToken = default)
        {
            var loaded = await saves.LoadRunRecoveryAsync(cancellationToken).ConfigureAwait(false);
            if (loaded.IsSuccess)
                return new RecoveryMarkerInspection(
                    RecoveryMarkerStatus.PromptRequired,
                    loaded.Value,
                    new SaveDiagnostic(SaveFailureCode.None, RecoveryRejectedPromptKey));
            if (loaded.Failure.Code == SaveFailureCode.NotFound)
                return new RecoveryMarkerInspection(RecoveryMarkerStatus.None, null, loaded.Failure);
            return new RecoveryMarkerInspection(RecoveryMarkerStatus.PromptRequired, null, loaded.Failure);
        }

        /// <summary>Clears a rejected marker; RecoveryRejected never mutates Profile.</summary>
        public async ValueTask<CommitResult> RejectRecoveryAsync(
            DemoRunCoordinator flow,
            RunDescriptor descriptor,
            CancellationToken cancellationToken = default)
        {
            if (flow == null) throw new ArgumentNullException(nameof(flow));
            if (descriptor == null) throw new ArgumentNullException(nameof(descriptor));
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (flow.Stage == DemoFlowStage.Title)
                {
                    if (!flow.RejectRecovery(descriptor))
                        return ValidationFailure("save.error.recovery_rejection_state");
                    flow.Tick(0d);
                }
                else if (flow.Stage != DemoFlowStage.Result || !flow.HasUncommittedResult ||
                         flow.LatestResult.Outcome != RunOutcome.RecoveryRejected)
                {
                    return ValidationFailure("save.error.recovery_rejection_state");
                }
                var clear = await saves.ClearRunRecoveryAsync(cancellationToken).ConfigureAwait(false);
                if (!clear.IsSuccess) return SaveFailure(clear.Diagnostic);
                flow.ConfirmResultCommitted(flow.LatestResult.Delta.TransactionId);
                LastDiagnostic = default;
                return new CommitResult(CommitStatus.Committed);
            }
            finally { gate.Release(); }
        }

        private async ValueTask<MetaOperationResult> SaveMetaMutationAsync(
            MetaOperationResult mutation,
            CancellationToken token)
        {
            if (!mutation.IsSuccess || mutation.Status == MetaOperationStatus.AlreadyApplied)
                return mutation;
            var saved = await saves.SaveProfileAsync(mutation.Profile, token).ConfigureAwait(false);
            if (!saved.IsSuccess)
            {
                LastDiagnostic = saved.Diagnostic;
                return new MetaOperationResult(MetaOperationStatus.SaveFailed, Profile, saved.Diagnostic);
            }
            Profile = mutation.Profile;
            LastDiagnostic = default;
            return mutation;
        }

        private SaveDiagnostic ValidateDelta(RunResult result)
        {
            if (!content.TryGet(result.Descriptor.CharacterId, out RuntimeCharacterDefinition _) ||
                !content.TryGet(result.Descriptor.MapId, out RuntimeMapDefinition _))
                return Diagnostic("save.error.run_identity_missing", result.Descriptor.MapId);
            var delta = result.Delta;
            for (var index = 0; index < delta.UnlockedContentIds.Count; index++)
                if (!content.TryGet(delta.UnlockedContentIds[index], out ContentRegistryEntry _))
                    return Diagnostic("save.error.result_unlock_missing", delta.UnlockedContentIds[index]);
            for (var index = 0; index < delta.ClaimedUniqueRewardIds.Count; index++)
            {
                var id = delta.ClaimedUniqueRewardIds[index];
                if (!content.TryGet(id, out ContentRegistryEntry entry) ||
                    !QinglanMetaProgression.HasTag(entry.Definition, UniqueProgressTag))
                    return Diagnostic("save.error.result_unique_invalid", id);
            }
            for (var index = 0; index < delta.CompletedStoryIds.Count; index++)
                if (!content.TryGet(delta.CompletedStoryIds[index], out RuntimeStoryDefinition _))
                    return Diagnostic("save.error.result_story_invalid", delta.CompletedStoryIds[index]);
            for (var index = 0; index < delta.CollectedCollectibleIds.Count; index++)
                if (!content.TryGet(delta.CollectedCollectibleIds[index], out RuntimeCollectibleDefinition _))
                    return Diagnostic("save.error.result_collectible_invalid", delta.CollectedCollectibleIds[index]);
            for (var index = 0; index < delta.CurrencyDeltas.Count; index++)
            {
                var item = delta.CurrencyDeltas[index];
                if (!string.Equals(item.Key, QinglanMetaProgression.SpiritSandCurrency, StringComparison.Ordinal) ||
                    item.Value < 0)
                    return new SaveDiagnostic(SaveFailureCode.InvalidFormat, "save.error.result_currency_invalid", item.Key);
                for (var previous = 0; previous < index; previous++)
                    if (string.Equals(delta.CurrencyDeltas[previous].Key, item.Key, StringComparison.Ordinal))
                        return new SaveDiagnostic(SaveFailureCode.InvalidFormat, "save.error.result_currency_duplicate", item.Key);
            }
            return default;
        }

        private ProfileSaveData Merge(RunResult result)
        {
            var delta = result.Delta;
            var unlocks = new List<ContentId>();
            for (var index = 0; index < Profile.UnlockedContentIds.Count; index++)
                AddUnique(unlocks, Profile.UnlockedContentIds[index]);
            for (var index = 0; index < delta.UnlockedContentIds.Count; index++)
            {
                var id = delta.UnlockedContentIds[index];
                content.TryGet(id, out ContentRegistryEntry entry);
                if (!result.IsVictory &&
                    (QinglanMetaProgression.HasTag(entry.Definition, UniqueProgressTag) ||
                     QinglanMetaProgression.HasTag(entry.Definition, VictoryOnlyTag)))
                    continue;
                AddUnique(unlocks, id);
            }

            var unique = CopyToList(Profile.ClaimedUniqueRewardIds);
            if (result.IsVictory)
                for (var index = 0; index < delta.ClaimedUniqueRewardIds.Count; index++)
                    AddUnique(unique, delta.ClaimedUniqueRewardIds[index]);

            var stories = CopyToList(Profile.CompletedStoryIds);
            for (var index = 0; index < delta.CompletedStoryIds.Count; index++)
                AddStoryForOutcome(stories, delta.CompletedStoryIds[index], result.IsVictory);
            for (var index = 0; index < result.Exploration.ClaimedLandmarkIds.Count; index++)
                if (content.TryGet(result.Exploration.ClaimedLandmarkIds[index], out RuntimeLandmarkDefinition landmark) &&
                    landmark.StoryId.IsValid)
                    AddStoryForOutcome(stories, landmark.StoryId, result.IsVictory);

            var firstClear = CopyToList(Profile.FirstClearMapIds);
            if (result.IsVictory) AddUnique(firstClear, result.Descriptor.MapId);
            var collectibles = CopyToList(Profile.CollectedCollectibleIds);
            for (var index = 0; index < delta.CollectedCollectibleIds.Count; index++)
                AddUnique(collectibles, delta.CollectedCollectibleIds[index]);
            InferCollectibles(result, stories, collectibles);

            for (var index = 0; index < unique.Count; index++) AddUnique(unlocks, unique[index]);
            for (var index = 0; index < stories.Count; index++) AddUnique(unlocks, stories[index]);
            for (var index = 0; index < collectibles.Count; index++) AddUnique(unlocks, collectibles[index]);

            var currencies = ProfileDataUtility.Copy(Profile.Currencies);
            for (var index = 0; index < delta.CurrencyDeltas.Count; index++)
                currencies = ProfileDataUtility.AddCounter(
                    currencies,
                    delta.CurrencyDeltas[index].Key,
                    delta.CurrencyDeltas[index].Value);
            var statistics = MergeStatistics(Profile.Statistics, result);
            var transactions = ProfileDataUtility.AddIds(Profile.CommittedTransactionIds, delta.TransactionId);

            var provisional = ProfileDataUtility.Clone(
                Profile,
                utcNow(),
                unlockedContentIds: Sorted(unlocks),
                currencies: currencies,
                statistics: statistics,
                firstClearMapIds: Sorted(firstClear),
                claimedUniqueRewardIds: Sorted(unique),
                completedStoryIds: Sorted(stories),
                collectedCollectibleIds: Sorted(collectibles),
                committedTransactionIds: transactions);

            if (result.IsVictory)
            {
                for (var index = 0; index < content.Count; index++)
                {
                    var entry = content.Get(new RuntimeContentIndex(index));
                    if (entry.IsSuccess && entry.Value.Definition is RuntimeStoryDefinition story &&
                        QinglanMetaProgression.HasTag(story, VictoryOnlyTag) &&
                        Meta.IsConditionSatisfied(provisional, story.UnlockConditionId))
                        AddUnique(stories, story.Id);
                }
            }
            for (var index = 0; index < stories.Count; index++) AddUnique(unlocks, stories[index]);
            provisional = ProfileDataUtility.Clone(
                provisional,
                utcNow(),
                unlockedContentIds: Sorted(unlocks),
                completedStoryIds: Sorted(stories));
            var facilities = Meta.GetSatisfiedFacilityIds(provisional);
            for (var index = 0; index < facilities.Length; index++) AddUnique(unlocks, facilities[index]);
            return ProfileDataUtility.Clone(
                provisional,
                utcNow(),
                unlockedContentIds: Sorted(unlocks));
        }

        private void InferCollectibles(
            RunResult result,
            List<ContentId> stories,
            List<ContentId> collectibles)
        {
            for (var index = 0; index < content.Count; index++)
            {
                var entry = content.Get(new RuntimeContentIndex(index));
                if (!entry.IsSuccess || !(entry.Value.Definition is RuntimeCollectibleDefinition collectible)) continue;
                var rule = collectible.AcquireRuleId;
                if (QinglanMetaProgression.Contains(result.Exploration.ClaimedLandmarkIds, rule) ||
                    QinglanMetaProgression.Contains(result.Exploration.CompletedObjectiveIds, rule) ||
                    QinglanMetaProgression.Contains(stories, rule) ||
                    QinglanMetaProgression.Contains(Profile.UnlockedContentIds, rule))
                    AddUnique(collectibles, collectible.Id);
            }
        }

        private void AddStoryForOutcome(List<ContentId> stories, ContentId id, bool victory)
        {
            if (!content.TryGet(id, out RuntimeStoryDefinition story)) return;
            if (!victory && QinglanMetaProgression.HasTag(story, VictoryOnlyTag)) return;
            AddUnique(stories, id);
        }

        private static SavedCounter[] MergeStatistics(
            IReadOnlyList<SavedCounter> source,
            RunResult result)
        {
            var statistics = ProfileDataUtility.AddCounter(source, "runs_completed", 1);
            var outcomeKey = result.Outcome == RunOutcome.Victory
                ? "runs_won"
                : result.Outcome == RunOutcome.Defeat ? "runs_defeated" : "runs_abandoned";
            statistics = ProfileDataUtility.AddCounter(statistics, outcomeKey, 1);
            statistics = ProfileDataUtility.AddCounter(statistics, "enemies_defeated", result.Statistics.EnemyDefeats);
            statistics = ProfileDataUtility.AddCounter(statistics, "elites_defeated", result.Statistics.EliteDefeats);
            statistics = ProfileDataUtility.AddCounter(statistics, "bosses_defeated", result.Statistics.BossDefeats);
            statistics = ProfileDataUtility.AddCounter(
                statistics,
                "landmarks_discovered",
                result.Exploration.DiscoveredLandmarkIds.Count);
            return statistics;
        }

        private CommitResult SaveFailure(SaveDiagnostic diagnostic)
        {
            LastDiagnostic = diagnostic;
            return new CommitResult(CommitStatus.SaveFailed, diagnostic);
        }

        private static CommitResult ValidationFailure(string key) =>
            new CommitResult(
                CommitStatus.ValidationFailed,
                new SaveDiagnostic(SaveFailureCode.InvalidFormat, key));

        private static SaveDiagnostic Diagnostic(string key, ContentId id) =>
            new SaveDiagnostic(SaveFailureCode.MissingContent, key, id.Value, id);

        private static List<ContentId> CopyToList(IReadOnlyList<ContentId> source)
        {
            var result = new List<ContentId>(source.Count);
            for (var index = 0; index < source.Count; index++) AddUnique(result, source[index]);
            return result;
        }

        private static ContentId[] Sorted(List<ContentId> source)
        {
            source.Sort(CompareId);
            return source.ToArray();
        }

        private static void AddUnique(List<ContentId> source, ContentId id)
        {
            if (!id.IsValid) return;
            for (var index = 0; index < source.Count; index++) if (source[index] == id) return;
            source.Add(id);
        }

        private static int CompareId(ContentId left, ContentId right) =>
            string.Compare(left.Value, right.Value, StringComparison.Ordinal);
    }
}
