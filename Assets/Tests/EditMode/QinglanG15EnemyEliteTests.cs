using System;
using System.Collections.Generic;
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
    public sealed class QinglanG15EnemyEliteTests
    {
        private const ulong Seed = 0x473135454E454D59UL;
        private static readonly ContentVersion GameVersion = new ContentVersion(0, 1, 0);
        private static readonly string[] EnemyIds =
        {
            "qinglan.enemy.grass_spirit",
            "qinglan.enemy.paper_crane_spirit",
            "qinglan.enemy.wooden_sword_puppet",
            "qinglan.enemy.stone_lantern_guard",
            "qinglan.enemy.wind_bell_spirit",
            "qinglan.enemy.explosive_seed_pod"
        };

        private static readonly EnemyMovementMode[] MovementModes =
        {
            EnemyMovementMode.Chase,
            EnemyMovementMode.Charge,
            EnemyMovementMode.Chase,
            EnemyMovementMode.Ranged,
            EnemyMovementMode.KeepDistance,
            EnemyMovementMode.Chase
        };

        private static readonly string[] AffixIds =
        {
            "qinglan.affix.rampaging",
            "qinglan.affix.barrier",
            "qinglan.affix.splitting",
            "qinglan.affix.quaking"
        };

        [Test]
        public void CheckedInPackContainsCompleteG15EnemySlice()
        {
            var registry = LoadRegistry(out var baked);

            Assert.That(baked.Manifest.SchemaVersion, Is.EqualTo(6));
            Assert.That(baked.Manifest.Version.CompareTo(new ContentVersion(0, 4, 0)), Is.GreaterThanOrEqualTo(0));
            Assert.That(baked.Definitions.Count, Is.GreaterThanOrEqualTo(93));
            Assert.That(baked.ContentHash, Is.Not.Empty);

            for (var index = 0; index < EnemyIds.Length; index++)
            {
                Assert.That(registry.TryGet(Id(EnemyIds[index]), out RuntimeEnemyDefinition enemy), Is.True);
                Assert.That(enemy.HasM5Data, Is.True, EnemyIds[index]);
                Assert.That(enemy.Behavior.MovementMode, Is.EqualTo(MovementModes[index]), EnemyIds[index]);
                Assert.That(registry.TryGet(enemy.AttackSkillId, out RuntimeSkillDefinition skill), Is.True);
                Assert.That(skill.IsExecutable, Is.True, EnemyIds[index]);
                Assert.That(enemy.VisualProfileId.IsValid, Is.True, EnemyIds[index]);
            }

            for (var index = 0; index < AffixIds.Length; index++)
            {
                Assert.That(registry.TryGet(Id(AffixIds[index]), out RuntimeEliteAffixDefinition affix), Is.True);
                Assert.That(affix.PresentationProfileId.IsValid, Is.True, AffixIds[index]);
                Assert.That(affix.RewardMultiplier, Is.InRange(1f, 2f), AffixIds[index]);
                Assert.That(
                    affix.ModifierOutputId.IsValid || affix.SkillId.IsValid || affix.DeathRewardId.IsValid,
                    Is.True,
                    AffixIds[index]);
            }

            Assert.That(SkillModuleIds.IsTargeting(SkillModuleIds.TargetingAlliesCircle), Is.True);
        }

        [Test]
        public void AffixCompositionIsDeterministicCompatibleAndBossSafe()
        {
            var registry = LoadRegistry(out _);
            var world = CreateWorld(registry, Seed, 64, out _);
            Assert.That(world.Enemies.Catalog.TryGet(Id(EnemyIds[0]), out var grass), Is.True);
            Assert.That(world.Enemies.Catalog.TryGet(Id(EnemyIds[1]), out var crane), Is.True);
            var pool = Array.ConvertAll(AffixIds, Id);
            var firstRandom = new RandomStream(Seed);
            var secondRandom = new RandomStream(Seed);

            var first = world.Enemies.ComposeAffixes(grass, pool, true, false, ref firstRandom);
            var second = world.Enemies.ComposeAffixes(grass, pool, true, false, ref secondRandom);
            Assert.That(first.Count, Is.EqualTo(2));
            Assert.That(second.Count, Is.EqualTo(first.Count));
            for (var index = 0; index < first.Count; index++)
                Assert.That(second.GetAt(index).Source.Id, Is.EqualTo(first.GetAt(index).Source.Id));

            var rampagingOnly = new[] { Id("qinglan.affix.rampaging") };
            var excluded = world.Enemies.ComposeAffixes(crane, rampagingOnly, true, false, ref firstRandom);
            Assert.That(excluded.Count, Is.Zero, "Fast paper cranes exclude Rampaging.");
            var boss = world.Enemies.ComposeAffixes(grass, pool, true, true, ref firstRandom);
            Assert.That(boss.Count, Is.Zero, "Boss spawns never inherit normal elite affixes.");
        }

        [Test]
        public void NamedAffixesInstallModifiersAndBoundedShieldSkills()
        {
            var registry = LoadRegistry(out _);
            var world = CreateWorld(registry, Seed, 64, out _);
            Assert.That(world.Enemies.Catalog.TryGet(Id(EnemyIds[0]), out var grass), Is.True);
            var random = new RandomStream(Seed);
            var rampaging = world.Enemies.ComposeAffixes(
                grass,
                new[] { Id("qinglan.affix.rampaging") },
                true,
                false,
                ref random);
            var rampagingEnemy = Spawn(world, grass, new Vector2(8f, 0f), true, rampaging);
            Assert.That(world.Enemies.GetAffixCount(rampagingEnemy), Is.EqualTo(1));
            Assert.That(world.Actors.TryReadStat(rampagingEnemy, BuiltInStatIndices.MoveSpeed, out var speed), Is.True);
            Assert.That(speed, Is.EqualTo(grass.Source.BaseMoveSpeed * 1.35f).Within(0.0001f));

            var barrier = world.Enemies.ComposeAffixes(
                grass,
                new[] { Id("qinglan.affix.barrier") },
                true,
                false,
                ref random);
            var barrierEnemy = Spawn(world, grass, new Vector2(10f, 0f), true, barrier);
            new FixedTickRunner(world).Advance(SimulationClock.TickDurationSeconds);
            Assert.That(world.Actors.TryReadShield(barrierEnemy, out var shield), Is.True);
            Assert.That(shield.Maximum, Is.EqualTo(18f).Within(0.0001f));
            Assert.That(shield.Current, Is.LessThanOrEqualTo(shield.Maximum));
        }

        [Test]
        public void WindBellTargetsAtMostSixNearbyAlliesAndNeverItself()
        {
            var registry = LoadRegistry(out _);
            var world = CreateWorld(registry, Seed, 64, out _);
            Assert.That(world.Enemies.Catalog.TryGet(Id(EnemyIds[0]), out var grass), Is.True);
            Assert.That(world.Enemies.Catalog.TryGet(Id(EnemyIds[4]), out var bell), Is.True);
            var bellHandle = Spawn(world, bell, Vector2.Zero, false, default);
            var allies = new EntityHandle[8];
            for (var index = 0; index < allies.Length; index++)
            {
                var angle = index * ((float)Math.PI * 2f / allies.Length);
                allies[index] = Spawn(
                    world,
                    grass,
                    new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * 2f,
                    false,
                    default);
            }

            new FixedTickRunner(world).Advance(SimulationClock.TickDurationSeconds);
            var shielded = 0;
            for (var index = 0; index < allies.Length; index++)
            {
                Assert.That(world.Actors.TryReadShield(allies[index], out var shield), Is.True);
                if (shield.Maximum > 0f) shielded++;
                Assert.That(shield.Maximum, Is.LessThanOrEqualTo(8f));
            }
            Assert.That(shielded, Is.EqualTo(6));
            Assert.That(world.Actors.TryReadShield(bellHandle, out var bellShield), Is.True);
            Assert.That(bellShield.Maximum, Is.Zero);
        }

        [Test]
        public void SplittingSpawnsOneGenerationOfReducedNonEliteChildren()
        {
            var registry = LoadRegistry(out _);
            var world = CreateWorld(registry, Seed, 64, out var player);
            Assert.That(world.Enemies.Catalog.TryGet(Id(EnemyIds[0]), out var grass), Is.True);
            var random = new RandomStream(Seed);
            var splitting = world.Enemies.ComposeAffixes(
                grass,
                new[] { Id("qinglan.affix.splitting") },
                true,
                false,
                ref random);
            var parent = Spawn(world, grass, new Vector2(8f, 0f), true, splitting);
            Kill(world, player, parent);

            var children = EnemyHandles(world);
            Assert.That(children.Count, Is.EqualTo(2));
            for (var index = 0; index < children.Count; index++)
            {
                Assert.That(world.Enemies.GetSplitGeneration(children[index]), Is.EqualTo(1));
                Assert.That(world.Enemies.GetAffixCount(children[index]), Is.Zero);
                Assert.That(world.Enemies.TryGetSnapshot(children[index], out var snapshot), Is.True);
                Assert.That(snapshot.Elite, Is.False);
                Assert.That(snapshot.ExperienceReward, Is.EqualTo(grass.Source.ExperienceReward * 0.35f).Within(0.0001f));
            }

            Kill(world, player, children[0]);
            Assert.That(world.Enemies.Count, Is.EqualTo(1), "Generation-one children cannot split again.");
        }

        [Test]
        public void SixHundredCentralizedEnemyDecisionsStayFiniteAndAllocationFree()
        {
            var registry = LoadRegistry(out _);
            var world = CreateWorld(registry, Seed, 1024, out _);
            Assert.That(world.Enemies.Catalog.TryGet(Id(EnemyIds[0]), out var grass), Is.True);
            for (var index = 0; index < 600; index++)
            {
                var row = index / 30;
                var column = index % 30;
                world.Enemies.PendingSpawns.Add(
                    new SpawnRequest(
                        grass.Index,
                        new Vector2(12f + column * 0.1f, -3f + row * 0.1f),
                        false,
                        false,
                        index));
            }
            new CleanupSystem().Execute(world);
            for (var index = 0; index < 10; index++) world.Enemies.TickDecisions(world);

            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var index = 0; index < 30; index++) world.Enemies.TickDecisions(world);
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.That(allocated, Is.Zero);
            Assert.That(world.Enemies.Count, Is.EqualTo(600));
            for (var dense = 0; dense < world.Actors.Count; dense++)
            {
                var state = world.Actors.GetStateAt(dense);
                Assert.That(float.IsNaN(state.Velocity.X) || float.IsInfinity(state.Velocity.X), Is.False);
                Assert.That(float.IsNaN(state.Velocity.Y) || float.IsInfinity(state.Velocity.Y), Is.False);
            }
        }

        private static SimulationWorld CreateWorld(
            ContentRegistry registry,
            ulong seed,
            int capacity,
            out EntityHandle player)
        {
            var modules = SkillModuleRegistry.CreateDefault();
            var skills = SkillRuntimeCatalog.Build(registry, modules);
            var enemies = EnemyRuntimeCatalog.Build(registry);
            Assert.That(skills.IsSuccess, Is.True, skills.IsSuccess ? string.Empty : skills.Error.ToString());
            Assert.That(enemies.IsSuccess, Is.True, enemies.IsSuccess ? string.Empty : enemies.Error.ToString());
            var world = new SimulationWorld(
                new QinglanRuntimeHub(),
                seed,
                capacity,
                2f,
                SimulationPipeline.CreateM5Default(),
                new RuntimeStatusCatalog(registry),
                null,
                new SkillRuntime(skills.Value, seed, capacity),
                new EnemyRuntime(enemies.Value, DifficultySnapshot.Default, capacity));
            player = world.CreateActor(
                SimulationEntityState.Create(new Vector2(50f, 0f), Vector2.Zero),
                ActorCombatInitialization.CreateDefault(1_000_000f, 0f));
            world.SetPlayer(player);
            return world;
        }

        private static EntityHandle Spawn(
            SimulationWorld world,
            CompiledEnemyDefinition enemy,
            Vector2 position,
            bool elite,
            in EliteAffixSelection affixes)
        {
            world.Enemies.PendingSpawns.Add(
                new SpawnRequest(enemy.Index, position, elite, false, 1, affixes, 0, 1f, 1f));
            new CleanupSystem().Execute(world);
            return world.Actors.GetHandleAt(world.Actors.Count - 1);
        }

        private static void Kill(SimulationWorld world, EntityHandle player, EntityHandle enemy)
        {
            world.QueueDamage(
                new DamagePacket(
                    new SpatialEntity(EntityKind.Actor, player),
                    new SpatialEntity(EntityKind.Actor, enemy),
                    Id("test.qinglan.g1_5.kill"),
                    DamageType.True,
                    DamageTags.Direct,
                    1_000_000f,
                    false,
                    0f,
                    Vector2.Zero,
                    Vector2.Zero,
                    0));
            new FixedTickRunner(world).Advance(SimulationClock.TickDurationSeconds);
        }

        private static List<EntityHandle> EnemyHandles(SimulationWorld world)
        {
            var output = new List<EntityHandle>();
            for (var index = 0; index < world.Actors.Count; index++)
            {
                var handle = world.Actors.GetHandleAt(index);
                if (world.Enemies.IsEnemy(handle)) output.Add(handle);
            }
            return output;
        }

        private static ContentRegistry LoadRegistry(out BakedContentCatalog baked)
        {
            var pack = AssetDatabase.LoadAssetAtPath<ContentPackAuthoring>(QinglanG12ContentSetup.PackPath);
            Assert.That(pack, Is.Not.Null, QinglanG12ContentSetup.PackPath);
            var bake = ContentBakeUtility.Bake(pack);
            Assert.That(bake.IsSuccess, Is.True, bake.IsSuccess ? string.Empty : bake.Error.ToString());
            baked = bake.Value;
            var registry = new ContentRegistry();
            var load = registry.Load(new[] { baked }, GameVersion);
            Assert.That(load.IsSuccess, Is.True, load.IsSuccess ? string.Empty : load.Error.ToString());
            return registry;
        }

        private static ContentId Id(string value) => ContentId.Create(value).Value;
    }
}
