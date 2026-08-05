using System;
using System.IO;
using Game.Content.Authoring;
using Game.Content.Runtime;
using Game.Core;
using Game.Simulation;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>Exports deterministic twelve-minute Qinglan encounter evidence.</summary>
    public static class QinglanG16HeadlessCommand
    {
        private const ulong Seed = 0x473136454E434E54UL;

        public static void Run()
        {
            try
            {
                var pack = AssetDatabase.LoadAssetAtPath<ContentPackAuthoring>(QinglanG12ContentSetup.PackPath);
                if (pack == null) throw new InvalidOperationException("Qinglan content pack is missing.");
                var bake = ContentBakeUtility.Bake(pack);
                if (!bake.IsSuccess) throw new InvalidOperationException(bake.Error.ToString());
                var registry = new ContentRegistry();
                var load = registry.Load(
                    new[] { bake.Value },
                    new ContentVersion(0, 1, 0));
                if (!load.IsSuccess) throw new InvalidOperationException(load.Error.ToString());
                var encounterId = ParseId("qinglan.encounter.old_court.demo_12m");
                var first = QinglanEncounterHeadlessHarness.Run(registry, encounterId, Seed);
                var second = QinglanEncounterHeadlessHarness.Run(registry, encounterId, Seed);
                if (!first.IsSuccess) throw new InvalidOperationException(first.Error.ToString());
                if (!second.IsSuccess) throw new InvalidOperationException(second.Error.ToString());

                var deterministic = Equivalent(first.Value, second.Value);
                var passed = Valid(first.Value) && Valid(second.Value) && deterministic;
                var report = new HeadlessReportDto
                {
                    schemaVersion = 1,
                    status = passed ? "PASS" : "FAIL",
                    failureReason = passed ? string.Empty : "Headless acceptance criteria were not met.",
                    generatedAtUtc = DateTime.UtcNow.ToString("O"),
                    seed = "0x" + Seed.ToString("X16"),
                    deterministic = deterministic,
                    first = ToDto(first.Value),
                    second = ToDto(second.Value)
                };
                var output = ResolveOutputPath();
                var directory = Path.GetDirectoryName(output);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
                File.WriteAllText(output, JsonUtility.ToJson(report, true));
                Debug.Log("[Qinglan G1.6 Headless] " + report.status + ": " + output);
                EditorApplication.Exit(passed ? 0 : 1);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static bool Valid(QinglanEncounterHeadlessSummary summary)
        {
            return summary.TickCount == QinglanEncounterHeadlessHarness.TwelveMinuteTickCount &&
                   summary.SpawnedEnemies > 0 && summary.Deaths > 0 &&
                   summary.EliteSpawns == 2 && summary.AffixedSpawns == 2 &&
                   summary.BossSpawns == 0 && summary.PositionsFinite &&
                   summary.ConcurrencyRespected && summary.StoppedAfterDuration &&
                   summary.BudgetCleared && summary.EntityLeakFree &&
                   summary.InvalidHandleAccesses == 0 && summary.Minutes.Length == 12;
        }

        private static bool Equivalent(
            QinglanEncounterHeadlessSummary left,
            QinglanEncounterHeadlessSummary right)
        {
            if (left.TickCount != right.TickCount ||
                left.SpawnedEnemies != right.SpawnedEnemies ||
                left.Deaths != right.Deaths ||
                left.EliteSpawns != right.EliteSpawns ||
                left.AffixedSpawns != right.AffixedSpawns ||
                left.BossSpawns != right.BossSpawns ||
                left.PeakEnemies != right.PeakEnemies ||
                left.SpawnChecksum != right.SpawnChecksum ||
                left.DeathChecksum != right.DeathChecksum ||
                left.CombinedChecksum != right.CombinedChecksum ||
                left.Minutes.Length != right.Minutes.Length)
            {
                return false;
            }

            for (var index = 0; index < left.Minutes.Length; index++)
            {
                var a = left.Minutes[index];
                var b = right.Minutes[index];
                if (a.Minute != b.Minute || a.SpawnedEnemies != b.SpawnedEnemies ||
                    a.Deaths != b.Deaths || a.EliteSpawns != b.EliteSpawns ||
                    a.PeakEnemies != b.PeakEnemies)
                    return false;
            }
            return true;
        }

        private static HeadlessSummaryDto ToDto(QinglanEncounterHeadlessSummary source)
        {
            var minutes = new HeadlessMinuteDto[source.Minutes.Length];
            for (var index = 0; index < minutes.Length; index++)
            {
                var minute = source.Minutes[index];
                minutes[index] = new HeadlessMinuteDto
                {
                    minute = minute.Minute,
                    spawnedEnemies = minute.SpawnedEnemies,
                    deaths = minute.Deaths,
                    eliteSpawns = minute.EliteSpawns,
                    peakEnemies = minute.PeakEnemies
                };
            }

            return new HeadlessSummaryDto
            {
                tickCount = source.TickCount,
                spawnedEnemies = source.SpawnedEnemies,
                deaths = source.Deaths,
                eliteSpawns = source.EliteSpawns,
                affixedSpawns = source.AffixedSpawns,
                bossSpawns = source.BossSpawns,
                peakEnemies = source.PeakEnemies,
                spawnChecksum = source.SpawnChecksum.ToString("x16"),
                deathChecksum = source.DeathChecksum.ToString("x16"),
                combinedChecksum = source.CombinedChecksum.ToString("x16"),
                positionsFinite = source.PositionsFinite,
                concurrencyRespected = source.ConcurrencyRespected,
                stoppedAfterDuration = source.StoppedAfterDuration,
                budgetCleared = source.BudgetCleared,
                entityLeakFree = source.EntityLeakFree,
                invalidHandleAccesses = source.InvalidHandleAccesses,
                minutes = minutes
            };
        }

        private static string ResolveOutputPath()
        {
            var configured = Environment.GetEnvironmentVariable("QINGLAN_G16_HEADLESS_OUTPUT");
            if (string.IsNullOrWhiteSpace(configured))
                configured = "TestResults/QinglanDemo/G1.6/headless.json";
            return Path.IsPathRooted(configured)
                ? configured
                : Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), configured));
        }

        private static ContentId ParseId(string value)
        {
            var result = ContentId.Create(value);
            if (!result.IsSuccess) throw new InvalidOperationException(result.Error.ToString());
            return result.Value;
        }

        [Serializable]
        private sealed class HeadlessReportDto
        {
            public int schemaVersion;
            public string status;
            public string failureReason;
            public string generatedAtUtc;
            public string seed;
            public bool deterministic;
            public HeadlessSummaryDto first;
            public HeadlessSummaryDto second;
        }

        [Serializable]
        private sealed class HeadlessSummaryDto
        {
            public long tickCount;
            public long spawnedEnemies;
            public long deaths;
            public int eliteSpawns;
            public int affixedSpawns;
            public int bossSpawns;
            public int peakEnemies;
            public string spawnChecksum;
            public string deathChecksum;
            public string combinedChecksum;
            public bool positionsFinite;
            public bool concurrencyRespected;
            public bool stoppedAfterDuration;
            public bool budgetCleared;
            public bool entityLeakFree;
            public long invalidHandleAccesses;
            public HeadlessMinuteDto[] minutes;
        }

        [Serializable]
        private sealed class HeadlessMinuteDto
        {
            public int minute;
            public long spawnedEnemies;
            public long deaths;
            public int eliteSpawns;
            public int peakEnemies;
        }
    }
}
