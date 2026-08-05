using System;
using System.Collections.Generic;
using Game.Content.Runtime;
using Game.Core;
using Game.Simulation;

namespace Game.Editor
{
    /// <summary>One phase of theoretical encounter-production data.</summary>
    public sealed class WaveTimelinePhaseReport
    {
        private readonly float[] eliteTimes;
        private readonly IReadOnlyList<float> eliteTimesView;
        private readonly float[] bossTimes;
        private readonly IReadOnlyList<float> bossTimesView;

        internal WaveTimelinePhaseReport(
            int index,
            RuntimeEncounterPhase phase,
            float integratedBudget,
            float expectedEnemyCount,
            int theoreticalConcurrency,
            float totalHealth,
            float experienceOutput,
            float[] phaseEliteTimes,
            float[] phaseBossTimes)
        {
            Index = index;
            StartTimeSeconds = phase.StartTimeSeconds;
            EndTimeSeconds = phase.EndTimeSeconds;
            BudgetPerSecondStart = phase.BudgetPerSecondStart;
            BudgetPerSecondEnd = phase.BudgetPerSecondEnd;
            SpawnIntervalStart = phase.SpawnIntervalStart;
            SpawnIntervalEnd = phase.SpawnIntervalEnd;
            IntegratedBudget = integratedBudget;
            ExpectedEnemyCount = expectedEnemyCount;
            TheoreticalConcurrency = theoreticalConcurrency;
            TotalHealth = totalHealth;
            ExperienceOutput = experienceOutput;
            eliteTimes = phaseEliteTimes ?? Array.Empty<float>();
            eliteTimesView = Array.AsReadOnly(eliteTimes);
            bossTimes = phaseBossTimes ?? Array.Empty<float>();
            bossTimesView = Array.AsReadOnly(bossTimes);
        }

        /// <summary>Gets the zero-based phase index.</summary>
        public int Index { get; }
        /// <summary>Gets the phase start time.</summary>
        public float StartTimeSeconds { get; }
        /// <summary>Gets the phase end time.</summary>
        public float EndTimeSeconds { get; }
        /// <summary>Gets the authored starting budget rate.</summary>
        public float BudgetPerSecondStart { get; }
        /// <summary>Gets the authored ending budget rate.</summary>
        public float BudgetPerSecondEnd { get; }
        /// <summary>Gets the authored starting spawn interval.</summary>
        public float SpawnIntervalStart { get; }
        /// <summary>Gets the authored ending spawn interval.</summary>
        public float SpawnIntervalEnd { get; }
        /// <summary>Gets the integrated budget after the preview multiplier.</summary>
        public float IntegratedBudget { get; }
        /// <summary>Gets the weighted theoretical enemy count.</summary>
        public float ExpectedEnemyCount { get; }
        /// <summary>Gets the effective authored concurrency cap.</summary>
        public int TheoreticalConcurrency { get; }
        /// <summary>Gets the weighted enemy and boss health output.</summary>
        public float TotalHealth { get; }
        /// <summary>Gets the weighted enemy and boss experience output.</summary>
        public float ExperienceOutput { get; }
        /// <summary>Gets authored one-shot elite spawn times for this phase.</summary>
        public IReadOnlyList<float> EliteTimes => eliteTimesView;
        /// <summary>Gets authored boss spawn times for this phase.</summary>
        public IReadOnlyList<float> BossTimes => bossTimesView;
    }

    /// <summary>Immutable M9 timeline analysis for one encounter.</summary>
    public sealed class WaveTimelineReport
    {
        private readonly WaveTimelinePhaseReport[] phases;
        private readonly IReadOnlyList<WaveTimelinePhaseReport> phasesView;

        internal WaveTimelineReport(
            ContentId encounterId,
            WaveTimelinePhaseReport[] phaseReports,
            float totalHealth,
            float experienceOutput)
        {
            EncounterId = encounterId;
            phases = phaseReports ?? Array.Empty<WaveTimelinePhaseReport>();
            phasesView = Array.AsReadOnly(phases);
            TotalHealth = totalHealth;
            ExperienceOutput = experienceOutput;
        }

        /// <summary>Gets the analyzed encounter ID.</summary>
        public ContentId EncounterId { get; }
        /// <summary>Gets phase reports in authored order.</summary>
        public IReadOnlyList<WaveTimelinePhaseReport> Phases => phasesView;
        /// <summary>Gets the encounter-wide theoretical health output.</summary>
        public float TotalHealth { get; }
        /// <summary>Gets the encounter-wide theoretical experience output.</summary>
        public float ExperienceOutput { get; }
    }

    /// <summary>Calculates deterministic wave previews without loading a Scene.</summary>
    public static class WaveTimelineAnalyzer
    {
        /// <summary>Analyzes phase curves, weighted enemies, bosses, health, and XP.</summary>
        public static Result<WaveTimelineReport> Analyze(
            RuntimeEncounterSchedule schedule,
            ContentRegistry registry,
            float spawnRateMultiplier = 1f)
        {
            if (schedule == null) throw new ArgumentNullException(nameof(schedule));
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            if (!FinitePositive(spawnRateMultiplier))
            {
                return Result<WaveTimelineReport>.Failure(
                    new Error(ErrorCode.InvalidAuthoringData, "Spawn-rate multiplier must be positive."));
            }

            var reports = new WaveTimelinePhaseReport[schedule.Phases.Count];
            var encounterHealth = 0f;
            var encounterExperience = 0f;
            for (var phaseIndex = 0; phaseIndex < schedule.Phases.Count; phaseIndex++)
            {
                var phase = schedule.Phases[phaseIndex];
                var duration = phase.EndTimeSeconds - phase.StartTimeSeconds;
                var integratedBudget = duration *
                                       (phase.BudgetPerSecondStart + phase.BudgetPerSecondEnd) *
                                       0.5f * spawnRateMultiplier;
                var totalWeight = 0f;
                var weightedCost = 0f;
                var weightedGroupSize = 0f;
                var weightedHealth = 0f;
                var weightedExperience = 0f;
                for (var entryIndex = 0; entryIndex < phase.EnemyEntries.Count; entryIndex++)
                {
                    var entry = phase.EnemyEntries[entryIndex];
                    if (!registry.TryGet(entry.EnemyId, out RuntimeEnemyDefinition enemy))
                    {
                        return Result<WaveTimelineReport>.Failure(
                            new Error(
                                ErrorCode.MissingReference,
                                "Wave timeline cannot resolve enemy '" + entry.EnemyId + "'.",
                                schedule.Id,
                                default,
                                schedule.SourceAssetPath));
                    }

                    totalWeight += entry.Weight;
                    weightedCost += entry.Weight * entry.BudgetCost;
                    weightedGroupSize += entry.Weight *
                                         (entry.MinimumGroupSize + entry.MaximumGroupSize) * 0.5f;
                    weightedHealth += entry.Weight * enemy.BaseMaxHealth;
                    weightedExperience += entry.Weight * enemy.ExperienceReward;
                }

                var expectedCount = 0f;
                var phaseHealth = 0f;
                var phaseExperience = 0f;
                if (totalWeight > 0f && weightedCost > 0f)
                {
                    var expectedCost = weightedCost / totalWeight;
                    var expectedGroup = weightedGroupSize / totalWeight;
                    var averageInterval =
                        (phase.SpawnIntervalStart + phase.SpawnIntervalEnd) *
                        0.5f / spawnRateMultiplier;
                    var budgetLimited = integratedBudget / expectedCost;
                    var intervalLimited = averageInterval > 0f
                        ? duration / averageInterval * expectedGroup
                        : budgetLimited;
                    expectedCount = Math.Min(budgetLimited, intervalLimited);
                    phaseHealth += expectedCount * weightedHealth / totalWeight;
                    phaseExperience += expectedCount * weightedExperience / totalWeight;
                }

                var eliteTimes = new float[phase.EliteRules.Count];
                for (var eliteIndex = 0; eliteIndex < phase.EliteRules.Count; eliteIndex++)
                {
                    var elite = phase.EliteRules[eliteIndex];
                    eliteTimes[eliteIndex] = elite.SpawnTimeSeconds;
                    if (!registry.TryGet(elite.EnemyId, out RuntimeEnemyDefinition enemy))
                    {
                        return Result<WaveTimelineReport>.Failure(
                            new Error(
                                ErrorCode.MissingReference,
                                "Wave timeline cannot resolve elite '" + elite.EnemyId + "'.",
                                schedule.Id,
                                default,
                                schedule.SourceAssetPath));
                    }

                    phaseHealth += enemy.BaseMaxHealth * 1.5f;
                    phaseExperience += enemy.ExperienceReward * 1.5f;
                }

                var bossTimes = new float[phase.BossRules.Count];
                for (var bossIndex = 0; bossIndex < phase.BossRules.Count; bossIndex++)
                {
                    var boss = phase.BossRules[bossIndex];
                    bossTimes[bossIndex] = boss.SpawnTimeSeconds;
                    if (!registry.TryGet(boss.EnemyId, out RuntimeEnemyDefinition enemy))
                    {
                        return Result<WaveTimelineReport>.Failure(
                            new Error(
                                ErrorCode.MissingReference,
                                "Wave timeline cannot resolve boss '" + boss.EnemyId + "'.",
                                schedule.Id,
                                default,
                                schedule.SourceAssetPath));
                    }

                    phaseHealth += enemy.BaseMaxHealth;
                    phaseExperience += enemy.ExperienceReward;
                }

                var concurrency = Math.Min(
                    schedule.MaximumConcurrentEnemies,
                    phase.MaximumConcurrentEnemies);
                reports[phaseIndex] = new WaveTimelinePhaseReport(
                    phaseIndex,
                    phase,
                    integratedBudget,
                    expectedCount,
                    concurrency,
                    phaseHealth,
                    phaseExperience,
                    eliteTimes,
                    bossTimes);
                encounterHealth += phaseHealth;
                encounterExperience += phaseExperience;
            }

            return Result<WaveTimelineReport>.Success(
                new WaveTimelineReport(
                    schedule.Id,
                    reports,
                    encounterHealth,
                    encounterExperience));
        }

        /// <summary>Samples the exact curve implementation used by EncounterScheduler.</summary>
        public static Result<EncounterTimelineSample> Sample(
            RuntimeEncounterSchedule schedule,
            float elapsedSeconds,
            float spawnRateMultiplier = 1f)
        {
            if (schedule == null) throw new ArgumentNullException(nameof(schedule));
            for (var index = 0; index < schedule.Phases.Count; index++)
            {
                var phase = schedule.Phases[index];
                if (elapsedSeconds >= phase.StartTimeSeconds &&
                    elapsedSeconds < phase.EndTimeSeconds)
                {
                    return Result<EncounterTimelineSample>.Success(
                        EncounterTimelineSampler.Sample(
                            phase,
                            elapsedSeconds,
                            spawnRateMultiplier));
                }
            }

            return Result<EncounterTimelineSample>.Failure(
                new Error(ErrorCode.InvalidAuthoringData, "Timeline time is outside all encounter phases."));
        }

        private static bool FinitePositive(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
    }
}
