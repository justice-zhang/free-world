using System;
using System.Numerics;
using Game.Content.Runtime;
using Game.Core;
using Game.Simulation;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public sealed class CombatStatusTests
    {
        private static readonly double TickSeconds = SimulationClock.TickDurationSeconds;
        private static readonly float TickFloat = (float)SimulationClock.TickDurationSeconds;

        [Test]
        public void BuiltInStatCatalogMapsFourteenStableIds()
        {
            var catalog = StatCatalog.Default;
            var ids = new[]
            {
                BuiltInStatIds.Health,
                BuiltInStatIds.MoveSpeed,
                BuiltInStatIds.Damage,
                BuiltInStatIds.AttackSpeed,
                BuiltInStatIds.Cooldown,
                BuiltInStatIds.Range,
                BuiltInStatIds.Duration,
                BuiltInStatIds.ProjectileCount,
                BuiltInStatIds.Pierce,
                BuiltInStatIds.CriticalChance,
                BuiltInStatIds.Armor,
                BuiltInStatIds.PickupRange,
                BuiltInStatIds.Luck,
                BuiltInStatIds.Regeneration
            };

            Assert.That(catalog.Count, Is.EqualTo(14));
            Assert.That(default(StatIndex).IsValid, Is.False);
            for (var index = 0; index < ids.Length; index++)
            {
                Assert.That(catalog.TryGetIndex(ids[index], out var runtimeIndex), Is.True);
                Assert.That(runtimeIndex.Value, Is.EqualTo(index));
                Assert.That(catalog.GetId(runtimeIndex), Is.EqualTo(ids[index]));
            }
        }

        [Test]
        public void ModifierCollectionAppliesAllStagesInDefinedOrder()
        {
            var collection = new ModifierCollection();
            Add(collection, "test.source.flat", ModifierOperation.AddFlat, 2f);
            Add(collection, "test.source.percent", ModifierOperation.AddPercent, 0.5f);
            Add(collection, "test.source.multiply", ModifierOperation.Multiply, 2f);
            Add(collection, "test.source.minimum", ModifierOperation.ClampMinimum, 5f);
            Add(collection, "test.source.maximum", ModifierOperation.ClampMaximum, 30f);

            Assert.That(
                collection.Evaluate(BuiltInStatIndices.Damage, 10f),
                Is.EqualTo(30f));

            Add(
                collection,
                "test.source.override",
                ModifierOperation.Override,
                7f,
                priority: 10);
            Add(
                collection,
                "test.source.lower_priority_override_added_last",
                ModifierOperation.Override,
                9f,
                priority: 5);
            Assert.That(
                collection.Evaluate(BuiltInStatIndices.Damage, 10f),
                Is.EqualTo(7f));
        }

        [Test]
        public void ModifierGroupsUsePriorityAndResumeSuppressedEntryAfterExpiry()
        {
            var collection = new ModifierCollection();
            var group = Id("test.stack.damage");
            Add(
                collection,
                "test.source.low",
                ModifierOperation.AddFlat,
                2f,
                priority: 1,
                group: group);
            Add(
                collection,
                "test.source.high",
                ModifierOperation.AddFlat,
                5f,
                priority: 2,
                group: group,
                duration: 0.5f);
            Add(
                collection,
                "test.source.ungrouped",
                ModifierOperation.AddFlat,
                1f);

            Assert.That(
                collection.Evaluate(BuiltInStatIndices.Damage, 10f),
                Is.EqualTo(16f));

            collection.Tick(0.5f);

            Assert.That(
                collection.Evaluate(BuiltInStatIndices.Damage, 10f),
                Is.EqualTo(13f));
            Assert.That(collection.Count, Is.EqualTo(2));
        }

        [Test]
        public void ModifierGroupTieUsesLatestEntryDeterministically()
        {
            var collection = new ModifierCollection();
            var group = Id("test.stack.tie");
            Add(
                collection,
                "test.source.first",
                ModifierOperation.AddFlat,
                2f,
                priority: 10,
                group: group);
            Add(
                collection,
                "test.source.second",
                ModifierOperation.AddFlat,
                5f,
                priority: 10,
                group: group);

            Assert.That(
                collection.Evaluate(BuiltInStatIndices.Damage, 10f),
                Is.EqualTo(15f));
        }

        [Test]
        public void PublicCombatApiExposesHealthAsReadOnlySnapshot()
        {
            Assert.That(
                typeof(ActorStore).GetMethod("SetHealth"),
                Is.Null);
            Assert.That(
                typeof(ActorStore).GetMethod("TryWriteHealth"),
                Is.Null);
            Assert.That(
                typeof(Health).GetProperty(nameof(Health.Current)).CanWrite,
                Is.False);
            Assert.That(
                typeof(Health).GetProperty(nameof(Health.Maximum)).CanWrite,
                Is.False);
        }

        [Test]
        public void M3PipelineExtendsM2WithoutChangingM2Order()
        {
            var m2 = SimulationPipeline.CreateM2Default();
            Assert.That(m2.Count, Is.EqualTo(4));
            Assert.That(m2.GetSystemId(0), Is.EqualTo(SimulationSystemId.Movement));
            Assert.That(m2.GetSystemId(3), Is.EqualTo(SimulationSystemId.SnapshotBuild));

            var m3 = SimulationPipeline.CreateM3Default();
            var expected = new[]
            {
                SimulationSystemId.Movement,
                SimulationSystemId.DamageResolution,
                SimulationSystemId.StatusTick,
                SimulationSystemId.Death,
                SimulationSystemId.Lifetime,
                SimulationSystemId.Cleanup,
                SimulationSystemId.EventFlush,
                SimulationSystemId.SnapshotBuild
            };

            Assert.That(m3.Count, Is.EqualTo(expected.Length));
            for (var index = 0; index < expected.Length; index++)
            {
                Assert.That(m3.GetSystemId(index), Is.EqualTo(expected[index]));
            }
        }

        [Test]
        public void DamageResolvesArmorThenShieldThenHealth()
        {
            var world = new SimulationWorld(seed: 10UL);
            var source = CreateActor(world, StatBaseValues.CreateDefault(100f, 5f));
            var targetStats = StatBaseValues.CreateDefault(100f, 5f);
            targetStats.Armor = 100f;
            var target = CreateActor(
                world,
                targetStats,
                currentShield: 20f,
                maximumShield: 20f);
            var packet = Packet(
                source,
                target,
                60f,
                DamageType.Physical,
                canCritical: false);

            Assert.That(world.QueueDamage(packet), Is.True);
            new FixedTickRunner(world).Advance(TickSeconds);

            Assert.That(world.Actors.TryReadShield(target.Handle, out var shield), Is.True);
            Assert.That(shield.Current, Is.EqualTo(0f));
            Assert.That(world.Actors.TryReadHealth(target.Handle, out var health), Is.True);
            Assert.That(health.Current, Is.EqualTo(90f).Within(0.0001f));
            Assert.That(world.CombatEvents.DamageAppliedCount, Is.EqualTo(1));
            Assert.That(world.CombatEvents.ShieldChangedCount, Is.EqualTo(1));
            var context = world.CombatEvents.GetDamageAppliedAt(0).Context;
            Assert.That(context.MitigatedValue, Is.EqualTo(30f).Within(0.0001f));
            Assert.That(context.ShieldAbsorbed, Is.EqualTo(20f));
            Assert.That(context.HealthDamage, Is.EqualTo(10f).Within(0.0001f));
        }

        [Test]
        public void CriticalResistanceTrueDamageAndBoundsAreDeterministic()
        {
            var rules = new CombatRules(0f, 50f, 2f, 100f, 0.95f, 4);
            var world = new SimulationWorld(seed: 99UL, combatRules: rules);
            var sourceStats = StatBaseValues.CreateDefault(100f, 5f);
            sourceStats.CriticalChance = 1f;
            var source = CreateActor(world, sourceStats);
            var target = CreateActor(
                world,
                StatBaseValues.CreateDefault(200f, 5f),
                resistances: new ResistanceProfile(0.5f, 0f, 0f, 0f));

            world.QueueDamage(Packet(
                source,
                target,
                20f,
                DamageType.Fire,
                canCritical: true));
            world.QueueDamage(Packet(
                source,
                target,
                float.PositiveInfinity,
                DamageType.True,
                canCritical: false));
            world.QueueDamage(Packet(
                source,
                target,
                -100f,
                DamageType.True,
                canCritical: false));
            new FixedTickRunner(world).Advance(TickSeconds);

            Assert.That(world.Actors.TryReadHealth(target.Handle, out var health), Is.True);
            Assert.That(health.Current, Is.EqualTo(130f).Within(0.0001f));
            Assert.That(world.CombatEvents.DamageAppliedCount, Is.EqualTo(3));
            Assert.That(world.CombatEvents.GetDamageAppliedAt(0).Context.WasCritical, Is.True);
            Assert.That(
                world.CombatEvents.GetDamageAppliedAt(0).Context.FinalDamage,
                Is.EqualTo(20f).Within(0.0001f));
            Assert.That(
                world.CombatEvents.GetDamageAppliedAt(1).Context.FinalDamage,
                Is.EqualTo(50f));
            Assert.That(
                world.CombatEvents.GetDamageAppliedAt(2).Context.FinalDamage,
                Is.EqualTo(0f));
        }

        [Test]
        public void InvalidTargetAndExcessProcDepthFailSafely()
        {
            var rules = new CombatRules(0f, 100f, 2f, 100f, 0.95f, 2);
            var world = new SimulationWorld(combatRules: rules);
            var source = CreateActor(world, StatBaseValues.CreateDefault());
            var invalid = new SpatialEntity(EntityKind.Actor, new EntityHandle(999, 1));

            Assert.That(
                world.QueueDamage(Packet(
                    source,
                    invalid,
                    10f,
                    DamageType.True,
                    canCritical: false)),
                Is.True);
            var overDepth = new DamagePacket(
                source,
                invalid,
                Id("test.skill.depth"),
                DamageType.True,
                DamageTags.Secondary,
                10f,
                false,
                1f,
                Vector2.Zero,
                Vector2.Zero,
                3);
            Assert.That(world.QueueDamage(overDepth), Is.False);

            Assert.DoesNotThrow(() => new FixedTickRunner(world).Advance(TickSeconds));
            Assert.That(world.CombatEvents.DamageAppliedCount, Is.Zero);
            Assert.That(world.Diagnostics.RejectedDamagePackets, Is.EqualTo(1));
            Assert.That(world.Diagnostics.TruncatedProcChains, Is.EqualTo(1));
            Assert.That(world.Diagnostics.InvalidHandleAccesses, Is.Zero);
        }

        [Test]
        public void SameSeedProducesSameCriticalSequence()
        {
            var first = CreateCriticalWorld(4242UL, out var firstSource, out var firstTarget);
            var second = CreateCriticalWorld(4242UL, out var secondSource, out var secondTarget);
            for (var index = 0; index < 20; index++)
            {
                first.QueueDamage(Packet(
                    firstSource,
                    firstTarget,
                    1f,
                    DamageType.True,
                    canCritical: true));
                second.QueueDamage(Packet(
                    secondSource,
                    secondTarget,
                    1f,
                    DamageType.True,
                    canCritical: true));
            }

            new FixedTickRunner(first).Advance(TickSeconds);
            new FixedTickRunner(second).Advance(TickSeconds);

            Assert.That(first.CombatEvents.DamageAppliedCount, Is.EqualTo(20));
            Assert.That(second.CombatEvents.DamageAppliedCount, Is.EqualTo(20));
            for (var index = 0; index < 20; index++)
            {
                Assert.That(
                    first.CombatEvents.GetDamageAppliedAt(index).Context.WasCritical,
                    Is.EqualTo(
                        second.CombatEvents.GetDamageAppliedAt(index).Context.WasCritical));
            }
        }

        [Test]
        public void MultipleLethalPacketsEmitOneDeathAndOneRemoval()
        {
            var world = new SimulationWorld();
            var source = CreateActor(world, StatBaseValues.CreateDefault());
            var target = CreateActor(world, StatBaseValues.CreateDefault(10f, 5f));
            world.QueueDamage(Packet(
                source,
                target,
                10f,
                DamageType.True,
                canCritical: false));
            world.QueueDamage(Packet(
                source,
                target,
                10f,
                DamageType.True,
                canCritical: false));

            new FixedTickRunner(world).Advance(TickSeconds);

            Assert.That(world.Actors.Contains(target.Handle), Is.False);
            Assert.That(world.CombatEvents.EntityDiedCount, Is.EqualTo(1));
            Assert.That(world.Diagnostics.RemovedEntities, Is.EqualTo(1));
            Assert.That(world.Diagnostics.InvalidHandleAccesses, Is.Zero);
        }

        [Test]
        public void AllFourStatusStackingPoliciesHonorMaximumsAndStrength()
        {
            var catalog = new RuntimeStatusCatalog();
            var refreshIndex = Register(
                catalog,
                0,
                Status("test.status.refresh", StatusStackingPolicy.RefreshDuration, 10f, 1));
            var stacksIndex = Register(
                catalog,
                1,
                Status("test.status.stacks", StatusStackingPolicy.AddStacks, 10f, 2));
            var strongerIndex = Register(
                catalog,
                2,
                Status("test.status.stronger", StatusStackingPolicy.ReplaceIfStronger, 10f, 1));
            var independentIndex = Register(
                catalog,
                3,
                Status("test.status.independent", StatusStackingPolicy.IndependentInstances, 10f, 2));
            var world = new SimulationWorld(statusCatalog: catalog);
            var source = CreateActor(world, StatBaseValues.CreateDefault());
            var refreshTarget = CreateActor(world, StatBaseValues.CreateDefault());
            var stacksTarget = CreateActor(world, StatBaseValues.CreateDefault());
            var strongerTarget = CreateActor(world, StatBaseValues.CreateDefault());
            var independentTarget = CreateActor(world, StatBaseValues.CreateDefault());

            world.QueueStatus(StatusRequest(source, refreshTarget, refreshIndex, 1f));
            world.QueueStatus(StatusRequest(source, refreshTarget, refreshIndex, 2f));
            world.QueueStatus(StatusRequest(source, stacksTarget, stacksIndex, 1f));
            world.QueueStatus(StatusRequest(source, stacksTarget, stacksIndex, 1f));
            world.QueueStatus(StatusRequest(source, stacksTarget, stacksIndex, 1f));
            world.QueueStatus(StatusRequest(source, strongerTarget, strongerIndex, 2f));
            world.QueueStatus(StatusRequest(source, strongerTarget, strongerIndex, 1f));
            world.QueueStatus(StatusRequest(source, strongerTarget, strongerIndex, 3f));
            world.QueueStatus(StatusRequest(source, independentTarget, independentIndex, 1f));
            world.QueueStatus(StatusRequest(source, independentTarget, independentIndex, 2f));
            world.QueueStatus(StatusRequest(source, independentTarget, independentIndex, 3f));

            new FixedTickRunner(world).Advance(TickSeconds);

            Assert.That(
                world.Actors.TryReadStatus(refreshTarget.Handle, refreshIndex, out var refresh),
                Is.True);
            Assert.That(refresh.Stacks, Is.EqualTo(1));
            Assert.That(refresh.Strength, Is.EqualTo(2f));
            Assert.That(
                world.Actors.TryReadStatus(stacksTarget.Handle, stacksIndex, out var stacks),
                Is.True);
            Assert.That(stacks.Stacks, Is.EqualTo(2));
            Assert.That(
                world.Actors.TryReadStatus(strongerTarget.Handle, strongerIndex, out var stronger),
                Is.True);
            Assert.That(stronger.Strength, Is.EqualTo(3f));
            Assert.That(
                world.Actors.GetStatusInstanceCount(independentTarget.Handle, independentIndex),
                Is.EqualTo(2));
            Assert.That(world.Diagnostics.RejectedStatusApplications, Is.EqualTo(2));
        }

        [Test]
        public void BurningPayloadTicksObservablyExpiresAndQueuesCentralDamage()
        {
            var catalog = new RuntimeStatusCatalog();
            var periodic = new RuntimeStatusPeriodicDamage(
                DamageType.Fire,
                DamageTags.DamageOverTime,
                5f,
                false,
                1f,
                Vector2.Zero);
            var burningIndex = Register(
                catalog,
                0,
                Status(
                    "test.status.burning",
                    StatusStackingPolicy.AddStacks,
                    TickFloat * 3f,
                    3,
                    TickFloat,
                    tags: new[] { Tag("status.debuff") },
                    dispelTags: new[] { Tag("dispel.debuff") },
                    behavior: new RuntimeStatusBehavior(default, periodic)));
            var world = new SimulationWorld(statusCatalog: catalog);
            var source = CreateActor(world, StatBaseValues.CreateDefault());
            var target = CreateActor(world, StatBaseValues.CreateDefault(100f, 5f));
            world.QueueStatus(StatusRequest(source, target, burningIndex, 1f));
            var runner = new FixedTickRunner(world);

            runner.Advance(TickSeconds);
            runner.Advance(TickSeconds);

            Assert.That(
                world.Actors.TryReadStatus(target.Handle, burningIndex, out var active),
                Is.True);
            Assert.That(active.TickCount, Is.EqualTo(1));
            Assert.That(world.DamageRequests.Count, Is.EqualTo(1));

            runner.Advance(TickSeconds);
            Assert.That(world.Actors.TryReadHealth(target.Handle, out var afterFirst), Is.True);
            Assert.That(afterFirst.Current, Is.EqualTo(95f).Within(0.0001f));

            runner.Advance(TickSeconds);
            Assert.That(world.Actors.TryReadStatus(target.Handle, burningIndex, out _), Is.False);
            runner.Advance(TickSeconds);
            Assert.That(world.Actors.TryReadHealth(target.Handle, out var final), Is.True);
            Assert.That(final.Current, Is.EqualTo(85f).Within(0.0001f));
        }

        [Test]
        public void SlowModifierIsRemovedWhenStatusExpires()
        {
            var catalog = new RuntimeStatusCatalog();
            var modifier = new RuntimeStatusModifier(
                BuiltInStatIds.MoveSpeed,
                ModifierOperation.Multiply,
                0.5f,
                10,
                Id("test.stack.slow"));
            var slowIndex = Register(
                catalog,
                0,
                Status(
                    "test.status.slow",
                    StatusStackingPolicy.ReplaceIfStronger,
                    TickFloat * 2f,
                    1,
                    behavior: new RuntimeStatusBehavior(modifier, default)));
            var world = new SimulationWorld(statusCatalog: catalog);
            var source = CreateActor(world, StatBaseValues.CreateDefault());
            var target = CreateActor(world, StatBaseValues.CreateDefault(100f, 5f));
            world.QueueStatus(StatusRequest(source, target, slowIndex, 1f));
            var runner = new FixedTickRunner(world);

            runner.Advance(TickSeconds);
            Assert.That(
                world.Actors.TryReadStat(
                    target.Handle,
                    BuiltInStatIndices.MoveSpeed,
                    out var slowed),
                Is.True);
            Assert.That(slowed, Is.EqualTo(2.5f).Within(0.0001f));

            runner.Advance(TickSeconds);
            runner.Advance(TickSeconds);

            Assert.That(world.Actors.TryReadStatus(target.Handle, slowIndex, out _), Is.False);
            Assert.That(
                world.Actors.TryReadStat(
                    target.Handle,
                    BuiltInStatIndices.MoveSpeed,
                    out var restored),
                Is.True);
            Assert.That(restored, Is.EqualTo(5f).Within(0.0001f));
        }

        [Test]
        public void InvalidDefinitionBehaviorRejectsApplicationSafely()
        {
            var catalog = new RuntimeStatusCatalog();
            var unknownStatResult = StatId.Create("test.stat.unknown");
            Assert.That(unknownStatResult.IsSuccess, Is.True);
            var invalidModifier = new RuntimeStatusModifier(
                unknownStatResult.Value,
                ModifierOperation.Multiply,
                0.25f,
                20,
                Id("test.stack.slow"));
            var slowIndex = Register(
                catalog,
                0,
                Status(
                    "test.status.slow",
                    StatusStackingPolicy.RefreshDuration,
                    10f,
                    1,
                    behavior: new RuntimeStatusBehavior(invalidModifier, default)));
            var world = new SimulationWorld(statusCatalog: catalog);
            var source = CreateActor(world, StatBaseValues.CreateDefault());
            var target = CreateActor(world, StatBaseValues.CreateDefault(100f, 5f));
            var runner = new FixedTickRunner(world);
            world.QueueStatus(StatusRequest(source, target, slowIndex, 2f));
            runner.Advance(TickSeconds);

            Assert.That(
                world.Actors.TryReadStatus(target.Handle, slowIndex, out _),
                Is.False);
            Assert.That(world.Diagnostics.RejectedStatusApplications, Is.EqualTo(1));
        }

        [Test]
        public void DispelAndImmunityUseDefinitionTagsWithoutContentSpecificBranches()
        {
            var debuffTag = Tag("status.debuff");
            var cleanseTag = Tag("dispel.protection");
            var catalog = new RuntimeStatusCatalog();
            var shieldedIndex = Register(
                catalog,
                0,
                Status(
                    "test.status.shielded",
                    StatusStackingPolicy.RefreshDuration,
                    10f,
                    1,
                    tags: new[] { Tag("status.buff") },
                    dispelTags: new[] { cleanseTag },
                    immunityTags: new[] { debuffTag }));
            var burningIndex = Register(
                catalog,
                1,
                Status(
                    "test.status.burning",
                    StatusStackingPolicy.AddStacks,
                    10f,
                    3,
                    tags: new[] { debuffTag }));
            var world = new SimulationWorld(statusCatalog: catalog);
            var source = CreateActor(world, StatBaseValues.CreateDefault());
            var target = CreateActor(world, StatBaseValues.CreateDefault());
            var runner = new FixedTickRunner(world);

            world.QueueStatus(StatusRequest(source, target, shieldedIndex, 1f));
            runner.Advance(TickSeconds);
            world.QueueStatus(StatusRequest(source, target, burningIndex, 1f));
            runner.Advance(TickSeconds);

            Assert.That(world.Actors.TryReadStatus(target.Handle, burningIndex, out _), Is.False);

            world.QueueStatusDispel(new StatusDispelRequest(target, cleanseTag));
            world.QueueStatus(StatusRequest(source, target, burningIndex, 1f));
            runner.Advance(TickSeconds);

            Assert.That(world.Actors.TryReadStatus(target.Handle, shieldedIndex, out _), Is.False);
            Assert.That(world.Actors.TryReadStatus(target.Handle, burningIndex, out _), Is.True);
        }

        [Test]
        public void ShieldedPayloadGrantsShieldAndEmitsChangedEvent()
        {
            var catalog = new RuntimeStatusCatalog();
            var shieldedIndex = Register(
                catalog,
                0,
                Status(
                    "test.status.shielded",
                    StatusStackingPolicy.RefreshDuration,
                    10f,
                    1,
                    behavior: new RuntimeStatusBehavior(default, default, 10f)));
            var world = new SimulationWorld(statusCatalog: catalog);
            var source = CreateActor(world, StatBaseValues.CreateDefault());
            var target = CreateActor(world, StatBaseValues.CreateDefault());
            world.QueueStatus(StatusRequest(source, target, shieldedIndex, 1f));

            new FixedTickRunner(world).Advance(TickSeconds);

            Assert.That(world.Actors.TryReadShield(target.Handle, out var shield), Is.True);
            Assert.That(shield.Current, Is.EqualTo(10f));
            Assert.That(shield.Maximum, Is.EqualTo(10f));
            Assert.That(world.CombatEvents.ShieldChangedCount, Is.EqualTo(1));
            Assert.That(world.CombatEvents.GetShieldChangedAt(0).Delta, Is.EqualTo(10f));
        }

        [Test]
        public void CombatEventsAccumulateCatchUpTicksAndClearOnNextRealBatch()
        {
            var world = new SimulationWorld();
            var source = CreateActor(world, StatBaseValues.CreateDefault());
            var target = CreateActor(world, StatBaseValues.CreateDefault(100f, 5f));
            world.QueueDamage(Packet(
                source,
                target,
                1f,
                DamageType.True,
                canCritical: false));
            var runner = new FixedTickRunner(world);

            Assert.That(runner.Advance(TickSeconds * 2d), Is.EqualTo(2));
            Assert.That(world.CombatEvents.DamageAppliedCount, Is.EqualTo(1));
            Assert.That(runner.Advance(0d), Is.Zero);
            Assert.That(world.CombatEvents.DamageAppliedCount, Is.EqualTo(1));

            Assert.That(runner.Advance(TickSeconds), Is.EqualTo(1));
            Assert.That(world.CombatEvents.DamageAppliedCount, Is.Zero);
        }

        private static void Add(
            ModifierCollection collection,
            string sourceId,
            ModifierOperation operation,
            float value,
            int priority = 0,
            ContentId group = default,
            float duration = float.PositiveInfinity)
        {
            var modifier = new Modifier(
                Id(sourceId),
                BuiltInStatIds.Damage,
                operation,
                value,
                priority,
                group,
                duration);
            Assert.That(collection.TryAdd(modifier, out _), Is.True);
        }

        private static SimulationWorld CreateCriticalWorld(
            ulong seed,
            out SpatialEntity source,
            out SpatialEntity target)
        {
            var world = new SimulationWorld(seed);
            var sourceStats = StatBaseValues.CreateDefault(100f, 5f);
            sourceStats.CriticalChance = 0.5f;
            source = CreateActor(world, sourceStats);
            target = CreateActor(world, StatBaseValues.CreateDefault(10_000f, 5f));
            return world;
        }

        private static SpatialEntity CreateActor(
            SimulationWorld world,
            StatBaseValues baseStats,
            float currentShield = 0f,
            float maximumShield = 0f,
            ResistanceProfile resistances = default)
        {
            var body = SimulationEntityState.Create(Vector2.Zero, Vector2.Zero);
            var combat = new ActorCombatInitialization(
                baseStats,
                baseStats.Health,
                currentShield,
                maximumShield,
                resistances);
            var handle = world.CreateActor(body, combat);
            return new SpatialEntity(EntityKind.Actor, handle);
        }

        private static DamagePacket Packet(
            SpatialEntity source,
            SpatialEntity target,
            float value,
            DamageType damageType,
            bool canCritical)
        {
            return new DamagePacket(
                source,
                target,
                Id("test.skill.damage"),
                damageType,
                DamageTags.Direct,
                value,
                canCritical,
                1f,
                Vector2.Zero,
                Vector2.Zero,
                0);
        }

        private static StatusApplicationRequest StatusRequest(
            SpatialEntity source,
            SpatialEntity target,
            RuntimeContentIndex statusIndex,
            float strength)
        {
            return new StatusApplicationRequest(
                source,
                target,
                Id("test.skill.status_source"),
                statusIndex,
                strength,
                0);
        }

        private static RuntimeContentIndex Register(
            RuntimeStatusCatalog catalog,
            int index,
            RuntimeStatusDefinition definition)
        {
            var runtimeIndex = new RuntimeContentIndex(index);
            catalog.Register(runtimeIndex, definition);
            return runtimeIndex;
        }

        private static RuntimeStatusDefinition Status(
            string id,
            StatusStackingPolicy policy,
            float duration,
            int maximumStacks,
            float tickInterval = 0f,
            ContentTag[] tags = null,
            ContentTag[] dispelTags = null,
            ContentTag[] immunityTags = null,
            RuntimeStatusBehavior behavior = default)
        {
            return new RuntimeStatusDefinition(
                Id(id),
                "content.test.status.name",
                "content.test.status.description",
                "Assets/Test/Status.asset",
                tags ?? Array.Empty<ContentTag>(),
                policy,
                duration,
                maximumStacks,
                tickInterval,
                dispelTags ?? Array.Empty<ContentTag>(),
                immunityTags ?? Array.Empty<ContentTag>(),
                behavior);
        }

        private static ContentId Id(string value)
        {
            var result = ContentId.Create(value);
            Assert.That(result.IsSuccess, Is.True, result.Error.ToString());
            return result.Value;
        }

        private static ContentTag Tag(string value)
        {
            var result = ContentTag.Create(value);
            Assert.That(result.IsSuccess, Is.True, result.Error.ToString());
            return result.Value;
        }
    }
}
