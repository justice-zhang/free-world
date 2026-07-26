using System;
using Game.Content.Runtime;
using Game.Core;
using UnityEngine;

namespace Game.Content.Authoring
{
    [Serializable]
    public sealed class EncounterEnemyEntryAuthoringData
    {
        public EnemyAuthoring enemy;
        public float weight = 1f;
        public float budgetCost = 1f;
        public int minimumGroupSize = 1;
        public int maximumGroupSize = 1;
        public bool elite;
    }

    [Serializable]
    public sealed class EncounterBossRuleAuthoringData
    {
        public EnemyAuthoring enemy;
        public float spawnTimeSeconds;
        public SpawnPattern pattern = SpawnPattern.Ring;
        public string anchorId = string.Empty;
    }

    [Serializable]
    public sealed class EncounterPhaseAuthoringData
    {
        public float startTimeSeconds;
        public float endTimeSeconds = 60f;
        public float budgetPerSecondStart = 1f;
        public float budgetPerSecondEnd = 1f;
        public float spawnIntervalStart = 1f;
        public float spawnIntervalEnd = 1f;
        public int maximumConcurrentEnemies = 32;
        public SpawnPattern spawnPattern = SpawnPattern.Ring;
        public string anchorId = string.Empty;
        public EncounterEnemyEntryAuthoringData[] enemies = Array.Empty<EncounterEnemyEntryAuthoringData>();
        public EncounterBossRuleAuthoringData[] bosses = Array.Empty<EncounterBossRuleAuthoringData>();
    }

    /// <summary>Schema-4 authoring for a map-independent encounter schedule.</summary>
    [CreateAssetMenu(menuName = "Free World/Content/Encounter Schedule", fileName = "EncounterSchedule")]
    public sealed class EncounterScheduleAuthoring : ContentAuthoringBase
    {
        [SerializeField] private int maximumConcurrentEnemies = 64;
        [SerializeField] private float minimumSpawnDistance = 10f;
        [SerializeField] private float maximumSpawnDistance = 16f;
        [SerializeField] private EncounterPhaseAuthoringData[] phases = Array.Empty<EncounterPhaseAuthoringData>();

        public void Configure(
            int maximumConcurrent,
            float minimumDistance,
            float maximumDistance,
            EncounterPhaseAuthoringData[] encounterPhases)
        {
            maximumConcurrentEnemies = maximumConcurrent;
            minimumSpawnDistance = minimumDistance;
            maximumSpawnDistance = maximumDistance;
            phases = encounterPhases == null
                ? Array.Empty<EncounterPhaseAuthoringData>()
                : (EncounterPhaseAuthoringData[])encounterPhases.Clone();
        }

        internal override Result<RuntimeContentDefinition> Bake(ContentId packId, string authorAssetPath)
        {
            var commonResult = BakeCommon(packId, authorAssetPath);
            if (!commonResult.IsSuccess)
            {
                return Result<RuntimeContentDefinition>.Failure(commonResult.Error);
            }

            var common = commonResult.Value;
            if (maximumConcurrentEnemies <= 0 || !IsFiniteNonNegative(minimumSpawnDistance) ||
                !IsFinitePositive(maximumSpawnDistance) ||
                minimumSpawnDistance > maximumSpawnDistance || phases == null || phases.Length == 0)
            {
                return Failure("Encounter limits, spawn distance, or phases are invalid.", common, packId);
            }

            var runtimePhases = new RuntimeEncounterPhase[phases.Length];
            for (var phaseIndex = 0; phaseIndex < phases.Length; phaseIndex++)
            {
                var source = phases[phaseIndex];
                if (source == null || !IsFiniteNonNegative(source.startTimeSeconds) ||
                    !IsFinitePositive(source.endTimeSeconds) || source.endTimeSeconds <= source.startTimeSeconds ||
                    !IsFiniteNonNegative(source.budgetPerSecondStart) ||
                    !IsFiniteNonNegative(source.budgetPerSecondEnd) ||
                    !IsFinitePositive(source.spawnIntervalStart) ||
                    !IsFinitePositive(source.spawnIntervalEnd) ||
                    source.maximumConcurrentEnemies <= 0 ||
                    !Enum.IsDefined(typeof(SpawnPattern), source.spawnPattern) ||
                    source.enemies == null || source.enemies.Length == 0)
                {
                    return Failure("Encounter phase " + phaseIndex + " is invalid.", common, packId);
                }

                var phaseAnchorResult = ParseOptionalId(source.anchorId, packId, authorAssetPath);
                if (!phaseAnchorResult.IsSuccess)
                    return Result<RuntimeContentDefinition>.Failure(phaseAnchorResult.Error);
                if (RequiresAnchor(source.spawnPattern) && !phaseAnchorResult.Value.IsValid)
                    return Failure("Encounter phase " + phaseIndex + " requires an anchor ID.", common, packId);

                var entries = new RuntimeEncounterEnemyEntry[source.enemies.Length];
                for (var entryIndex = 0; entryIndex < source.enemies.Length; entryIndex++)
                {
                    var entry = source.enemies[entryIndex];
                    if (entry == null || entry.enemy == null || !IsFinitePositive(entry.weight) ||
                        !IsFinitePositive(entry.budgetCost) || entry.minimumGroupSize <= 0 ||
                        entry.maximumGroupSize < entry.minimumGroupSize)
                    {
                        return Failure(
                            "Encounter phase " + phaseIndex + " enemy entry " + entryIndex + " is invalid.",
                            common,
                            packId);
                    }

                    var enemyId = ContentId.Create(entry.enemy.ContentIdText, packId, authorAssetPath);
                    if (!enemyId.IsSuccess) return Result<RuntimeContentDefinition>.Failure(enemyId.Error);
                    entries[entryIndex] = new RuntimeEncounterEnemyEntry(
                        enemyId.Value,
                        entry.weight,
                        entry.budgetCost,
                        entry.minimumGroupSize,
                        entry.maximumGroupSize,
                        entry.elite);
                }

                var sourceBosses = source.bosses ?? Array.Empty<EncounterBossRuleAuthoringData>();
                var bosses = new RuntimeEncounterBossRule[sourceBosses.Length];
                for (var bossIndex = 0; bossIndex < sourceBosses.Length; bossIndex++)
                {
                    var boss = sourceBosses[bossIndex];
                    if (boss == null || boss.enemy == null || !IsFiniteNonNegative(boss.spawnTimeSeconds) ||
                        !Enum.IsDefined(typeof(SpawnPattern), boss.pattern))
                    {
                        return Failure(
                            "Encounter phase " + phaseIndex + " boss rule " + bossIndex + " is invalid.",
                            common,
                            packId);
                    }

                    var enemyId = ContentId.Create(boss.enemy.ContentIdText, packId, authorAssetPath);
                    if (!enemyId.IsSuccess) return Result<RuntimeContentDefinition>.Failure(enemyId.Error);
                    var anchorId = ParseOptionalId(boss.anchorId, packId, authorAssetPath);
                    if (!anchorId.IsSuccess) return Result<RuntimeContentDefinition>.Failure(anchorId.Error);
                    if (RequiresAnchor(boss.pattern) && !anchorId.Value.IsValid)
                        return Failure("Boss rule requires an anchor ID.", common, packId);
                    bosses[bossIndex] = new RuntimeEncounterBossRule(
                        enemyId.Value,
                        boss.spawnTimeSeconds,
                        boss.pattern,
                        anchorId.Value);
                }

                runtimePhases[phaseIndex] = new RuntimeEncounterPhase(
                    source.startTimeSeconds,
                    source.endTimeSeconds,
                    source.budgetPerSecondStart,
                    source.budgetPerSecondEnd,
                    source.spawnIntervalStart,
                    source.spawnIntervalEnd,
                    source.maximumConcurrentEnemies,
                    source.spawnPattern,
                    phaseAnchorResult.Value,
                    entries,
                    bosses);
            }

            return Result<RuntimeContentDefinition>.Success(
                new RuntimeEncounterSchedule(
                    common.Id,
                    common.LocalizedNameKey,
                    common.LocalizedDescriptionKey,
                    common.AuthorAssetPath,
                    common.Tags,
                    maximumConcurrentEnemies,
                    minimumSpawnDistance,
                    maximumSpawnDistance,
                    runtimePhases));
        }

        private static bool RequiresAnchor(SpawnPattern pattern) =>
            pattern == SpawnPattern.Portal || pattern == SpawnPattern.FixedAnchor;

        private static Result<ContentId> ParseOptionalId(
            string value,
            ContentId packId,
            string authorAssetPath)
        {
            return string.IsNullOrWhiteSpace(value)
                ? Result<ContentId>.Success(default)
                : ContentId.Create(value, packId, authorAssetPath);
        }

        private static bool IsFinitePositive(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;

        private static bool IsFiniteNonNegative(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;

        private static Result<RuntimeContentDefinition> Failure(
            string message,
            AuthoringCommonData common,
            ContentId packId)
        {
            return Result<RuntimeContentDefinition>.Failure(
                new Error(
                    ErrorCode.InvalidAuthoringData,
                    message,
                    common.Id,
                    packId,
                    common.AuthorAssetPath));
        }
    }
}
