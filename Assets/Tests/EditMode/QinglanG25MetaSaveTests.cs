using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Game.Application;
using Game.Content.Runtime;
using Game.Core;
using Game.Infrastructure;
using Game.Platform.Null;
using Game.Simulation;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization.Tables;

namespace Game.Tests.EditMode
{
    public sealed class QinglanG25MetaSaveTests
    {
        private static readonly ContentVersion GameVersion = new ContentVersion(0, 1, 0);

        [Test]
        public void CatalogContainsCompleteMetaTopologyAndDataDrivenOutputs()
        {
            var registry = LoadRegistry();
            var nodes = 0;
            var terminals = 0;
            var inserts = 0;
            var facilities = 0;
            var stories = 0;
            var collectibles = 0;
            for (var index = 0; index < registry.Count; index++)
            {
                var entry = registry.Get(new RuntimeContentIndex(index)).Value.Definition;
                if (entry is RuntimeMetaNodeDefinition node)
                {
                    nodes++;
                    if (node.NodeKind == MetaNodeKind.Terminal) terminals++;
                    Assert.That(node.OutputIds.Count, Is.GreaterThan(0));
                    Assert.That(node.Cost, Is.GreaterThanOrEqualTo(0));
                }
                else if (entry is RuntimeMetaInsertDefinition insert)
                {
                    inserts++;
                    Assert.That(insert.SlotTags.Count, Is.EqualTo(1));
                    Assert.That(insert.OutputIds.Count, Is.EqualTo(1));
                }
                else if (entry is RuntimeMetaFacilityDefinition) facilities++;
                else if (entry is RuntimeStoryDefinition) stories++;
                else if (entry is RuntimeCollectibleDefinition) collectibles++;
            }

            Assert.That(nodes, Is.EqualTo(12));
            Assert.That(terminals, Is.EqualTo(3));
            Assert.That(inserts, Is.EqualTo(3));
            Assert.That(facilities, Is.EqualTo(4));
            Assert.That(stories, Is.EqualTo(3));
            Assert.That(collectibles, Is.EqualTo(6));
            var collection = LocalizationEditorSettings.GetStringTableCollection("UI");
            var english = collection?.GetTable("en") as StringTable;
            var chinese = collection?.GetTable("zh-Hans") as StringTable;
            Assert.That(english?.GetEntry("save.prompt.recovery_rejected")?.Value, Is.Not.Empty);
            Assert.That(chinese?.GetEntry("save.prompt.recovery_rejected")?.Value, Is.Not.Empty);
            Assert.That(english?.GetEntry("save.status.result_saved")?.Value, Is.Not.Empty);
            Assert.That(chinese?.GetEntry("save.status.result_saved")?.Value, Is.Not.Empty);
        }

        [Test]
        public void PurchaseConsumesSpiritSandAndFreeResetOnlyReplacesStableIds()
        {
            var registry = LoadRegistry();
            var meta = new QinglanMetaProgression(registry);
            var first = Id("qinglan.meta.lu_qingye.innate.01");
            var second = Id("qinglan.meta.lu_qingye.innate.02");
            var insert = Id("qinglan.insert.qinglan_wind_pattern");
            var profile = Profile(
                100,
                new[] { first, insert },
                new[] { new SavedContentLevel(first, 1), new SavedContentLevel(insert, 1) });

            var purchased = meta.Purchase(profile, second, "later");

            Assert.That(purchased.Status, Is.EqualTo(MetaOperationStatus.Applied));
            Assert.That(Counter(purchased.Profile.Currencies, QinglanMetaProgression.SpiritSandCurrency), Is.EqualTo(80));
            Assert.That(purchased.Profile.UnlockedContentIds, Does.Contain(second));
            Assert.That(purchased.Profile.UnlockedContentIds, Does.Contain(Id("qinglan.facility.vein_inquiry_platform")));
            Assert.That(purchased.Profile.UnlockedContentIds, Does.Contain(Id("qinglan.facility.hundred_artifact_pavilion")));
            var reset = meta.ResetLoadout(
                purchased.Profile,
                new MetaLoadout(new[] { first, second }, new[] { insert }),
                "reset");
            Assert.That(reset.Status, Is.EqualTo(MetaOperationStatus.Applied));
            Assert.That(Counter(reset.Profile.Currencies, QinglanMetaProgression.SpiritSandCurrency), Is.EqualTo(80));
            CollectionAssert.AreEquivalent(new[] { first, second, insert }, reset.Profile.ActiveMetaLoadoutIds);
        }

        [Test]
        public void CapacityMutexAndMissingSavedIdsFallBackWithoutRewritingProfile()
        {
            var registry = LoadRegistry();
            var meta = new QinglanMetaProgression(registry);
            var sevenNodes = new[]
            {
                Id("qinglan.meta.lu_qingye.innate.01"),
                Id("qinglan.meta.lu_qingye.innate.02"),
                Id("qinglan.meta.lu_qingye.innate.03"),
                Id("qinglan.meta.lu_qingye.movement.01"),
                Id("qinglan.meta.lu_qingye.movement.02"),
                Id("qinglan.meta.lu_qingye.movement.03"),
                Id("qinglan.meta.lu_qingye.mind.01")
            };
            var unlocked = new List<ContentId>(sevenNodes)
            {
                Id("qinglan.meta.lu_qingye.innate.04"),
                Id("qinglan.meta.lu_qingye.movement.04")
            };
            var profile = Profile(100, unlocked.ToArray(), Levels(unlocked));
            var capacity = meta.ResetLoadout(profile, new MetaLoadout(sevenNodes, Array.Empty<ContentId>()), "now");
            Assert.That(capacity.Status, Is.EqualTo(MetaOperationStatus.InvalidSelection));

            var originalIds = new[]
            {
                Id("qinglan.meta.lu_qingye.innate.04"),
                Id("qinglan.meta.lu_qingye.movement.04")
            };
            var invalidSaved = Profile(100, unlocked.ToArray(), Levels(unlocked), originalIds);
            var projection = meta.ProjectLoadout(invalidSaved);
            Assert.That(projection.UsedSafeFallback, Is.True);
            Assert.That(projection.Loadout.EquippedNodeIds, Is.Empty);
            CollectionAssert.AreEqual(originalIds, invalidSaved.ActiveMetaLoadoutIds,
                "fallback must not silently rewrite the source document");
        }

        [Test]
        public async Task VictoryCommitIsOrderedIdempotentAndUnlocksFirstClearContent()
        {
            var registry = LoadRegistry();
            var storage = new RecordingStorage();
            var events = new ApplicationEventStream();
            events.Published += value =>
            {
                if (value.Type == ApplicationEventType.RunResultCommitted)
                    storage.Operations.Add("event");
            };
            var owner = Owner(registry, storage, events, Profile(0));
            var result = Result(
                RunEndReason.Completed,
                0x2501UL,
                new[] { Id("qinglan.landmark.wind_vein_stele") },
                new RunResultDelta(
                    Id("run.result.g25_victory"),
                    claimedUniqueRewardIds: new[] { Id("qinglan.progress.region_mark.qinglan") },
                    currencyChanges: new[] { new SavedCounter(QinglanMetaProgression.SpiritSandCurrency, 25) }));

            var committed = await owner.CommitRunResultAsync(result);

            Assert.That(committed.Status, Is.EqualTo(CommitStatus.Committed));
            CollectionAssert.AreEqual(new[] { "save", "delete", "event" }, storage.Operations);
            Assert.That(Counter(owner.Profile.Currencies, QinglanMetaProgression.SpiritSandCurrency), Is.EqualTo(25));
            Assert.That(owner.Profile.FirstClearMapIds, Does.Contain(Id("qinglan.map.old_court")));
            Assert.That(owner.Profile.ClaimedUniqueRewardIds, Does.Contain(Id("qinglan.progress.region_mark.qinglan")));
            Assert.That(owner.Profile.CompletedStoryIds, Does.Contain(Id("qinglan.story.lu_qingye.hearing_sword")));
            Assert.That(owner.Profile.CompletedStoryIds, Does.Contain(Id("qinglan.story.lu_qingye.refusing_inheritance")));
            Assert.That(owner.Profile.CollectedCollectibleIds, Does.Contain(Id("qinglan.collectible.old_court.01")));
            Assert.That(owner.Profile.CollectedCollectibleIds, Does.Contain(Id("qinglan.collectible.old_court.06")));
            Assert.That(owner.Profile.UnlockedContentIds, Does.Contain(Id("qinglan.facility.vein_inquiry_platform")));
            Assert.That(owner.Profile.UnlockedContentIds, Does.Contain(Id("qinglan.facility.scroll_pavilion")));
            Assert.That(owner.Profile.UnlockedContentIds, Does.Contain(Id("qinglan.facility.myriad_phenomena_pavilion")));

            var duplicate = await owner.CommitRunResultAsync(result);
            Assert.That(duplicate.Status, Is.EqualTo(CommitStatus.AlreadyCommitted));
            Assert.That(storage.SaveCount, Is.EqualTo(1));
            Assert.That(storage.Operations.FindAll(value => value == "event").Count, Is.EqualTo(1));
            Assert.That(Counter(owner.Profile.Currencies, QinglanMetaProgression.SpiritSandCurrency), Is.EqualTo(25));
        }

        [Test]
        public async Task DefeatKeepsLegalRewardsButDropsUniqueFirstClearAndVictoryStory()
        {
            var registry = LoadRegistry();
            var storage = new RecordingStorage();
            var owner = Owner(registry, storage, new ApplicationEventStream(), Profile(0));
            var result = Result(
                RunEndReason.PlayerDefeated,
                0x2502UL,
                new[] { Id("qinglan.landmark.wind_vein_stele") },
                new RunResultDelta(
                    Id("run.result.g25_defeat"),
                    claimedUniqueRewardIds: new[] { Id("qinglan.progress.region_mark.qinglan") },
                    completedStoryIds: new[] { Id("qinglan.story.lu_qingye.refusing_inheritance") },
                    currencyChanges: new[] { new SavedCounter(QinglanMetaProgression.SpiritSandCurrency, 9) }));

            var committed = await owner.CommitRunResultAsync(result);

            Assert.That(committed.Status, Is.EqualTo(CommitStatus.Committed));
            Assert.That(Counter(owner.Profile.Currencies, QinglanMetaProgression.SpiritSandCurrency), Is.EqualTo(9));
            Assert.That(owner.Profile.FirstClearMapIds, Is.Empty);
            Assert.That(owner.Profile.ClaimedUniqueRewardIds, Is.Empty);
            Assert.That(owner.Profile.CompletedStoryIds, Does.Contain(Id("qinglan.story.lu_qingye.hearing_sword")));
            Assert.That(owner.Profile.CompletedStoryIds, Has.None.EqualTo(Id("qinglan.story.lu_qingye.refusing_inheritance")));
            Assert.That(owner.Profile.CollectedCollectibleIds, Does.Contain(Id("qinglan.collectible.old_court.01")));
            Assert.That(Counter(owner.Profile.Statistics, "runs_defeated"), Is.EqualTo(1));
        }

        [Test]
        public async Task SaveAndRecoveryFailuresRemainRetryableWithoutDoublePublication()
        {
            var registry = LoadRegistry();
            var storage = new RecordingStorage { FailNextSave = true };
            var events = new ApplicationEventStream();
            var publications = 0;
            events.Published += value =>
            {
                if (value.Type == ApplicationEventType.RunResultCommitted) publications++;
            };
            var owner = Owner(registry, storage, events, Profile(0));
            var result = Result(
                RunEndReason.Abandoned,
                0x2503UL,
                Array.Empty<ContentId>(),
                new RunResultDelta(
                    Id("run.result.g25_retry"),
                    currencyChanges: new[] { new SavedCounter(QinglanMetaProgression.SpiritSandCurrency, 4) }));

            var failedSave = await owner.CommitRunResultAsync(result);
            Assert.That(failedSave.Status, Is.EqualTo(CommitStatus.SaveFailed));
            Assert.That(owner.Profile.CommittedTransactionIds, Is.Empty);
            Assert.That(storage.DeleteCount, Is.Zero);
            Assert.That(publications, Is.Zero);

            storage.FailNextDelete = true;
            var failedClear = await owner.CommitRunResultAsync(result);
            Assert.That(failedClear.Status, Is.EqualTo(CommitStatus.SaveFailed));
            Assert.That(owner.Profile.CommittedTransactionIds, Does.Contain(result.Delta.TransactionId));
            Assert.That(publications, Is.Zero);

            var recovered = await owner.CommitRunResultAsync(result);
            Assert.That(recovered.Status, Is.EqualTo(CommitStatus.AlreadyCommitted));
            Assert.That(storage.SaveCount, Is.EqualTo(1));
            Assert.That(publications, Is.EqualTo(1));
            Assert.That(Counter(owner.Profile.Currencies, QinglanMetaProgression.SpiritSandCurrency), Is.EqualTo(4));
        }

        [Test]
        public async Task RecoveryMarkerOnlyOffersLocalizedRejectionAndNeverMutatesProfile()
        {
            var registry = LoadRegistry();
            var storage = new RecordingStorage();
            var saves = new SaveCoordinator(storage, new UnityJsonSaveCodec(), registry);
            var marker = new RunRecoverySaveData(
                99UL,
                120,
                Id("qinglan.character.lu_qingye"),
                Id("qinglan.map.old_court"),
                Array.Empty<SavePackVersion>(),
                Array.Empty<SavedContentLevel>(),
                "2026-08-08T00:00:00Z");
            Assert.That((await saves.SaveRunRecoveryAsync(marker)).IsSuccess, Is.True);
            var initial = Profile(3);
            var owner = new QinglanProfileCoordinator(
                saves,
                registry,
                new ApplicationEventStream(),
                initial,
                () => "2026-08-08T00:00:00Z");
            var flow = new DemoRunCoordinator(new GameStateMachine(), new NeverFactory(), true);
            var descriptor = new RunDescriptor(
                0x2506UL,
                0x2507UL,
                Id("qinglan.character.lu_qingye"),
                Id("qinglan.map.old_court"),
                Id("base.difficulty.normal"),
                2,
                Id("qinglan.boss.tingfeng"),
                Array.Empty<RunPackSnapshot>());

            var inspection = await owner.InspectRecoveryAsync();
            Assert.That(inspection.RequiresPrompt, Is.True);
            Assert.That(inspection.HasValidMarker, Is.True);
            Assert.That(inspection.Diagnostic.MessageKey, Is.EqualTo("save.prompt.recovery_rejected"));
            storage.FailNextDelete = true;
            var failedClear = await owner.RejectRecoveryAsync(flow, descriptor);
            Assert.That(failedClear.Status, Is.EqualTo(CommitStatus.SaveFailed));
            Assert.That(flow.HasUncommittedResult, Is.True);
            var rejected = await owner.RejectRecoveryAsync(flow, descriptor);

            Assert.That(rejected.Status, Is.EqualTo(CommitStatus.Committed));
            Assert.That(flow.LatestResult.Outcome, Is.EqualTo(RunOutcome.RecoveryRejected));
            Assert.That(flow.HasUncommittedResult, Is.False);
            Assert.That(owner.Profile, Is.SameAs(initial));
            Assert.That(owner.Profile.CommittedTransactionIds, Is.Empty);
            Assert.That(flow.ContinueToHub(), Is.True);
            Assert.That((await owner.InspectRecoveryAsync()).Status, Is.EqualTo(RecoveryMarkerStatus.None));
        }

        [Test]
        public void ValidatedMetaLoadoutIsFrozenAndAppliedByRunFactory()
        {
            var application = Application(LoadCatalog());
            var factory = new QinglanDemoRunFactory(application);
            var node = Id("qinglan.meta.lu_qingye.movement.01");
            var descriptor = factory.CreateDescriptor(
                0x2504UL,
                0x2505UL,
                new MetaLoadout(new[] { node }, Array.Empty<ContentId>()),
                new[] { Id("qinglan.progress.region_mark.qinglan") });
            Assert.That(descriptor.IsSuccess, Is.True, descriptor.Error.ToString());
            Assert.That(descriptor.Value.MetaLoadout.EquippedNodeIds, Does.Contain(node));
            Assert.That(descriptor.Value.OwnedUniqueRewardIds.Count, Is.EqualTo(1));

            var created = factory.Create(descriptor.Value, new GameStateMachine());
            Assert.That(created.IsSuccess, Is.True, created.Error.ToString());
            created.Value.Dispose();
        }

        private static QinglanProfileCoordinator Owner(
            ContentRegistry registry,
            RecordingStorage storage,
            ApplicationEventStream events,
            ProfileSaveData profile) =>
            new QinglanProfileCoordinator(
                new SaveCoordinator(storage, new UnityJsonSaveCodec(), registry),
                registry,
                events,
                profile,
                () => "2026-08-08T00:00:00Z");

        private static RunResult Result(
            RunEndReason reason,
            ulong runId,
            ContentId[] claimedLandmarks,
            RunResultDelta delta)
        {
            var descriptor = new RunDescriptor(
                runId,
                runId + 1UL,
                Id("qinglan.character.lu_qingye"),
                Id("qinglan.map.old_court"),
                Id("base.difficulty.normal"),
                2,
                Id("qinglan.boss.tingfeng"),
                Array.Empty<RunPackSnapshot>());
            return new RunResult(
                reason,
                descriptor,
                600,
                3,
                0,
                new RunStatisticsSnapshot(12, 2, reason == RunEndReason.Completed ? 2 : 0, 8, 20d, 2, 0, 0, 0, 1UL),
                RunBuildSnapshot.Empty,
                new RunExplorationSnapshot(null, null, claimedLandmarks, claimedLandmarks),
                delta,
                1UL,
                2UL,
                3UL);
        }

        private static ProfileSaveData Profile(
            long spiritSand,
            ContentId[] unlocked = null,
            SavedContentLevel[] levels = null,
            ContentId[] loadout = null) =>
            new ProfileSaveData(
                "g25-profile",
                Array.Empty<SavePackVersion>(),
                unlocked ?? Array.Empty<ContentId>(),
                levels ?? Array.Empty<SavedContentLevel>(),
                new[] { new SavedCounter(QinglanMetaProgression.SpiritSandCurrency, spiritSand) },
                Array.Empty<SavedCounter>(),
                "2026-08-08T00:00:00Z",
                loadout,
                null,
                null,
                null,
                null,
                null);

        private static SavedContentLevel[] Levels(IReadOnlyList<ContentId> ids)
        {
            var result = new SavedContentLevel[ids.Count];
            for (var index = 0; index < result.Length; index++) result[index] = new SavedContentLevel(ids[index], 1);
            return result;
        }

        private static long Counter(IReadOnlyList<SavedCounter> counters, string key)
        {
            for (var index = 0; index < counters.Count; index++)
                if (string.Equals(counters[index].Key, key, StringComparison.Ordinal)) return counters[index].Value;
            return 0;
        }

        private static ContentRegistry LoadRegistry()
        {
            var registry = new ContentRegistry();
            var loaded = registry.Load(new[] { LoadCatalog() }, GameVersion);
            Assert.That(loaded.IsSuccess, Is.True, loaded.Error.ToString());
            return registry;
        }

        private static BakedContentCatalog LoadCatalog()
        {
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(
                "Assets/GameAssets/Placeholder/QinglanDemo/QinglanDemoContentPack.baked.json");
            Assert.That(asset, Is.Not.Null);
            var parsed = JsonUtility.FromJson<BakedContentCatalogDto>(asset.text).ToCatalog();
            Assert.That(parsed.IsSuccess, Is.True, parsed.Error.ToString());
            return parsed.Value;
        }

        private static GameApplication Application(BakedContentCatalog catalog)
        {
            var application = new GameApplication(new NullPlatformFacade(), new GameStateMachine());
            var initialized = application.Initialize(new[] { catalog }, GameVersion);
            Assert.That(initialized.IsSuccess, Is.True, initialized.Error.ToString());
            return application;
        }

        private static ContentId Id(string value) => ContentId.Create(value).Value;

        private sealed class RecordingStorage : ISaveStorage
        {
            private readonly Dictionary<string, byte[]> data = new Dictionary<string, byte[]>();
            public readonly List<string> Operations = new List<string>();
            public bool FailNextSave;
            public bool FailNextDelete;
            public int SaveCount { get; private set; }
            public int DeleteCount { get; private set; }

            public ValueTask<SaveStorageReadResult> ReadAsync(string slot, CancellationToken cancellationToken)
            {
                return data.TryGetValue(slot, out var value)
                    ? new ValueTask<SaveStorageReadResult>(SaveStorageReadResult.Success(value, default))
                    : new ValueTask<SaveStorageReadResult>(SaveStorageReadResult.Failure(
                        new SaveDiagnostic(SaveFailureCode.NotFound, "save.error.not_found")));
            }

            public ValueTask<SaveStorageWriteResult> WriteAtomicAsync(
                string slot,
                ReadOnlyMemory<byte> bytes,
                CancellationToken cancellationToken)
            {
                if (FailNextSave)
                {
                    FailNextSave = false;
                    return new ValueTask<SaveStorageWriteResult>(SaveStorageWriteResult.Failure(
                        new SaveDiagnostic(SaveFailureCode.IoFailure, "save.error.test_write")));
                }
                data[slot] = bytes.ToArray();
                SaveCount++;
                Operations.Add("save");
                return new ValueTask<SaveStorageWriteResult>(SaveStorageWriteResult.Success());
            }

            public ValueTask<SaveStorageWriteResult> DeleteAsync(string slot, CancellationToken cancellationToken)
            {
                DeleteCount++;
                if (FailNextDelete)
                {
                    FailNextDelete = false;
                    return new ValueTask<SaveStorageWriteResult>(SaveStorageWriteResult.Failure(
                        new SaveDiagnostic(SaveFailureCode.IoFailure, "save.error.test_delete")));
                }
                data.Remove(slot);
                Operations.Add("delete");
                return new ValueTask<SaveStorageWriteResult>(SaveStorageWriteResult.Success());
            }
        }

        private sealed class NeverFactory : IRunSessionFactory
        {
            public Result<IRunSessionHandle> Create(
                RunDescriptor descriptor,
                GameStateMachine stateMachine)
            {
                Assert.Fail("Recovery rejection must not assemble a run.");
                return default;
            }
        }
    }
}
