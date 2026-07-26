using System.Numerics;
using Game.Content.Runtime;
using Game.Simulation;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public sealed class M5MapSpawnTests
    {
        [Test]
        public void FiniteMapWalkabilityAndMovementResolutionRespectBoundsAndObstacles()
        {
            var fixture = M5TestFactory.Create();
            var map = MapRuntimeFactory.Create(fixture.FiniteMap, 7UL);

            Assert.That(map.IsWalkable(Vector2.Zero), Is.True);
            Assert.That(map.IsWalkable(new Vector2(0f, -3f)), Is.False);
            Assert.That(map.IsWalkable(new Vector2(30f, 0f)), Is.False);
            var bounded = map.ResolveMovement(Vector2.Zero, new Vector2(30f, 0f), 0.5f);
            var blocked = map.ResolveMovement(Vector2.Zero, new Vector2(0f, -3f), 0.5f);
            Assert.That(map.IsWalkable(bounded), Is.True);
            Assert.That(bounded.X, Is.EqualTo(19.5f).Within(0.0001f));
            Assert.That(blocked, Is.EqualTo(Vector2.Zero));
        }

        [TestCase(SpawnPattern.Ring)]
        [TestCase(SpawnPattern.Edge)]
        [TestCase(SpawnPattern.Cluster)]
        [TestCase(SpawnPattern.Line)]
        [TestCase(SpawnPattern.Ambush)]
        [TestCase(SpawnPattern.Portal)]
        [TestCase(SpawnPattern.FixedAnchor)]
        [TestCase(SpawnPattern.OffscreenRandom)]
        public void EverySpawnPatternProducesWalkableFiniteMapPosition(SpawnPattern pattern)
        {
            var fixture = M5TestFactory.Create();
            var map = MapRuntimeFactory.Create(fixture.FiniteMap, 11UL);
            var random = new RandomStream(11UL);

            for (var index = 0; index < 12; index++)
            {
                var position = SpawnPatternGenerator.Generate(
                    pattern,
                    map,
                    Vector2.Zero,
                    8f,
                    12f,
                    pattern == SpawnPattern.Portal
                        ? SkillTestFactory.Id("test.anchor.portal")
                        : SkillTestFactory.Id("test.anchor.boss"),
                    index,
                    ref random);
                Assert.That(map.IsWalkable(position), Is.True, pattern + " at " + position);
            }
        }

        [Test]
        public void ChunkSignaturesAndSpawnSequenceAreDeterministicForFixedSeed()
        {
            var fixture = M5TestFactory.Create(20f, 10f, 8);
            var firstMap = (ChunkedInfiniteMapRuntime)MapRuntimeFactory.Create(fixture.InfiniteMap, 99UL);
            var secondMap = (ChunkedInfiniteMapRuntime)MapRuntimeFactory.Create(fixture.InfiniteMap, 99UL);
            Assert.That(firstMap.GetChunkSignature(12, -9),
                Is.EqualTo(secondMap.GetChunkSignature(12, -9)));

            var first = M5HeadlessHarness.Run(
                fixture.Registry,
                fixture.InfiniteMap.Id,
                fixture.Encounter.Id,
                300,
                99UL);
            var second = M5HeadlessHarness.Run(
                fixture.Registry,
                fixture.InfiniteMap.Id,
                fixture.Encounter.Id,
                300,
                99UL);

            Assert.That(first.IsSuccess, Is.True, first.Error.ToString());
            Assert.That(second.IsSuccess, Is.True, second.Error.ToString());
            Assert.That(first.Value.SpawnChecksum, Is.EqualTo(second.Value.SpawnChecksum));
            Assert.That(first.Value.SpawnedEnemies, Is.EqualTo(second.Value.SpawnedEnemies));
        }

        [Test]
        public void SchedulerRespectsBudgetConcurrencyAndQueuesBossExactlyOnce()
        {
            var fixture = M5TestFactory.Create(6f, 1f, 5);
            var world = M5TestFactory.World(fixture, fixture.FiniteMap, 123UL);
            var runner = new FixedTickRunner(world);

            for (var tick = 0; tick < 180; tick++)
            {
                runner.Advance(SimulationClock.TickDurationSeconds);
                Assert.That(world.Enemies.Count + world.Enemies.PendingSpawns.Count, Is.LessThanOrEqualTo(5));
            }

            Assert.That(world.Encounter.AccumulatedBudget, Is.GreaterThanOrEqualTo(0f));
            Assert.That(world.Enemies.BossSpawnedCount, Is.EqualTo(1));
            Assert.That(world.Encounter.BossRequestCount, Is.EqualTo(1));
        }
    }
}
