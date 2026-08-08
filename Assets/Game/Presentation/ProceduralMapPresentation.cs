using System;
using System.Collections.Generic;
using Game.Application;
using Game.Core;
using UnityEngine;

namespace Game.Presentation
{
    public readonly struct ProceduralMapObstacle
    {
        public ProceduralMapObstacle(Vector2 minimum, Vector2 maximum)
        {
            Minimum = minimum;
            Maximum = maximum;
        }

        public Vector2 Minimum { get; }
        public Vector2 Maximum { get; }
    }

    public readonly struct ProceduralMapMarker
    {
        public ProceduralMapMarker(ContentId stateId, byte kind, Vector2 position)
        {
            StateId = stateId;
            Kind = kind;
            Position = position;
        }

        public ContentId StateId { get; }
        public byte Kind { get; }
        public Vector2 Position { get; }
    }

    /// <summary>Pure DTO built outside Presentation from a runtime map definition.</summary>
    public sealed class ProceduralMapConfiguration
    {
        public ProceduralMapConfiguration(
            Vector2 minimum,
            Vector2 maximum,
            float chunkSize,
            ProceduralMapObstacle[] obstacles,
            Vector2[] zones,
            ProceduralMapMarker[] markers)
        {
            Minimum = minimum;
            Maximum = maximum;
            ChunkSize = Mathf.Max(1f, chunkSize);
            Obstacles = obstacles == null ? Array.Empty<ProceduralMapObstacle>() :
                (ProceduralMapObstacle[])obstacles.Clone();
            Zones = zones == null ? Array.Empty<Vector2>() : (Vector2[])zones.Clone();
            Markers = markers == null ? Array.Empty<ProceduralMapMarker>() :
                (ProceduralMapMarker[])markers.Clone();
        }

        public Vector2 Minimum { get; }
        public Vector2 Maximum { get; }
        public float ChunkSize { get; }
        public IReadOnlyList<ProceduralMapObstacle> Obstacles { get; }
        public IReadOnlyList<Vector2> Zones { get; }
        public IReadOnlyList<ProceduralMapMarker> Markers { get; }
    }

    internal sealed class ProceduralMapMarkerView
    {
        public ProceduralMapMarker Definition;
        public SpriteRenderer Renderer;
        public Color BaseColor;
        public Vector3 BaseScale;
    }

    /// <summary>Fixed, programmatic map layer; it owns no gameplay or map state.</summary>
    public sealed class ProceduralMapPresentation : IDisposable
    {
        private readonly GameObject root;
        private readonly List<ProceduralMapMarkerView> markers;
        private readonly ProceduralPresentationCatalog profiles;
        private readonly ProceduralVisualLibrary library;
        private ColorVisionMode colorVision;
        private long lastSnapshotTick = long.MinValue;

        internal ProceduralMapPresentation(
            Transform owner,
            ProceduralMapConfiguration configuration,
            ProceduralPresentationCatalog profileCatalog,
            ProceduralVisualLibrary visualLibrary,
            ColorVisionMode initialColorVision)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));
            profiles = profileCatalog ?? throw new ArgumentNullException(nameof(profileCatalog));
            library = visualLibrary ?? throw new ArgumentNullException(nameof(visualLibrary));
            colorVision = initialColorVision;
            root = new GameObject("G2_7_ProceduralMap");
            root.transform.SetParent(owner, false);
            markers = new List<ProceduralMapMarkerView>(configuration.Markers.Count);
            BuildBounds(configuration);
            BuildZones(configuration);
            BuildObstacles(configuration);
            BuildMarkers(configuration);
        }

        public int MarkerCount => markers.Count;

        public void Sync(RunUiSnapshot snapshot, ColorVisionMode mode)
        {
            var styleChanged = false;
            if (mode != colorVision)
            {
                colorVision = mode;
                RefreshMarkerStyles();
                styleChanged = true;
            }
            if (snapshot == null) return;
            if (!styleChanged && snapshot.Tick == lastSnapshotTick) return;
            lastSnapshotTick = snapshot.Tick;
            for (var markerIndex = 0; markerIndex < markers.Count; markerIndex++)
            {
                var marker = markers[markerIndex];
                var found = false;
                for (var stateIndex = 0; stateIndex < snapshot.MapCount; stateIndex++)
                {
                    var state = snapshot.GetMapAt(stateIndex);
                    if (!string.Equals(state.ContentId, marker.Definition.StateId.Value, StringComparison.Ordinal))
                        continue;
                    found = true;
                    marker.Renderer.enabled = state.State != 1;
                    var completed = state.Progress >= 0.999f || state.State == 6;
                    var color = completed ? new Color(0.52f, 0.58f, 0.54f, 0.55f) : marker.BaseColor;
                    marker.Renderer.color = color;
                    marker.Renderer.transform.localScale = marker.BaseScale *
                        (completed ? 0.8f : 1f + (Mathf.Clamp01(state.Progress) * 0.25f));
                    break;
                }
                if (!found) marker.Renderer.enabled = false;
            }
        }

        public void Dispose() => UnityObjectLifetime.Destroy(root);

        private void BuildBounds(ProceduralMapConfiguration configuration)
        {
            var minimum = configuration.Minimum;
            var maximum = configuration.Maximum;
            var width = maximum.x - minimum.x;
            var height = maximum.y - minimum.y;
            var center = (minimum + maximum) * 0.5f;
            var color = new Color(0.68f, 0.58f, 0.42f, 0.75f);
            CreateRect("Boundary_North", new Vector2(center.x, maximum.y), new Vector2(width, 0.32f), color, -8);
            CreateRect("Boundary_South", new Vector2(center.x, minimum.y), new Vector2(width, 0.32f), color, -8);
            CreateRect("Boundary_West", new Vector2(minimum.x, center.y), new Vector2(0.32f, height), color, -8);
            CreateRect("Boundary_East", new Vector2(maximum.x, center.y), new Vector2(0.32f, height), color, -8);
        }

        private void BuildZones(ProceduralMapConfiguration configuration)
        {
            var size = Mathf.Max(6f, configuration.ChunkSize * 0.72f);
            for (var index = 0; index < configuration.Zones.Count; index++)
                CreateRect(
                    "Zone_" + index,
                    configuration.Zones[index],
                    Vector2.one * size,
                    new Color(0.18f, 0.36f, 0.32f, 0.055f),
                    -20);
        }

        private void BuildObstacles(ProceduralMapConfiguration configuration)
        {
            for (var index = 0; index < configuration.Obstacles.Count; index++)
            {
                var item = configuration.Obstacles[index];
                CreateRect(
                    "Obstacle_" + index,
                    (item.Minimum + item.Maximum) * 0.5f,
                    item.Maximum - item.Minimum,
                    new Color(0.25f, 0.22f, 0.17f, 0.88f),
                    -6);
            }
        }

        private void BuildMarkers(ProceduralMapConfiguration configuration)
        {
            for (var index = 0; index < configuration.Markers.Count; index++)
            {
                var definition = configuration.Markers[index];
                profiles.TryResolveEffect(definition.StateId, colorVision, out var style);
                var renderer = CreateRenderer("MapMarker_" + definition.Kind + "_" + index, -3);
                renderer.transform.position = new Vector3(definition.Position.x, definition.Position.y, 0.2f);
                renderer.sprite = library.GetSprite(ShapeFor(definition.Kind, style.Shape));
                renderer.color = style.Color;
                renderer.transform.localScale = Vector3.one * (definition.Kind == 1 ? 1.45f : 1.05f);
                renderer.enabled = false;
                markers.Add(new ProceduralMapMarkerView
                {
                    Definition = definition,
                    Renderer = renderer,
                    BaseColor = style.Color,
                    BaseScale = renderer.transform.localScale
                });
            }
        }

        private void RefreshMarkerStyles()
        {
            for (var index = 0; index < markers.Count; index++)
            {
                var marker = markers[index];
                profiles.TryResolveEffect(marker.Definition.StateId, colorVision, out var style);
                marker.BaseColor = style.Color;
                marker.Renderer.color = style.Color;
            }
        }

        private SpriteRenderer CreateRect(string name, Vector2 position, Vector2 size, Color color, int order)
        {
            var renderer = CreateRenderer(name, order);
            renderer.sprite = library.GetSprite(ProceduralShape.Square);
            renderer.color = color;
            renderer.transform.position = new Vector3(position.x, position.y, 0.5f);
            renderer.transform.localScale = new Vector3(Mathf.Max(0.05f, size.x), Mathf.Max(0.05f, size.y), 1f);
            return renderer;
        }

        private SpriteRenderer CreateRenderer(string name, int order)
        {
            var value = new GameObject(name);
            value.transform.SetParent(root.transform, false);
            var renderer = value.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = order;
            return renderer;
        }

        private static ProceduralShape ShapeFor(byte kind, ProceduralShape fallback)
        {
            if (kind == 1) return ProceduralShape.Ring;
            if (kind == 2) return ProceduralShape.Chevron;
            if (kind == 3) return ProceduralShape.Diamond;
            return fallback;
        }
    }
}
