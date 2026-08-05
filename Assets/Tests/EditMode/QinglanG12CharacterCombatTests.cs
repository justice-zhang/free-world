using System;
using System.IO;
using System.Numerics;
using Game.Content.Authoring;
using Game.Content.Runtime;
using Game.Core;
using Game.Editor;
using Game.Simulation;
using NUnit.Framework;
using UnityEditor;

namespace Game.Tests.EditMode
{
    public sealed class QinglanG12CharacterCombatTests
    {
        private const double TickSeconds = SimulationClock.TickDurationSeconds;
        private static readonly ContentVersion GameVersion = new ContentVersion(0, 1, 0);

        [Test]
        public void CheckedInSliceBakesCharacterMechanicTraitsAndSevenStatuses()
        {
            var registry = LoadRegistry(out var catalog);
            Assert.That(catalog.Manifest.SchemaVersion, Is.EqualTo(6));
            Assert.That(catalog.Manifest.PackId, Is.EqualTo(Id("qinglan.pack.demo")));
            Assert.That(catalog.Definitions.Count, Is.EqualTo(12));

            Assert.That(
                registry.TryGet(Id("qinglan.character.lu_qingye"), out RuntimeCharacterDefinition character),
                Is.True);
            Assert.That(character.BaseMaxHealth, Is.EqualTo(120f));
            Assert.That(character.MoveSpeed, Is.EqualTo(6f));
            Assert.That(character.StartingSkillIds, Is.Empty,
                "G1.3 owns the starting weapon implementation and will fill this reference.");
            Assert.That(character.MechanicIds, Is.EqualTo(new[]
            {
                Id("qinglan.mechanic.lu_qingye.riding_wind")
            }));

            Assert.That(
                registry.TryGet(character.MechanicIds[0], out RuntimeCharacterMechanicDefinition mechanic),
                Is.True);
            Assert.That(mechanic.ResourceId, Is.EqualTo(Id("qinglan.resource.riding_wind")));
            Assert.That(mechanic.GainPerUnit, Is.EqualTo(1f));
            Assert.That(mechanic.LossOnDamage, Is.EqualTo(8f));
            Assert.That(mechanic.Tiers.Count, Is.EqualTo(3));
            Assert.That(mechanic.Tiers[0].Threshold, Is.EqualTo(6f));
            Assert.That(mechanic.Tiers[1].Threshold, Is.EqualTo(16f));
            Assert.That(mechanic.Tiers[2].Threshold, Is.EqualTo(30f));
            Assert.That(mechanic.Tiers[0].OutputId,
                Is.EqualTo(Id("qinglan.trait.lu_qingye.riding_wind.breeze")));
            Assert.That(mechanic.Tiers[1].OutputId,
                Is.EqualTo(Id("qinglan.trait.lu_qingye.riding_wind.swift")));
            Assert.That(mechanic.Tiers[2].OutputId,
                Is.EqualTo(Id("qinglan.trait.lu_qingye.riding_wind")));
            Assert.That(HasTag(
                Entry(registry, "qinglan.trait.lu_qingye.riding_wind.breeze").Definition,
                "mechanic.output.affinity_only"), Is.True);
            Assert.That(HasTag(
                Entry(registry, "qinglan.trait.lu_qingye.riding_wind.swift").Definition,
                "mechanic.output.innate_only"), Is.True);
            Assert.That(HasTag(
                Entry(registry, "qinglan.trait.lu_qingye.riding_wind").Definition,
                "mechanic.output.return_secondary"), Is.True);

            var statusIds = new[]
            {
                "qinglan.status.burning",
                "qinglan.status.poisoned",
                "qinglan.status.slowed",
                "qinglan.status.rooted",
                "qinglan.status.armor_broken",
                "qinglan.status.marked",
                "qinglan.status.damage_immunity"
            };
            for (var index = 0; index < statusIds.Length; index++)
                Assert.That(registry.TryGet(Id(statusIds[index]), out RuntimeStatusDefinition _), Is.True, statusIds[index]);

            var burning = Status(registry, "qinglan.status.burning");
            Assert.That(burning.StackingPolicy, Is.EqualTo(StatusStackingPolicy.AddStacks));
            Assert.That(burning.MaxStacks, Is.EqualTo(5));
            Assert.That(burning.Behavior.PeriodicDamage.DamageType, Is.EqualTo(DamageType.Fire));
            var poisoned = Status(registry, "qinglan.status.poisoned");
            Assert.That(poisoned.StackingPolicy, Is.EqualTo(StatusStackingPolicy.IndependentInstances));
            Assert.That(poisoned.MaxStacks, Is.EqualTo(4));
            var slowed = Status(registry, "qinglan.status.slowed");
            Assert.That(slowed.Behavior.Modifier.StatId, Is.EqualTo(BuiltInStatIds.MoveSpeed));
            Assert.That(slowed.Behavior.Modifier.Value, Is.EqualTo(0.70f));
            var rooted = Status(registry, "qinglan.status.rooted");
            Assert.That(rooted.Behavior.Modifier.Operation, Is.EqualTo(ModifierOperation.Override));
            Assert.That(rooted.Behavior.Modifier.Value, Is.Zero);
            var armorBroken = Status(registry, "qinglan.status.armor_broken");
            Assert.That(armorBroken.Behavior.Modifier.StatId, Is.EqualTo(BuiltInStatIds.Armor));
            var marked = Status(registry, "qinglan.status.marked");
            Assert.That(marked.StackingPolicy, Is.EqualTo(StatusStackingPolicy.AddStacks));
            Assert.That(marked.MaxStacks, Is.EqualTo(6));
            Assert.That(HasTag(Status(registry, "qinglan.status.damage_immunity"),
                "base.damage_policy.immune.all"), Is.True);
        }

        [Test]
        public void ResolvedPlayerMovementCrossesAllTiersWhilePauseTeleportAndBlockedMovementDoNotAccumulate()
        {
            var registry = LoadRegistry(out _);
            var mechanics = new CharacterMechanicRuntime(1);
            var hub = new QinglanRuntimeHub(mechanics);
            var world = World(hub, null, new RuntimeStatusCatalog(registry));
            var player = Actor(world, 120f, 0f);
            Assert.That(QinglanCharacterBinding.Attach(
                registry, Id("qinglan.character.lu_qingye"), player.Handle, mechanics).IsSuccess, Is.True);
            var runner = new FixedTickRunner(world);

            SetVelocity(world, player.Handle, new Vector2(60f, 0f));
            AdvanceCommandTicks(world, runner, player.Handle, 3);
            AssertSnapshot(mechanics, player.Handle, 6f, 1);
            AssertTierEvent(mechanics, 0, 1, CharacterMechanicChangeReason.ResolvedMovement, 3);
            AdvanceCommandTicks(world, runner, player.Handle, 5);
            AssertSnapshot(mechanics, player.Handle, 16f, 2);
            AdvanceCommandTicks(world, runner, player.Handle, 7);
            AssertSnapshot(mechanics, player.Handle, 30f, 3);

            runner.Clock.Pause();
            world.MovementSources.SetSource(player.Handle, MovementSource.PlayerCommand);
            Assert.That(runner.Advance(1d), Is.Zero);
            AssertSnapshot(mechanics, player.Handle, 30f, 3);
            runner.Clock.Resume();

            world.MovementSources.SetSource(player.Handle, MovementSource.Teleport);
            runner.Advance(TickSeconds);
            AssertSnapshot(mechanics, player.Handle, 30f, 3);

            var blockedMechanics = new CharacterMechanicRuntime(1);
            var blockedWorld = World(
                new QinglanRuntimeHub(blockedMechanics),
                new BlockingMapRuntime(),
                new RuntimeStatusCatalog(registry));
            var blockedPlayer = Actor(blockedWorld, 120f, 0f);
            Assert.That(QinglanCharacterBinding.Attach(
                registry, Id("qinglan.character.lu_qingye"), blockedPlayer.Handle, blockedMechanics).IsSuccess, Is.True);
            SetVelocity(blockedWorld, blockedPlayer.Handle, new Vector2(60f, 0f));
            var blockedRunner = new FixedTickRunner(blockedWorld);
            AdvanceCommandTicks(blockedWorld, blockedRunner, blockedPlayer.Handle, 10);
            AssertSnapshot(blockedMechanics, blockedPlayer.Handle, 0f, 0);
        }

        [Test]
        public void ActualShieldOrHealthDamageDropsExactlyOneTierPerTickButZeroImmunityAndBarrierDoNot()
        {
            var registry = LoadRegistry(out _);
            var mechanics = new CharacterMechanicRuntime(1);
            var world = World(new QinglanRuntimeHub(mechanics), null, new RuntimeStatusCatalog(registry));
            var target = Actor(world, 120f, 5f);
            var source = Actor(world, 120f, 0f);
            Assert.That(QinglanCharacterBinding.Attach(
                registry, Id("qinglan.character.lu_qingye"), target.Handle, mechanics).IsSuccess, Is.True);
            mechanics.Accumulate(new ResolvedMovement(target, MovementSource.PlayerCommand, 45f));
            AssertSnapshot(mechanics, target.Handle, 45f, 3);
            var runner = new FixedTickRunner(world);

            world.QueueDamage(Packet(source, target, 1f));
            world.QueueDamage(Packet(source, target, 1f));
            runner.Advance(TickSeconds);
            Assert.That(mechanics.TryGet(target.Handle, out var afterShield), Is.True);
            Assert.That(afterShield.Tier, Is.EqualTo(2));
            Assert.That(afterShield.CurrentValue, Is.GreaterThanOrEqualTo(16f).And.LessThan(30f));
            Assert.That(afterShield.LastDamageTick, Is.EqualTo(1));
            AssertTierEvent(mechanics, 3, 2, CharacterMechanicChangeReason.ActualDamage, 1);

            var beforeNoDamage = afterShield.CurrentValue;
            world.QueueDamage(Packet(source, target, 0f));
            runner.Advance(TickSeconds);
            AssertSnapshot(mechanics, target.Handle, beforeNoDamage, 2);

            Assert.That(world.DamageChannels.SetImmune(
                target.Handle, BuiltInDamageChannels.Direct, true), Is.True);
            world.QueueDamage(Packet(source, target, 10f));
            runner.Advance(TickSeconds);
            AssertSnapshot(mechanics, target.Handle, beforeNoDamage, 2);

            Assert.That(world.DamageChannels.SetImmune(
                target.Handle, BuiltInDamageChannels.Direct, false), Is.True);
            Assert.That(world.DamageChannels.SetBarrier(
                target.Handle, BuiltInDamageChannels.Direct, 10f), Is.True);
            world.QueueDamage(Packet(source, target, 5f));
            runner.Advance(TickSeconds);
            AssertSnapshot(mechanics, target.Handle, beforeNoDamage, 2);

            Assert.That(world.DamageChannels.SetBarrier(
                target.Handle, BuiltInDamageChannels.Direct, 0f), Is.True);
            world.QueueDamage(Packet(source, target, 5f));
            runner.Advance(TickSeconds);
            Assert.That(mechanics.TryGet(target.Handle, out var afterHealth), Is.True);
            Assert.That(afterHealth.Tier, Is.EqualTo(1));
            Assert.That(afterHealth.CurrentValue, Is.GreaterThanOrEqualTo(6f).And.LessThan(16f));
        }

        [Test]
        public void DamageImmunityStatusAndMarkedStacksExecuteThroughCentralStatusAndDamagePipelines()
        {
            var registry = LoadRegistry(out _);
            var statusCatalog = new RuntimeStatusCatalog(registry);
            var world = World(new QinglanRuntimeHub(), null, statusCatalog);
            var source = Actor(world, 100f, 0f);
            var target = Actor(world, 100f, 0f);
            var immunityIndex = Entry(registry, "qinglan.status.damage_immunity").Index;
            var markedIndex = Entry(registry, "qinglan.status.marked").Index;
            var runner = new FixedTickRunner(world);

            world.QueueStatus(StatusRequest(source, target, immunityIndex));
            world.QueueStatus(StatusRequest(source, target, markedIndex));
            world.QueueStatus(StatusRequest(source, target, markedIndex));
            world.QueueStatus(StatusRequest(source, target, markedIndex));
            runner.Advance(TickSeconds);
            Assert.That(world.Actors.TryReadStatus(target.Handle, immunityIndex, out _), Is.True);
            Assert.That(world.Actors.TryReadStatus(target.Handle, markedIndex, out var marked), Is.True);
            Assert.That(marked.Stacks, Is.EqualTo(3));

            world.QueueDamage(Packet(source, target, 20f));
            runner.Advance(TickSeconds);
            Assert.That(world.Actors.TryReadHealth(target.Handle, out var health), Is.True);
            Assert.That(health.Current, Is.EqualTo(100f));
            Assert.That(world.CombatEvents.DamageResolvedCount, Is.EqualTo(1));
            Assert.That(world.CombatEvents.GetDamageResolvedAt(0).Outcome,
                Is.EqualTo(DamageResolutionOutcome.Immune));
            Assert.That(world.CombatEvents.DamageAppliedCount, Is.Zero);
        }

        [Test]
        public void MechanicCleanupRemovesOwnerAndFixedSeedChecksumIsStable()
        {
            var registry = LoadRegistry(out _);
            var mechanics = new CharacterMechanicRuntime(1);
            var world = World(new QinglanRuntimeHub(mechanics), null, new RuntimeStatusCatalog(registry));
            var player = Actor(world, 120f, 0f);
            Assert.That(QinglanCharacterBinding.Attach(
                registry, Id("qinglan.character.lu_qingye"), player.Handle, mechanics).IsSuccess, Is.True);
            world.Commands.Remove(EntityKind.Actor, player.Handle);
            new FixedTickRunner(world).Advance(TickSeconds);
            Assert.That(mechanics.TryGet(player.Handle, out _), Is.False);
            Assert.That(mechanics.AvailableCapacity, Is.EqualTo(1));

            var first = RunChecksum(registry, 0x4731325249444555UL);
            var second = RunChecksum(registry, 0x4731325249444555UL);
            var different = RunChecksum(registry, 0x4731325249444556UL);
            TestContext.Out.WriteLine("G1.2 riding-wind checksum: 0x" + first.ToString("X16"));
            Assert.That(first, Is.EqualTo(0xFD82A621E9E5AD8EUL));
            Assert.That(first, Is.EqualTo(second));
            Assert.That(first, Is.Not.EqualTo(different));
            Assert.That(first, Is.Not.Zero);
        }

        [Test]
        public void ActualMechanicFiftyFourThousandTicksAllocateZeroBytesAndRuntimeHasNoQinglanIdBranch()
        {
            var registry = LoadRegistry(out _);
            var entry = Entry(registry, "qinglan.mechanic.lu_qingye.riding_wind");
            var mechanic = (RuntimeCharacterMechanicDefinition)entry.Definition;
            var runtime = new CharacterMechanicRuntime(1);
            var owner = new EntityHandle(0, 1);
            Assert.That(runtime.TryAttach(owner, entry.Index, mechanic), Is.True);
            var movement = new ResolvedMovement(
                new SpatialEntity(EntityKind.Actor, owner), MovementSource.PlayerCommand, 0.01f);
            runtime.Accumulate(movement);

            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var tick = 0; tick < 54_000; tick++) runtime.Accumulate(movement);
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(allocated, Is.Zero);
            Assert.That(runtime.RejectedNonFiniteInputs, Is.Zero);

            var overflowRuntime = new CharacterMechanicRuntime(1);
            overflowRuntime.TryAttach(owner, entry.Index, mechanic);
            overflowRuntime.Accumulate(new ResolvedMovement(
                new SpatialEntity(EntityKind.Actor, owner), MovementSource.PlayerCommand, float.MaxValue));
            overflowRuntime.Accumulate(new ResolvedMovement(
                new SpatialEntity(EntityKind.Actor, owner), MovementSource.PlayerCommand, float.MaxValue));
            Assert.That(overflowRuntime.RejectedNonFiniteInputs, Is.EqualTo(1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ResolvedMovement(
                new SpatialEntity(EntityKind.Actor, owner), MovementSource.PlayerCommand, float.NaN));

            var files = Directory.GetFiles(
                Path.GetFullPath("Assets/Game/Simulation"), "*.cs", SearchOption.AllDirectories);
            for (var index = 0; index < files.Length; index++)
                Assert.That(File.ReadAllText(files[index]), Does.Not.Contain("qinglan."), files[index]);
        }

        private static ulong RunChecksum(ContentRegistry registry, ulong seed)
        {
            var entry = Entry(registry, "qinglan.mechanic.lu_qingye.riding_wind");
            var runtime = new CharacterMechanicRuntime(1);
            var owner = new EntityHandle(0, 1);
            runtime.TryAttach(owner, entry.Index, (RuntimeCharacterMechanicDefinition)entry.Definition);
            var random = new RandomStream(seed);
            var entity = new SpatialEntity(EntityKind.Actor, owner);
            var hash = 1469598103934665603UL;
            for (var tick = 1; tick <= 900; tick++)
            {
                var distance = random.NextFloat(0.01f, 0.35f);
                runtime.Accumulate(new ResolvedMovement(entity, MovementSource.PlayerCommand, distance));
                if (tick % 97 == 0) runtime.ReactToDamage(owner, tick, 0f, 1f);
                runtime.TryGet(owner, out var snapshot);
                unchecked
                {
                    hash ^= (uint)BitConverter.SingleToInt32Bits(snapshot.CurrentValue);
                    hash *= 1099511628211UL;
                    hash ^= (uint)snapshot.Tier;
                    hash *= 1099511628211UL;
                    hash ^= (ulong)snapshot.LastDamageTick;
                    hash *= 1099511628211UL;
                }
            }
            return hash;
        }

        private static ContentRegistry LoadRegistry(out BakedContentCatalog catalog)
        {
            var pack = AssetDatabase.LoadAssetAtPath<ContentPackAuthoring>(QinglanG12ContentSetup.PackPath);
            Assert.That(pack, Is.Not.Null, QinglanG12ContentSetup.PackPath);
            var bake = ContentBakeUtility.Bake(pack);
            Assert.That(bake.IsSuccess, Is.True, bake.IsSuccess ? string.Empty : bake.Error.ToString());
            catalog = bake.Value;
            var registry = new ContentRegistry();
            var load = registry.Load(new[] { catalog }, GameVersion);
            Assert.That(load.IsSuccess, Is.True, load.IsSuccess ? string.Empty : load.Error.ToString());
            return registry;
        }

        private static SimulationWorld World(
            QinglanRuntimeHub hub,
            IMapRuntime map,
            RuntimeStatusCatalog statusCatalog) =>
            new SimulationWorld(
                hub,
                seed: 0x473132434F4D4241UL,
                initialEntityCapacity: 16,
                pipeline: SimulationPipeline.CreateQinglanDemo(),
                statusCatalog: statusCatalog,
                mapRuntime: map);

        private static SpatialEntity Actor(SimulationWorld world, float health, float shield)
        {
            var stats = StatBaseValues.CreateDefault(health, 6f);
            stats.Armor = 20f;
            var handle = world.CreateActor(
                SimulationEntityState.Create(Vector2.Zero, Vector2.Zero),
                new ActorCombatInitialization(stats, health, shield, shield, default));
            return new SpatialEntity(EntityKind.Actor, handle);
        }

        private static void SetVelocity(SimulationWorld world, EntityHandle actor, Vector2 velocity)
        {
            Assert.That(world.Actors.TryRead(actor, out var state), Is.True);
            state.Velocity = velocity;
            Assert.That(world.Actors.TryWrite(actor, state), Is.True);
        }

        private static void AdvanceCommandTicks(
            SimulationWorld world,
            FixedTickRunner runner,
            EntityHandle actor,
            int count)
        {
            for (var tick = 0; tick < count; tick++)
            {
                Assert.That(world.MovementSources.SetSource(actor, MovementSource.PlayerCommand), Is.True);
                Assert.That(runner.Advance(TickSeconds), Is.EqualTo(1));
            }
        }

        private static void AssertSnapshot(
            CharacterMechanicRuntime runtime,
            EntityHandle owner,
            float value,
            int tier)
        {
            Assert.That(runtime.TryGet(owner, out var snapshot), Is.True);
            Assert.That(snapshot.CurrentValue, Is.EqualTo(value).Within(0.0001f));
            Assert.That(snapshot.Tier, Is.EqualTo(tier));
        }

        private static void AssertTierEvent(
            CharacterMechanicRuntime runtime,
            int previous,
            int current,
            CharacterMechanicChangeReason reason,
            long tick)
        {
            Assert.That(runtime.TierChangeCount, Is.EqualTo(1));
            var change = runtime.GetTierChangeAt(0);
            Assert.That(change.PreviousTier, Is.EqualTo(previous));
            Assert.That(change.CurrentTier, Is.EqualTo(current));
            Assert.That(change.Reason, Is.EqualTo(reason));
            Assert.That(change.Tick, Is.EqualTo(tick));
            Assert.That(change.ResourceId, Is.EqualTo(Id("qinglan.resource.riding_wind")));
        }

        private static DamagePacket Packet(SpatialEntity source, SpatialEntity target, float value) =>
            new DamagePacket(
                source,
                target,
                Id("test.skill.g12_damage"),
                DamageType.True,
                DamageTags.Direct,
                value,
                false,
                1f,
                Vector2.Zero,
                Vector2.Zero,
                0,
                BuiltInDamageChannels.Direct,
                0);

        private static StatusApplicationRequest StatusRequest(
            SpatialEntity source,
            SpatialEntity target,
            RuntimeContentIndex statusIndex) =>
            new StatusApplicationRequest(
                source,
                target,
                Id("test.skill.g12_status"),
                statusIndex,
                1f,
                0);

        private static RuntimeStatusDefinition Status(ContentRegistry registry, string id)
        {
            Assert.That(registry.TryGet(Id(id), out RuntimeStatusDefinition status), Is.True, id);
            return status;
        }

        private static ContentRegistryEntry Entry(ContentRegistry registry, string id)
        {
            Assert.That(registry.TryGet(Id(id), out ContentRegistryEntry entry), Is.True, id);
            return entry;
        }

        private static bool HasTag(RuntimeContentDefinition definition, string value)
        {
            var tag = ContentTag.Create(value).Value;
            for (var index = 0; index < definition.Tags.Count; index++)
                if (definition.Tags[index] == tag) return true;
            return false;
        }

        private static ContentId Id(string value) => ContentId.Create(value).Value;

        private sealed class BlockingMapRuntime : IMapRuntime
        {
            public void Initialize(in MapRuntimeContext context) { }
            public bool IsWalkable(Vector2 position) => position == Vector2.Zero;
            public Vector2 SampleEnemySpawnPosition(
                Vector2 playerPosition,
                float minimumDistance,
                float maximumDistance,
                ref RandomStream random) => playerPosition;
            public Vector2 ResolveMovement(Vector2 currentPosition, Vector2 desiredPosition, float radius) => currentPosition;
            public bool TryGetAnchor(ContentId anchorId, out Vector2 position)
            {
                position = default;
                return false;
            }
            public void UpdateFocus(Vector2 focus) { }
            public MapEnvironmentSnapshot GetEnvironmentSnapshot() => default;
        }
    }
}
