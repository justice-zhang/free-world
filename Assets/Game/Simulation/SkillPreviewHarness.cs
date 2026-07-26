using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using Game.Content.Runtime;
using Game.Core;

namespace Game.Simulation
{
    /// <summary>Deterministic result produced by the pure M4 skill preview harness.</summary>
    public readonly struct SkillPreviewSummary : IEquatable<SkillPreviewSummary>
    {
        /// <summary>Initializes one preview summary.</summary>
        public SkillPreviewSummary(
            ulong seed,
            float durationSeconds,
            float damagePerSecond,
            long hitCount,
            long triggerCount)
        {
            Seed = seed;
            DurationSeconds = durationSeconds;
            DamagePerSecond = damagePerSecond;
            HitCount = hitCount;
            TriggerCount = triggerCount;
        }

        /// <summary>Gets the fixed seed.</summary>
        public ulong Seed { get; }
        /// <summary>Gets simulated duration.</summary>
        public float DurationSeconds { get; }
        /// <summary>Gets resolved shield plus health damage per second.</summary>
        public float DamagePerSecond { get; }
        /// <summary>Gets resolved damage hit count.</summary>
        public long HitCount { get; }
        /// <summary>Gets successful skill activation count.</summary>
        public long TriggerCount { get; }

        /// <inheritdoc />
        public bool Equals(SkillPreviewSummary other)
        {
            return Seed == other.Seed &&
                   DurationSeconds.Equals(other.DurationSeconds) &&
                   DamagePerSecond.Equals(other.DamagePerSecond) &&
                   HitCount == other.HitCount &&
                   TriggerCount == other.TriggerCount;
        }

        /// <inheritdoc />
        public override bool Equals(object obj) =>
            obj is SkillPreviewSummary other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                var hash = Seed.GetHashCode();
                hash = (hash * 397) ^ DurationSeconds.GetHashCode();
                hash = (hash * 397) ^ DamagePerSecond.GetHashCode();
                hash = (hash * 397) ^ HitCount.GetHashCode();
                return (hash * 397) ^ TriggerCount.GetHashCode();
            }
        }
    }

    /// <summary>Parameters shared by the headless harness and M9 editor preview.</summary>
    public readonly struct SkillPreviewRequest
    {
        /// <summary>Initializes one deterministic preview request.</summary>
        public SkillPreviewRequest(
            ulong seed,
            float durationSeconds,
            int targetCount,
            int level = 1,
            float damageMultiplier = 1f,
            float criticalChance = 0f)
        {
            Seed = seed;
            DurationSeconds = durationSeconds;
            TargetCount = targetCount;
            Level = level;
            DamageMultiplier = damageMultiplier;
            CriticalChance = criticalChance;
        }

        /// <summary>Gets the fixed random seed.</summary>
        public ulong Seed { get; }
        /// <summary>Gets simulated duration in seconds.</summary>
        public float DurationSeconds { get; }
        /// <summary>Gets the number of stationary preview targets.</summary>
        public int TargetCount { get; }
        /// <summary>Gets the one-based authored skill level.</summary>
        public int Level { get; }
        /// <summary>Gets the actual source Damage stat multiplier.</summary>
        public float DamageMultiplier { get; }
        /// <summary>Gets the actual source critical-hit probability.</summary>
        public float CriticalChance { get; }
    }

    /// <summary>Numeric targeting and delivery shape shown by the M9 editor UI.</summary>
    public readonly struct SkillPreviewGeometry
    {
        internal SkillPreviewGeometry(
            ContentId targetingId,
            ContentId deliveryId,
            float range,
            float hitboxRadius)
        {
            TargetingId = targetingId;
            DeliveryId = deliveryId;
            Range = range;
            HitboxRadius = hitboxRadius;
        }

        /// <summary>Gets the targeting module ID.</summary>
        public ContentId TargetingId { get; }
        /// <summary>Gets the delivery module ID.</summary>
        public ContentId DeliveryId { get; }
        /// <summary>Gets the outer target-search or placement range.</summary>
        public float Range { get; }
        /// <summary>Gets the delivery collision/area radius when applicable.</summary>
        public float HitboxRadius { get; }
    }

    /// <summary>Detailed deterministic preview data consumed by editor tooling.</summary>
    public sealed class SkillPreviewReport
    {
        private readonly string[] logLines;
        private readonly IReadOnlyList<string> logLinesView;

        internal SkillPreviewReport(
            in SkillPreviewSummary summary,
            in SkillPreviewGeometry geometry,
            long managedAllocationBytes,
            string[] logs)
        {
            Summary = summary;
            Geometry = geometry;
            ManagedAllocationBytes = managedAllocationBytes;
            logLines = logs ?? Array.Empty<string>();
            logLinesView = Array.AsReadOnly(logLines);
        }

        /// <summary>Gets the legacy DPS/hit/trigger summary.</summary>
        public SkillPreviewSummary Summary { get; }
        /// <summary>Gets numeric targeting and delivery geometry.</summary>
        public SkillPreviewGeometry Geometry { get; }
        /// <summary>Gets managed bytes allocated by the fixed-tick portion of this preview.</summary>
        public long ManagedAllocationBytes { get; }
        /// <summary>Gets deterministic, bounded diagnostic lines created after simulation.</summary>
        public IReadOnlyList<string> LogLines => logLinesView;
    }

    /// <summary>Runs presentation-free fixed-seed previews for authored skills.</summary>
    public static class SkillPreviewHarness
    {
        /// <summary>Runs one isolated preview and reports DPS, hits, and activations.</summary>
        public static Result<SkillPreviewSummary> Run(
            ContentRegistry content,
            RuntimeContentIndex skillIndex,
            ulong seed,
            float durationSeconds = 5f,
            int targetCount = 16)
        {
            var detailed = RunDetailed(
                content,
                skillIndex,
                new SkillPreviewRequest(seed, durationSeconds, targetCount));
            return detailed.IsSuccess
                ? Result<SkillPreviewSummary>.Success(detailed.Value.Summary)
                : Result<SkillPreviewSummary>.Failure(detailed.Error);
        }

        /// <summary>
        /// Runs a level- and attribute-aware preview and returns geometry, allocation, and logs.
        /// </summary>
        public static Result<SkillPreviewReport> RunDetailed(
            ContentRegistry content,
            RuntimeContentIndex skillIndex,
            in SkillPreviewRequest request)
        {
            if (content == null) throw new ArgumentNullException(nameof(content));
            if (!IsFinite(request.DurationSeconds) || request.DurationSeconds <= 0f ||
                request.TargetCount <= 0 || request.Level <= 0 ||
                !IsFinite(request.DamageMultiplier) || request.DamageMultiplier < 0f ||
                !IsFinite(request.CriticalChance) || request.CriticalChance < 0f ||
                request.CriticalChance > 1f)
            {
                return Result<SkillPreviewReport>.Failure(
                    new Error(ErrorCode.InvalidAuthoringData, "Preview duration and target count must be positive."));
            }

            var modules = SkillModuleRegistry.CreateDefault();
            var skillCatalogResult = SkillRuntimeCatalog.Build(content, modules);
            if (!skillCatalogResult.IsSuccess)
            {
                return Result<SkillPreviewReport>.Failure(skillCatalogResult.Error);
            }

            if (!skillCatalogResult.Value.TryGet(skillIndex, out var compiled) ||
                request.Level > compiled.MaximumLevel)
            {
                return Result<SkillPreviewReport>.Failure(
                    new Error(ErrorCode.InvalidAuthoringData, "Preview skill or level is unavailable."));
            }

            var runtimeLevel = compiled.GetLevel(request.Level);

            var skills = new SkillRuntime(skillCatalogResult.Value, request.Seed, 32);
            var world = new SimulationWorld(
                request.Seed,
                Math.Max(64, request.TargetCount + 8),
                2f,
                statusCatalog: new RuntimeStatusCatalog(content),
                skillRuntime: skills);
            var ownerStats = StatBaseValues.CreateDefault(1_000_000f, 0f);
            ownerStats.Damage = request.DamageMultiplier;
            ownerStats.CriticalChance = request.CriticalChance;
            var ownerHandle = world.CreateActor(
                SimulationEntityState.Create(Vector2.Zero, Vector2.Zero),
                new ActorCombatInitialization(
                    ownerStats,
                    1_000_000f,
                    0f,
                    0f,
                    default));
            var owner = new SpatialEntity(EntityKind.Actor, ownerHandle);
            for (var index = 0; index < request.TargetCount; index++)
            {
                var angle = index * 2f * (float)Math.PI / request.TargetCount;
                var radius = 1f + (index % 4);
                world.CreateActor(
                    SimulationEntityState.Create(
                        new Vector2(
                            (float)Math.Cos(angle) * radius,
                            (float)Math.Sin(angle) * radius),
                        Vector2.Zero),
                    ActorCombatInitialization.CreateDefault(1_000_000f, 0f));
            }

            var addResult = skills.AddInstance(owner, skillIndex, request.Level);
            if (!addResult.IsSuccess)
            {
                return Result<SkillPreviewReport>.Failure(addResult.Error);
            }

            skills.SetResource(owner, float.MaxValue);
            var runner = new FixedTickRunner(world);
            var ticks = (int)Math.Ceiling(
                request.DurationSeconds / SimulationClock.TickDurationSeconds);
            var totalDamage = 0f;
            var hits = 0L;
            var allocationsBefore = GC.GetAllocatedBytesForCurrentThread();
            for (var tick = 0; tick < ticks; tick++)
            {
                runner.Advance(SimulationClock.TickDurationSeconds);
                for (var eventIndex = 0;
                     eventIndex < world.CombatEvents.DamageAppliedCount;
                     eventIndex++)
                {
                    var damage = world.CombatEvents.GetDamageAppliedAt(eventIndex).Context;
                    totalDamage += damage.ShieldAbsorbed + damage.HealthDamage;
                    hits++;
                }
            }
            var allocatedBytes = Math.Max(
                0L,
                GC.GetAllocatedBytesForCurrentThread() - allocationsBefore);

            var simulatedDuration = ticks * (float)SimulationClock.TickDurationSeconds;
            var summary = new SkillPreviewSummary(
                request.Seed,
                simulatedDuration,
                totalDamage / simulatedDuration,
                hits,
                skills.TriggerCount);
            var geometry = BuildGeometry(runtimeLevel);
            var logs = new[]
            {
                "seed=" + request.Seed.ToString(CultureInfo.InvariantCulture),
                "level=" + request.Level.ToString(CultureInfo.InvariantCulture) +
                ", targets=" + request.TargetCount.ToString(CultureInfo.InvariantCulture),
                "ticks=" + ticks.ToString(CultureInfo.InvariantCulture) +
                ", duration=" + simulatedDuration.ToString("R", CultureInfo.InvariantCulture),
                "hits=" + hits.ToString(CultureInfo.InvariantCulture) +
                ", triggers=" + skills.TriggerCount.ToString(CultureInfo.InvariantCulture),
                "dps=" + summary.DamagePerSecond.ToString("R", CultureInfo.InvariantCulture)
            };
            return Result<SkillPreviewReport>.Success(
                new SkillPreviewReport(summary, geometry, allocatedBytes, logs));
        }

        private static SkillPreviewGeometry BuildGeometry(RuntimeSkillLevel level)
        {
            var targeting = level.Targeting;
            var range = 0f;
            if (targeting.ModuleId == SkillModuleIds.TargetingRing ||
                targeting.ModuleId == SkillModuleIds.TargetingRandomPointAroundPlayer)
            {
                range = Math.Max(targeting.Value0, targeting.Value1);
            }
            else if (targeting.ModuleId != SkillModuleIds.TargetingSelf)
            {
                range = Math.Max(0f, targeting.Value0);
            }

            var delivery = level.Delivery;
            var hitbox = 0f;
            if (delivery.ModuleId == SkillModuleIds.DeliveryProjectile ||
                delivery.ModuleId == SkillModuleIds.DeliveryOrbit)
            {
                hitbox = Math.Max(0f, delivery.Value1);
            }
            else if (delivery.ModuleId == SkillModuleIds.DeliveryArea ||
                     delivery.ModuleId == SkillModuleIds.DeliveryAura)
            {
                hitbox = Math.Max(0f, delivery.Value0);
            }

            return new SkillPreviewGeometry(
                targeting.ModuleId,
                delivery.ModuleId,
                range,
                hitbox);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
