using System.Collections;
using System.IO;
using Game.Application;
using Game.Content.Runtime;
using Game.Core;
using Game.Infrastructure;
using Game.Platform.Null;
using Game.Simulation;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    public sealed class QinglanG24GameFlowPlayModeTests
    {
        private const ulong RunId = 0x473234504C415931UL;
        private const ulong Seed = 0x473234504C415932UL;

        [UnityTest]
        public IEnumerator TitleRunResultHubAndStartAgainUseOneOwnedLifecycle()
        {
            var catalog = LoadCatalog();
            var stateMachine = new GameStateMachine();
            var application = new GameApplication(new NullPlatformFacade(), stateMachine);
            var initialized = application.Initialize(
                new[] { catalog },
                new ContentVersion(0, 1, 0));
            Assert.That(initialized.IsSuccess, Is.True, initialized.Error.ToString());
            var factory = new QinglanDemoRunFactory(application);
            var descriptor = factory.CreateDescriptor(RunId, Seed);
            Assert.That(descriptor.IsSuccess, Is.True, descriptor.Error.ToString());
            var published = 0;
            application.Events.Published += _ => published++;
            var coordinator = new DemoRunCoordinator(stateMachine, factory);

            Assert.That(coordinator.Stage, Is.EqualTo(DemoFlowStage.Title));
            Assert.That(coordinator.ShowCharacterSelect(), Is.True);
            Assert.That(coordinator.ShowMapSelect(), Is.True);
            Assert.That(coordinator.BeginRun(descriptor.Value), Is.True);
            Assert.That(coordinator.Stage, Is.EqualTo(DemoFlowStage.Preparing));
            Assert.That(coordinator.Session, Is.Null);
            Assert.That(coordinator.Tick(0d), Is.Zero);
            Assert.That(coordinator.Stage, Is.EqualTo(DemoFlowStage.Active));
            Assert.That(coordinator.Session, Is.Not.Null);

            var tickBeforePause = coordinator.Session.Runner.Clock.TickCount;
            Assert.That(coordinator.Pause(), Is.True);
            Assert.That(coordinator.Tick(1d), Is.Zero);
            Assert.That(coordinator.Session.Runner.Clock.TickCount, Is.EqualTo(tickBeforePause));
            Assert.That(coordinator.Resume(), Is.True);
            Assert.That(coordinator.Tick(SimulationClock.TickDurationSeconds), Is.EqualTo(1));

            Assert.That(coordinator.EndRun(RunEndReason.Completed), Is.True);
            Assert.That(coordinator.Stage, Is.EqualTo(DemoFlowStage.Ending));
            Assert.That(coordinator.HasResult, Is.False);
            Assert.That(coordinator.Tick(0d), Is.Zero);
            Assert.That(coordinator.Stage, Is.EqualTo(DemoFlowStage.Result));
            Assert.That(coordinator.LatestResult.Outcome, Is.EqualTo(RunOutcome.Victory));
            Assert.That(coordinator.LatestResult.Descriptor.RunId, Is.EqualTo(RunId));
            Assert.That(coordinator.LatestResult.Descriptor.LoadedPacks.Count, Is.EqualTo(1));
            Assert.That(coordinator.HasUncommittedResult, Is.True);
            Assert.That(published, Is.Zero,
                "G2.4 freezes the result; G2.5 owns persistence and completion publication.");

            Assert.That(coordinator.ContinueToHub(), Is.True);
            Assert.That(coordinator.Stage, Is.EqualTo(DemoFlowStage.Hub));
            Assert.That(coordinator.Session, Is.Null);
            Assert.That(coordinator.StartAgain(), Is.True);
            Assert.That(coordinator.Stage, Is.EqualTo(DemoFlowStage.CharacterSelect));
            Assert.That(coordinator.HasResult, Is.False);

            var secondDescriptor = factory.CreateDescriptor(RunId + 1UL, Seed + 1UL);
            Assert.That(secondDescriptor.IsSuccess, Is.True, secondDescriptor.Error.ToString());
            Assert.That(coordinator.ShowMapSelect(), Is.True);
            Assert.That(coordinator.BeginRun(secondDescriptor.Value), Is.True);
            Assert.That(coordinator.Tick(0d), Is.Zero);
            Assert.That(coordinator.EndRun(RunEndReason.Abandoned), Is.True);
            Assert.That(coordinator.Tick(0d), Is.Zero);
            Assert.That(coordinator.LatestResult.Outcome, Is.EqualTo(RunOutcome.Abandoned));
            Assert.That(coordinator.LatestResult.Descriptor.RunId, Is.EqualTo(RunId + 1UL));
            coordinator.Dispose();
            Assert.That(coordinator.Stage, Is.EqualTo(DemoFlowStage.Disposed));
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
    }
}
