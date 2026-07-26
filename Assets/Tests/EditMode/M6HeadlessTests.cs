using Game.Simulation;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public sealed class M6HeadlessTests
    {
        [Test]
        [Timeout(60000)]
        public void TenMinuteAutomaticRunCompletesWithStableProgressionStatistics()
        {
            var fixture = M6TestFactory.Create();

            var first = M6HeadlessHarness.RunTenMinutes(
                fixture.Registry,
                fixture.M5.FiniteMap.Id,
                fixture.M5.Encounter.Id,
                fixture.SourceSkill.Id,
                20260726UL);
            var second = M6HeadlessHarness.RunTenMinutes(
                fixture.Registry,
                fixture.M5.FiniteMap.Id,
                fixture.M5.Encounter.Id,
                fixture.SourceSkill.Id,
                20260726UL);

            Assert.That(first.IsSuccess, Is.True, first.Error.ToString());
            Assert.That(second.IsSuccess, Is.True, second.Error.ToString());
            Assert.That(first.Value.TickCount, Is.EqualTo(M6HeadlessHarness.TenMinuteTickCount));
            Assert.That(first.Value.Statistics.EnemyDefeats, Is.GreaterThan(0));
            Assert.That(first.Value.Statistics.PickupsCollected, Is.GreaterThan(0));
            Assert.That(first.Value.Statistics.OffersSelected, Is.GreaterThan(0));
            Assert.That(first.Value.Level, Is.GreaterThan(1));
            Assert.That(first.Value.EntityLeakFree, Is.True);
            Assert.That(first.Value.InvalidHandleAccesses, Is.EqualTo(0));
            Assert.That(second.Value.Checksum, Is.EqualTo(first.Value.Checksum));
            Assert.That(second.Value.Level, Is.EqualTo(first.Value.Level));
            Assert.That(second.Value.Statistics.EnemyDefeats, Is.EqualTo(first.Value.Statistics.EnemyDefeats));
            Assert.That(second.Value.Statistics.PickupsCollected, Is.EqualTo(first.Value.Statistics.PickupsCollected));
            Assert.That(second.Value.Statistics.OffersSelected, Is.EqualTo(first.Value.Statistics.OffersSelected));
        }
    }
}
