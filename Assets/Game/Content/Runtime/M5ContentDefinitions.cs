using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using Game.Core;

namespace Game.Content.Runtime
{
    /// <summary>Configurable high-level movement decision used by M5 enemies.</summary>
    public enum EnemyMovementMode : byte
    {
        Chase = 1,
        KeepDistance = 2,
        Charge = 3,
        Ranged = 4
    }

    /// <summary>Stable spawn layouts supported by the M5 scheduler.</summary>
    public enum SpawnPattern : byte
    {
        Ring = 1,
        Edge = 2,
        Cluster = 3,
        Line = 4,
        Ambush = 5,
        Portal = 6,
        FixedAnchor = 7,
        OffscreenRandom = 8
    }

    /// <summary>Declares the pure map-runtime backend selected by a map definition.</summary>
    public enum MapBoundsMode : byte
    {
        Finite = 1,
        ChunkedInfinite = 2
    }

    /// <summary>Immutable numeric behavior parameters shared by enemies using the same definition.</summary>
    public readonly struct RuntimeEnemyBehavior
    {
        public RuntimeEnemyBehavior(
            EnemyMovementMode movementMode,
            float preferredDistance,
            float decisionIntervalSeconds,
            float chargeWindupSeconds,
            float chargeDurationSeconds,
            float chargeSpeedMultiplier,
            float attackCooldownSeconds,
            float separationRadius,
            float separationWeight,
            float obstacleAvoidanceWeight)
        {
            MovementMode = movementMode;
            PreferredDistance = preferredDistance;
            DecisionIntervalSeconds = decisionIntervalSeconds;
            ChargeWindupSeconds = chargeWindupSeconds;
            ChargeDurationSeconds = chargeDurationSeconds;
            ChargeSpeedMultiplier = chargeSpeedMultiplier;
            AttackCooldownSeconds = attackCooldownSeconds;
            SeparationRadius = separationRadius;
            SeparationWeight = separationWeight;
            ObstacleAvoidanceWeight = obstacleAvoidanceWeight;
        }

        public EnemyMovementMode MovementMode { get; }
        public float PreferredDistance { get; }
        public float DecisionIntervalSeconds { get; }
        public float ChargeWindupSeconds { get; }
        public float ChargeDurationSeconds { get; }
        public float ChargeSpeedMultiplier { get; }
        public float AttackCooldownSeconds { get; }
        public float SeparationRadius { get; }
        public float SeparationWeight { get; }
        public float ObstacleAvoidanceWeight { get; }

        internal void AppendDeterministicData(StringBuilder builder)
        {
            ContentHashUtility.AppendInt(builder, (int)MovementMode);
            ContentHashUtility.AppendFloat(builder, PreferredDistance);
            ContentHashUtility.AppendFloat(builder, DecisionIntervalSeconds);
            ContentHashUtility.AppendFloat(builder, ChargeWindupSeconds);
            ContentHashUtility.AppendFloat(builder, ChargeDurationSeconds);
            ContentHashUtility.AppendFloat(builder, ChargeSpeedMultiplier);
            ContentHashUtility.AppendFloat(builder, AttackCooldownSeconds);
            ContentHashUtility.AppendFloat(builder, SeparationRadius);
            ContentHashUtility.AppendFloat(builder, SeparationWeight);
            ContentHashUtility.AppendFloat(builder, ObstacleAvoidanceWeight);
        }
    }

    /// <summary>One axis-aligned obstacle baked into a pure map definition.</summary>
    public readonly struct RuntimeMapObstacle
    {
        public RuntimeMapObstacle(Vector2 minimum, Vector2 maximum)
        {
            Minimum = minimum;
            Maximum = maximum;
        }

        public Vector2 Minimum { get; }
        public Vector2 Maximum { get; }
    }

    /// <summary>One stable spawn anchor baked into a pure map definition.</summary>
    public readonly struct RuntimeMapAnchor
    {
        public RuntimeMapAnchor(ContentId id, Vector2 position)
        {
            Id = id;
            Position = position;
        }

        public ContentId Id { get; }
        public Vector2 Position { get; }
    }

    /// <summary>Weighted enemy group used by an encounter phase.</summary>
    public readonly struct RuntimeEncounterEnemyEntry
    {
        public RuntimeEncounterEnemyEntry(
            ContentId enemyId,
            float weight,
            float budgetCost,
            int minimumGroupSize,
            int maximumGroupSize,
            bool elite)
        {
            EnemyId = enemyId;
            Weight = weight;
            BudgetCost = budgetCost;
            MinimumGroupSize = minimumGroupSize;
            MaximumGroupSize = maximumGroupSize;
            Elite = elite;
        }

        public ContentId EnemyId { get; }
        public float Weight { get; }
        public float BudgetCost { get; }
        public int MinimumGroupSize { get; }
        public int MaximumGroupSize { get; }
        public bool Elite { get; }
    }

    /// <summary>One deterministic, one-shot Boss spawn rule.</summary>
    public readonly struct RuntimeEncounterBossRule
    {
        public RuntimeEncounterBossRule(
            ContentId enemyId,
            float spawnTimeSeconds,
            SpawnPattern pattern,
            ContentId anchorId)
        {
            EnemyId = enemyId;
            SpawnTimeSeconds = spawnTimeSeconds;
            Pattern = pattern;
            AnchorId = anchorId;
        }

        public ContentId EnemyId { get; }
        public float SpawnTimeSeconds { get; }
        public SpawnPattern Pattern { get; }
        public ContentId AnchorId { get; }
    }

    /// <summary>Immutable encounter phase with linearly sampled budget and interval values.</summary>
    public sealed class RuntimeEncounterPhase
    {
        private readonly RuntimeEncounterEnemyEntry[] enemies;
        private readonly RuntimeEncounterBossRule[] bosses;
        private readonly IReadOnlyList<RuntimeEncounterEnemyEntry> enemiesView;
        private readonly IReadOnlyList<RuntimeEncounterBossRule> bossesView;

        public RuntimeEncounterPhase(
            float startTimeSeconds,
            float endTimeSeconds,
            float budgetPerSecondStart,
            float budgetPerSecondEnd,
            float spawnIntervalStart,
            float spawnIntervalEnd,
            int maximumConcurrentEnemies,
            SpawnPattern spawnPattern,
            ContentId anchorId,
            RuntimeEncounterEnemyEntry[] enemyEntries,
            RuntimeEncounterBossRule[] bossRules)
        {
            StartTimeSeconds = startTimeSeconds;
            EndTimeSeconds = endTimeSeconds;
            BudgetPerSecondStart = budgetPerSecondStart;
            BudgetPerSecondEnd = budgetPerSecondEnd;
            SpawnIntervalStart = spawnIntervalStart;
            SpawnIntervalEnd = spawnIntervalEnd;
            MaximumConcurrentEnemies = maximumConcurrentEnemies;
            SpawnPattern = spawnPattern;
            AnchorId = anchorId;
            enemies = enemyEntries == null
                ? Array.Empty<RuntimeEncounterEnemyEntry>()
                : (RuntimeEncounterEnemyEntry[])enemyEntries.Clone();
            bosses = bossRules == null
                ? Array.Empty<RuntimeEncounterBossRule>()
                : (RuntimeEncounterBossRule[])bossRules.Clone();
            enemiesView = Array.AsReadOnly(enemies);
            bossesView = Array.AsReadOnly(bosses);
        }

        public float StartTimeSeconds { get; }
        public float EndTimeSeconds { get; }
        public float BudgetPerSecondStart { get; }
        public float BudgetPerSecondEnd { get; }
        public float SpawnIntervalStart { get; }
        public float SpawnIntervalEnd { get; }
        public int MaximumConcurrentEnemies { get; }
        public SpawnPattern SpawnPattern { get; }
        public ContentId AnchorId { get; }
        public IReadOnlyList<RuntimeEncounterEnemyEntry> EnemyEntries => enemiesView;
        public IReadOnlyList<RuntimeEncounterBossRule> BossRules => bossesView;

        internal void AppendDeterministicData(StringBuilder builder)
        {
            ContentHashUtility.AppendFloat(builder, StartTimeSeconds);
            ContentHashUtility.AppendFloat(builder, EndTimeSeconds);
            ContentHashUtility.AppendFloat(builder, BudgetPerSecondStart);
            ContentHashUtility.AppendFloat(builder, BudgetPerSecondEnd);
            ContentHashUtility.AppendFloat(builder, SpawnIntervalStart);
            ContentHashUtility.AppendFloat(builder, SpawnIntervalEnd);
            ContentHashUtility.AppendInt(builder, MaximumConcurrentEnemies);
            ContentHashUtility.AppendInt(builder, (int)SpawnPattern);
            ContentHashUtility.AppendToken(builder, AnchorId.IsValid ? AnchorId.Value : string.Empty);
            ContentHashUtility.AppendInt(builder, enemies.Length);
            for (var index = 0; index < enemies.Length; index++)
            {
                var entry = enemies[index];
                ContentHashUtility.AppendToken(builder, entry.EnemyId.Value);
                ContentHashUtility.AppendFloat(builder, entry.Weight);
                ContentHashUtility.AppendFloat(builder, entry.BudgetCost);
                ContentHashUtility.AppendInt(builder, entry.MinimumGroupSize);
                ContentHashUtility.AppendInt(builder, entry.MaximumGroupSize);
                ContentHashUtility.AppendInt(builder, entry.Elite ? 1 : 0);
            }

            ContentHashUtility.AppendInt(builder, bosses.Length);
            for (var index = 0; index < bosses.Length; index++)
            {
                var boss = bosses[index];
                ContentHashUtility.AppendToken(builder, boss.EnemyId.Value);
                ContentHashUtility.AppendFloat(builder, boss.SpawnTimeSeconds);
                ContentHashUtility.AppendInt(builder, (int)boss.Pattern);
                ContentHashUtility.AppendToken(builder, boss.AnchorId.IsValid ? boss.AnchorId.Value : string.Empty);
            }
        }
    }

    /// <summary>Schema-4 encounter content independent of any map scene.</summary>
    public sealed class RuntimeEncounterSchedule : RuntimeContentDefinition
    {
        private readonly RuntimeEncounterPhase[] phases;
        private readonly IReadOnlyList<RuntimeEncounterPhase> phasesView;

        public RuntimeEncounterSchedule(
            ContentId id,
            string localizedNameKey,
            string localizedDescriptionKey,
            string sourceAssetPath,
            ContentTag[] tags,
            int maximumConcurrentEnemies,
            float minimumSpawnDistance,
            float maximumSpawnDistance,
            RuntimeEncounterPhase[] phases)
            : base(
                id,
                localizedNameKey,
                localizedDescriptionKey,
                sourceAssetPath,
                tags,
                CollectReferences(phases))
        {
            MaximumConcurrentEnemies = maximumConcurrentEnemies;
            MinimumSpawnDistance = minimumSpawnDistance;
            MaximumSpawnDistance = maximumSpawnDistance;
            this.phases = phases == null
                ? Array.Empty<RuntimeEncounterPhase>()
                : (RuntimeEncounterPhase[])phases.Clone();
            phasesView = Array.AsReadOnly(this.phases);
        }

        public override string Kind => RuntimeContentKinds.Encounter;
        public int MaximumConcurrentEnemies { get; }
        public float MinimumSpawnDistance { get; }
        public float MaximumSpawnDistance { get; }
        public IReadOnlyList<RuntimeEncounterPhase> Phases => phasesView;

        protected override void AppendTypeSpecificDeterministicData(StringBuilder builder)
        {
            ContentHashUtility.AppendInt(builder, MaximumConcurrentEnemies);
            ContentHashUtility.AppendFloat(builder, MinimumSpawnDistance);
            ContentHashUtility.AppendFloat(builder, MaximumSpawnDistance);
            ContentHashUtility.AppendInt(builder, phases.Length);
            for (var index = 0; index < phases.Length; index++)
            {
                phases[index].AppendDeterministicData(builder);
            }
        }

        private static ContentId[] CollectReferences(RuntimeEncounterPhase[] source)
        {
            if (source == null || source.Length == 0) return Array.Empty<ContentId>();
            var count = 0;
            for (var phaseIndex = 0; phaseIndex < source.Length; phaseIndex++)
            {
                var phase = source[phaseIndex];
                if (phase == null) continue;
                count += phase.EnemyEntries.Count + phase.BossRules.Count;
            }

            var references = new ContentId[count];
            var output = 0;
            for (var phaseIndex = 0; phaseIndex < source.Length; phaseIndex++)
            {
                var phase = source[phaseIndex];
                if (phase == null) continue;
                for (var index = 0; index < phase.EnemyEntries.Count; index++)
                    references[output++] = phase.EnemyEntries[index].EnemyId;
                for (var index = 0; index < phase.BossRules.Count; index++)
                    references[output++] = phase.BossRules[index].EnemyId;
            }

            return references;
        }
    }
}
