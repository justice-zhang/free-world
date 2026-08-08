using System;
using System.Collections.Generic;
using Game.Application;
using Game.Core;
using Game.Simulation;
using UnityEngine;

namespace Game.Presentation
{
    /// <summary>Presentation-only degradation order. Lower numeric values are more important.</summary>
    public enum PresentationPriority : byte
    {
        CriticalDanger = 0,
        Mechanic = 1,
        Combat = 2,
        Decoration = 3
    }

    /// <summary>Procedural silhouettes available without imported bitmap assets.</summary>
    public enum ProceduralShape : byte
    {
        Square = 0,
        Circle = 1,
        Diamond = 2,
        Triangle = 3,
        Ring = 4,
        Cross = 5,
        Chevron = 6,
        Hexagon = 7,
        Line = 8
    }

    /// <summary>Semantic generated-tone cue; it is not an AudioClip or gameplay state.</summary>
    public enum PresentationAudioCue : byte
    {
        None = 0,
        Hit = 1,
        Death = 2,
        Pickup = 3,
        MechanicRise = 4,
        Objective = 5,
        Danger = 6,
        BossPhase = 7,
        Confirm = 8
    }

    /// <summary>Pure presentation values resolved from an authored stable profile identity.</summary>
    public readonly struct ProceduralPresentationStyle
    {
        public ProceduralPresentationStyle(
            ProceduralShape shape,
            Color color,
            Color outlineColor,
            Vector2 size,
            PresentationPriority priority,
            PresentationAudioCue audioCue,
            bool hostile,
            bool directional = false)
        {
            Shape = shape;
            Color = color;
            OutlineColor = outlineColor;
            Size = new Vector2(Mathf.Max(0.05f, size.x), Mathf.Max(0.05f, size.y));
            Priority = priority;
            AudioCue = audioCue;
            Hostile = hostile;
            Directional = directional;
        }

        public ProceduralShape Shape { get; }
        public Color Color { get; }
        public Color OutlineColor { get; }
        public Vector2 Size { get; }
        public PresentationPriority Priority { get; }
        public PresentationAudioCue AudioCue { get; }
        public bool Hostile { get; }
        public bool Directional { get; }

        public ProceduralPresentationStyle WithColor(Color color, Color outline) =>
            new ProceduralPresentationStyle(
                Shape,
                color,
                outline,
                Size,
                Priority,
                AudioCue,
                Hostile,
                Directional);
    }

    /// <summary>
    /// Low-frequency catalog of generated Placeholder profiles. It is populated by
    /// Infrastructure from content tags and never consulted by fixed simulation Tick.
    /// </summary>
    public sealed class ProceduralPresentationCatalog
    {
        private readonly Dictionary<ContentId, ProceduralPresentationStyle> styles;

        public ProceduralPresentationCatalog(int capacity = 64)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            styles = new Dictionary<ContentId, ProceduralPresentationStyle>(capacity);
        }

        public int Count => styles.Count;

        public void Register(ContentId profileId, in ProceduralPresentationStyle style)
        {
            if (!profileId.IsValid) throw new ArgumentException("Profile ID must be valid.", nameof(profileId));
            styles[profileId] = style;
        }

        public bool TryResolve(
            ContentId profileId,
            EntityKind kind,
            bool isPlayer,
            ColorVisionMode colorVision,
            out ProceduralPresentationStyle style)
        {
            if (!profileId.IsValid || !styles.TryGetValue(profileId, out style))
            {
                style = Fallback(kind, isPlayer);
                style = ApplyColorVision(style, colorVision);
                return false;
            }

            style = ApplyColorVision(style, colorVision);
            return true;
        }

        public bool TryResolveEffect(
            ContentId sourceId,
            ColorVisionMode colorVision,
            out ProceduralPresentationStyle style)
        {
            if (sourceId.IsValid && styles.TryGetValue(sourceId, out style))
            {
                style = ApplyColorVision(style, colorVision);
                return true;
            }

            style = ApplyColorVision(Fallback(EntityKind.Actor, false), colorVision);
            return false;
        }

        public static ProceduralPresentationStyle Fallback(EntityKind kind, bool isPlayer)
        {
            if (isPlayer)
                return new ProceduralPresentationStyle(
                    ProceduralShape.Triangle,
                    new Color(0.35f, 0.78f, 0.76f, 1f),
                    new Color(0.96f, 0.94f, 0.84f, 1f),
                    new Vector2(1.05f, 1.2f),
                    PresentationPriority.Mechanic,
                    PresentationAudioCue.None,
                    false,
                    true);

            switch (kind)
            {
                case EntityKind.Actor:
                    return new ProceduralPresentationStyle(
                        ProceduralShape.Circle, new Color(0.38f, 0.48f, 0.32f, 1f),
                        new Color(0.24f, 0.2f, 0.17f, 1f), Vector2.one,
                        PresentationPriority.Combat, PresentationAudioCue.Hit, true);
                case EntityKind.Projectile:
                    return new ProceduralPresentationStyle(
                        ProceduralShape.Diamond, new Color(0.95f, 0.8f, 0.3f, 1f),
                        new Color(0.25f, 0.2f, 0.12f, 1f), Vector2.one * 0.38f,
                        PresentationPriority.Combat, PresentationAudioCue.Hit, false, true);
                case EntityKind.Area:
                    return new ProceduralPresentationStyle(
                        ProceduralShape.Ring, new Color(0.85f, 0.35f, 0.24f, 0.55f),
                        new Color(0.25f, 0.08f, 0.04f, 0.9f), Vector2.one * 2f,
                        PresentationPriority.CriticalDanger, PresentationAudioCue.Danger, true);
                case EntityKind.Pickup:
                    return new ProceduralPresentationStyle(
                        ProceduralShape.Cross, new Color(0.84f, 0.72f, 0.36f, 1f),
                        new Color(1f, 0.95f, 0.72f, 1f), Vector2.one * 0.5f,
                        PresentationPriority.Mechanic, PresentationAudioCue.Pickup, false);
                default:
                    return new ProceduralPresentationStyle(
                        ProceduralShape.Square, Color.magenta, Color.black, Vector2.one,
                        PresentationPriority.Decoration, PresentationAudioCue.None, false);
            }
        }

        public static ProceduralPresentationStyle ApplyColorVision(
            in ProceduralPresentationStyle source,
            ColorVisionMode mode)
        {
            if (mode == ColorVisionMode.Standard) return source;
            Color color;
            Color outline;
            if (mode == ColorVisionMode.HighContrast)
            {
                color = source.Hostile ? new Color(1f, 0.82f, 0.12f, source.Color.a) :
                    new Color(0.1f, 0.86f, 1f, source.Color.a);
                outline = source.Hostile ? Color.black : Color.white;
            }
            else
            {
                color = Transform(source.Color, mode);
                outline = Transform(source.OutlineColor, mode);
            }
            return source.WithColor(color, outline);
        }

        private static Color Transform(Color value, ColorVisionMode mode)
        {
            var r = value.r;
            var g = value.g;
            var b = value.b;
            if (mode == ColorVisionMode.Protanopia)
                return new Color((0.57f * r) + (0.43f * g), (0.56f * r) + (0.44f * g), b, value.a);
            if (mode == ColorVisionMode.Deuteranopia)
                return new Color((0.63f * r) + (0.37f * g), (0.70f * r) + (0.30f * g), b, value.a);
            return new Color(r, (0.43f * g) + (0.57f * b), (0.48f * g) + (0.52f * b), value.a);
        }
    }

    /// <summary>One bounded, priority-aware transient visual request.</summary>
    public readonly struct ProceduralVfxRequest
    {
        public ProceduralVfxRequest(
            Vector2 position,
            in ProceduralPresentationStyle style,
            float size,
            float duration,
            float rotationDegrees = 0f)
        {
            Position = position;
            Style = style;
            Size = Mathf.Max(0.05f, size);
            Duration = Mathf.Max(0.01f, duration);
            RotationDegrees = rotationDegrees;
        }

        public Vector2 Position { get; }
        public ProceduralPresentationStyle Style { get; }
        public float Size { get; }
        public float Duration { get; }
        public float RotationDegrees { get; }
    }
}
