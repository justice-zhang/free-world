using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Game.Application;
using Game.Content.Runtime;
using Game.Core;
using Game.Infrastructure;
using Game.Platform.Null;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    public sealed class QinglanG25SettlementPlayModeTests
    {
        [UnityTest]
        public IEnumerator ResultPageCannotLeaveUntilProfileSaveAndRecoveryClearComplete()
        {
            var catalog = LoadCatalog();
            var state = new GameStateMachine();
            var application = new GameApplication(new NullPlatformFacade(), state);
            var initialized = application.Initialize(
                new[] { catalog },
                new ContentVersion(0, 1, 0));
            Assert.That(initialized.IsSuccess, Is.True, initialized.Error.ToString());
            var factory = new QinglanDemoRunFactory(application);
            var descriptor = factory.CreateDescriptor(0x473235504C415931UL, 0x473235504C415932UL);
            Assert.That(descriptor.IsSuccess, Is.True, descriptor.Error.ToString());
            var flow = new DemoRunCoordinator(state, factory, true);
            Assert.That(flow.ShowCharacterSelect(), Is.True);
            Assert.That(flow.ShowMapSelect(), Is.True);
            Assert.That(flow.BeginRun(descriptor.Value), Is.True);
            flow.Tick(0d);
            Assert.That(flow.EndRun(RunEndReason.Abandoned), Is.True);
            flow.Tick(0d);
            Assert.That(flow.Stage, Is.EqualTo(DemoFlowStage.Result));
            Assert.That(flow.ContinueToHub(), Is.False);

            var storage = new MemoryStorage();
            var profile = new ProfileSaveData(
                "playmode-g25",
                Array.Empty<SavePackVersion>(),
                Array.Empty<ContentId>(),
                Array.Empty<SavedContentLevel>(),
                Array.Empty<SavedCounter>(),
                Array.Empty<SavedCounter>(),
                "2026-08-08T00:00:00Z");
            var owner = new QinglanProfileCoordinator(
                new SaveCoordinator(storage, new UnityJsonSaveCodec(), application.ContentRegistry),
                application.ContentRegistry,
                application.Events,
                profile,
                () => "2026-08-08T00:00:00Z");
            var published = 0;
            application.Events.Published += value =>
            {
                if (value.Type == ApplicationEventType.RunResultCommitted) published++;
            };
            var committed = owner.CommitRunResultAsync(flow.LatestResult, flow).AsTask().GetAwaiter().GetResult();

            Assert.That(committed.Status, Is.EqualTo(CommitStatus.Committed));
            Assert.That(storage.Operations, Is.EqualTo(new[] { "save", "delete" }));
            Assert.That(published, Is.EqualTo(1));
            Assert.That(flow.HasUncommittedResult, Is.False);
            Assert.That(flow.ContinueToHub(), Is.True);
            Assert.That(flow.Stage, Is.EqualTo(DemoFlowStage.Hub));
            Assert.That(owner.Profile.CommittedTransactionIds, Does.Contain(flow.LatestResult.Delta.TransactionId));
            flow.Dispose();
            yield return null;
        }

        private static BakedContentCatalog LoadCatalog()
        {
            var path = Path.Combine(
                UnityEngine.Application.dataPath,
                "GameAssets/Placeholder/QinglanDemo/QinglanDemoContentPack.baked.json");
            var dto = UnityEngine.JsonUtility.FromJson<BakedContentCatalogDto>(File.ReadAllText(path));
            var catalog = dto.ToCatalog();
            Assert.That(catalog.IsSuccess, Is.True, catalog.Error.ToString());
            return catalog.Value;
        }

        private sealed class MemoryStorage : ISaveStorage
        {
            private readonly Dictionary<string, byte[]> data = new Dictionary<string, byte[]>();
            public readonly List<string> Operations = new List<string>();

            public ValueTask<SaveStorageReadResult> ReadAsync(string slot, CancellationToken cancellationToken) =>
                data.TryGetValue(slot, out var bytes)
                    ? new ValueTask<SaveStorageReadResult>(SaveStorageReadResult.Success(bytes, default))
                    : new ValueTask<SaveStorageReadResult>(SaveStorageReadResult.Failure(
                        new SaveDiagnostic(SaveFailureCode.NotFound, "save.error.not_found")));

            public ValueTask<SaveStorageWriteResult> WriteAtomicAsync(
                string slot,
                ReadOnlyMemory<byte> bytes,
                CancellationToken cancellationToken)
            {
                data[slot] = bytes.ToArray();
                Operations.Add("save");
                return new ValueTask<SaveStorageWriteResult>(SaveStorageWriteResult.Success());
            }

            public ValueTask<SaveStorageWriteResult> DeleteAsync(string slot, CancellationToken cancellationToken)
            {
                data.Remove(slot);
                Operations.Add("delete");
                return new ValueTask<SaveStorageWriteResult>(SaveStorageWriteResult.Success());
            }
        }
    }
}
