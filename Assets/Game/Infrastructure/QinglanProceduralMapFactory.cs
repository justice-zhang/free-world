using System;
using System.Collections.Generic;
using Game.Content.Runtime;
using Game.Core;
using Game.Presentation;
using UnityEngine;

namespace Game.Infrastructure
{
    /// <summary>Maps content-owned pure map metadata into a presentation-only DTO.</summary>
    public static class QinglanProceduralMapFactory
    {
        public static ProceduralMapConfiguration Build(ContentRegistry registry, ContentId mapId)
        {
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            if (!mapId.IsValid || !registry.TryGet(mapId, out RuntimeMapDefinition map) || !map.HasM5Data)
                return null;

            var obstacles = new ProceduralMapObstacle[map.Obstacles.Count];
            for (var index = 0; index < obstacles.Length; index++)
            {
                var item = map.Obstacles[index];
                obstacles[index] = new ProceduralMapObstacle(ToUnity(item.Minimum), ToUnity(item.Maximum));
            }

            var zoneCount = Math.Min(5, map.Anchors.Count);
            var zones = new Vector2[zoneCount];
            for (var index = 0; index < zoneCount; index++) zones[index] = ToUnity(map.Anchors[index].Position);

            var markers = new List<ProceduralMapMarker>(
                map.ObjectiveIds.Count + map.EventIds.Count + map.LandmarkIds.Count);
            AddMarkers(markers, map.ObjectiveIds, map.Anchors, 1, 0);
            AddMarkers(markers, map.EventIds, map.Anchors, 2, zoneCount);
            AddMarkers(markers, map.LandmarkIds, map.Anchors, 3, zoneCount + map.ObjectiveIds.Count);
            return new ProceduralMapConfiguration(
                ToUnity(map.Minimum),
                ToUnity(map.Maximum),
                map.ChunkSize,
                obstacles,
                zones,
                markers.ToArray());
        }

        private static void AddMarkers(
            List<ProceduralMapMarker> target,
            IReadOnlyList<ContentId> ids,
            IReadOnlyList<RuntimeMapAnchor> anchors,
            byte kind,
            int fallbackOffset)
        {
            for (var index = 0; index < ids.Count; index++)
            {
                var position = ResolvePosition(ids[index], anchors, fallbackOffset + index);
                target.Add(new ProceduralMapMarker(ids[index], kind, position));
            }
        }

        private static Vector2 ResolvePosition(
            ContentId stateId,
            IReadOnlyList<RuntimeMapAnchor> anchors,
            int fallbackIndex)
        {
            var value = stateId.Value;
            var suffixIndex = value.LastIndexOf('.');
            var suffix = suffixIndex < 0 ? value : value.Substring(suffixIndex + 1);
            for (var index = 0; index < anchors.Count; index++)
                if (anchors[index].Id.Value.IndexOf(suffix, StringComparison.Ordinal) >= 0)
                    return ToUnity(anchors[index].Position);
            return anchors.Count == 0 ? Vector2.zero : ToUnity(anchors[fallbackIndex % anchors.Count].Position);
        }

        private static Vector2 ToUnity(System.Numerics.Vector2 value) => new Vector2(value.X, value.Y);
    }
}
