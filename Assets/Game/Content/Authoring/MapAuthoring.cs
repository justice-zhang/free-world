using System;
using Game.Content.Runtime;
using Game.Core;
using UnityEngine;

namespace Game.Content.Authoring
{
    [Serializable]
    public sealed class MapObstacleAuthoringData
    {
        public Vector2 minimum;
        public Vector2 maximum;
    }

    [Serializable]
    public sealed class MapAnchorAuthoringData
    {
        public string id = string.Empty;
        public Vector2 position;
    }

    /// <summary>
    /// Minimal M1 map authoring metadata. No map runtime is instantiated.
    /// </summary>
    [CreateAssetMenu(menuName = "Free World/Content/Map", fileName = "Map")]
    public sealed class MapAuthoring : ContentAuthoringBase
    {
        [SerializeField] private string runtimeProviderId = string.Empty;
        [SerializeField] private string sceneAddress = string.Empty;
        [SerializeField] private bool m5RuntimeEnabled;
        [SerializeField] private MapBoundsMode boundsMode = MapBoundsMode.Finite;
        [SerializeField] private Vector2 minimum = new Vector2(-24f, -14f);
        [SerializeField] private Vector2 maximum = new Vector2(24f, 14f);
        [SerializeField] private float chunkSize = 16f;
        [SerializeField] private int activeChunkRadius = 2;
        [SerializeField] private EncounterScheduleAuthoring encounterSchedule;
        [SerializeField] private string visualProfileId = string.Empty;
        [SerializeField] private MapObstacleAuthoringData[] obstacles = Array.Empty<MapObstacleAuthoringData>();
        [SerializeField] private MapAnchorAuthoringData[] anchors = Array.Empty<MapAnchorAuthoringData>();
        [SerializeField] private QinglanDefinitionAuthoring[] objectives = Array.Empty<QinglanDefinitionAuthoring>();
        [SerializeField] private QinglanDefinitionAuthoring[] events = Array.Empty<QinglanDefinitionAuthoring>();
        [SerializeField] private QinglanDefinitionAuthoring[] landmarks = Array.Empty<QinglanDefinitionAuthoring>();

        public bool M5RuntimeEnabled => m5RuntimeEnabled;

        /// <summary>
        /// Configures the deferred runtime provider and scene address.
        /// </summary>
        public void Configure(string providerId, string address)
        {
            runtimeProviderId = providerId ?? string.Empty;
            sceneAddress = address ?? string.Empty;
            m5RuntimeEnabled = false;
        }

        /// <summary>Configures a schema-4 finite or deterministic chunked map.</summary>
        public void ConfigureM5(
            string providerId,
            string address,
            MapBoundsMode mode,
            Vector2 mapMinimum,
            Vector2 mapMaximum,
            float mapChunkSize,
            int chunkRadius,
            EncounterScheduleAuthoring encounter,
            string visualId,
            MapObstacleAuthoringData[] mapObstacles,
            MapAnchorAuthoringData[] mapAnchors)
        {
            runtimeProviderId = providerId ?? string.Empty;
            sceneAddress = address ?? string.Empty;
            boundsMode = mode;
            minimum = mapMinimum;
            maximum = mapMaximum;
            chunkSize = mapChunkSize;
            activeChunkRadius = chunkRadius;
            encounterSchedule = encounter;
            visualProfileId = visualId ?? string.Empty;
            obstacles = mapObstacles == null ? Array.Empty<MapObstacleAuthoringData>() : (MapObstacleAuthoringData[])mapObstacles.Clone();
            anchors = mapAnchors == null ? Array.Empty<MapAnchorAuthoringData>() : (MapAnchorAuthoringData[])mapAnchors.Clone();
            m5RuntimeEnabled = true;
        }

        /// <summary>Configures schema-6 map objective, event, and landmark references.</summary>
        public void ConfigureQinglanReferences(
            QinglanDefinitionAuthoring[] mapObjectives,
            QinglanDefinitionAuthoring[] mapEvents,
            QinglanDefinitionAuthoring[] mapLandmarks)
        {
            objectives = Copy(mapObjectives);
            events = Copy(mapEvents);
            landmarks = Copy(mapLandmarks);
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
            if (string.IsNullOrWhiteSpace(runtimeProviderId) ||
                string.IsNullOrWhiteSpace(sceneAddress))
            {
                return Result<RuntimeContentDefinition>.Failure(
                    new Error(
                        ErrorCode.InvalidAuthoringData,
                        "Map runtime provider ID and scene address are required.",
                        common.Id,
                        packId,
                        authorAssetPath));
            }

            if (!m5RuntimeEnabled)
            {
                return Result<RuntimeContentDefinition>.Success(
                    new RuntimeMapDefinition(
                        common.Id,
                        common.LocalizedNameKey,
                        common.LocalizedDescriptionKey,
                        common.AuthorAssetPath,
                        common.Tags,
                        runtimeProviderId,
                        sceneAddress));
            }

            if (!Enum.IsDefined(typeof(MapBoundsMode), boundsMode) ||
                !IsFinite(minimum) || !IsFinite(maximum) ||
                minimum.x >= maximum.x || minimum.y >= maximum.y ||
                !IsFinitePositive(chunkSize) || activeChunkRadius < 1 ||
                encounterSchedule == null)
            {
                return Failure("M5 map bounds, chunk settings, or encounter reference are invalid.", common, packId);
            }

            var encounterId = ContentId.Create(encounterSchedule.ContentIdText, packId, authorAssetPath);
            if (!encounterId.IsSuccess) return Result<RuntimeContentDefinition>.Failure(encounterId.Error);
            var visualId = ContentId.Create(visualProfileId, packId, authorAssetPath);
            if (!visualId.IsSuccess) return Result<RuntimeContentDefinition>.Failure(visualId.Error);

            var runtimeObstacles = new RuntimeMapObstacle[obstacles == null ? 0 : obstacles.Length];
            for (var index = 0; index < runtimeObstacles.Length; index++)
            {
                var source = obstacles[index];
                if (source == null || !IsFinite(source.minimum) || !IsFinite(source.maximum) ||
                    source.minimum.x >= source.maximum.x || source.minimum.y >= source.maximum.y)
                {
                    return Failure("M5 map obstacle " + index + " has invalid bounds.", common, packId);
                }

                runtimeObstacles[index] = new RuntimeMapObstacle(
                    new System.Numerics.Vector2(source.minimum.x, source.minimum.y),
                    new System.Numerics.Vector2(source.maximum.x, source.maximum.y));
            }

            var runtimeAnchors = new RuntimeMapAnchor[anchors == null ? 0 : anchors.Length];
            for (var index = 0; index < runtimeAnchors.Length; index++)
            {
                var source = anchors[index];
                if (source == null || !IsFinite(source.position))
                {
                    return Failure("M5 map anchor " + index + " is invalid.", common, packId);
                }

                var idResult = ContentId.Create(source.id, packId, authorAssetPath);
                if (!idResult.IsSuccess) return Result<RuntimeContentDefinition>.Failure(idResult.Error);
                runtimeAnchors[index] = new RuntimeMapAnchor(
                    idResult.Value,
                    new System.Numerics.Vector2(source.position.x, source.position.y));
            }


            var objectiveIds = ParseQinglanIds(
                objectives,
                RuntimeContentKinds.MapObjective,
                "objective",
                common,
                packId);
            if (!objectiveIds.IsSuccess) return Result<RuntimeContentDefinition>.Failure(objectiveIds.Error);
            var eventIds = ParseQinglanIds(
                events,
                RuntimeContentKinds.MapEvent,
                "event",
                common,
                packId);
            if (!eventIds.IsSuccess) return Result<RuntimeContentDefinition>.Failure(eventIds.Error);
            var landmarkIds = ParseQinglanIds(
                landmarks,
                RuntimeContentKinds.Landmark,
                "landmark",
                common,
                packId);
            if (!landmarkIds.IsSuccess) return Result<RuntimeContentDefinition>.Failure(landmarkIds.Error);

            return Result<RuntimeContentDefinition>.Success(
                new RuntimeMapDefinition(
                    common.Id,
                    common.LocalizedNameKey,
                    common.LocalizedDescriptionKey,
                    common.AuthorAssetPath,
                    common.Tags,
                    runtimeProviderId,
                    sceneAddress,
                    boundsMode,
                    new System.Numerics.Vector2(minimum.x, minimum.y),
                    new System.Numerics.Vector2(maximum.x, maximum.y),
                    chunkSize,
                    activeChunkRadius,
                    encounterId.Value,
                    visualId.Value,
                    runtimeObstacles,
                    runtimeAnchors,
                    objectiveIds.Value,
                    eventIds.Value,
                    landmarkIds.Value));
        }

        private static QinglanDefinitionAuthoring[] Copy(QinglanDefinitionAuthoring[] source) =>
            source == null ? Array.Empty<QinglanDefinitionAuthoring>() : (QinglanDefinitionAuthoring[])source.Clone();

        private static Result<ContentId[]> ParseQinglanIds(
            QinglanDefinitionAuthoring[] source,
            string expectedKind,
            string label,
            AuthoringCommonData common,
            ContentId packId)
        {
            source = source ?? Array.Empty<QinglanDefinitionAuthoring>();
            var result = new ContentId[source.Length];
            for (var index = 0; index < result.Length; index++)
            {
                if (source[index] == null || source[index].RuntimeKind != expectedKind)
                    return Result<ContentId[]>.Failure(
                        new Error(
                            ErrorCode.MissingReference,
                            "Map " + label + " reference is null or has the wrong kind at index " + index + ".",
                            common.Id,
                            packId,
                            common.AuthorAssetPath));
                var id = ContentId.Create(source[index].ContentIdText, packId, common.AuthorAssetPath);
                if (!id.IsSuccess) return Result<ContentId[]>.Failure(id.Error);
                result[index] = id.Value;
            }
            return Result<ContentId[]>.Success(ContentBaker.CanonicalizeSet(result));
        }

        private static bool IsFinite(Vector2 value) =>
            !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
            !float.IsNaN(value.y) && !float.IsInfinity(value.y);

        private static bool IsFinitePositive(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;

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
