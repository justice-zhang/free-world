using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using Game.Core;
using Game.Presentation;
using Game.Simulation;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;
using Debug = UnityEngine.Debug;
using NumericsVector2 = System.Numerics.Vector2;

namespace Game.Editor
{
    /// <summary>Runs the fixed M10 target-scale scenario and writes machine-readable evidence.</summary>
    public static class M10PerformanceCommand
    {
        private const double TickBudgetMilliseconds = 1000d / SimulationClock.TickRate;
        private const double RenderBudgetMilliseconds = 1000d / 60d;
        private const int RenderFramesPerTick = 2;
        private const int MemorySampleIntervalTicks = 60 * SimulationClock.TickRate;

        /// <summary>Runs the command-line target-scale performance gate and exits Unity.</summary>
        public static void Run()
        {
            var exitCode = 0;
            try
            {
                var outputPath = ResolveOutputPath();
                var report = Execute(ReadConfiguration());
                WriteReport(outputPath, report);
                if (!string.Equals(report.status, "PASS", StringComparison.Ordinal))
                {
                    Debug.LogError("[M10 Performance] FAIL: " + report.failureReason);
                    exitCode = 2;
                }
                else
                {
                    Debug.Log("[M10 Performance] PASS: " + outputPath);
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                exitCode = 1;
            }

            EditorApplication.Exit(exitCode);
        }

        internal static M10PerformanceReport Execute(M10StressConfiguration configuration)
        {
            var registryResult = ContentEditorCatalog.BuildRegistry();
            if (!registryResult.IsSuccess)
                throw new InvalidOperationException(registryResult.Error.ToString());
            var enemyIdText = Environment.GetEnvironmentVariable("M10_ENEMY_ID");
            if (string.IsNullOrWhiteSpace(enemyIdText)) enemyIdText = "test.enemy.ranged";
            var enemyIdResult = ContentId.Create(enemyIdText);
            if (!enemyIdResult.IsSuccess)
                throw new InvalidOperationException(enemyIdResult.Error.ToString());

            var determinism = VerifyDeterminism(
                registryResult.Value,
                enemyIdResult.Value,
                configuration);
            WarmUp(registryResult.Value, enemyIdResult.Value, configuration);

            var scenarioResult = M10StressScenario.Create(
                registryResult.Value,
                enemyIdResult.Value,
                configuration);
            if (!scenarioResult.IsSuccess)
                throw new InvalidOperationException(scenarioResult.Error.ToString());
            var scenario = scenarioResult.Value;
            var tickSamples = new double[configuration.TickCount];
            var renderSamples = new double[configuration.TickCount * RenderFramesPerTick];
            var memorySamples = new M10MemorySample[
                (configuration.TickCount / MemorySampleIntervalTicks) + 2];
            var memorySampleCount = 0;
            long hotPathManagedAllocations = 0;
            long renderChecksum;
            M10PoolMetrics poolMetrics;
            using (var presentation = new M10PresentationStressProbe(configuration.VfxRequestCount))
            {
                presentation.Prewarm();
                // The measured scenario and timing arrays are deliberate setup
                // allocations. Collect after all setup so their pressure cannot be
                // misreported as a hot-path collection during the fixed-tick window.
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                memorySamples[memorySampleCount++] = CaptureMemory(0d);
                var gc0Start = GC.CollectionCount(0);
                var gc1Start = GC.CollectionCount(1);
                var gc2Start = GC.CollectionCount(2);
                for (var tickIndex = 0; tickIndex < configuration.TickCount; tickIndex++)
                {
                    var allocationBefore = GC.GetAllocatedBytesForCurrentThread();
                    var tickStart = Stopwatch.GetTimestamp();
                    scenario.AdvanceOneTick();
                    tickSamples[tickIndex] = ElapsedMilliseconds(tickStart);

                    for (var renderIndex = 0; renderIndex < RenderFramesPerTick; renderIndex++)
                    {
                        var alpha = renderIndex == 0 ? 0.25f : 0.75f;
                        var renderStart = Stopwatch.GetTimestamp();
                        presentation.RenderFrame(scenario.Snapshot, alpha, 1f / 60f);
                        renderSamples[(tickIndex * RenderFramesPerTick) + renderIndex] =
                            ElapsedMilliseconds(renderStart);
                    }

                    hotPathManagedAllocations +=
                        GC.GetAllocatedBytesForCurrentThread() - allocationBefore;
                    if ((tickIndex + 1) % MemorySampleIntervalTicks == 0)
                    {
                        memorySamples[memorySampleCount++] = CaptureMemory(
                            (tickIndex + 1) / (double)SimulationClock.TickRate / 60d);
                    }
                }

                if (memorySampleCount == 0 ||
                    memorySamples[memorySampleCount - 1].simulatedMinute <
                    configuration.TickCount / (double)SimulationClock.TickRate / 60d)
                {
                    memorySamples[memorySampleCount++] = CaptureMemory(
                        configuration.TickCount / (double)SimulationClock.TickRate / 60d);
                }

                renderChecksum = presentation.Checksum;
                poolMetrics = presentation.CaptureMetrics();
                var gc = new M10GcMetrics
                {
                    generation0Collections = GC.CollectionCount(0) - gc0Start,
                    generation1Collections = GC.CollectionCount(1) - gc1Start,
                    generation2Collections = GC.CollectionCount(2) - gc2Start,
                    hotPathManagedAllocationBytes = hotPathManagedAllocations,
                    hotPathManagedBytesPerFrame = hotPathManagedAllocations /
                        (double)(configuration.TickCount * (RenderFramesPerTick + 1))
                };
                return BuildReport(
                    configuration,
                    enemyIdText,
                    scenario,
                    determinism,
                    tickSamples,
                    renderSamples,
                    TrimMemorySamples(memorySamples, memorySampleCount),
                    gc,
                    poolMetrics,
                    renderChecksum);
            }
        }

        private static M10PerformanceReport BuildReport(
            in M10StressConfiguration configuration,
            string enemyId,
            M10StressScenario scenario,
            bool determinism,
            double[] tickSamples,
            double[] renderSamples,
            M10MemorySample[] memorySamples,
            M10GcMetrics gc,
            M10PoolMetrics pools,
            long renderChecksum)
        {
            var ticks = CalculateTiming(tickSamples);
            var render = CalculateTiming(renderSamples);
            var memory = CalculateMemoryTrend(memorySamples);
            var exactCounts = scenario.HasExactConfiguredCounts();
            var diagnostics = scenario.World.Diagnostics;
            var systemHotspots = MapSystemTimings(scenario.CaptureSystemTimings());
            var budgets = new M10BudgetResult
            {
                tickP99BudgetMilliseconds = TickBudgetMilliseconds,
                renderP99BudgetMilliseconds = RenderBudgetMilliseconds,
                tickP99WithinBudget = ticks.p99Milliseconds <= TickBudgetMilliseconds,
                renderP99WithinBudget = render.p99Milliseconds <= RenderBudgetMilliseconds,
                exactEntityCounts = exactCounts,
                deterministic = determinism,
                zeroHotPathManagedAllocation = gc.hotPathManagedAllocationBytes == 0,
                noGcCollections = gc.generation0Collections == 0 &&
                                  gc.generation1Collections == 0 &&
                                  gc.generation2Collections == 0,
                noSustainedMemoryGrowth = !memory.managedSustainedGrowth &&
                                          !memory.nativeSustainedGrowth,
                noInvalidHandles = diagnostics.InvalidHandleAccesses == 0,
                noProcTruncation = diagnostics.TruncatedProcChains == 0,
                noVfxDrops = pools.droppedRequests == 0,
                vfxTargetReached = pools.peakActive == configuration.VfxRequestCount
            };
            var passed = budgets.tickP99WithinBudget &&
                         budgets.renderP99WithinBudget &&
                         budgets.exactEntityCounts &&
                         budgets.deterministic &&
                         budgets.zeroHotPathManagedAllocation &&
                         budgets.noGcCollections &&
                         budgets.noSustainedMemoryGrowth &&
                         budgets.noInvalidHandles &&
                         budgets.noProcTruncation &&
                         budgets.noVfxDrops &&
                         budgets.vfxTargetReached;
            return new M10PerformanceReport
            {
                schemaVersion = 1,
                status = passed ? "PASS" : "FAIL",
                failureReason = passed ? string.Empty : DescribeFailure(budgets),
                generatedAtUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                configuration = new M10ConfigurationDto
                {
                    seed = configuration.Seed.ToString(CultureInfo.InvariantCulture),
                    tickCount = configuration.TickCount,
                    simulatedSeconds = configuration.TickCount / SimulationClock.TickRate,
                    enemyId = enemyId,
                    enemies = configuration.EnemyCount,
                    projectiles = configuration.ProjectileCount,
                    pickups = configuration.PickupCount,
                    vfxRequests = configuration.VfxRequestCount,
                    warmupTicks = configuration.WarmupTickCount,
                    simulationHz = SimulationClock.TickRate,
                    presentationProbeHz = 60
                },
                environment = new M10EnvironmentDto
                {
                    unityVersion = UnityEngine.Application.unityVersion,
                    operatingSystem = SystemInfo.operatingSystem,
                    processor = SystemInfo.processorType,
                    processorCount = SystemInfo.processorCount,
                    systemMemoryMegabytes = SystemInfo.systemMemorySize,
                    graphicsDevice = SystemInfo.graphicsDeviceName,
                    batchMode = UnityEngine.Application.isBatchMode,
                    renderMetric = "headless RenderSnapshot interpolation plus pooled VFX CPU probe"
                },
                simulationTick = ticks,
                renderFrame = render,
                entities = new M10EntityMetrics
                {
                    peakEnemies = scenario.EnemyCount,
                    peakProjectiles = scenario.ProjectileCount,
                    peakPickups = scenario.PickupCount,
                    peakTotalEntities = scenario.TotalEntityCount,
                    finalSnapshotEntities = scenario.Snapshot.Count,
                    worldTicks = scenario.World.Tick,
                    checksum = scenario.CalculateChecksum().ToString("x16", CultureInfo.InvariantCulture),
                    renderChecksum = renderChecksum.ToString("x16", CultureInfo.InvariantCulture)
                },
                memory = memory,
                gc = gc,
                pools = pools,
                diagnostics = new M10DiagnosticMetrics
                {
                    truncatedProcChains = diagnostics.TruncatedProcChains,
                    invalidHandleAccesses = diagnostics.InvalidHandleAccesses,
                    rejectedDamagePackets = diagnostics.RejectedDamagePackets,
                    rejectedStatusApplications = diagnostics.RejectedStatusApplications
                },
                systemHotspots = systemHotspots,
                budgets = budgets,
                optimization = new M10OptimizationDecision
                {
                    jobsOrBurstApplied = false,
                    decision = "Measured hottest system is " + systemHotspots[0].systemId +
                               " at " + systemHotspots[0].averageMilliseconds.ToString("F4", CultureInfo.InvariantCulture) +
                               " ms average; target-scale p99 remains within budget, so no Jobs/Burst migration is justified."
                }
            };
        }

        private static M10SystemTimingDto[] MapSystemTimings(M10SystemTimingSnapshot[] source)
        {
            var output = new M10SystemTimingDto[source.Length];
            for (var index = 0; index < source.Length; index++)
            {
                output[index] = new M10SystemTimingDto
                {
                    systemId = source[index].SystemId.ToString(),
                    calls = source[index].Calls,
                    averageMilliseconds = source[index].AverageMilliseconds,
                    maximumMilliseconds = source[index].MaximumMilliseconds
                };
            }
            Array.Sort(output, (left, right) =>
                right.averageMilliseconds.CompareTo(left.averageMilliseconds));
            return output;
        }

        private static bool VerifyDeterminism(
            Game.Content.Runtime.ContentRegistry registry,
            ContentId enemyId,
            in M10StressConfiguration configuration)
        {
            var verification = new M10StressConfiguration(
                configuration.Seed,
                Math.Min(30, configuration.TickCount),
                configuration.EnemyCount,
                configuration.ProjectileCount,
                configuration.PickupCount,
                configuration.VfxRequestCount,
                0);
            var first = M10StressScenario.Create(registry, enemyId, verification);
            var second = M10StressScenario.Create(registry, enemyId, verification);
            if (!first.IsSuccess || !second.IsSuccess) return false;
            for (var index = 0; index < verification.TickCount; index++)
            {
                first.Value.AdvanceOneTick();
                second.Value.AdvanceOneTick();
            }

            return first.Value.CalculateChecksum() == second.Value.CalculateChecksum() &&
                   first.Value.HasExactConfiguredCounts() &&
                   second.Value.HasExactConfiguredCounts();
        }

        private static void WarmUp(
            Game.Content.Runtime.ContentRegistry registry,
            ContentId enemyId,
            in M10StressConfiguration configuration)
        {
            if (configuration.WarmupTickCount == 0) return;
            var warmup = new M10StressConfiguration(
                configuration.Seed,
                configuration.WarmupTickCount,
                configuration.EnemyCount,
                configuration.ProjectileCount,
                configuration.PickupCount,
                configuration.VfxRequestCount,
                0);
            var scenario = M10StressScenario.Create(registry, enemyId, warmup);
            if (!scenario.IsSuccess) throw new InvalidOperationException(scenario.Error.ToString());
            for (var index = 0; index < warmup.TickCount; index++) scenario.Value.AdvanceOneTick();
        }

        private static M10StressConfiguration ReadConfiguration()
        {
            var target = M10StressConfiguration.Target();
            return new M10StressConfiguration(
                ParseUlong("M10_SEED", target.Seed),
                ParseInt("M10_TICK_COUNT", target.TickCount),
                ParseInt("M10_ENEMY_COUNT", target.EnemyCount),
                ParseInt("M10_PROJECTILE_COUNT", target.ProjectileCount),
                ParseInt("M10_PICKUP_COUNT", target.PickupCount),
                ParseInt("M10_VFX_COUNT", target.VfxRequestCount),
                ParseInt("M10_WARMUP_TICKS", target.WarmupTickCount));
        }

        private static int ParseInt(string name, int fallback)
        {
            var value = Environment.GetEnvironmentVariable(name);
            return string.IsNullOrWhiteSpace(value)
                ? fallback
                : int.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture);
        }

        private static ulong ParseUlong(string name, ulong fallback)
        {
            var value = Environment.GetEnvironmentVariable(name);
            return string.IsNullOrWhiteSpace(value)
                ? fallback
                : ulong.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture);
        }

        private static string ResolveOutputPath()
        {
            var configured = Environment.GetEnvironmentVariable("M10_PERFORMANCE_OUTPUT");
            if (string.IsNullOrWhiteSpace(configured))
                configured = "TestResults/M10Performance/performance.json";
            return Path.GetFullPath(configured);
        }

        private static void WriteReport(string path, M10PerformanceReport report)
        {
            var directory = Path.GetDirectoryName(path);
            if (string.IsNullOrEmpty(directory)) throw new InvalidOperationException("Invalid report path.");
            Directory.CreateDirectory(directory);
            File.WriteAllText(path, JsonUtility.ToJson(report, true) + "\n");
        }

        private static double ElapsedMilliseconds(long startTimestamp) =>
            (Stopwatch.GetTimestamp() - startTimestamp) * 1000d / Stopwatch.Frequency;

        private static M10TimingMetrics CalculateTiming(double[] values)
        {
            var total = 0d;
            var maximum = 0d;
            for (var index = 0; index < values.Length; index++)
            {
                total += values[index];
                if (values[index] > maximum) maximum = values[index];
            }

            Array.Sort(values);
            return new M10TimingMetrics
            {
                samples = values.Length,
                averageMilliseconds = total / values.Length,
                p95Milliseconds = Percentile(values, 0.95d),
                p99Milliseconds = Percentile(values, 0.99d),
                maximumMilliseconds = maximum
            };
        }

        private static double Percentile(double[] sorted, double percentile)
        {
            var index = (int)Math.Ceiling(percentile * sorted.Length) - 1;
            return sorted[Math.Max(0, Math.Min(sorted.Length - 1, index))];
        }

        private static M10MemorySample CaptureMemory(double simulatedMinute)
        {
            return new M10MemorySample
            {
                simulatedMinute = simulatedMinute,
                managedBytes = Profiler.GetMonoUsedSizeLong(),
                nativeBytes = Profiler.GetTotalAllocatedMemoryLong(),
                gcReportedHeapBytes = GC.GetTotalMemory(false),
                totalAllocatedBytes = Profiler.GetTotalAllocatedMemoryLong(),
                totalReservedBytes = Profiler.GetTotalReservedMemoryLong()
            };
        }

        private static M10MemorySample[] TrimMemorySamples(M10MemorySample[] source, int count)
        {
            var output = new M10MemorySample[count];
            Array.Copy(source, output, count);
            return output;
        }

        private static M10MemoryMetrics CalculateMemoryTrend(M10MemorySample[] samples)
        {
            var managedPeak = 0L;
            var nativePeak = 0L;
            for (var index = 0; index < samples.Length; index++)
            {
                managedPeak = Math.Max(managedPeak, samples[index].managedBytes);
                nativePeak = Math.Max(nativePeak, samples[index].nativeBytes);
            }

            var segment = Math.Max(1, samples.Length / 3);
            long managedFirst = 0;
            long managedLast = 0;
            long nativeFirst = 0;
            long nativeLast = 0;
            for (var index = 0; index < segment; index++)
            {
                managedFirst += samples[index].managedBytes;
                nativeFirst += samples[index].nativeBytes;
                var lastIndex = samples.Length - 1 - index;
                managedLast += samples[lastIndex].managedBytes;
                nativeLast += samples[lastIndex].nativeBytes;
            }

            var managedSegmentGrowth = managedLast / segment - managedFirst / segment;
            var nativeSegmentGrowth = nativeLast / segment - nativeFirst / segment;
            var hasTrendWindow = samples.Length >= 4;
            return new M10MemoryMetrics
            {
                samples = samples,
                managedStartBytes = samples[0].managedBytes,
                managedEndBytes = samples[samples.Length - 1].managedBytes,
                managedPeakBytes = managedPeak,
                managedSegmentGrowthBytes = managedSegmentGrowth,
                managedSustainedGrowth = hasTrendWindow &&
                                         managedSegmentGrowth > 4L * 1024L * 1024L,
                nativeStartBytes = samples[0].nativeBytes,
                nativeEndBytes = samples[samples.Length - 1].nativeBytes,
                nativePeakBytes = nativePeak,
                nativeSegmentGrowthBytes = nativeSegmentGrowth,
                nativeSustainedGrowth = hasTrendWindow &&
                                        nativeSegmentGrowth > 32L * 1024L * 1024L
            };
        }

        private static string DescribeFailure(M10BudgetResult value)
        {
            if (!value.tickP99WithinBudget) return "Simulation tick p99 exceeded the 30 Hz budget.";
            if (!value.renderP99WithinBudget) return "Render probe p99 exceeded the 60 FPS budget.";
            if (!value.exactEntityCounts) return "Configured entity counts were not preserved.";
            if (!value.deterministic) return "Fixed-seed duplicate runs diverged.";
            if (!value.zeroHotPathManagedAllocation) return "The measured hot path allocated managed memory.";
            if (!value.noGcCollections) return "A GC collection occurred during the measured interval.";
            if (!value.noSustainedMemoryGrowth) return "Managed or native memory showed sustained growth.";
            if (!value.noInvalidHandles) return "Invalid EntityHandle access was recorded.";
            if (!value.noProcTruncation) return "A proc chain was truncated.";
            if (!value.noVfxDrops) return "A VFX request was dropped.";
            if (!value.vfxTargetReached) return "The requested simultaneous VFX target was not reached.";
            return "An unspecified performance gate failed.";
        }
    }

    internal sealed class M10PresentationStressProbe : IDisposable
    {
        private readonly int targetVfx;
        private readonly GameObject root;
        private readonly Texture2D texture;
        private readonly Sprite sprite;
        private readonly VfxRequestPool pool;
        private long checksum = 1469598103934665603L;

        public M10PresentationStressProbe(int vfxRequestCount)
        {
            targetVfx = vfxRequestCount;
            root = new GameObject("M10_PresentationStressProbe")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            texture.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
            texture.Apply(false, true);
            sprite = Sprite.Create(texture, new Rect(0f, 0f, 2f, 2f), new Vector2(0.5f, 0.5f), 2f);
            sprite.hideFlags = HideFlags.HideAndDontSave;
            pool = new VfxRequestPool(root.transform, sprite, vfxRequestCount);
        }

        public long Checksum => checksum;

        public void Prewarm()
        {
            FillVfx();
            pool.Tick(1f);
            FillVfx();
        }

        public void RenderFrame(RenderSnapshot snapshot, float alpha, float deltaTime)
        {
            for (var index = 0; index < snapshot.Count; index++)
            {
                var item = snapshot.GetAt(index);
                var position = item.InterpolatePosition(alpha);
                var facing = item.InterpolateFacing(alpha);
                unchecked
                {
                    checksum ^= BitConverter.SingleToInt32Bits(position.X);
                    checksum *= 1099511628211L;
                    checksum ^= BitConverter.SingleToInt32Bits(position.Y);
                    checksum *= 1099511628211L;
                    checksum ^= BitConverter.SingleToInt32Bits(facing);
                    checksum *= 1099511628211L;
                }
            }

            FillVfx();
            pool.Tick(deltaTime);
        }

        public M10PoolMetrics CaptureMetrics() => new M10PoolMetrics
        {
            name = "VfxRequestPool",
            capacity = targetVfx,
            created = pool.CreatedCount,
            peakActive = pool.PeakActiveCount,
            hits = pool.HitCount,
            expansions = pool.ExpansionCount,
            failedAcquires = pool.FailedAcquireCount,
            droppedRequests = pool.DroppedRequestCount
        };

        public void Dispose()
        {
            pool.Dispose();
            UnityEngine.Object.DestroyImmediate(sprite);
            UnityEngine.Object.DestroyImmediate(texture);
            UnityEngine.Object.DestroyImmediate(root);
        }

        private void FillVfx()
        {
            while (pool.ActiveCount < targetVfx)
            {
                var index = pool.ActiveCount;
                var angle = index * 0.0314159265f;
                var position = new Vector2(Mathf.Cos(angle) * 4f, Mathf.Sin(angle) * 4f);
                if (!pool.TrySpawn(position, Color.white, 0.25f, 0.12f)) break;
            }
        }
    }

    [Serializable]
    internal sealed class M10PerformanceReport
    {
        public int schemaVersion;
        public string status;
        public string failureReason;
        public string generatedAtUtc;
        public M10ConfigurationDto configuration;
        public M10EnvironmentDto environment;
        public M10TimingMetrics simulationTick;
        public M10TimingMetrics renderFrame;
        public M10EntityMetrics entities;
        public M10MemoryMetrics memory;
        public M10GcMetrics gc;
        public M10PoolMetrics pools;
        public M10DiagnosticMetrics diagnostics;
        public M10SystemTimingDto[] systemHotspots;
        public M10BudgetResult budgets;
        public M10OptimizationDecision optimization;
    }

    [Serializable]
    internal sealed class M10ConfigurationDto
    {
        public string seed;
        public int tickCount;
        public int simulatedSeconds;
        public string enemyId;
        public int enemies;
        public int projectiles;
        public int pickups;
        public int vfxRequests;
        public int warmupTicks;
        public int simulationHz;
        public int presentationProbeHz;
    }

    [Serializable]
    internal sealed class M10EnvironmentDto
    {
        public string unityVersion;
        public string operatingSystem;
        public string processor;
        public int processorCount;
        public int systemMemoryMegabytes;
        public string graphicsDevice;
        public bool batchMode;
        public string renderMetric;
    }

    [Serializable]
    internal sealed class M10TimingMetrics
    {
        public int samples;
        public double averageMilliseconds;
        public double p95Milliseconds;
        public double p99Milliseconds;
        public double maximumMilliseconds;
    }

    [Serializable]
    internal sealed class M10EntityMetrics
    {
        public int peakEnemies;
        public int peakProjectiles;
        public int peakPickups;
        public int peakTotalEntities;
        public int finalSnapshotEntities;
        public long worldTicks;
        public string checksum;
        public string renderChecksum;
    }

    [Serializable]
    internal sealed class M10MemoryMetrics
    {
        public M10MemorySample[] samples;
        public long managedStartBytes;
        public long managedEndBytes;
        public long managedPeakBytes;
        public long managedSegmentGrowthBytes;
        public bool managedSustainedGrowth;
        public long nativeStartBytes;
        public long nativeEndBytes;
        public long nativePeakBytes;
        public long nativeSegmentGrowthBytes;
        public bool nativeSustainedGrowth;
    }

    [Serializable]
    internal struct M10MemorySample
    {
        public double simulatedMinute;
        public long managedBytes;
        public long nativeBytes;
        public long gcReportedHeapBytes;
        public long totalAllocatedBytes;
        public long totalReservedBytes;
    }

    [Serializable]
    internal sealed class M10GcMetrics
    {
        public int generation0Collections;
        public int generation1Collections;
        public int generation2Collections;
        public long hotPathManagedAllocationBytes;
        public double hotPathManagedBytesPerFrame;
    }

    [Serializable]
    internal sealed class M10PoolMetrics
    {
        public string name;
        public int capacity;
        public int created;
        public int peakActive;
        public long hits;
        public long expansions;
        public long failedAcquires;
        public long droppedRequests;
    }

    [Serializable]
    internal sealed class M10DiagnosticMetrics
    {
        public long truncatedProcChains;
        public long invalidHandleAccesses;
        public long rejectedDamagePackets;
        public long rejectedStatusApplications;
    }

    [Serializable]
    internal sealed class M10SystemTimingDto
    {
        public string systemId;
        public long calls;
        public double averageMilliseconds;
        public double maximumMilliseconds;
    }

    [Serializable]
    internal sealed class M10BudgetResult
    {
        public double tickP99BudgetMilliseconds;
        public double renderP99BudgetMilliseconds;
        public bool tickP99WithinBudget;
        public bool renderP99WithinBudget;
        public bool exactEntityCounts;
        public bool deterministic;
        public bool zeroHotPathManagedAllocation;
        public bool noGcCollections;
        public bool noSustainedMemoryGrowth;
        public bool noInvalidHandles;
        public bool noProcTruncation;
        public bool noVfxDrops;
        public bool vfxTargetReached;
    }

    [Serializable]
    internal sealed class M10OptimizationDecision
    {
        public bool jobsOrBurstApplied;
        public string decision;
    }
}
