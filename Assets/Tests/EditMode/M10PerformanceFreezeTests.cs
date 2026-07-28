using System;
using Game.Presentation;
using Game.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    public sealed class M10PerformanceFreezeTests
    {
        [Test]
        public void TargetConfigurationPinsThirtyMinutesAndAllRequiredCounts()
        {
            var configuration = M10StressConfiguration.Target(17UL);

            Assert.That(configuration.TickCount, Is.EqualTo(54_000));
            Assert.That(configuration.EnemyCount, Is.EqualTo(1_500));
            Assert.That(configuration.ProjectileCount, Is.EqualTo(3_000));
            Assert.That(configuration.PickupCount, Is.EqualTo(5_000));
            Assert.That(configuration.VfxRequestCount, Is.EqualTo(200));
            Assert.That(configuration.ExpectedEntityCount, Is.EqualTo(9_501));
        }

        [Test]
        public void ProductionStoresReachAndPreserveExactTargetScale()
        {
            var fixture = M5TestFactory.Create();
            var configuration = new M10StressConfiguration(
                23UL,
                1,
                1_500,
                3_000,
                5_000,
                200,
                0);

            var result = M10StressScenario.Create(
                fixture.Registry,
                fixture.Ranged.Id,
                configuration);

            Assert.That(result.IsSuccess, Is.True, result.Error.ToString());
            Assert.That(result.Value.HasExactConfiguredCounts(), Is.True);
            result.Value.AdvanceOneTick();
            Assert.That(result.Value.HasExactConfiguredCounts(), Is.True);
            Assert.That(result.Value.World.Diagnostics.InvalidHandleAccesses, Is.Zero);
            Assert.That(result.Value.World.Diagnostics.TruncatedProcChains, Is.Zero);
        }

        [Test]
        public void SameSeedProducesSameStressChecksum()
        {
            var fixture = M5TestFactory.Create();
            var configuration = new M10StressConfiguration(
                29UL,
                60,
                32,
                64,
                96,
                8,
                0);
            var first = M10StressScenario.Create(fixture.Registry, fixture.Ranged.Id, configuration);
            var second = M10StressScenario.Create(fixture.Registry, fixture.Ranged.Id, configuration);
            Assert.That(first.IsSuccess, Is.True, first.Error.ToString());
            Assert.That(second.IsSuccess, Is.True, second.Error.ToString());

            for (var index = 0; index < configuration.TickCount; index++)
            {
                first.Value.AdvanceOneTick();
                second.Value.AdvanceOneTick();
            }

            Assert.That(second.Value.CalculateChecksum(), Is.EqualTo(first.Value.CalculateChecksum()));
            Assert.That(first.Value.HasExactConfiguredCounts(), Is.True);
            Assert.That(second.Value.HasExactConfiguredCounts(), Is.True);
        }

        [Test]
        public void WarmStressTicksDoNotAllocateManagedMemory()
        {
            var fixture = M5TestFactory.Create();
            var configuration = new M10StressConfiguration(
                31UL,
                60,
                24,
                48,
                72,
                8,
                0);
            var result = M10StressScenario.Create(fixture.Registry, fixture.Ranged.Id, configuration);
            Assert.That(result.IsSuccess, Is.True, result.Error.ToString());
            for (var index = 0; index < 10; index++) result.Value.AdvanceOneTick();

            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var index = 0; index < 30; index++) result.Value.AdvanceOneTick();
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.That(allocated, Is.Zero);
        }

        [Test]
        public void VfxPoolReportsHitsExpansionFailureAndDrops()
        {
            var root = new GameObject("M10_VfxPoolTestRoot");
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            texture.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
            texture.Apply(false, true);
            var sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, 2f, 2f),
                new Vector2(0.5f, 0.5f),
                2f);
            var pool = new VfxRequestPool(root.transform, sprite, 4);
            try
            {
                for (var index = 0; index < 4; index++)
                    Assert.That(pool.TrySpawn(Vector2.zero, Color.white, 1f, 0.1f), Is.True);
                Assert.That(pool.TrySpawn(Vector2.zero, Color.white, 1f, 0.1f), Is.False);
                Assert.That(pool.CreatedCount, Is.EqualTo(4));
                Assert.That(pool.ExpansionCount, Is.EqualTo(4));
                Assert.That(pool.PeakActiveCount, Is.EqualTo(4));
                Assert.That(pool.FailedAcquireCount, Is.EqualTo(1));
                Assert.That(pool.DroppedRequestCount, Is.EqualTo(1));

                pool.Tick(1f);
                Assert.That(pool.TrySpawn(Vector2.zero, Color.white, 1f, 0.1f), Is.True);
                Assert.That(pool.HitCount, Is.EqualTo(1));
            }
            finally
            {
                pool.Dispose();
                UnityEngine.Object.DestroyImmediate(sprite);
                UnityEngine.Object.DestroyImmediate(texture);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void StressConfigurationRejectsNonPositiveCounts()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new M10StressConfiguration(1UL, 0, 1, 1, 1, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new M10StressConfiguration(1UL, 1, 0, 1, 1, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new M10StressConfiguration(1UL, 1, 1, 0, 1, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new M10StressConfiguration(1UL, 1, 1, 1, 0, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new M10StressConfiguration(1UL, 1, 1, 1, 1, 0));
        }
    }
}
