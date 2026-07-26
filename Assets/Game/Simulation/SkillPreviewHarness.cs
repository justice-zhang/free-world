using System;
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
            if (content == null) throw new ArgumentNullException(nameof(content));
            if (!IsFinite(durationSeconds) || durationSeconds <= 0f || targetCount <= 0)
            {
                return Result<SkillPreviewSummary>.Failure(
                    new Error(ErrorCode.InvalidAuthoringData, "Preview duration and target count must be positive."));
            }

            var modules = SkillModuleRegistry.CreateDefault();
            var skillCatalogResult = SkillRuntimeCatalog.Build(content, modules);
            if (!skillCatalogResult.IsSuccess)
            {
                return Result<SkillPreviewSummary>.Failure(skillCatalogResult.Error);
            }

            var skills = new SkillRuntime(skillCatalogResult.Value, seed, 32);
            var world = new SimulationWorld(
                seed,
                Math.Max(64, targetCount + 8),
                2f,
                statusCatalog: new RuntimeStatusCatalog(content),
                skillRuntime: skills);
            var ownerHandle = world.CreateActor(
                SimulationEntityState.Create(Vector2.Zero, Vector2.Zero),
                ActorCombatInitialization.CreateDefault(1_000_000f, 0f));
            var owner = new SpatialEntity(EntityKind.Actor, ownerHandle);
            for (var index = 0; index < targetCount; index++)
            {
                var angle = index * 2f * (float)Math.PI / targetCount;
                var radius = 1f + (index % 4);
                world.CreateActor(
                    SimulationEntityState.Create(
                        new Vector2(
                            (float)Math.Cos(angle) * radius,
                            (float)Math.Sin(angle) * radius),
                        Vector2.Zero),
                    ActorCombatInitialization.CreateDefault(1_000_000f, 0f));
            }

            var addResult = skills.AddInstance(owner, skillIndex);
            if (!addResult.IsSuccess)
            {
                return Result<SkillPreviewSummary>.Failure(addResult.Error);
            }

            skills.SetResource(owner, float.MaxValue);
            var runner = new FixedTickRunner(world);
            var ticks = (int)Math.Ceiling(durationSeconds / SimulationClock.TickDurationSeconds);
            var totalDamage = 0f;
            var hits = 0L;
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

            var simulatedDuration = ticks * (float)SimulationClock.TickDurationSeconds;
            return Result<SkillPreviewSummary>.Success(
                new SkillPreviewSummary(
                    seed,
                    simulatedDuration,
                    totalDamage / simulatedDuration,
                    hits,
                    skills.TriggerCount));
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
