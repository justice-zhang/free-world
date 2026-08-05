using System;
using System.Numerics;
using Game.Core;

namespace Game.Content.Runtime
{
    [Serializable]
    public sealed class EnemyRuntimeDefinitionDto
    {
        public float baseMoveSpeed;
        public float baseDamage;
        public float attackRange;
        public string attackSkillId;
        public float experienceReward;
        public float lootReward;
        public string visualProfileId;
        public int movementMode;
        public float preferredDistance;
        public float decisionIntervalSeconds;
        public float chargeWindupSeconds;
        public float chargeDurationSeconds;
        public float chargeSpeedMultiplier;
        public float attackCooldownSeconds;
        public float separationRadius;
        public float separationWeight;
        public float obstacleAvoidanceWeight;

        internal Result<RuntimeContentDefinition> ToDefinition(
            ContentId packId,
            ContentId id,
            string nameKey,
            string descriptionKey,
            string sourcePath,
            ContentTag[] tags,
            float baseMaxHealth,
            float collisionRadius)
        {
            var skill = CatalogDtoParsing.ParseCanonicalId(
                attackSkillId,
                packId,
                sourcePath,
                "enemy attack skill ID");
            if (!skill.IsSuccess) return Result<RuntimeContentDefinition>.Failure(skill.Error);
            var visual = CatalogDtoParsing.ParseCanonicalId(
                visualProfileId,
                packId,
                sourcePath,
                "enemy visual profile ID");
            if (!visual.IsSuccess) return Result<RuntimeContentDefinition>.Failure(visual.Error);

            return Result<RuntimeContentDefinition>.Success(
                new RuntimeEnemyDefinition(
                    id,
                    nameKey,
                    descriptionKey,
                    sourcePath,
                    tags,
                    baseMaxHealth,
                    collisionRadius,
                    baseMoveSpeed,
                    baseDamage,
                    attackRange,
                    skill.Value,
                    experienceReward,
                    lootReward,
                    visual.Value,
                    new RuntimeEnemyBehavior(
                        (EnemyMovementMode)movementMode,
                        preferredDistance,
                        decisionIntervalSeconds,
                        chargeWindupSeconds,
                        chargeDurationSeconds,
                        chargeSpeedMultiplier,
                        attackCooldownSeconds,
                        separationRadius,
                        separationWeight,
                        obstacleAvoidanceWeight)));
        }

        internal static EnemyRuntimeDefinitionDto FromDefinition(RuntimeEnemyDefinition enemy)
        {
            var behavior = enemy.Behavior;
            return new EnemyRuntimeDefinitionDto
            {
                baseMoveSpeed = enemy.BaseMoveSpeed,
                baseDamage = enemy.BaseDamage,
                attackRange = enemy.AttackRange,
                attackSkillId = enemy.AttackSkillId.Value,
                experienceReward = enemy.ExperienceReward,
                lootReward = enemy.LootReward,
                visualProfileId = enemy.VisualProfileId.Value,
                movementMode = (int)behavior.MovementMode,
                preferredDistance = behavior.PreferredDistance,
                decisionIntervalSeconds = behavior.DecisionIntervalSeconds,
                chargeWindupSeconds = behavior.ChargeWindupSeconds,
                chargeDurationSeconds = behavior.ChargeDurationSeconds,
                chargeSpeedMultiplier = behavior.ChargeSpeedMultiplier,
                attackCooldownSeconds = behavior.AttackCooldownSeconds,
                separationRadius = behavior.SeparationRadius,
                separationWeight = behavior.SeparationWeight,
                obstacleAvoidanceWeight = behavior.ObstacleAvoidanceWeight
            };
        }
    }

    [Serializable]
    public sealed class MapObstacleDto
    {
        public float minimumX;
        public float minimumY;
        public float maximumX;
        public float maximumY;
    }

    [Serializable]
    public sealed class MapAnchorDto
    {
        public string id;
        public float positionX;
        public float positionY;
    }

    [Serializable]
    public sealed class MapRuntimeDefinitionDto
    {
        public int boundsMode;
        public float minimumX;
        public float minimumY;
        public float maximumX;
        public float maximumY;
        public float chunkSize;
        public int activeChunkRadius;
        public string encounterScheduleId;
        public string visualProfileId;
        public MapObstacleDto[] obstacles;
        public MapAnchorDto[] anchors;
        public string[] objectiveIds;
        public string[] eventIds;
        public string[] landmarkIds;

        internal Result<RuntimeContentDefinition> ToDefinition(
            ContentId packId,
            ContentId id,
            string nameKey,
            string descriptionKey,
            string sourcePath,
            ContentTag[] tags,
            string runtimeProviderId,
            string sceneAddress,
            int schemaVersion)
        {
            var encounter = CatalogDtoParsing.ParseCanonicalId(
                encounterScheduleId,
                packId,
                sourcePath,
                "map encounter schedule ID");
            if (!encounter.IsSuccess) return Result<RuntimeContentDefinition>.Failure(encounter.Error);
            var visual = CatalogDtoParsing.ParseCanonicalId(
                visualProfileId,
                packId,
                sourcePath,
                "map visual profile ID");
            if (!visual.IsSuccess) return Result<RuntimeContentDefinition>.Failure(visual.Error);

            var sourceObstacles = obstacles ?? Array.Empty<MapObstacleDto>();
            var runtimeObstacles = new RuntimeMapObstacle[sourceObstacles.Length];
            for (var index = 0; index < sourceObstacles.Length; index++)
            {
                if (sourceObstacles[index] == null)
                    return Failure("Serialized map contains a null obstacle.", packId, id, sourcePath);
                runtimeObstacles[index] = new RuntimeMapObstacle(
                    new Vector2(sourceObstacles[index].minimumX, sourceObstacles[index].minimumY),
                    new Vector2(sourceObstacles[index].maximumX, sourceObstacles[index].maximumY));
            }

            var sourceAnchors = anchors ?? Array.Empty<MapAnchorDto>();
            var runtimeAnchors = new RuntimeMapAnchor[sourceAnchors.Length];
            for (var index = 0; index < sourceAnchors.Length; index++)
            {
                if (sourceAnchors[index] == null)
                    return Failure("Serialized map contains a null anchor.", packId, id, sourcePath);
                var anchorId = CatalogDtoParsing.ParseCanonicalId(
                    sourceAnchors[index].id,
                    packId,
                    sourcePath,
                    "map anchor ID");
                if (!anchorId.IsSuccess) return Result<RuntimeContentDefinition>.Failure(anchorId.Error);
                runtimeAnchors[index] = new RuntimeMapAnchor(
                    anchorId.Value,
                    new Vector2(sourceAnchors[index].positionX, sourceAnchors[index].positionY));
            }

            var objectives = schemaVersion >= ContentPackTopology.QinglanDemoSchemaVersion
                ? CatalogDtoParsing.ParseIds(objectiveIds, packId, id, sourcePath)
                : Result<ContentId[]>.Success(Array.Empty<ContentId>());
            if (!objectives.IsSuccess) return Result<RuntimeContentDefinition>.Failure(objectives.Error);
            var events = schemaVersion >= ContentPackTopology.QinglanDemoSchemaVersion
                ? CatalogDtoParsing.ParseIds(eventIds, packId, id, sourcePath)
                : Result<ContentId[]>.Success(Array.Empty<ContentId>());
            if (!events.IsSuccess) return Result<RuntimeContentDefinition>.Failure(events.Error);
            var landmarks = schemaVersion >= ContentPackTopology.QinglanDemoSchemaVersion
                ? CatalogDtoParsing.ParseIds(landmarkIds, packId, id, sourcePath)
                : Result<ContentId[]>.Success(Array.Empty<ContentId>());
            if (!landmarks.IsSuccess) return Result<RuntimeContentDefinition>.Failure(landmarks.Error);

            return Result<RuntimeContentDefinition>.Success(
                new RuntimeMapDefinition(
                    id,
                    nameKey,
                    descriptionKey,
                    sourcePath,
                    tags,
                    runtimeProviderId,
                    sceneAddress,
                    (MapBoundsMode)boundsMode,
                    new Vector2(minimumX, minimumY),
                    new Vector2(maximumX, maximumY),
                    chunkSize,
                    activeChunkRadius,
                    encounter.Value,
                    visual.Value,
                    runtimeObstacles,
                    runtimeAnchors,
                    objectives.Value,
                    events.Value,
                    landmarks.Value));
        }

        internal static MapRuntimeDefinitionDto FromDefinition(RuntimeMapDefinition map)
        {
            var dto = new MapRuntimeDefinitionDto
            {
                boundsMode = (int)map.BoundsMode,
                minimumX = map.Minimum.X,
                minimumY = map.Minimum.Y,
                maximumX = map.Maximum.X,
                maximumY = map.Maximum.Y,
                chunkSize = map.ChunkSize,
                activeChunkRadius = map.ActiveChunkRadius,
                encounterScheduleId = map.EncounterScheduleId.Value,
                visualProfileId = map.VisualProfileId.Value,
                obstacles = new MapObstacleDto[map.Obstacles.Count],
                anchors = new MapAnchorDto[map.Anchors.Count],
                objectiveIds = ToIds(map.ObjectiveIds),
                eventIds = ToIds(map.EventIds),
                landmarkIds = ToIds(map.LandmarkIds)
            };
            for (var index = 0; index < dto.obstacles.Length; index++)
            {
                dto.obstacles[index] = new MapObstacleDto
                {
                    minimumX = map.Obstacles[index].Minimum.X,
                    minimumY = map.Obstacles[index].Minimum.Y,
                    maximumX = map.Obstacles[index].Maximum.X,
                    maximumY = map.Obstacles[index].Maximum.Y
                };
            }

            for (var index = 0; index < dto.anchors.Length; index++)
            {
                dto.anchors[index] = new MapAnchorDto
                {
                    id = map.Anchors[index].Id.Value,
                    positionX = map.Anchors[index].Position.X,
                    positionY = map.Anchors[index].Position.Y
                };
            }

            return dto;
        }

        private static string[] ToIds(System.Collections.Generic.IReadOnlyList<ContentId> source)
        {
            var result = new string[source.Count];
            for (var index = 0; index < result.Length; index++) result[index] = source[index].Value;
            return result;
        }

        private static Result<RuntimeContentDefinition> Failure(
            string message,
            ContentId packId,
            ContentId ownerId,
            string sourcePath)
        {
            return Result<RuntimeContentDefinition>.Failure(
                new Error(ErrorCode.InvalidCatalog, message, ownerId, packId, sourcePath));
        }
    }

    [Serializable]
    public sealed class EncounterEnemyEntryDto
    {
        public string enemyId;
        public float weight;
        public float budgetCost;
        public int minimumGroupSize;
        public int maximumGroupSize;
        public bool elite;
        public string[] affixPoolIds;
    }

    [Serializable]
    public sealed class EncounterBossRuleDto
    {
        public string enemyId;
        public float spawnTimeSeconds;
        public int pattern;
        public string anchorId;
        public string bossDefinitionId;
    }

    [Serializable]
    public sealed class EncounterPhaseDto
    {
        public float startTimeSeconds;
        public float endTimeSeconds;
        public float budgetPerSecondStart;
        public float budgetPerSecondEnd;
        public float spawnIntervalStart;
        public float spawnIntervalEnd;
        public int maximumConcurrentEnemies;
        public int spawnPattern;
        public string anchorId;
        public EncounterEnemyEntryDto[] enemies;
        public EncounterBossRuleDto[] bosses;
    }

    [Serializable]
    public sealed class EncounterScheduleDefinitionDto
    {
        public int maximumConcurrentEnemies;
        public float minimumSpawnDistance;
        public float maximumSpawnDistance;
        public EncounterPhaseDto[] phases;

        internal Result<RuntimeContentDefinition> ToDefinition(
            ContentId packId,
            ContentId id,
            string nameKey,
            string descriptionKey,
            string sourcePath,
            ContentTag[] tags,
            int schemaVersion)
        {
            var sourcePhases = phases ?? Array.Empty<EncounterPhaseDto>();
            var runtimePhases = new RuntimeEncounterPhase[sourcePhases.Length];
            for (var phaseIndex = 0; phaseIndex < sourcePhases.Length; phaseIndex++)
            {
                var phase = sourcePhases[phaseIndex];
                if (phase == null)
                    return Failure("Serialized encounter contains a null phase.", packId, id, sourcePath);
                var anchor = ParseOptionalId(phase.anchorId, packId, sourcePath);
                if (!anchor.IsSuccess) return Result<RuntimeContentDefinition>.Failure(anchor.Error);

                var sourceEntries = phase.enemies ?? Array.Empty<EncounterEnemyEntryDto>();
                var entries = new RuntimeEncounterEnemyEntry[sourceEntries.Length];
                for (var entryIndex = 0; entryIndex < sourceEntries.Length; entryIndex++)
                {
                    var entry = sourceEntries[entryIndex];
                    if (entry == null)
                        return Failure("Serialized encounter contains a null enemy entry.", packId, id, sourcePath);
                    var enemy = CatalogDtoParsing.ParseCanonicalId(
                        entry.enemyId,
                        packId,
                        sourcePath,
                        "encounter enemy ID");
                    if (!enemy.IsSuccess) return Result<RuntimeContentDefinition>.Failure(enemy.Error);
                    var affixes = schemaVersion >= ContentPackTopology.QinglanDemoSchemaVersion
                        ? CatalogDtoParsing.ParseIds(entry.affixPoolIds, packId, id, sourcePath)
                        : Result<ContentId[]>.Success(Array.Empty<ContentId>());
                    if (!affixes.IsSuccess) return Result<RuntimeContentDefinition>.Failure(affixes.Error);
                    entries[entryIndex] = new RuntimeEncounterEnemyEntry(
                        enemy.Value,
                        entry.weight,
                        entry.budgetCost,
                        entry.minimumGroupSize,
                        entry.maximumGroupSize,
                        entry.elite,
                        affixes.Value);
                }

                var sourceBosses = phase.bosses ?? Array.Empty<EncounterBossRuleDto>();
                var bosses = new RuntimeEncounterBossRule[sourceBosses.Length];
                for (var bossIndex = 0; bossIndex < sourceBosses.Length; bossIndex++)
                {
                    var boss = sourceBosses[bossIndex];
                    if (boss == null)
                        return Failure("Serialized encounter contains a null boss rule.", packId, id, sourcePath);
                    var enemy = CatalogDtoParsing.ParseCanonicalId(
                        boss.enemyId,
                        packId,
                        sourcePath,
                        "encounter boss enemy ID");
                    if (!enemy.IsSuccess) return Result<RuntimeContentDefinition>.Failure(enemy.Error);
                    var bossAnchor = ParseOptionalId(boss.anchorId, packId, sourcePath);
                    if (!bossAnchor.IsSuccess) return Result<RuntimeContentDefinition>.Failure(bossAnchor.Error);
                    var bossDefinition = schemaVersion >= ContentPackTopology.QinglanDemoSchemaVersion
                        ? ParseOptionalId(boss.bossDefinitionId, packId, sourcePath)
                        : Result<ContentId>.Success(default);
                    if (!bossDefinition.IsSuccess) return Result<RuntimeContentDefinition>.Failure(bossDefinition.Error);
                    bosses[bossIndex] = new RuntimeEncounterBossRule(
                        enemy.Value,
                        boss.spawnTimeSeconds,
                        (SpawnPattern)boss.pattern,
                        bossAnchor.Value,
                        bossDefinition.Value);
                }

                runtimePhases[phaseIndex] = new RuntimeEncounterPhase(
                    phase.startTimeSeconds,
                    phase.endTimeSeconds,
                    phase.budgetPerSecondStart,
                    phase.budgetPerSecondEnd,
                    phase.spawnIntervalStart,
                    phase.spawnIntervalEnd,
                    phase.maximumConcurrentEnemies,
                    (SpawnPattern)phase.spawnPattern,
                    anchor.Value,
                    entries,
                    bosses);
            }

            return Result<RuntimeContentDefinition>.Success(
                new RuntimeEncounterSchedule(
                    id,
                    nameKey,
                    descriptionKey,
                    sourcePath,
                    tags,
                    maximumConcurrentEnemies,
                    minimumSpawnDistance,
                    maximumSpawnDistance,
                    runtimePhases));
        }

        internal static EncounterScheduleDefinitionDto FromDefinition(RuntimeEncounterSchedule schedule)
        {
            var dto = new EncounterScheduleDefinitionDto
            {
                maximumConcurrentEnemies = schedule.MaximumConcurrentEnemies,
                minimumSpawnDistance = schedule.MinimumSpawnDistance,
                maximumSpawnDistance = schedule.MaximumSpawnDistance,
                phases = new EncounterPhaseDto[schedule.Phases.Count]
            };
            for (var phaseIndex = 0; phaseIndex < dto.phases.Length; phaseIndex++)
            {
                var phase = schedule.Phases[phaseIndex];
                var phaseDto = new EncounterPhaseDto
                {
                    startTimeSeconds = phase.StartTimeSeconds,
                    endTimeSeconds = phase.EndTimeSeconds,
                    budgetPerSecondStart = phase.BudgetPerSecondStart,
                    budgetPerSecondEnd = phase.BudgetPerSecondEnd,
                    spawnIntervalStart = phase.SpawnIntervalStart,
                    spawnIntervalEnd = phase.SpawnIntervalEnd,
                    maximumConcurrentEnemies = phase.MaximumConcurrentEnemies,
                    spawnPattern = (int)phase.SpawnPattern,
                    anchorId = phase.AnchorId.Value,
                    enemies = new EncounterEnemyEntryDto[phase.EnemyEntries.Count],
                    bosses = new EncounterBossRuleDto[phase.BossRules.Count]
                };
                for (var entryIndex = 0; entryIndex < phaseDto.enemies.Length; entryIndex++)
                {
                    var entry = phase.EnemyEntries[entryIndex];
                    phaseDto.enemies[entryIndex] = new EncounterEnemyEntryDto
                    {
                        enemyId = entry.EnemyId.Value,
                        weight = entry.Weight,
                        budgetCost = entry.BudgetCost,
                        minimumGroupSize = entry.MinimumGroupSize,
                        maximumGroupSize = entry.MaximumGroupSize,
                        elite = entry.Elite,
                        affixPoolIds = ToIds(entry.AffixPoolIds)
                    };
                }

                for (var bossIndex = 0; bossIndex < phaseDto.bosses.Length; bossIndex++)
                {
                    var boss = phase.BossRules[bossIndex];
                    phaseDto.bosses[bossIndex] = new EncounterBossRuleDto
                    {
                        enemyId = boss.EnemyId.Value,
                        spawnTimeSeconds = boss.SpawnTimeSeconds,
                        pattern = (int)boss.Pattern,
                        anchorId = boss.AnchorId.Value,
                        bossDefinitionId = boss.BossDefinitionId.Value
                    };
                }

                dto.phases[phaseIndex] = phaseDto;
            }

            return dto;
        }

        private static string[] ToIds(System.Collections.Generic.IReadOnlyList<ContentId> source)
        {
            var result = new string[source.Count];
            for (var index = 0; index < result.Length; index++) result[index] = source[index].Value;
            return result;
        }

        private static Result<ContentId> ParseOptionalId(
            string value,
            ContentId packId,
            string sourcePath)
        {
            return string.IsNullOrEmpty(value)
                ? Result<ContentId>.Success(default)
                : CatalogDtoParsing.ParseCanonicalId(value, packId, sourcePath, "optional anchor ID");
        }

        private static Result<RuntimeContentDefinition> Failure(
            string message,
            ContentId packId,
            ContentId ownerId,
            string sourcePath)
        {
            return Result<RuntimeContentDefinition>.Failure(
                new Error(ErrorCode.InvalidCatalog, message, ownerId, packId, sourcePath));
        }
    }
}
