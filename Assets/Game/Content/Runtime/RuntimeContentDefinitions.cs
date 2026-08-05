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

        /// <summary>Identifies an M6 passive definition.</summary>
        public const string Passive = "passive";

        /// <summary>Identifies an M6 trait definition.</summary>
        public const string Trait = "trait";

        /// <summary>Identifies an M6 weighted upgrade-offer definition.</summary>
        public const string Offer = "offer";

        /// <summary>Identifies an M6 build synergy definition.</summary>
        public const string Synergy = "synergy";

        /// <summary>Identifies an M6 skill evolution definition.</summary>
        public const string Evolution = "evolution";

        public const string CharacterMechanic = "character_mechanic";
        public const string Reward = "reward";
        public const string Pickup = "pickup";
        public const string Relic = "relic";
        public const string MapObjective = "map_objective";
        public const string MapEvent = "map_event";
        public const string Landmark = "landmark";
        public const string Boss = "boss";
        public const string EliteAffix = "elite_affix";
        public const string MetaNode = "meta_node";
        public const string MetaInsert = "meta_insert";
        public const string MetaFacility = "meta_facility";
        public const string Story = "story";
        public const string Collectible = "collectible";
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
        private readonly ContentId[] mechanicIds;
        private readonly IReadOnlyList<ContentId> mechanicIdsView;

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
            : this(
                id,
                localizedNameKey,
                localizedDescriptionKey,
                sourceAssetPath,
                tags,
                baseMaxHealth,
                moveSpeed,
                startingSkillIds,
                Array.Empty<ContentId>())
        {
        }

        /// <summary>Initializes schema-6 character data with generic mechanic references.</summary>
        public RuntimeCharacterDefinition(
            ContentId id,
            string localizedNameKey,
            string localizedDescriptionKey,
            string sourceAssetPath,
            ContentTag[] tags,
            float baseMaxHealth,
            float moveSpeed,
            ContentId[] startingSkillIds,
            ContentId[] characterMechanicIds)
            : base(
                id,
                localizedNameKey,
                localizedDescriptionKey,
                sourceAssetPath,
                tags,
                Combine(startingSkillIds, characterMechanicIds))
        {
            BaseMaxHealth = baseMaxHealth;
            MoveSpeed = moveSpeed;
            this.startingSkillIds = startingSkillIds == null
                ? Array.Empty<ContentId>()
                : (ContentId[])startingSkillIds.Clone();
            startingSkillIdsView = Array.AsReadOnly(this.startingSkillIds);
            mechanicIds = characterMechanicIds == null
                ? Array.Empty<ContentId>()
                : (ContentId[])characterMechanicIds.Clone();
            mechanicIdsView = Array.AsReadOnly(mechanicIds);
        }

        /// <inheritdoc />
        public override string Kind => RuntimeContentKinds.Character;

        /// <summary>Gets the base maximum health.</summary>
        public float BaseMaxHealth { get; }

        /// <summary>Gets the movement speed.</summary>
        public float MoveSpeed { get; }

        /// <summary>Gets starting skill IDs in author order.</summary>
        public IReadOnlyList<ContentId> StartingSkillIds => startingSkillIdsView;

        /// <summary>Gets schema-6 character-mechanic IDs in author order.</summary>
        public IReadOnlyList<ContentId> MechanicIds => mechanicIdsView;

        protected override void AppendTypeSpecificDeterministicData(StringBuilder builder)
        {
            ContentHashUtility.AppendFloat(builder, BaseMaxHealth);
            ContentHashUtility.AppendFloat(builder, MoveSpeed);
            ContentHashUtility.AppendInt(builder, startingSkillIds.Length);
            for (var index = 0; index < startingSkillIds.Length; index++)
            {
                ContentHashUtility.AppendToken(builder, startingSkillIds[index].Value);
            }

            if (mechanicIds.Length > 0)
            {
                ContentHashUtility.AppendInt(builder, mechanicIds.Length);
                for (var index = 0; index < mechanicIds.Length; index++)
                    ContentHashUtility.AppendToken(builder, mechanicIds[index].Value);
            }
        }

        private static ContentId[] Combine(ContentId[] first, ContentId[] second)
        {
            var firstLength = first == null ? 0 : first.Length;
            var secondLength = second == null ? 0 : second.Length;
            if (firstLength + secondLength == 0) return Array.Empty<ContentId>();
            var result = new ContentId[firstLength + secondLength];
            if (firstLength > 0) Array.Copy(first, result, firstLength);
            if (secondLength > 0) Array.Copy(second, 0, result, firstLength, secondLength);
            return result;
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
        private readonly ContentId[] objectiveIds;
        private readonly ContentId[] eventIds;
        private readonly ContentId[] landmarkIds;
        private readonly IReadOnlyList<ContentId> objectiveIdsView;
        private readonly IReadOnlyList<ContentId> eventIdsView;
        private readonly IReadOnlyList<ContentId> landmarkIdsView;

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
            objectiveIds = Array.Empty<ContentId>();
            eventIds = Array.Empty<ContentId>();
            landmarkIds = Array.Empty<ContentId>();
            objectiveIdsView = Array.AsReadOnly(objectiveIds);
            eventIdsView = Array.AsReadOnly(eventIds);
            landmarkIdsView = Array.AsReadOnly(landmarkIds);
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
            : this(
                id, localizedNameKey, localizedDescriptionKey, sourceAssetPath, tags,
                runtimeProviderId, sceneAddress, boundsMode, minimum, maximum, chunkSize,
                activeChunkRadius, encounterScheduleId, visualProfileId, mapObstacles, mapAnchors,
                Array.Empty<ContentId>(), Array.Empty<ContentId>(), Array.Empty<ContentId>())
        {
        }

        /// <summary>Initializes schema-6 map data with objective, event, and landmark references.</summary>
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
            RuntimeMapAnchor[] mapAnchors,
            ContentId[] mapObjectiveIds,
            ContentId[] mapEventIds,
            ContentId[] mapLandmarkIds)
            : base(
                id,
                localizedNameKey,
                localizedDescriptionKey,
                sourceAssetPath,
                tags,
                CombineMapReferences(
                    encounterScheduleId,
                    mapObjectiveIds,
                    mapEventIds,
                    mapLandmarkIds))
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
            objectiveIds = Clone(mapObjectiveIds);
            eventIds = Clone(mapEventIds);
            landmarkIds = Clone(mapLandmarkIds);
            objectiveIdsView = Array.AsReadOnly(objectiveIds);
            eventIdsView = Array.AsReadOnly(eventIds);
            landmarkIdsView = Array.AsReadOnly(landmarkIds);
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
        public IReadOnlyList<ContentId> ObjectiveIds => objectiveIdsView;
        public IReadOnlyList<ContentId> EventIds => eventIdsView;
        public IReadOnlyList<ContentId> LandmarkIds => landmarkIdsView;

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

            if (objectiveIds.Length + eventIds.Length + landmarkIds.Length > 0)
            {
                AppendIds(builder, objectiveIds);
                AppendIds(builder, eventIds);
                AppendIds(builder, landmarkIds);
            }
        }

        private static ContentId[] Clone(ContentId[] source) =>
            source == null ? Array.Empty<ContentId>() : (ContentId[])source.Clone();

        private static ContentId[] CombineMapReferences(
            ContentId encounter,
            ContentId[] objectives,
            ContentId[] events,
            ContentId[] landmarks)
        {
            var count = (encounter.IsValid ? 1 : 0) +
                        (objectives == null ? 0 : objectives.Length) +
                        (events == null ? 0 : events.Length) +
                        (landmarks == null ? 0 : landmarks.Length);
            if (count == 0) return Array.Empty<ContentId>();
            var result = new ContentId[count];
            var write = 0;
            if (encounter.IsValid) result[write++] = encounter;
            if (objectives != null) { Array.Copy(objectives, 0, result, write, objectives.Length); write += objectives.Length; }
            if (events != null) { Array.Copy(events, 0, result, write, events.Length); write += events.Length; }
            if (landmarks != null) Array.Copy(landmarks, 0, result, write, landmarks.Length);
            return result;
        }

        private static void AppendIds(StringBuilder builder, ContentId[] values)
        {
            ContentHashUtility.AppendInt(builder, values.Length);
            for (var index = 0; index < values.Length; index++)
                ContentHashUtility.AppendToken(builder, values[index].Value);
        }
    }
}
