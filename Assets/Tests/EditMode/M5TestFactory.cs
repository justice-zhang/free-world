using System;
using System.Numerics;
using Game.Content.Runtime;
using Game.Core;
using Game.Simulation;

namespace Game.Tests.EditMode
{
    internal sealed class M5Fixture
    {
        public ContentRegistry Registry;
        public BakedContentCatalog Catalog;
        public RuntimeSkillDefinition Skill;
        public RuntimeEnemyDefinition Chase;
        public RuntimeEnemyDefinition KeepDistance;
        public RuntimeEnemyDefinition Charge;
        public RuntimeEnemyDefinition Ranged;
        public RuntimeEnemyDefinition Boss;
        public RuntimeEncounterSchedule Encounter;
        public RuntimeMapDefinition FiniteMap;
        public RuntimeMapDefinition InfiniteMap;
    }

    internal static class M5TestFactory
    {
        public static M5Fixture Create(float duration = 300f, float bossTime = 150f, int cap = 16)
        {
            var skill = SkillTestFactory.Skill(
                "test.skill.m5_shared_attack",
                SkillModuleIds.TriggerTimer,
                SkillTestFactory.Module(SkillModuleIds.TargetingNearest, 16f, int0: 1),
                SkillTestFactory.Module(SkillModuleIds.DeliveryInstant),
                new[] { SkillTestFactory.Damage(1f) },
                0.5f);
            var chase = Enemy("test.enemy.m5_chase", EnemyMovementMode.Chase, skill.Id, 2f, 1.5f);
            var keep = Enemy("test.enemy.m5_keep", EnemyMovementMode.KeepDistance, skill.Id, 4f, 6f);
            var charge = Enemy("test.enemy.m5_charge", EnemyMovementMode.Charge, skill.Id, 2f, 4f);
            var ranged = Enemy("test.enemy.m5_ranged", EnemyMovementMode.Ranged, skill.Id, 5f, 7f);
            var boss = Enemy("test.enemy.m5_boss", EnemyMovementMode.Charge, skill.Id, 3f, 6f, 250f);
            var encounter = Encounter(duration, bossTime, cap, chase, keep, charge, ranged, boss);
            var finite = Map(
                "test.map.m5_finite",
                MapBoundsMode.Finite,
                encounter.Id,
                new[]
                {
                    new RuntimeMapObstacle(new Vector2(-1f, -4f), new Vector2(1f, -2f))
                });
            var infinite = Map(
                "test.map.m5_infinite",
                MapBoundsMode.ChunkedInfinite,
                encounter.Id,
                Array.Empty<RuntimeMapObstacle>());
            var definitions = new RuntimeContentDefinition[]
            {
                skill, chase, keep, charge, ranged, boss, encounter, finite, infinite
            };
            var manifest = new ContentPackManifest(
                Id("test.pack.m5_runtime"),
                SkillTestFactory.GameVersion,
                ContentPackTopology.EnemyMapEncounterSchemaVersion,
                SkillTestFactory.GameVersion,
                null,
                Array.Empty<ContentPackDependency>(),
                "packs/test/m5_runtime",
                "pack.test.m5_runtime",
                false,
                "Assets/Test/M5RuntimePack.asset");
            var catalog = BakedContentCatalog.Create(manifest, definitions);
            var registry = new ContentRegistry();
            var load = registry.Load(new[] { catalog }, SkillTestFactory.GameVersion);
            if (!load.IsSuccess) throw new InvalidOperationException(load.Error.ToString());
            return new M5Fixture
            {
                Registry = registry,
                Catalog = catalog,
                Skill = skill,
                Chase = chase,
                KeepDistance = keep,
                Charge = charge,
                Ranged = ranged,
                Boss = boss,
                Encounter = encounter,
                FiniteMap = finite,
                InfiniteMap = infinite
            };
        }

        public static SimulationWorld World(
            M5Fixture fixture,
            RuntimeMapDefinition mapDefinition,
            ulong seed = 17UL,
            bool schedule = true,
            DifficultySnapshot? difficulty = null)
        {
            var skillCatalog = SkillRuntimeCatalog.Build(
                fixture.Registry,
                SkillModuleRegistry.CreateDefault());
            if (!skillCatalog.IsSuccess) throw new InvalidOperationException(skillCatalog.Error.ToString());
            var enemyCatalog = EnemyRuntimeCatalog.Build(fixture.Registry);
            if (!enemyCatalog.IsSuccess) throw new InvalidOperationException(enemyCatalog.Error.ToString());
            var difficultySnapshot = difficulty ?? DifficultySnapshot.Default;
            var map = MapRuntimeFactory.Create(mapDefinition, seed);
            var enemies = new EnemyRuntime(enemyCatalog.Value, difficultySnapshot, 32);
            var skills = new SkillRuntime(skillCatalog.Value, seed, 32);
            var encounter = schedule
                ? new EncounterScheduler(fixture.Encounter, map, difficultySnapshot, seed)
                : null;
            var world = new SimulationWorld(
                seed,
                32,
                2f,
                SimulationPipeline.CreateM5Default(),
                null,
                null,
                skills,
                enemies,
                map,
                encounter);
            var player = world.CreateActor(
                SimulationEntityState.Create(Vector2.Zero, Vector2.Zero),
                ActorCombatInitialization.CreateDefault(1_000_000f, 5f));
            world.SetPlayer(player);
            return world;
        }

        public static EntityHandle Spawn(
            SimulationWorld world,
            ContentRegistry registry,
            RuntimeEnemyDefinition enemy,
            Vector2 position)
        {
            if (!registry.TryGet(enemy.Id, out var entry)) throw new InvalidOperationException();
            world.Enemies.PendingSpawns.Add(
                new SpawnRequest(entry.Index, position, false, false, 0));
            new CleanupSystem().Execute(world);
            for (var index = 0; index < world.Actors.Count; index++)
            {
                var handle = world.Actors.GetHandleAt(index);
                if (world.Enemies.IsEnemy(handle)) return handle;
            }

            throw new InvalidOperationException("Enemy fixture did not spawn.");
        }

        private static RuntimeEnemyDefinition Enemy(
            string id,
            EnemyMovementMode mode,
            ContentId skillId,
            float preferredDistance,
            float attackRange,
            float health = 40f)
        {
            return new RuntimeEnemyDefinition(
                Id(id),
                "content." + id + ".name",
                "content." + id + ".description",
                "Assets/Test/" + id + ".asset",
                Array.Empty<ContentTag>(),
                health,
                0.5f,
                2f,
                1f,
                attackRange,
                skillId,
                2f,
                1f,
                Id("placeholder.visual." + id.Replace("test.enemy.", string.Empty)),
                new RuntimeEnemyBehavior(
                    mode,
                    preferredDistance,
                    0.1f,
                    0.1f,
                    0.2f,
                    2f,
                    0.4f,
                    1.25f,
                    0.5f,
                    1f));
        }

        private static RuntimeEncounterSchedule Encounter(
            float duration,
            float bossTime,
            int cap,
            RuntimeEnemyDefinition chase,
            RuntimeEnemyDefinition keep,
            RuntimeEnemyDefinition charge,
            RuntimeEnemyDefinition ranged,
            RuntimeEnemyDefinition boss)
        {
            return new RuntimeEncounterSchedule(
                Id("test.encounter.m5_shared"),
                "content.test.encounter.m5_shared.name",
                "content.test.encounter.m5_shared.description",
                "Assets/Test/M5Encounter.asset",
                Array.Empty<ContentTag>(),
                cap,
                8f,
                12f,
                new[]
                {
                    new RuntimeEncounterPhase(
                        0f,
                        duration,
                        3f,
                        6f,
                        0.5f,
                        0.25f,
                        cap,
                        SpawnPattern.Ring,
                        default,
                        new[]
                        {
                            new RuntimeEncounterEnemyEntry(chase.Id, 2f, 1f, 1, 3, false),
                            new RuntimeEncounterEnemyEntry(keep.Id, 1f, 2f, 1, 2, false),
                            new RuntimeEncounterEnemyEntry(charge.Id, 1f, 3f, 1, 2, false),
                            new RuntimeEncounterEnemyEntry(ranged.Id, 1f, 2f, 1, 2, true)
                        },
                        bossTime >= 0f && bossTime < duration
                            ? new[]
                            {
                                new RuntimeEncounterBossRule(
                                    boss.Id,
                                    bossTime,
                                    SpawnPattern.FixedAnchor,
                                    Id("test.anchor.boss"))
                            }
                            : Array.Empty<RuntimeEncounterBossRule>())
                });
        }

        private static RuntimeMapDefinition Map(
            string id,
            MapBoundsMode mode,
            ContentId encounterId,
            RuntimeMapObstacle[] obstacles)
        {
            return new RuntimeMapDefinition(
                Id(id),
                "content." + id + ".name",
                "content." + id + ".description",
                "Assets/Test/" + id + ".asset",
                Array.Empty<ContentTag>(),
                mode == MapBoundsMode.Finite ? "base.map.finite_arena" : "base.map.chunked_infinite",
                "maps/" + id,
                mode,
                new Vector2(-20f, -14f),
                new Vector2(20f, 14f),
                16f,
                2,
                encounterId,
                Id("placeholder.visual." + id.Replace("test.map.", string.Empty)),
                obstacles,
                new[]
                {
                    new RuntimeMapAnchor(Id("test.anchor.boss"), new Vector2(10f, 0f)),
                    new RuntimeMapAnchor(Id("test.anchor.portal"), new Vector2(-10f, 0f))
                });
        }

        private static ContentId Id(string value) => SkillTestFactory.Id(value);
    }
}
