using Game.Application;
using Game.Content.Runtime;
using Game.Core;
using Game.Editor;
using Game.Platform.Null;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public sealed class QinglanG28VerticalSliceTests
    {
        private static readonly ContentVersion GameVersion = new ContentVersion(0, 1, 0);

        [Test]
        public void RealFactoryRunsTwelveMinutesDeterministicallyAcrossThreeObjectiveAndBuildRoutes()
        {
            var catalogs = ContentEditorCatalog.BakeAll();
            Assert.That(catalogs.IsSuccess, Is.True, catalogs.Error.ToString());
            var primary = Run(catalogs.Value, 0x4732385645525441UL, 2);
            var repeated = Run(catalogs.Value, 0x4732385645525441UL, 2);
            var mobility = Run(catalogs.Value, 0x4732384D4F42494CUL, 0);
            var field = Run(catalogs.Value, 0x4732384649454C44UL, 1);

            AssertSlice(primary, 3, 7);
            AssertSlice(repeated, 3, 7);
            AssertSlice(mobility, 1, 1);
            AssertSlice(field, 2, 6);
            Assert.That(repeated.combinedChecksum, Is.EqualTo(primary.combinedChecksum));
            Assert.That(repeated.completedTicks, Is.EqualTo(primary.completedTicks));
            Assert.That(repeated.enemyDefeats, Is.EqualTo(primary.enemyDefeats));
            Assert.That(repeated.decisionChecksum, Is.EqualTo(primary.decisionChecksum));
            Assert.That(mobility.decisionChecksum, Is.Not.EqualTo(primary.decisionChecksum));
            Assert.That(field.decisionChecksum, Is.Not.EqualTo(primary.decisionChecksum));
            Assert.That(field.decisionChecksum, Is.Not.EqualTo(mobility.decisionChecksum));
        }

        [Test]
        public void ActualOldCourtSchedulerKeepsEveryTimelineSpawnWalkableAndProtected()
        {
            var catalogs = ContentEditorCatalog.BakeAll();
            Assert.That(catalogs.IsSuccess, Is.True, catalogs.Error.ToString());
            var application = CreateApplication(catalogs.Value);
            var result = QinglanG28VerticalSliceCommand.RunSpawnFairness(
                application.ContentRegistry,
                0x4732385645525441UL);

            Assert.That(result.passed, Is.True,
                $"normal={result.normalRequests}, boss={result.bossRequests}, " +
                $"min={result.minimumDistance}, max={result.maximumDistance}, " +
                $"walkable={result.allPositionsWalkable}, protected={result.spawnProtectionRespected}, " +
                $"stopped={result.schedulerStoppedAtDuration}");
            Assert.That(result.tickCount, Is.EqualTo(21_600));
            Assert.That(result.normalRequests, Is.GreaterThan(0));
            Assert.That(result.bossRequests, Is.EqualTo(2));
            Assert.That(result.minimumDistance, Is.GreaterThanOrEqualTo(13.999f));
            Assert.That(result.maximumDistance, Is.LessThanOrEqualTo(60.001f));
            Assert.That(result.allPositionsWalkable, Is.True);
            Assert.That(result.spawnProtectionRespected, Is.True);
            Assert.That(result.schedulerStoppedAtDuration, Is.True);
        }

        private static QinglanG28SliceSummary Run(
            BakedContentCatalog[] catalogs,
            ulong seed,
            int route)
        {
            return QinglanG28VerticalSliceCommand.Execute(CreateApplication(catalogs), seed, route);
        }

        private static GameApplication CreateApplication(BakedContentCatalog[] catalogs)
        {
            var application = new GameApplication(new NullPlatformFacade(), new GameStateMachine());
            var initialized = application.Initialize(catalogs, GameVersion);
            Assert.That(initialized.IsSuccess, Is.True, initialized.Error.ToString());
            return application;
        }

        private static void AssertSlice(
            QinglanG28SliceSummary value,
            int completedObjectives,
            int ruleMask)
        {
            Assert.That(value.victory, Is.True);
            Assert.That(value.completedTicks, Is.GreaterThanOrEqualTo(21_600));
            Assert.That(value.durationSeconds, Is.GreaterThanOrEqualTo(720d));
            Assert.That(value.bossDefeats, Is.EqualTo(2));
            Assert.That(value.completedObjectives, Is.EqualTo(completedObjectives));
            Assert.That(value.completedEvents, Is.EqualTo(3));
            Assert.That(value.claimedLandmarks, Is.EqualTo(5));
            Assert.That(value.offersSelected, Is.GreaterThan(0));
            Assert.That(value.rewardChoicesSelected, Is.GreaterThanOrEqualTo(2));
            Assert.That(value.relicCount, Is.GreaterThan(0));
            Assert.That(value.zhezhiPhaseMask, Is.EqualTo(7));
            Assert.That(value.tingfengPhaseMask, Is.EqualTo(7));
            Assert.That(value.tingfengRuleMask, Is.EqualTo(ruleMask));
            Assert.That(value.movementDistance, Is.GreaterThan(50d));
            Assert.That(value.positionsWalkable, Is.True);
            Assert.That(value.invalidHandleAccesses, Is.Zero);
            Assert.That(value.activeEntitiesBeforeDispose, Is.GreaterThan(0));
        }
    }
}
