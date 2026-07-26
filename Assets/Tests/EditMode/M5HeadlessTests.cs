using Game.Simulation;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public sealed class M5HeadlessTests
    {
        [Test]
        [Timeout(30000)]
        public void FiveMinuteFiniteMapEncounterIsBoundedDeterministicAndLeakFree()
        {
            var fixture = M5TestFactory.Create();

            var result = M5HeadlessHarness.RunFiveMinutes(
                fixture.Registry,
                fixture.FiniteMap.Id,
                fixture.Encounter.Id,
                20260726UL);

            Assert.That(result.IsSuccess, Is.True, result.Error.ToString());
            Assert.That(result.Value.TickCount, Is.EqualTo(M5HeadlessHarness.FiveMinuteTickCount));
            Assert.That(result.Value.SpawnedEnemies, Is.GreaterThan(0));
            Assert.That(result.Value.BossSpawnCount, Is.EqualTo(1));
            Assert.That(result.Value.PeakEnemyCount, Is.LessThanOrEqualTo(16));
            Assert.That(result.Value.PositionsFinite, Is.True);
            Assert.That(result.Value.ConcurrencyRespected, Is.True);
            Assert.That(result.Value.EntityLeakFree, Is.True);
            Assert.That(result.Value.InvalidHandleAccesses, Is.EqualTo(0));
        }

        [Test]
        [Timeout(30000)]
        public void FiveMinuteEncounterAlsoRunsOnChunkedInfiniteMap()
        {
            var fixture = M5TestFactory.Create();

            var result = M5HeadlessHarness.RunFiveMinutes(
                fixture.Registry,
                fixture.InfiniteMap.Id,
                fixture.Encounter.Id,
                20260726UL);

            Assert.That(result.IsSuccess, Is.True, result.Error.ToString());
            Assert.That(result.Value.TickCount, Is.EqualTo(M5HeadlessHarness.FiveMinuteTickCount));
            Assert.That(result.Value.BossSpawnCount, Is.EqualTo(1));
            Assert.That(result.Value.PositionsFinite, Is.True);
            Assert.That(result.Value.ConcurrencyRespected, Is.True);
            Assert.That(result.Value.EntityLeakFree, Is.True);
        }
    }
}
