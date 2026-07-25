using System;
using System.Collections.Generic;
using System.Numerics;
using Game.Simulation;
using NUnit.Framework;
using UnityEngine;
using NumericsVector2 = System.Numerics.Vector2;
using UnityObject = UnityEngine.Object;

namespace Game.Tests.EditMode
{
    public sealed class SimulationKernelTests
    {
        [Test]
        public void FixedTickProducesSameTicksAndStateAcrossPresentationDeltas()
        {
            var sixtyFps = CreateMovingWorld(out var sixtyFpsHandle);
            var twentyFps = CreateMovingWorld(out var twentyFpsHandle);
            var sixtyFpsRunner = new FixedTickRunner(sixtyFps);
            var twentyFpsRunner = new FixedTickRunner(twentyFps);

            for (var frame = 0; frame < 120; frame++)
            {
                sixtyFpsRunner.Advance(1d / 60d);
            }

            for (var frame = 0; frame < 40; frame++)
            {
                twentyFpsRunner.Advance(1d / 20d);
            }

            Assert.That(sixtyFpsRunner.Clock.TickCount, Is.EqualTo(60));
            Assert.That(twentyFpsRunner.Clock.TickCount, Is.EqualTo(60));
            Assert.That(sixtyFps.Tick, Is.EqualTo(twentyFps.Tick));
            Assert.That(sixtyFps.Actors.TryRead(sixtyFpsHandle, out var sixtyFpsState), Is.True);
            Assert.That(twentyFps.Actors.TryRead(twentyFpsHandle, out var twentyFpsState), Is.True);
            Assert.That(sixtyFpsState.Position, Is.EqualTo(twentyFpsState.Position));
        }

        [Test]
        public void ClockCapsCatchUpAndRetainsBacklog()
        {
            var world = new SimulationWorld();
            var clock = new SimulationClock(2);
            var runner = new FixedTickRunner(world, clock);

            Assert.That(runner.Advance(SimulationClock.TickDurationSeconds * 5d), Is.EqualTo(2));
            Assert.That(runner.Advance(0d), Is.EqualTo(2));
            Assert.That(runner.Advance(0d), Is.EqualTo(1));
            Assert.That(clock.TickCount, Is.EqualTo(5));
            Assert.That(clock.AccumulatorSeconds, Is.EqualTo(0d).Within(0.0000000001d));
        }

        [Test]
        public void CatchUpAdvanceRetainsEventsFromEveryExecutedTick()
        {
            var world = new SimulationWorld();
            var handle = world.CreateProjectile(
                SimulationEntityState.Create(
                    new NumericsVector2(2f, 3f),
                    NumericsVector2.Zero,
                    lifetimeSeconds: 0f));
            var runner = new FixedTickRunner(world);

            Assert.That(
                runner.Advance(SimulationClock.TickDurationSeconds * 2d),
                Is.EqualTo(2));

            Assert.That(world.Projectiles.Contains(handle), Is.False);
            Assert.That(world.Events.Count, Is.EqualTo(1));
            Assert.That(world.Events.GetAt(0).Type, Is.EqualTo(SimulationEventType.Removed));
            Assert.That(world.Events.GetAt(0).Tick, Is.EqualTo(1));
            Assert.That(runner.Advance(0d), Is.Zero);
            Assert.That(world.Events.Count, Is.EqualTo(1));
        }

        [Test]
        public void PauseIgnoresElapsedTimeAndSingleStepRunsExactlyOnce()
        {
            var world = new SimulationWorld();
            var runner = new FixedTickRunner(world);
            runner.Clock.Pause();

            Assert.That(runner.Advance(1d), Is.Zero);
            runner.Step();

            Assert.That(world.Tick, Is.EqualTo(1));
            Assert.That(runner.Clock.TickCount, Is.EqualTo(1));
            runner.Clock.Resume();
            Assert.That(runner.Advance(SimulationClock.TickDurationSeconds), Is.EqualTo(1));
            Assert.That(world.Tick, Is.EqualTo(2));
        }

        [Test]
        public void RemovedHandleIsInvalidAndCannotReadReusedSlot()
        {
            var diagnostics = new SimulationDiagnostics();
            var store = new ActorStore(1, diagnostics);
            var firstState = SimulationEntityState.Create(
                new NumericsVector2(1f, 2f),
                NumericsVector2.Zero);
            var first = store.Create(firstState);

            Assert.That(store.Remove(first), Is.True);
            var secondState = SimulationEntityState.Create(
                new NumericsVector2(9f, 10f),
                NumericsVector2.Zero);
            var second = store.Create(secondState);

            Assert.That(second.Index, Is.EqualTo(first.Index));
            Assert.That(second.Generation, Is.Not.EqualTo(first.Generation));
            Assert.That(store.TryRead(first, out _), Is.False);
            Assert.That(store.TryWrite(first, firstState), Is.False);
            Assert.That(store.TryRead(second, out var actual), Is.True);
            Assert.That(actual.Position, Is.EqualTo(secondState.Position));
            Assert.That(diagnostics.InvalidHandleAccesses, Is.EqualTo(2));
        }

        [Test]
        public void SwapBackKeepsMovedEntityHandleValid()
        {
            var store = new ActorStore(2);
            var first = store.Create(CreateState(1f));
            var middle = store.Create(CreateState(2f));
            var last = store.Create(CreateState(3f));

            Assert.That(store.Remove(middle), Is.True);

            Assert.That(store.Count, Is.EqualTo(2));
            Assert.That(store.Contains(first), Is.True);
            Assert.That(store.Contains(last), Is.True);
            Assert.That(store.TryRead(last, out var movedState), Is.True);
            Assert.That(movedState.Position.X, Is.EqualTo(3f));
            Assert.That(store.GetHandleAt(1), Is.EqualTo(last));
        }

        [Test]
        public void MovementIntegratesAnyNonZeroVelocity()
        {
            var world = new SimulationWorld();
            var velocity = new NumericsVector2(0.00001f, 0f);
            var handle = world.CreateActor(
                SimulationEntityState.Create(NumericsVector2.Zero, velocity));

            new FixedTickRunner(world).Advance(SimulationClock.TickDurationSeconds);

            Assert.That(world.Actors.TryRead(handle, out var state), Is.True);
            Assert.That(
                state.Position.X,
                Is.EqualTo(velocity.X * world.DeltaTimeSeconds).Within(0.000000000001f));
            Assert.That(
                state.StateFlags & SimulationStateFlags.Moving,
                Is.EqualTo(SimulationStateFlags.Moving));
        }

        [Test]
        public void StoresExpandAndReuseFreeSlots()
        {
            var actorStore = new ActorStore(1);
            var handles = new EntityHandle[64];
            for (var index = 0; index < handles.Length; index++)
            {
                handles[index] = actorStore.Create(CreateState(index));
            }

            Assert.That(actorStore.Count, Is.EqualTo(handles.Length));
            Assert.That(actorStore.Remove(handles[17]), Is.True);
            var reused = actorStore.Create(CreateState(100f));
            Assert.That(reused.Index, Is.EqualTo(handles[17].Index));
            Assert.That(reused.Generation, Is.Not.EqualTo(handles[17].Generation));

            var projectileStore = new ProjectileStore(1);
            var areaStore = new AreaStore(1);
            var pickupStore = new PickupStore(1);
            Assert.That(projectileStore.Create(CreateState(1f)).Generation, Is.EqualTo(1));
            Assert.That(areaStore.Create(CreateState(2f)).Generation, Is.EqualTo(1));
            Assert.That(pickupStore.Create(CreateState(3f)).Generation, Is.EqualTo(1));
            Assert.That(projectileStore.Count, Is.EqualTo(1));
            Assert.That(areaStore.Count, Is.EqualTo(1));
            Assert.That(pickupStore.Count, Is.EqualTo(1));
        }

        [Test]
        public void DefaultPipelineOrderIsFixedAndExecutionOrderIsObservable()
        {
            var defaultPipeline = SimulationPipeline.CreateM2Default();

            Assert.That(defaultPipeline.Count, Is.EqualTo(4));
            Assert.That(defaultPipeline.GetSystemId(0), Is.EqualTo(SimulationSystemId.Movement));
            Assert.That(defaultPipeline.GetSystemId(1), Is.EqualTo(SimulationSystemId.Lifetime));
            Assert.That(defaultPipeline.GetSystemId(2), Is.EqualTo(SimulationSystemId.Cleanup));
            Assert.That(defaultPipeline.GetSystemId(3), Is.EqualTo(SimulationSystemId.SnapshotBuild));

            var calls = new int[3];
            var callCount = 0;
            var pipeline = new SimulationPipeline(
                new RecordingSystem(11, calls, () => callCount++),
                new RecordingSystem(22, calls, () => callCount++),
                new RecordingSystem(33, calls, () => callCount++));
            var world = new SimulationWorld(pipeline: pipeline);
            new FixedTickRunner(world).Advance(SimulationClock.TickDurationSeconds);

            CollectionAssert.AreEqual(new[] { 11, 22, 33 }, calls);
        }

        [Test]
        public void SpatialRadiusQueryMatchesBruteForce()
        {
            const int entityCount = 128;
            var grid = new SpatialGrid(1.25f, 4);
            var random = new RandomStream(0x5A17UL);
            var positions = new NumericsVector2[entityCount];
            var expected = new HashSet<SpatialEntity>();
            var center = new NumericsVector2(2.5f, -1.25f);
            const float radius = 7.5f;
            var radiusSquared = radius * radius;

            for (var index = 0; index < entityCount; index++)
            {
                positions[index] = new NumericsVector2(
                    random.NextFloat(-20f, 20f),
                    random.NextFloat(-20f, 20f));
                var entity = new SpatialEntity(
                    EntityKind.Actor,
                    new EntityHandle(index, 1));
                Assert.That(grid.Insert(entity, positions[index]), Is.True);
                if ((positions[index] - center).LengthSquared() <= radiusSquared)
                {
                    expected.Add(entity);
                }
            }

            var results = new SpatialQueryBuffer(4);
            grid.QueryRadius(center, radius, results);
            var actual = new HashSet<SpatialEntity>();
            for (var index = 0; index < results.Count; index++)
            {
                actual.Add(results[index].Entity);
            }

            Assert.That(actual, Is.EquivalentTo(expected));
        }

        [Test]
        public void SpatialGridSupportsUpdateDeleteAndNearbyQuery()
        {
            var grid = new SpatialGrid(1f, 1);
            var source = new SpatialEntity(EntityKind.Actor, new EntityHandle(1, 1));
            var removed = new SpatialEntity(EntityKind.Projectile, new EntityHandle(1, 1));
            var moved = new SpatialEntity(EntityKind.Pickup, new EntityHandle(1, 1));
            grid.Insert(source, NumericsVector2.Zero);
            grid.Insert(removed, new NumericsVector2(0.5f, 0f));
            grid.Insert(moved, new NumericsVector2(10f, 10f));

            Assert.That(grid.Update(moved, new NumericsVector2(0.25f, 0f)), Is.True);
            Assert.That(grid.Remove(removed), Is.True);
            var results = new SpatialQueryBuffer();
            Assert.That(grid.QueryNearby(source, 1f, results), Is.True);

            Assert.That(results.Count, Is.EqualTo(1));
            Assert.That(results[0].Entity, Is.EqualTo(moved));
            Assert.That(grid.Count, Is.EqualTo(2));
            Assert.That(grid.TryGetPosition(removed, out _), Is.False);
        }

        [Test]
        public void RandomStreamsRepeatAndDerivedStreamsIgnoreParentCallOrder()
        {
            var first = new RandomStream(123456UL);
            var second = new RandomStream(123456UL);
            for (var index = 0; index < 100; index++)
            {
                Assert.That(first.NextUInt(), Is.EqualTo(second.NextUInt()));
            }

            var parent = new RandomStream(99UL);
            var childBeforeCalls = parent.Derive(7UL);
            parent.NextUInt();
            parent.NextUInt();
            var childAfterCalls = parent.Derive(7UL);

            Assert.That(childBeforeCalls.NextUInt(), Is.EqualTo(childAfterCalls.NextUInt()));
            Assert.That(parent.Calls, Is.EqualTo(2UL));
        }

        [Test]
        public void SnapshotContainsPreviousAndCurrentStateAndInterpolates()
        {
            var world = new SimulationWorld();
            var initial = SimulationEntityState.Create(
                NumericsVector2.Zero,
                new NumericsVector2(30f, 0f),
                stateFlags: SimulationStateFlags.Active);
            var handle = world.CreateActor(initial);
            var runner = new FixedTickRunner(world);

            runner.Advance(SimulationClock.TickDurationSeconds);

            var entity = new SpatialEntity(EntityKind.Actor, handle);
            Assert.That(world.RenderSnapshot.Tick, Is.EqualTo(1));
            Assert.That(world.RenderSnapshot.TryGet(entity, out var snapshot), Is.True);
            Assert.That(snapshot.PreviousPosition, Is.EqualTo(NumericsVector2.Zero));
            Assert.That(snapshot.CurrentPosition, Is.EqualTo(new NumericsVector2(1f, 0f)));
            Assert.That(
                snapshot.InterpolatePosition(0.25f),
                Is.EqualTo(new NumericsVector2(0.25f, 0f)));
            Assert.That(
                snapshot.CurrentStateFlags & SimulationStateFlags.Moving,
                Is.EqualTo(SimulationStateFlags.Moving));
        }

        [Test]
        public void LifetimeQueuesRemovalAndCleanupEmitsEventAndDiagnostics()
        {
            var world = new SimulationWorld();
            var handle = world.CreateProjectile(
                SimulationEntityState.Create(
                    new NumericsVector2(2f, 3f),
                    NumericsVector2.Zero,
                    lifetimeSeconds: 0f));
            var runner = new FixedTickRunner(world);

            runner.Advance(SimulationClock.TickDurationSeconds);

            Assert.That(world.Projectiles.Contains(handle), Is.False);
            Assert.That(world.SpatialGrid.Count, Is.Zero);
            Assert.That(world.Commands.Count, Is.Zero);
            Assert.That(world.Events.Count, Is.EqualTo(1));
            Assert.That(world.Events.GetAt(0).Type, Is.EqualTo(SimulationEventType.Removed));
            Assert.That(world.Events.GetAt(0).Tick, Is.EqualTo(1));
            Assert.That(world.Diagnostics.ActiveEntities, Is.Zero);
            Assert.That(world.Diagnostics.CreatedEntities, Is.EqualTo(1));
            Assert.That(world.Diagnostics.RemovedEntities, Is.EqualTo(1));
            Assert.That(world.Diagnostics.CompletedTicks, Is.EqualTo(1));
            Assert.That(world.Diagnostics.LastTickMilliseconds, Is.GreaterThanOrEqualTo(0d));
        }

        [Test]
        public void StructuralCreateCommandIsAppliedOnlyByCleanup()
        {
            var world = new SimulationWorld();
            var state = SimulationEntityState.Create(
                new NumericsVector2(4f, 5f),
                new NumericsVector2(30f, 0f));
            world.Commands.Create(EntityKind.Actor, state);

            Assert.That(world.Actors.Count, Is.Zero);
            new FixedTickRunner(world).Advance(SimulationClock.TickDurationSeconds);

            Assert.That(world.Actors.Count, Is.EqualTo(1));
            Assert.That(world.Actors.GetStateAt(0).Position, Is.EqualTo(state.Position));
            Assert.That(world.Events.Count, Is.EqualTo(1));
            Assert.That(world.Events.GetAt(0).Type, Is.EqualTo(SimulationEventType.Created));
        }

        [Test]
        public void SameSeedProducesIdenticalHeadlessMovement()
        {
            var first = HeadlessSimulationHarness.Run(180, 424242UL, 8);
            var second = HeadlessSimulationHarness.Run(180, 424242UL, 8);
            var different = HeadlessSimulationHarness.Run(180, 424243UL, 8);

            Assert.That(first.TickCount, Is.EqualTo(180));
            Assert.That(first.AggregateActorPosition, Is.EqualTo(second.AggregateActorPosition));
            Assert.That(first.ExportInvariant(), Is.EqualTo(second.ExportInvariant()));
            Assert.That(first.AggregateActorPosition, Is.Not.EqualTo(different.AggregateActorPosition));
            Assert.That(first.InvalidHandleAccesses, Is.Zero);
        }

        [Test]
        public void HeadlessHarnessDoesNotCreateGameObjects()
        {
            var countBefore = UnityObject.FindObjectsByType<GameObject>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None).Length;

            var summary = HeadlessSimulationHarness.Run(10, 7UL, 2);

            var countAfter = UnityObject.FindObjectsByType<GameObject>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None).Length;
            Assert.That(summary.ActorCount, Is.EqualTo(2));
            Assert.That(countAfter, Is.EqualTo(countBefore));
        }

        private static SimulationWorld CreateMovingWorld(out EntityHandle handle)
        {
            var world = new SimulationWorld();
            handle = world.CreateActor(
                SimulationEntityState.Create(
                    new NumericsVector2(3f, -2f),
                    new NumericsVector2(1.5f, -0.25f)));
            return world;
        }

        private static SimulationEntityState CreateState(float x)
        {
            return SimulationEntityState.Create(
                new NumericsVector2(x, 0f),
                NumericsVector2.Zero);
        }

        private sealed class RecordingSystem : ISimulationSystem
        {
            private readonly int value;
            private readonly int[] calls;
            private readonly Func<int> nextIndex;

            public RecordingSystem(int value, int[] calls, Func<int> nextIndex)
            {
                this.value = value;
                this.calls = calls;
                this.nextIndex = nextIndex;
            }

            public SimulationSystemId Id => SimulationSystemId.Movement;

            public void Execute(SimulationWorld world)
            {
                calls[nextIndex()] = value;
            }
        }
    }
}
