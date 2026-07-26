using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using Game.Core;

namespace Game.Content.Runtime
{
    /// <summary>
    /// Defines stable string discriminators used by the M1 baked catalog format.
    /// </summary>
    public static class RuntimeContentKinds
    {
        /// <summary>Identifies a character definition.</summary>
        public const string Character = "character";

        /// <summary>Identifies a skill definition.</summary>
        public const string Skill = "skill";

        /// <summary>Identifies an enemy definition.</summary>
        public const string Enemy = "enemy";

        /// <summary>Identifies a map definition.</summary>
        public const string Map = "map";

        /// <summary>Identifies a status-effect definition.</summary>
        public const string Status = "status";

        /// <summary>Identifies an M5 encounter schedule definition.</summary>
        public const string Encounter = "encounter";
    }

    /// <summary>
    /// Base class for pure runtime content definitions.
    /// </summary>
    public abstract class RuntimeContentDefinition
    {
        private readonly ContentTag[] tags;
        private readonly IReadOnlyList<ContentTag> tagsView;
        private readonly ContentId[] referencedContentIds;
        private readonly IReadOnlyList<ContentId> referencedContentIdsView;

        protected RuntimeContentDefinition(
            ContentId id,
            string localizedNameKey,
            string localizedDescriptionKey,
            string sourceAssetPath,
            ContentTag[] tags,
            ContentId[] referencedContentIds)
        {
            Id = id;
            LocalizedNameKey = localizedNameKey ?? string.Empty;
            LocalizedDescriptionKey = localizedDescriptionKey ?? string.Empty;
            SourceAssetPath = sourceAssetPath ?? string.Empty;
            this.tags = tags == null ? Array.Empty<ContentTag>() : (ContentTag[])tags.Clone();
            tagsView = Array.AsReadOnly(this.tags);
            this.referencedContentIds = referencedContentIds == null
                ? Array.Empty<ContentId>()
                : (ContentId[])referencedContentIds.Clone();
            referencedContentIdsView = Array.AsReadOnly(this.referencedContentIds);
        }

        /// <summary>
        /// Gets the stable content ID.
        /// </summary>
        public ContentId Id { get; }

        /// <summary>
        /// Gets the localization key for the display name.
        /// </summary>
        public string LocalizedNameKey { get; }

        /// <summary>
        /// Gets the localization key for the description.
        /// </summary>
        public string LocalizedDescriptionKey { get; }

        /// <summary>
        /// Gets the author asset path used for diagnostics.
        /// </summary>
        public string SourceAssetPath { get; }

        /// <summary>
        /// Gets the compositional content tags.
        /// </summary>
        public IReadOnlyList<ContentTag> Tags => tagsView;

        /// <summary>
        /// Gets stable IDs referenced by this definition.
        /// </summary>
        public IReadOnlyList<ContentId> ReferencedContentIds => referencedContentIdsView;

        /// <summary>
        /// Gets the stable format discriminator.
        /// </summary>
        public abstract string Kind { get; }

        internal void AppendDeterministicData(StringBuilder builder)
        {
            ContentHashUtility.AppendToken(builder, Kind);
            ContentHashUtility.AppendToken(builder, Id.Value);
            ContentHashUtility.AppendToken(builder, LocalizedNameKey);
            ContentHashUtility.AppendToken(builder, LocalizedDescriptionKey);
            ContentHashUtility.AppendToken(builder, SourceAssetPath);
            ContentHashUtility.AppendInt(builder, tags.Length);
            for (var index = 0; index < tags.Length; index++)
            {
                ContentHashUtility.AppendToken(builder, tags[index].Value);
            }

            ContentHashUtility.AppendInt(builder, referencedContentIds.Length);
            for (var index = 0; index < referencedContentIds.Length; index++)
            {
                ContentHashUtility.AppendToken(builder, referencedContentIds[index].Value);
            }

            AppendTypeSpecificDeterministicData(builder);
        }

        protected abstract void AppendTypeSpecificDeterministicData(StringBuilder builder);
    }

    /// <summary>
    /// Runtime character data required to seed later simulation milestones.
    /// </summary>
    public sealed class RuntimeCharacterDefinition : RuntimeContentDefinition
    {
        private readonly ContentId[] startingSkillIds;
        private readonly IReadOnlyList<ContentId> startingSkillIdsView;

        /// <summary>
        /// Initializes pure runtime character data.
        /// </summary>
        public RuntimeCharacterDefinition(
            ContentId id,
            string localizedNameKey,
            string localizedDescriptionKey,
            string sourceAssetPath,
            ContentTag[] tags,
            float baseMaxHealth,
            float moveSpeed,
            ContentId[] startingSkillIds)
            : base(
                id,
                localizedNameKey,
                localizedDescriptionKey,
                sourceAssetPath,
                tags,
                startingSkillIds)
        {
            BaseMaxHealth = baseMaxHealth;
            MoveSpeed = moveSpeed;
            this.startingSkillIds = startingSkillIds == null
                ? Array.Empty<ContentId>()
                : (ContentId[])startingSkillIds.Clone();
            startingSkillIdsView = Array.AsReadOnly(this.startingSkillIds);
        }

        /// <inheritdoc />
        public override string Kind => RuntimeContentKinds.Character;

        /// <summary>Gets the base maximum health.</summary>
        public float BaseMaxHealth { get; }

        /// <summary>Gets the movement speed.</summary>
        public float MoveSpeed { get; }

        /// <summary>Gets starting skill IDs in author order.</summary>
        public IReadOnlyList<ContentId> StartingSkillIds => startingSkillIdsView;

        protected override void AppendTypeSpecificDeterministicData(StringBuilder builder)
        {
            ContentHashUtility.AppendFloat(builder, BaseMaxHealth);
            ContentHashUtility.AppendFloat(builder, MoveSpeed);
            ContentHashUtility.AppendInt(builder, startingSkillIds.Length);
            for (var index = 0; index < startingSkillIds.Length; index++)
            {
                ContentHashUtility.AppendToken(builder, startingSkillIds[index].Value);
            }
        }
    }

    /// <summary>
    /// Runtime enemy metadata. M1 stores data only and does not spawn entities.
    /// </summary>
    public sealed class RuntimeEnemyDefinition : RuntimeContentDefinition
    {
        /// <summary>
        /// Initializes pure runtime enemy metadata.
        /// </summary>
        public RuntimeEnemyDefinition(
            ContentId id,
            string localizedNameKey,
            string localizedDescriptionKey,
            string sourceAssetPath,
            ContentTag[] tags,
            float baseMaxHealth,
            float collisionRadius)
            : base(
                id,
                localizedNameKey,
                localizedDescriptionKey,
                sourceAssetPath,
                tags,
                Array.Empty<ContentId>())
        {
            BaseMaxHealth = baseMaxHealth;
            CollisionRadius = collisionRadius;
            HasM5Data = false;
            Behavior = default;
        }

        /// <summary>Initializes schema-4 enemy combat, behavior, reward, and presentation metadata.</summary>
        public RuntimeEnemyDefinition(
            ContentId id,
            string localizedNameKey,
            string localizedDescriptionKey,
            string sourceAssetPath,
            ContentTag[] tags,
            float baseMaxHealth,
            float collisionRadius,
            float baseMoveSpeed,
            float baseDamage,
            float attackRange,
            ContentId attackSkillId,
            float experienceReward,
            float lootReward,
            ContentId visualProfileId,
            RuntimeEnemyBehavior behavior)
            : base(
                id,
                localizedNameKey,
                localizedDescriptionKey,
                sourceAssetPath,
                tags,
                attackSkillId.IsValid ? new[] { attackSkillId } : Array.Empty<ContentId>())
        {
            BaseMaxHealth = baseMaxHealth;
            CollisionRadius = collisionRadius;
            BaseMoveSpeed = baseMoveSpeed;
            BaseDamage = baseDamage;
            AttackRange = attackRange;
            AttackSkillId = attackSkillId;
            ExperienceReward = experienceReward;
            LootReward = lootReward;
            VisualProfileId = visualProfileId;
            Behavior = behavior;
            HasM5Data = true;
        }

        /// <inheritdoc />
        public override string Kind => RuntimeContentKinds.Enemy;

        /// <summary>Gets the base maximum health.</summary>
        public float BaseMaxHealth { get; }

        /// <summary>Gets the collision radius.</summary>
        public float CollisionRadius { get; }

        /// <summary>Gets whether schema-4 runtime fields were baked.</summary>
        public bool HasM5Data { get; }

        public float BaseMoveSpeed { get; }
        public float BaseDamage { get; }
        public float AttackRange { get; }
        public ContentId AttackSkillId { get; }
        public float ExperienceReward { get; }
        public float LootReward { get; }
        public ContentId VisualProfileId { get; }
        public RuntimeEnemyBehavior Behavior { get; }

        protected override void AppendTypeSpecificDeterministicData(StringBuilder builder)
        {
            ContentHashUtility.AppendFloat(builder, BaseMaxHealth);
            ContentHashUtility.AppendFloat(builder, CollisionRadius);
            if (!HasM5Data) return;

            ContentHashUtility.AppendInt(builder, 1);
            ContentHashUtility.AppendFloat(builder, BaseMoveSpeed);
            ContentHashUtility.AppendFloat(builder, BaseDamage);
            ContentHashUtility.AppendFloat(builder, AttackRange);
            ContentHashUtility.AppendToken(builder, AttackSkillId.Value);
            ContentHashUtility.AppendFloat(builder, ExperienceReward);
            ContentHashUtility.AppendFloat(builder, LootReward);
            ContentHashUtility.AppendToken(builder, VisualProfileId.Value);
            Behavior.AppendDeterministicData(builder);
        }
    }

    /// <summary>
    /// Runtime map metadata. M1 does not instantiate scenes or encounters.
    /// </summary>
    public sealed class RuntimeMapDefinition : RuntimeContentDefinition
    {
        private readonly RuntimeMapObstacle[] obstacles;
        private readonly RuntimeMapAnchor[] anchors;
        private readonly IReadOnlyList<RuntimeMapObstacle> obstaclesView;
        private readonly IReadOnlyList<RuntimeMapAnchor> anchorsView;

        /// <summary>
        /// Initializes pure runtime map metadata.
        /// </summary>
        public RuntimeMapDefinition(
            ContentId id,
            string localizedNameKey,
            string localizedDescriptionKey,
            string sourceAssetPath,
            ContentTag[] tags,
            string runtimeProviderId,
            string sceneAddress)
            : base(
                id,
                localizedNameKey,
                localizedDescriptionKey,
                sourceAssetPath,
                tags,
                Array.Empty<ContentId>())
        {
            RuntimeProviderId = runtimeProviderId ?? string.Empty;
            SceneAddress = sceneAddress ?? string.Empty;
            HasM5Data = false;
            obstacles = Array.Empty<RuntimeMapObstacle>();
            anchors = Array.Empty<RuntimeMapAnchor>();
            obstaclesView = Array.AsReadOnly(obstacles);
            anchorsView = Array.AsReadOnly(anchors);
        }

        /// <summary>Initializes a schema-4 pure map-runtime definition.</summary>
        public RuntimeMapDefinition(
            ContentId id,
            string localizedNameKey,
            string localizedDescriptionKey,
            string sourceAssetPath,
            ContentTag[] tags,
            string runtimeProviderId,
            string sceneAddress,
            MapBoundsMode boundsMode,
            Vector2 minimum,
            Vector2 maximum,
            float chunkSize,
            int activeChunkRadius,
            ContentId encounterScheduleId,
            ContentId visualProfileId,
            RuntimeMapObstacle[] mapObstacles,
            RuntimeMapAnchor[] mapAnchors)
            : base(
                id,
                localizedNameKey,
                localizedDescriptionKey,
                sourceAssetPath,
                tags,
                encounterScheduleId.IsValid
                    ? new[] { encounterScheduleId }
                    : Array.Empty<ContentId>())
        {
            RuntimeProviderId = runtimeProviderId ?? string.Empty;
            SceneAddress = sceneAddress ?? string.Empty;
            BoundsMode = boundsMode;
            Minimum = minimum;
            Maximum = maximum;
            ChunkSize = chunkSize;
            ActiveChunkRadius = activeChunkRadius;
            EncounterScheduleId = encounterScheduleId;
            VisualProfileId = visualProfileId;
            obstacles = mapObstacles == null
                ? Array.Empty<RuntimeMapObstacle>()
                : (RuntimeMapObstacle[])mapObstacles.Clone();
            anchors = mapAnchors == null
                ? Array.Empty<RuntimeMapAnchor>()
                : (RuntimeMapAnchor[])mapAnchors.Clone();
            obstaclesView = Array.AsReadOnly(obstacles);
            anchorsView = Array.AsReadOnly(anchors);
            HasM5Data = true;
        }

        /// <inheritdoc />
        public override string Kind => RuntimeContentKinds.Map;

        /// <summary>Gets the explicitly registered runtime provider ID.</summary>
        public string RuntimeProviderId { get; }

        /// <summary>Gets the scene address consumed by a later map loader.</summary>
        public string SceneAddress { get; }

        public bool HasM5Data { get; }
        public MapBoundsMode BoundsMode { get; }
        public Vector2 Minimum { get; }
        public Vector2 Maximum { get; }
        public float ChunkSize { get; }
        public int ActiveChunkRadius { get; }
        public ContentId EncounterScheduleId { get; }
        public ContentId VisualProfileId { get; }
        public IReadOnlyList<RuntimeMapObstacle> Obstacles => obstaclesView;
        public IReadOnlyList<RuntimeMapAnchor> Anchors => anchorsView;

        protected override void AppendTypeSpecificDeterministicData(StringBuilder builder)
        {
            ContentHashUtility.AppendToken(builder, RuntimeProviderId);
            ContentHashUtility.AppendToken(builder, SceneAddress);
            if (!HasM5Data) return;

            ContentHashUtility.AppendInt(builder, 1);
            ContentHashUtility.AppendInt(builder, (int)BoundsMode);
            ContentHashUtility.AppendFloat(builder, Minimum.X);
            ContentHashUtility.AppendFloat(builder, Minimum.Y);
            ContentHashUtility.AppendFloat(builder, Maximum.X);
            ContentHashUtility.AppendFloat(builder, Maximum.Y);
            ContentHashUtility.AppendFloat(builder, ChunkSize);
            ContentHashUtility.AppendInt(builder, ActiveChunkRadius);
            ContentHashUtility.AppendToken(builder, EncounterScheduleId.Value);
            ContentHashUtility.AppendToken(builder, VisualProfileId.Value);
            ContentHashUtility.AppendInt(builder, obstacles.Length);
            for (var index = 0; index < obstacles.Length; index++)
            {
                ContentHashUtility.AppendFloat(builder, obstacles[index].Minimum.X);
                ContentHashUtility.AppendFloat(builder, obstacles[index].Minimum.Y);
                ContentHashUtility.AppendFloat(builder, obstacles[index].Maximum.X);
                ContentHashUtility.AppendFloat(builder, obstacles[index].Maximum.Y);
            }

            ContentHashUtility.AppendInt(builder, anchors.Length);
            for (var index = 0; index < anchors.Length; index++)
            {
                ContentHashUtility.AppendToken(builder, anchors[index].Id.Value);
                ContentHashUtility.AppendFloat(builder, anchors[index].Position.X);
                ContentHashUtility.AppendFloat(builder, anchors[index].Position.Y);
            }
        }
    }
}
