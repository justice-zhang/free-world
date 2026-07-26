using System;
using System.Numerics;
using Game.Content.Runtime;
using Game.Core;
using Game.Simulation;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public sealed class StatusLifecycleEdgeTests
    {
        private static readonly double TickSeconds = SimulationClock.TickDurationSeconds;
        private static readonly float TickFloat = (float)SimulationClock.TickDurationSeconds;

        [Test]
        public void PeriodicTickAdvancesOnlyForRemainingActiveDuration()
        {
            var catalog = new RuntimeStatusCatalog();
            var statusIndex = Register(
                catalog,
                Status(
                    "test.status.short_periodic",
                    TickFloat * 0.25f,
                    TickFloat * 0.5f,
                    PeriodicBehavior(5f)));
            var world = new SimulationWorld(statusCatalog: catalog);
            var source = CreateActor(world, 100f);
            var target = CreateActor(world, 100f);
            Assert.That(
                world.QueueStatus(
                    StatusRequest(source, target, statusIndex, 0)),
                Is.True);
            var runner = new FixedTickRunner(world);

            runner.Advance(TickSeconds);
            Assert.That(
                world.Actors.TryReadStatus(target.Handle, statusIndex, out _),
                Is.True);

            runner.Advance(TickSeconds);

            Assert.That(
                world.Actors.TryReadStatus(target.Handle, statusIndex, out _),
                Is.False);
            Assert.That(world.DamageRequests.Count, Is.Zero);
            Assert.That(world.Diagnostics.TruncatedProcChains, Is.Zero);
        }

        [Test]
        public void DeathPendingActorDoesNotAdvancePeriodicStatus()
        {
            var rules = RulesWithMaximumProcDepth(0);
            var catalog = new RuntimeStatusCatalog();
            var statusIndex = Register(
                catalog,
                Status(
                    "test.status.death_boundary",
                    10f,
                    TickFloat,
                    PeriodicBehavior(1f)));
            var world = new SimulationWorld(
                statusCatalog: catalog,
                combatRules: rules);
            var source = CreateActor(world, 100f);
            var target = CreateActor(world, 10f);
            Assert.That(
                world.QueueStatus(
                    StatusRequest(
                        source,
                        target,
                        statusIndex,
                        0)),
                Is.True);
            var runner = new FixedTickRunner(world);
            runner.Advance(TickSeconds);
            Assert.That(world.QueueDamage(Packet(source, target, 10f, 0)), Is.True);

            runner.Advance(TickSeconds);

            Assert.That(world.Diagnostics.TruncatedProcChains, Is.Zero);
            Assert.That(world.DamageRequests.Count, Is.Zero);
        }

        [Test]
        public void InvalidAndNonFiniteBehaviorsAreRejectedWithoutThrowingDuringTick()
        {
            var catalog = new RuntimeStatusCatalog();
            var world = new SimulationWorld(statusCatalog: catalog);
            var source = CreateActor(world, 100f);
            var target = CreateActor(world, 100f);
            var behaviors = new[]
            {
                ModifierBehavior((ModifierOperation)0, 1f),
                ModifierBehavior(ModifierOperation.AddFlat, float.NaN),
                PeriodicBehavior(1f, damageType: (DamageType)0),
                PeriodicBehavior(float.PositiveInfinity),
                PeriodicBehavior(1f, procCoefficient: float.NaN),
                PeriodicBehavior(
                    1f,
                    knockback: new Vector2(float.PositiveInfinity, 0f)),
                PeriodicBehavior(1f, tags: (DamageTags)(1UL << 63)),
                PeriodicBehavior(-1f),
                PeriodicBehavior(1f, procCoefficient: -1f),
                new RuntimeStatusBehavior(
                    default,
                    default,
                    float.PositiveInfinity),
                new RuntimeStatusBehavior(default, default, -1f),
                ModifierBehavior(ModifierOperation.AddFlat, float.MaxValue)
            };
            var statusIndices = new RuntimeContentIndex[behaviors.Length];

            for (var index = 0; index < behaviors.Length; index++)
            {
                statusIndices[index] = Register(
                    catalog,
                    index,
                    Status(
                        "test.status.invalid_behavior." + index,
                        10f,
                        TickFloat,
                        behaviors[index]));
                var strength = index == behaviors.Length - 1
                    ? float.MaxValue
                    : 1f;
                Assert.That(
                    world.QueueStatus(
                        StatusRequest(
                            source,
                            target,
                            statusIndices[index],
                            0,
                            strength)),
                    Is.True);
            }

            Assert.DoesNotThrow(
                () => new FixedTickRunner(world).Advance(TickSeconds));
            for (var index = 0; index < statusIndices.Length; index++)
            {
                Assert.That(
                    world.Actors.TryReadStatus(
                        target.Handle,
                        statusIndices[index],
                        out _),
                    Is.False);
            }

            Assert.That(
                world.Diagnostics.RejectedStatusApplications,
                Is.EqualTo(behaviors.Length));
        }

        [Test]
        public void PeriodicProcBeyondMaximumDepthIsTruncatedBeforeDamageQueue()
        {
            var rules = RulesWithMaximumProcDepth(0);
            var catalog = new RuntimeStatusCatalog();
            var statusIndex = Register(
                catalog,
                Status(
                    "test.status.proc_limit",
                    10f,
                    TickFloat,
                    PeriodicBehavior(10f)));
            var world = new SimulationWorld(
                statusCatalog: catalog,
                combatRules: rules);
            var source = CreateActor(world, 100f);
            var target = CreateActor(world, 100f);
            Assert.That(
                world.QueueStatus(
                    StatusRequest(
                        source,
                        target,
                        statusIndex,
                        0)),
                Is.True);
            var runner = new FixedTickRunner(world);
            runner.Advance(TickSeconds);

            runner.Advance(TickSeconds);

            Assert.That(world.Diagnostics.TruncatedProcChains, Is.EqualTo(1));
            Assert.That(world.DamageRequests.Count, Is.Zero);
            Assert.That(world.Actors.TryReadHealth(target.Handle, out var health), Is.True);
            Assert.That(health.Current, Is.EqualTo(100f));
        }

        private static CombatRules RulesWithMaximumProcDepth(int maximumProcDepth)
        {
            return new CombatRules(
                0f,
                1_000_000f,
                2f,
                100f,
                0.95f,
                maximumProcDepth);
        }

        private static RuntimeStatusBehavior ModifierBehavior(
            ModifierOperation operation,
            float value)
        {
            var modifier = new RuntimeStatusModifier(
                BuiltInStatIds.MoveSpeed,
                operation,
                value,
                0,
                default);
            return new RuntimeStatusBehavior(modifier, default);
        }

        private static RuntimeStatusBehavior PeriodicBehavior(
            float baseValue,
            DamageType damageType = DamageType.Fire,
            float procCoefficient = 1f,
            Vector2 knockback = default,
            DamageTags tags = DamageTags.DamageOverTime)
        {
            var periodic = new RuntimeStatusPeriodicDamage(
                damageType,
                tags,
                baseValue,
                false,
                procCoefficient,
                knockback);
            return new RuntimeStatusBehavior(default, periodic);
        }

        private static StatusApplicationRequest StatusRequest(
            SpatialEntity source,
            SpatialEntity target,
            RuntimeContentIndex statusIndex,
            int procDepth,
            float strength = 1f)
        {
            return new StatusApplicationRequest(
                source,
                target,
                Id("test.skill.status_source"),
                statusIndex,
                strength,
                procDepth);
        }

        private static DamagePacket Packet(
            SpatialEntity source,
            SpatialEntity target,
            float value,
            int procDepth)
        {
            return new DamagePacket(
                source,
                target,
                Id("test.skill.lethal"),
                DamageType.True,
                DamageTags.Direct,
                value,
                false,
                1f,
                Vector2.Zero,
                Vector2.Zero,
                procDepth);
        }

        private static RuntimeContentIndex Register(
            RuntimeStatusCatalog catalog,
            RuntimeStatusDefinition definition)
        {
            return Register(catalog, 0, definition);
        }

        private static RuntimeContentIndex Register(
            RuntimeStatusCatalog catalog,
            int value,
            RuntimeStatusDefinition definition)
        {
            var index = new RuntimeContentIndex(value);
            catalog.Register(index, definition);
            return index;
        }

        private static RuntimeStatusDefinition Status(
            string id,
            float duration,
            float tickInterval,
            RuntimeStatusBehavior behavior = default)
        {
            return new RuntimeStatusDefinition(
                Id(id),
                "content.test.status.name",
                "content.test.status.description",
                "Assets/Test/Status.asset",
                Array.Empty<ContentTag>(),
                StatusStackingPolicy.RefreshDuration,
                duration,
                1,
                tickInterval,
                Array.Empty<ContentTag>(),
                Array.Empty<ContentTag>(),
                behavior);
        }

        private static SpatialEntity CreateActor(
            SimulationWorld world,
            float health)
        {
            var body = SimulationEntityState.Create(Vector2.Zero, Vector2.Zero);
            var stats = StatBaseValues.CreateDefault(health, 5f);
            var combat = new ActorCombatInitialization(
                stats,
                health,
                0f,
                0f,
                default);
            var handle = world.CreateActor(body, combat);
            return new SpatialEntity(EntityKind.Actor, handle);
        }

        private static ContentId Id(string value)
        {
            var result = ContentId.Create(value);
            Assert.That(result.IsSuccess, Is.True, result.Error.ToString());
            return result.Value;
        }
    }
}
