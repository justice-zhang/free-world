using System;
using Game.Content.Authoring;
using Game.Content.Runtime;
using Game.Core;
using Game.Editor;
using Game.Simulation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests.EditMode
{
    public sealed class QinglanG16EncounterTests
    {
        private const ulong Seed = 0x473136454E434E54UL;
        private static readonly ContentVersion GameVersion = new ContentVersion(0, 1, 0);
        private static readonly ContentId EncounterId = Id("qinglan.encounter.old_court.demo_12m");

        [Test]
        public void CheckedInPackContainsTwelveMinuteEncounter()
        {
            var registry = LoadRegistry(out var baked);

            Assert.That(baked.Manifest.Version.CompareTo(new ContentVersion(0, 5, 0)), Is.GreaterThanOrEqualTo(0));
            Assert.That(baked.Manifest.SchemaVersion, Is.EqualTo(6));
            Assert.That(baked.Definitions.Count, Is.GreaterThanOrEqualTo(94));
            Assert.That(baked.ContentHash, Has.Length.EqualTo(64));
            Assert.That(registry.TryGet(EncounterId, out RuntimeEncounterSchedule schedule), Is.True);
            Assert.That(schedule.MaximumConcurrentEnemies, Is.EqualTo(720));
            Assert.That(schedule.MinimumSpawnDistance, Is.EqualTo(14f));
            Assert.That(schedule.MaximumSpawnDistance, Is.EqualTo(24f));
        }

        [Test]
        public void TimelineIsContinuousAndAddsEnemiesByDesignedPhase()
        {
            var registry = LoadRegistry(out _);
            Assert.That(registry.TryGet(EncounterId, out RuntimeEncounterSchedule schedule), Is.True);
            var expectedStarts = new[] { 0f, 90f, 180f, 270f, 360f, 390f, 450f, 540f, 630f };
            var expectedEnds = new[] { 90f, 180f, 270f, 360f, 390f, 450f, 540f, 630f, 720f };
            var expectedEnemyCounts = new[] { 1, 2, 3, 4, 2, 5, 6, 6, 6 };
            var expectedCaps = new[] { 120, 180, 240, 320, 80, 360, 440, 520, 600 };
            Assert.That(schedule.Phases.Count, Is.EqualTo(expectedStarts.Length));
            var eliteCount = 0;
            for (var index = 0; index < schedule.Phases.Count; index++)
            {
                var phase = schedule.Phases[index];
                Assert.That(phase.StartTimeSeconds, Is.EqualTo(expectedStarts[index]));
                Assert.That(phase.EndTimeSeconds, Is.EqualTo(expectedEnds[index]));
                Assert.That(phase.EnemyEntries.Count, Is.EqualTo(expectedEnemyCounts[index]));
                Assert.That(phase.MaximumConcurrentEnemies, Is.EqualTo(expectedCaps[index]));
                Assert.That(phase.BossRules.Count, Is.Zero, "Boss definitions belong to G2.2.");
                for (var entryIndex = 0; entryIndex < phase.EnemyEntries.Count; entryIndex++)
                    Assert.That(phase.EnemyEntries[entryIndex].AffixPoolIds.Count, Is.EqualTo(4));
                eliteCount += phase.EliteRules.Count;
            }

            Assert.That(eliteCount, Is.EqualTo(2));
            Assert.That(schedule.Phases[2].EliteRules[0].SpawnTimeSeconds, Is.EqualTo(180f));
            Assert.That(schedule.Phases[6].EliteRules[0].SpawnTimeSeconds, Is.EqualTo(450f));
            Assert.That(schedule.Phases[2].EliteRules[0].AffixPoolIds.Count, Is.EqualTo(4));
            Assert.That(schedule.Phases[6].EliteRules[0].AffixPoolIds.Count, Is.EqualTo(4));
        }

        [Test]
        public void SchemaSixJsonRoundTripPreservesEliteRulesAndHash()
        {
            LoadRegistry(out var baked);
            var json = JsonUtility.ToJson(baked.ToDto());
            var restored = JsonUtility.FromJson<BakedContentCatalogDto>(json).ToCatalog();

            Assert.That(restored.IsSuccess, Is.True, restored.IsSuccess ? string.Empty : restored.Error.ToString());
            Assert.That(restored.Value.ContentHash, Is.EqualTo(baked.ContentHash));
            RuntimeEncounterSchedule schedule = null;
            for (var index = 0; index < restored.Value.Definitions.Count; index++)
            {
                if (restored.Value.Definitions[index].Id == EncounterId)
                {
                    schedule = restored.Value.Definitions[index] as RuntimeEncounterSchedule;
                    break;
                }
            }

            Assert.That(schedule, Is.Not.Null);
            Assert.That(schedule.Phases[2].EliteRules.Count, Is.EqualTo(1));
            Assert.That(schedule.Phases[6].EliteRules.Count, Is.EqualTo(1));
            Assert.That(schedule.Phases[2].EliteRules[0].EnemyId,
                Is.EqualTo(Id("qinglan.enemy.wooden_sword_puppet")));
        }

        [Test]
        public void TimelineAnalyzerReportsBothEliteMilestones()
        {
            var registry = LoadRegistry(out _);
            Assert.That(registry.TryGet(EncounterId, out RuntimeEncounterSchedule schedule), Is.True);
            var report = WaveTimelineAnalyzer.Analyze(schedule, registry);

            Assert.That(report.IsSuccess, Is.True, report.IsSuccess ? string.Empty : report.Error.ToString());
            Assert.That(report.Value.Phases.Count, Is.EqualTo(9));
            Assert.That(report.Value.Phases[2].EliteTimes, Is.EqualTo(new[] { 180f }));
            Assert.That(report.Value.Phases[6].EliteTimes, Is.EqualTo(new[] { 450f }));
            Assert.That(report.Value.TotalHealth, Is.GreaterThan(0f));
            Assert.That(report.Value.ExperienceOutput, Is.GreaterThan(0f));
        }

        [Test]
        public void SchedulerReservesCapacityForFutureEliteInsteadOfFillingWithNormalGroup()
        {
            var registry = LoadRegistry(out _);
            Assert.That(registry.TryGet(EncounterId, out RuntimeEncounterSchedule checkedIn), Is.True);
            var grassId = Id("qinglan.enemy.grass_spirit");
            var affixIds = new ContentId[checkedIn.Phases[2].EliteRules[0].AffixPoolIds.Count];
            for (var index = 0; index < affixIds.Length; index++)
                affixIds[index] = checkedIn.Phases[2].EliteRules[0].AffixPoolIds[index];
            var schedule = new RuntimeEncounterSchedule(
                Id("test.encounter.g1_6.reservation"),
                "content.test.encounter.g1_6.reservation.name",
                "content.test.encounter.g1_6.reservation.description",
                "Assets/Test/G16ReservationEncounter.asset",
                Array.Empty<ContentTag>(),
                1,
                8f,
                12f,
                new[]
                {
                    new RuntimeEncounterPhase(
                        0f,
                        2f,
                        100f,
                        100f,
                        0.01f,
                        0.01f,
                        1,
                        SpawnPattern.Ring,
                        default,
                        new[] { new RuntimeEncounterEnemyEntry(grassId, 1f, 1f, 1, 1, false) },
                        new[]
                        {
                            new RuntimeEncounterEliteRule(
                                grassId,
                                1f,
                                SpawnPattern.Ring,
                                default,
                                affixIds)
                        },
                        Array.Empty<RuntimeEncounterBossRule>())
                });
            var skillCatalog = SkillRuntimeCatalog.Build(registry, SkillModuleRegistry.CreateDefault());
            var enemyCatalog = EnemyRuntimeCatalog.Build(registry);
            Assert.That(skillCatalog.IsSuccess, Is.True, skillCatalog.Error.ToString());
            Assert.That(enemyCatalog.IsSuccess, Is.True, enemyCatalog.Error.ToString());
            var mapDefinition = new RuntimeMapDefinition(
                Id("test.map.g1_6.reservation"),
                "content.test.map.g1_6.reservation.name",
                "content.test.map.g1_6.reservation.description",
                "Assets/Test/G16ReservationMap.asset",
                Array.Empty<ContentTag>(),
                "base.map.finite_arena",
                "maps/test/g1_6_reservation",
                MapBoundsMode.Finite,
                new System.Numerics.Vector2(-20f, -14f),
                new System.Numerics.Vector2(20f, 14f),
                16f,
                2,
                schedule.Id,
                Id("placeholder.presentation.test.g1_6.reservation"),
                Array.Empty<RuntimeMapObstacle>(),
                Array.Empty<RuntimeMapAnchor>());
            var map = MapRuntimeFactory.Create(mapDefinition, Seed);
            var enemies = new EnemyRuntime(enemyCatalog.Value, DifficultySnapshot.Default, 8);
            var skills = new SkillRuntime(skillCatalog.Value, Seed, 8);
            var scheduler = new EncounterScheduler(schedule, map, DifficultySnapshot.Default, Seed);
            var world = new SimulationWorld(
                Seed,
                8,
                2f,
                SimulationPipeline.CreateM5Default(),
                null,
                null,
                skills,
                enemies,
                map,
                scheduler);
            var player = world.CreateActor(
                SimulationEntityState.Create(System.Numerics.Vector2.Zero, System.Numerics.Vector2.Zero),
                ActorCombatInitialization.CreateDefault(1_000_000f, 0f));
            world.SetPlayer(player);
            var runner = new FixedTickRunner(world);

            for (var tick = 0; tick < SimulationClock.TickRate; tick++)
                runner.Advance(SimulationClock.TickDurationSeconds);
            Assert.That(enemies.Count, Is.Zero, "The pending elite must own the only capacity slot.");

            runner.Advance(SimulationClock.TickDurationSeconds);
            runner.Advance(SimulationClock.TickDurationSeconds);
            Assert.That(enemies.Count, Is.EqualTo(1));
            Assert.That(enemies.EliteSpawnedCount, Is.EqualTo(1));
            Assert.That(scheduler.EliteRequestCount, Is.EqualTo(1));
            Assert.That(scheduler.SpawnedRequestCount, Is.EqualTo(1));
        }

        [Test]
        public void TwelveMinuteHeadlessRunsAreDeterministicAndStopCleanly()
        {
            var registry = LoadRegistry(out _);
            var first = QinglanEncounterHeadlessHarness.Run(registry, EncounterId, Seed);
            var second = QinglanEncounterHeadlessHarness.Run(registry, EncounterId, Seed);

            Assert.That(first.IsSuccess, Is.True, first.IsSuccess ? string.Empty : first.Error.ToString());
            Assert.That(second.IsSuccess, Is.True, second.IsSuccess ? string.Empty : second.Error.ToString());
            var a = first.Value;
            var b = second.Value;
            Assert.That(a.TickCount, Is.EqualTo(QinglanEncounterHeadlessHarness.TwelveMinuteTickCount));
            Assert.That(a.SpawnedEnemies, Is.GreaterThan(0));
            Assert.That(a.Deaths, Is.GreaterThan(0));
            Assert.That(a.EliteSpawns, Is.EqualTo(2));
            Assert.That(a.AffixedSpawns, Is.EqualTo(2));
            Assert.That(a.BossSpawns, Is.Zero);
            Assert.That(a.PeakEnemies, Is.LessThanOrEqualTo(600));
            Assert.That(a.PositionsFinite, Is.True);
            Assert.That(a.ConcurrencyRespected, Is.True);
            Assert.That(a.StoppedAfterDuration, Is.True);
            Assert.That(a.BudgetCleared, Is.True);
            Assert.That(a.EntityLeakFree, Is.True);
            Assert.That(a.InvalidHandleAccesses, Is.Zero);
            Assert.That(a.Minutes.Length, Is.EqualTo(12));
            Assert.That(a.CombinedChecksum, Is.EqualTo(b.CombinedChecksum));
            Assert.That(a.SpawnedEnemies, Is.EqualTo(b.SpawnedEnemies));
            Assert.That(a.Deaths, Is.EqualTo(b.Deaths));
            Assert.That(a.EliteSpawns, Is.EqualTo(b.EliteSpawns));
            Assert.That(a.PeakEnemies, Is.EqualTo(b.PeakEnemies));
        }

        [Test]
        public void LegacyPhaseConstructorDefaultsEliteRulesToEmpty()
        {
            var phase = new RuntimeEncounterPhase(
                0f,
                1f,
                1f,
                1f,
                1f,
                1f,
                2,
                SpawnPattern.Ring,
                default,
                new[]
                {
                    new RuntimeEncounterEnemyEntry(
                        Id("test.enemy.g1_6.legacy"), 1f, 1f, 1, 1, false)
                },
                Array.Empty<RuntimeEncounterBossRule>());

            Assert.That(phase.EliteRules.Count, Is.Zero);
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
