using System;
using Game.Core;
using Game.Simulation;
using NumericsVector2 = System.Numerics.Vector2;

namespace Game.Presentation
{
    public enum PresentationRequestType : byte
    {
        Hit = 1,
        Death = 2,
        Status = 3
    }

    /// <summary>Presentation-only request translated from immutable simulation events.</summary>
    public readonly struct PresentationRequest
    {
        public PresentationRequest(
            PresentationRequestType type,
            SpatialEntity target,
            NumericsVector2 position,
            float magnitude,
            bool emphasized,
            ContentId contentId)
        {
            Type = type;
            Target = target;
            Position = position;
            Magnitude = magnitude;
            Emphasized = emphasized;
            ContentId = contentId;
        }

        public PresentationRequestType Type { get; }
        public SpatialEntity Target { get; }
        public NumericsVector2 Position { get; }
        public float Magnitude { get; }
        public bool Emphasized { get; }
        public ContentId ContentId { get; }
    }

    /// <summary>Reusable request storage; cleared only after the frame is routed.</summary>
    public sealed class PresentationRequestBuffer
    {
        private PresentationRequest[] requests;

        public PresentationRequestBuffer(int capacity = 32)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            requests = new PresentationRequest[capacity];
        }

        public int Count { get; private set; }

        public PresentationRequest GetAt(int index)
        {
            if (index < 0 || index >= Count) throw new ArgumentOutOfRangeException(nameof(index));
            return requests[index];
        }

        public void Clear()
        {
            Array.Clear(requests, 0, Count);
            Count = 0;
        }

        public void Add(in PresentationRequest request)
        {
            if (Count == requests.Length) Array.Resize(ref requests, requests.Length * 2);
            requests[Count++] = request;
        }
    }
}
