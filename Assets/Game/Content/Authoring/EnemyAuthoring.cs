using System;
using Game.Content.Runtime;
using Game.Core;
using UnityEngine;

namespace Game.Content.Authoring
{
    /// <summary>
    /// Minimal M1 enemy authoring metadata. No entity or spawn behavior is implemented.
    /// </summary>
    [CreateAssetMenu(menuName = "Free World/Content/Enemy", fileName = "Enemy")]
    public sealed class EnemyAuthoring : ContentAuthoringBase
    {
        [SerializeField] private float baseMaxHealth = 10f;
        [SerializeField] private float collisionRadius = 0.5f;
        [SerializeField] private bool m5RuntimeEnabled;
        [SerializeField] private float baseMoveSpeed = 2f;
        [SerializeField] private float baseDamage = 1f;
        [SerializeField] private float attackRange = 1f;
        [SerializeField] private SkillAuthoring attackSkill;
        [SerializeField] private float experienceReward = 1f;
        [SerializeField] private float lootReward;
        [SerializeField] private string visualProfileId = string.Empty;
        [SerializeField] private EnemyMovementMode movementMode = EnemyMovementMode.Chase;
        [SerializeField] private float preferredDistance = 1f;
        [SerializeField] private float decisionIntervalSeconds = 0.1f;
        [SerializeField] private float chargeWindupSeconds = 0.5f;
        [SerializeField] private float chargeDurationSeconds = 0.5f;
        [SerializeField] private float chargeSpeedMultiplier = 2f;
        [SerializeField] private float attackCooldownSeconds = 1f;
        [SerializeField] private float separationRadius = 1f;
        [SerializeField] private float separationWeight = 0.5f;
        [SerializeField] private float obstacleAvoidanceWeight = 1f;

        public bool M5RuntimeEnabled => m5RuntimeEnabled;

        /// <summary>
        /// Configures M1 enemy values.
        /// </summary>
        public void Configure(float maximumHealth, float radius)
        {
            baseMaxHealth = maximumHealth;
            collisionRadius = radius;
            m5RuntimeEnabled = false;
        }

        /// <summary>Configures schema-4 enemy simulation data.</summary>
        public void ConfigureM5(
            float maximumHealth,
            float radius,
            float moveSpeed,
            float damage,
            float range,
            SkillAuthoring skill,
            float experience,
            float loot,
            string visualId,
            EnemyMovementMode mode,
            float desiredDistance,
            float decisionInterval,
            float windup,
            float chargeDuration,
            float chargeMultiplier,
            float attackCooldown,
            float separationDistance,
            float separationStrength,
            float avoidanceStrength)
        {
            baseMaxHealth = maximumHealth;
            collisionRadius = radius;
            baseMoveSpeed = moveSpeed;
            baseDamage = damage;
            attackRange = range;
            attackSkill = skill;
            experienceReward = experience;
            lootReward = loot;
            visualProfileId = visualId ?? string.Empty;
            movementMode = mode;
            preferredDistance = desiredDistance;
            decisionIntervalSeconds = decisionInterval;
            chargeWindupSeconds = windup;
            chargeDurationSeconds = chargeDuration;
            chargeSpeedMultiplier = chargeMultiplier;
            attackCooldownSeconds = attackCooldown;
            separationRadius = separationDistance;
            separationWeight = separationStrength;
            obstacleAvoidanceWeight = avoidanceStrength;
            m5RuntimeEnabled = true;
        }

        internal override Result<RuntimeContentDefinition> Bake(
            ContentId packId,
            string authorAssetPath)
        {
            var commonResult = BakeCommon(packId, authorAssetPath);
            if (!commonResult.IsSuccess)
            {
                return Result<RuntimeContentDefinition>.Failure(commonResult.Error);
            }

            var common = commonResult.Value;
            if (baseMaxHealth <= 0f || collisionRadius <= 0f)
            {
                return Result<RuntimeContentDefinition>.Failure(
                    new Error(
                        ErrorCode.InvalidAuthoringData,
                        "Enemy health and collision radius must be positive.",
                        common.Id,
                        packId,
                        authorAssetPath));
            }

            if (!m5RuntimeEnabled)
            {
                return Result<RuntimeContentDefinition>.Success(
                    new RuntimeEnemyDefinition(
                        common.Id,
                        common.LocalizedNameKey,
                        common.LocalizedDescriptionKey,
                        common.AuthorAssetPath,
                        common.Tags,
                        baseMaxHealth,
                        collisionRadius));
            }

            if (!IsFinitePositive(baseMoveSpeed) || !IsFiniteNonNegative(baseDamage) ||
                !IsFinitePositive(attackRange) || !IsFiniteNonNegative(experienceReward) ||
                !IsFiniteNonNegative(lootReward) || attackSkill == null)
            {
                return Failure(
                    "M5 enemy movement, combat, reward values, and attack skill are invalid.",
                    common,
                    packId);
            }

            var attackId = ContentId.Create(attackSkill.ContentIdText, packId, authorAssetPath);
            if (!attackId.IsSuccess) return Result<RuntimeContentDefinition>.Failure(attackId.Error);
            var visualId = ContentId.Create(visualProfileId, packId, authorAssetPath);
            if (!visualId.IsSuccess) return Result<RuntimeContentDefinition>.Failure(visualId.Error);

            var behaviorValuesValid = Enum.IsDefined(typeof(EnemyMovementMode), movementMode) &&
                                      IsFiniteNonNegative(preferredDistance) &&
                                      IsFinitePositive(decisionIntervalSeconds) &&
                                      IsFiniteNonNegative(chargeWindupSeconds) &&
                                      IsFiniteNonNegative(chargeDurationSeconds) &&
                                      IsFinitePositive(chargeSpeedMultiplier) &&
                                      IsFiniteNonNegative(attackCooldownSeconds) &&
                                      IsFiniteNonNegative(separationRadius) &&
                                      IsFiniteNonNegative(separationWeight) &&
                                      IsFiniteNonNegative(obstacleAvoidanceWeight);
            if (!behaviorValuesValid)
            {
                return Failure("M5 enemy behavior values are invalid.", common, packId);
            }

            return Result<RuntimeContentDefinition>.Success(
                new RuntimeEnemyDefinition(
                    common.Id,
                    common.LocalizedNameKey,
                    common.LocalizedDescriptionKey,
                    common.AuthorAssetPath,
                    common.Tags,
                    baseMaxHealth,
                    collisionRadius,
                    baseMoveSpeed,
                    baseDamage,
                    attackRange,
                    attackId.Value,
                    experienceReward,
                    lootReward,
                    visualId.Value,
                    new RuntimeEnemyBehavior(
                        movementMode,
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
