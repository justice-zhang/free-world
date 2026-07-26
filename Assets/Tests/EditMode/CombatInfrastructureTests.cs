using System;
using System.Numerics;
using System.Reflection;
using Game.Content.Runtime;
using Game.Core;
using Game.Simulation;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public sealed class CombatInfrastructureTests
    {
        private static readonly double TickSeconds = SimulationClock.TickDurationSeconds;
        private static readonly float TickFloat = (float)SimulationClock.TickDurationSeconds;

        [Test]
        public void ActorStoreRejectsDefaultCombatStateAtomically()
        {
            var store = new ActorStore(1);
            var state = SimulationEntityState.Create(Vector2.Zero, Vector2.Zero);

            Assert.Throws<ArgumentException>(() => store.Create(state, default));
            Assert.That(store.Count, Is.Zero);
        }

        [Test]
        public void ActorStoreReusesCombatRecordWhenAHandleSlotIsReused()
        {
            var store = new ActorStore(1);
            var state = SimulationEntityState.Create(Vector2.Zero, Vector2.Zero);
            var firstStats = StatBaseValues.CreateDefault(10f, 5f);
            var firstCombat = new ActorCombatInitialization(
                firstStats,
                10f,
                5f,
                5f,
                default);
            var first = store.Create(state, firstCombat);
            var firstRecord = GetCombatRecord(store, first.Index);

            Assert.That(store.Remove(first), Is.True);
            var secondStats = StatBaseValues.CreateDefault(20f, 7f);
            var secondCombat = new ActorCombatInitialization(
                secondStats,
                20f,
                0f,
                0f,
                default);
            var second = store.Create(state, secondCombat);
            var secondRecord = GetCombatRecord(store, second.Index);

            Assert.That(second.Index, Is.EqualTo(first.Index));
            Assert.That(secondRecord, Is.SameAs(firstRecord));
            Assert.That(store.TryReadHealth(second, out var health), Is.True);
            Assert.That(health.Current, Is.EqualTo(20f));
            Assert.That(health.Maximum, Is.EqualTo(20f));
            Assert.That(store.TryReadShield(second, out var shield), Is.True);
            Assert.That(shield.Current, Is.Zero);
            Assert.That(shield.Maximum, Is.Zero);
        }

        [Test]
        public void CombatEventsFlushAtTickEndEvenWhenCustomPipelineOmitsFlushSystem()
        {
            var pipeline = new SimulationPipeline(new DamageResolutionSystem());
            var world = new SimulationWorld(pipeline: pipeline);
            var source = CreateActor(world, 100f);
            var target = CreateActor(world, 100f);
            world.QueueDamage(
                new DamagePacket(
                    source,
                    target,
                    Id("test.skill.custom_pipeline"),
                    DamageType.True,
                    DamageTags.Direct,
                    1f,
                    false,
                    1f,
                    Vector2.Zero,
                    Vector2.Zero,
                    0));

            new FixedTickRunner(world).Advance(TickSeconds);

            Assert.That(world.CombatEvents.DamageAppliedCount, Is.EqualTo(1));
        }

        [Test]
        public void TemporaryShieldCapacityDoesNotStackOnRefreshAndExpires()
        {
            var catalog = new RuntimeStatusCatalog();
            var index = new RuntimeContentIndex(0);
            catalog.Register(
                index,
                new RuntimeStatusDefinition(
                    Id("test.status.temporary_shield"),
                    "content.test.status.temporary_shield.name",
                    "content.test.status.temporary_shield.description",
                    "Assets/Test/TemporaryShield.asset",
                    Array.Empty<ContentTag>(),
                    StatusStackingPolicy.RefreshDuration,
                    TickFloat * 2f,
                    1,
                    0f,
                    Array.Empty<ContentTag>(),
                    Array.Empty<ContentTag>(),
                    new RuntimeStatusBehavior(default, default, 10f)));
            var world = new SimulationWorld(statusCatalog: catalog);
            var source = CreateActor(world, 100f);
            var target = CreateActor(world, 100f);
            var runner = new FixedTickRunner(world);

            world.QueueStatus(Request(source, target, index));
            runner.Advance(TickSeconds);
            world.QueueStatus(Request(source, target, index));
            runner.Advance(TickSeconds);

            Assert.That(world.Actors.TryReadShield(target.Handle, out var refreshed), Is.True);
            Assert.That(refreshed.Current, Is.EqualTo(10f));
            Assert.That(refreshed.Maximum, Is.EqualTo(10f));

            runner.Advance(TickSeconds);
            runner.Advance(TickSeconds);

            Assert.That(world.Actors.TryReadShield(target.Handle, out var expired), Is.True);
            Assert.That(expired.Current, Is.Zero);
            Assert.That(expired.Maximum, Is.Zero);
            Assert.That(world.CombatEvents.ShieldChangedCount, Is.EqualTo(1));
            Assert.That(world.CombatEvents.GetShieldChangedAt(0).Delta, Is.EqualTo(-10f));
        }

        [Test]
        public void ConsumedTemporaryShieldStillEmitsCapacityChangeWhenItExpires()
        {
            var catalog = new RuntimeStatusCatalog();
            var index = new RuntimeContentIndex(0);
            catalog.Register(
                index,
                new RuntimeStatusDefinition(
                    Id("test.status.consumed_temporary_shield"),
                    "content.test.status.consumed_temporary_shield.name",
                    "content.test.status.consumed_temporary_shield.description",
                    "Assets/Test/ConsumedTemporaryShield.asset",
                    Array.Empty<ContentTag>(),
                    StatusStackingPolicy.RefreshDuration,
                    TickFloat,
                    1,
                    0f,
                    Array.Empty<ContentTag>(),
                    Array.Empty<ContentTag>(),
                    new RuntimeStatusBehavior(default, default, 10f)));
            var world = new SimulationWorld(statusCatalog: catalog);
            var source = CreateActor(world, 100f);
            var target = CreateActor(world, 100f);
            var runner = new FixedTickRunner(world);

            world.QueueStatus(Request(source, target, index));
            runner.Advance(TickSeconds);
            world.QueueDamage(
                new DamagePacket(
                    source,
                    target,
                    Id("test.skill.consume_temporary_shield"),
                    DamageType.True,
                    DamageTags.Direct,
                    10f,
                    false,
                    1f,
                    Vector2.Zero,
                    Vector2.Zero,
                    0));

            runner.Advance(TickSeconds);

            Assert.That(world.Actors.TryReadShield(target.Handle, out var shield), Is.True);
            Assert.That(shield.Current, Is.Zero);
            Assert.That(shield.Maximum, Is.Zero);
            Assert.That(world.CombatEvents.ShieldChangedCount, Is.EqualTo(2));
            var expiration = world.CombatEvents.GetShieldChangedAt(1);
            Assert.That(expiration.Delta, Is.Zero);
            Assert.That(expiration.MaximumDelta, Is.EqualTo(-10f));
            Assert.That(
                expiration.SourceContentId,
                Is.EqualTo(Id("test.status.consumed_temporary_shield")));
        }

        [Test]
        public void TemporaryShieldApplicationRejectsAggregateCapacityOverflow()
        {
            var catalog = new RuntimeStatusCatalog();
            var index = new RuntimeContentIndex(0);
            catalog.Register(
                index,
                new RuntimeStatusDefinition(
                    Id("test.status.shield_overflow"),
                    "content.test.status.shield_overflow.name",
                    "content.test.status.shield_overflow.description",
                    "Assets/Test/ShieldOverflow.asset",
                    Array.Empty<ContentTag>(),
                    StatusStackingPolicy.RefreshDuration,
                    10f,
                    1,
                    0f,
                    Array.Empty<ContentTag>(),
                    Array.Empty<ContentTag>(),
                    new RuntimeStatusBehavior(default, default, float.MaxValue)));
            var world = new SimulationWorld(statusCatalog: catalog);
            var source = CreateActor(world, 100f);
            var state = SimulationEntityState.Create(Vector2.Zero, Vector2.Zero);
            var stats = StatBaseValues.CreateDefault(100f, 5f);
            var combat = new ActorCombatInitialization(
                stats,
                100f,
                0f,
                float.MaxValue,
                default);
            var target = new SpatialEntity(
                EntityKind.Actor,
                world.Actors.Create(state, combat));

            world.QueueStatus(Request(source, target, index));
            new FixedTickRunner(world).Advance(TickSeconds);

            Assert.That(world.Actors.TryReadStatus(target.Handle, index, out _), Is.False);
            Assert.That(world.Actors.TryReadShield(target.Handle, out var shield), Is.True);
            Assert.That(shield.Current, Is.Zero);
            Assert.That(shield.Maximum, Is.EqualTo(float.MaxValue));
            Assert.That(world.CombatEvents.ShieldChangedCount, Is.Zero);
            Assert.That(world.Diagnostics.RejectedStatusApplications, Is.EqualTo(1));
        }

        [Test]
        public void StatusApplicationRequestCannotOverrideBakedBehavior()
        {
            Assert.That(
                typeof(StatusApplicationRequest).GetProperty("Payload"),
                Is.Null);
            var constructors = typeof(StatusApplicationRequest).GetConstructors();
            Assert.That(constructors, Has.Length.EqualTo(1));
            Assert.That(constructors[0].GetParameters(), Has.Length.EqualTo(6));
        }

        private static object GetCombatRecord(ActorStore store, int index)
        {
            var combatField = typeof(ActorStore).GetField(
                "combat",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(combatField, Is.Not.Null);
            var storage = combatField.GetValue(store);
            var recordsField = storage.GetType().GetField(
                "records",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(recordsField, Is.Not.Null);
            var records = (Array)recordsField.GetValue(storage);
            return records.GetValue(index);
        }

        private static SpatialEntity CreateActor(SimulationWorld world, float health)
        {
            var state = SimulationEntityState.Create(Vector2.Zero, Vector2.Zero);
            var combat = ActorCombatInitialization.CreateDefault(health, 5f);
            return new SpatialEntity(EntityKind.Actor, world.Actors.Create(state, combat));
        }

        private static StatusApplicationRequest Request(
            SpatialEntity source,
            SpatialEntity target,
            RuntimeContentIndex statusIndex)
        {
            return new StatusApplicationRequest(
                source,
                target,
                Id("test.skill.status_source"),
                statusIndex,
                1f,
                0);
        }

        private static ContentId Id(string value)
        {
            var result = ContentId.Create(value);
            Assert.That(result.IsSuccess, Is.True, result.Error.ToString());
            return result.Value;
        }
    }
}
