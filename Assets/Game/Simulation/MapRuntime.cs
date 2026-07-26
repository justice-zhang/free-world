using System;
using System.Numerics;
using Game.Content.Runtime;
using Game.Core;

namespace Game.Simulation
{
    /// <summary>Immutable runtime setup passed to a pure map provider.</summary>
    public readonly struct MapRuntimeContext
    {
        public MapRuntimeContext(RuntimeMapDefinition definition, ulong seed)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            Seed = seed;
        }

        public RuntimeMapDefinition Definition { get; }
        public ulong Seed { get; }
    }

    /// <summary>Small environment state consumed by diagnostics and presentation adapters.</summary>
    public readonly struct MapEnvironmentSnapshot
    {
        public MapEnvironmentSnapshot(
            ContentId mapId,
            MapBoundsMode boundsMode,
            Vector2 focus,
            int activeChunkCount,
            long focusChunkSignature)
        {
            MapId = mapId;
            BoundsMode = boundsMode;
            Focus = focus;
            ActiveChunkCount = activeChunkCount;
            FocusChunkSignature = focusChunkSignature;
        }

        public ContentId MapId { get; }
        public MapBoundsMode BoundsMode { get; }
        public Vector2 Focus { get; }
        public int ActiveChunkCount { get; }
        public long FocusChunkSignature { get; }
    }

    /// <summary>Frozen multipliers for one run; difficulty is not queried through global state.</summary>
    public readonly struct DifficultySnapshot
    {
        public DifficultySnapshot(
            float healthMultiplier,
            float damageMultiplier,
            float speedMultiplier,
            float spawnRateMultiplier,
            float eliteProbability,
            float rewardMultiplier)
        {
            if (!Positive(healthMultiplier) || !Positive(damageMultiplier) ||
                !Positive(speedMultiplier) || !Positive(spawnRateMultiplier) ||
                !Finite(eliteProbability) || eliteProbability < 0f || eliteProbability > 1f ||
                !Positive(rewardMultiplier))
            {
                throw new ArgumentOutOfRangeException(nameof(healthMultiplier));
            }

            HealthMultiplier = healthMultiplier;
            DamageMultiplier = damageMultiplier;
            SpeedMultiplier = speedMultiplier;
            SpawnRateMultiplier = spawnRateMultiplier;
            EliteProbability = eliteProbability;
            RewardMultiplier = rewardMultiplier;
        }

        public float HealthMultiplier { get; }
        public float DamageMultiplier { get; }
        public float SpeedMultiplier { get; }
        public float SpawnRateMultiplier { get; }
        public float EliteProbability { get; }
        public float RewardMultiplier { get; }

        public static DifficultySnapshot Default => new DifficultySnapshot(1f, 1f, 1f, 1f, 0f, 1f);

        private static bool Positive(float value) => Finite(value) && value > 0f;
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    /// <summary>Pure map queries used by movement and encounter simulation.</summary>
    public interface IMapRuntime
    {
        void Initialize(in MapRuntimeContext context);
        bool IsWalkable(Vector2 position);
        Vector2 SampleEnemySpawnPosition(
            Vector2 playerPosition,
            float minimumDistance,
            float maximumDistance,
            ref RandomStream random);
        Vector2 ResolveMovement(Vector2 currentPosition, Vector2 desiredPosition, float radius);
        bool TryGetAnchor(ContentId anchorId, out Vector2 position);
        void UpdateFocus(Vector2 focus);
        MapEnvironmentSnapshot GetEnvironmentSnapshot();
    }

    public abstract class MapRuntimeBase : IMapRuntime
    {
        protected RuntimeMapDefinition Definition { get; private set; }
        protected ulong Seed { get; private set; }
        protected Vector2 Focus { get; private set; }

        public virtual void Initialize(in MapRuntimeContext context)
        {
            if (!context.Definition.HasM5Data)
                throw new ArgumentException("Map runtime requires schema-4 map data.", nameof(context));
            Definition = context.Definition;
            Seed = context.Seed;
            Focus = Vector2.Zero;
        }

        public abstract bool IsWalkable(Vector2 position);

        public virtual Vector2 SampleEnemySpawnPosition(
            Vector2 playerPosition,
            float minimumDistance,
            float maximumDistance,
            ref RandomStream random)
        {
            if (minimumDistance < 0f || maximumDistance < minimumDistance)
                throw new ArgumentOutOfRangeException(nameof(minimumDistance));
            UpdateFocus(playerPosition);
            for (var attempt = 0; attempt < 24; attempt++)
            {
                var angle = random.NextFloat() * 2f * (float)Math.PI;
                var radiusSquared = minimumDistance * minimumDistance +
                                    ((maximumDistance * maximumDistance) -
                                     (minimumDistance * minimumDistance)) * random.NextFloat();
                var radius = (float)Math.Sqrt(radiusSquared);
                var candidate = playerPosition + new Vector2(
                    (float)Math.Cos(angle) * radius,
                    (float)Math.Sin(angle) * radius);
                if (IsWalkable(candidate)) return candidate;
            }

            return ResolveMovement(playerPosition, playerPosition + Vector2.UnitX * minimumDistance, 0f);
        }

        public abstract Vector2 ResolveMovement(Vector2 currentPosition, Vector2 desiredPosition, float radius);

        public bool TryGetAnchor(ContentId anchorId, out Vector2 position)
        {
            for (var index = 0; index < Definition.Anchors.Count; index++)
            {
                if (Definition.Anchors[index].Id == anchorId)
                {
                    position = Definition.Anchors[index].Position;
                    return true;
                }
            }

            position = default;
            return false;
        }

        public virtual void UpdateFocus(Vector2 focus)
        {
            if (!Finite(focus)) throw new ArgumentOutOfRangeException(nameof(focus));
            Focus = focus;
        }

        public abstract MapEnvironmentSnapshot GetEnvironmentSnapshot();

        protected bool IsOutsideObstacle(Vector2 position, float radius)
        {
            for (var index = 0; index < Definition.Obstacles.Count; index++)
            {
                var obstacle = Definition.Obstacles[index];
                if (position.X >= obstacle.Minimum.X - radius &&
                    position.X <= obstacle.Maximum.X + radius &&
                    position.Y >= obstacle.Minimum.Y - radius &&
                    position.Y <= obstacle.Maximum.Y + radius)
                {
                    return false;
                }
            }

            return true;
        }

        protected Vector2 ResolveObstacles(Vector2 currentPosition, Vector2 desiredPosition, float radius)
        {
            if (IsOutsideObstacle(desiredPosition, radius)) return desiredPosition;
            var xOnly = new Vector2(desiredPosition.X, currentPosition.Y);
            if (IsOutsideObstacle(xOnly, radius)) return xOnly;
            var yOnly = new Vector2(currentPosition.X, desiredPosition.Y);
            return IsOutsideObstacle(yOnly, radius) ? yOnly : currentPosition;
        }

        protected static bool Finite(Vector2 value) =>
            !float.IsNaN(value.X) && !float.IsInfinity(value.X) &&
            !float.IsNaN(value.Y) && !float.IsInfinity(value.Y);
    }

    /// <summary>Finite rectangular arena with axis-aligned obstacle resolution.</summary>
    public sealed class FiniteArenaMapRuntime : MapRuntimeBase
    {
        public override bool IsWalkable(Vector2 position)
        {
            return Finite(position) &&
                   position.X >= Definition.Minimum.X && position.X <= Definition.Maximum.X &&
                   position.Y >= Definition.Minimum.Y && position.Y <= Definition.Maximum.Y &&
                   IsOutsideObstacle(position, 0f);
        }

        public override Vector2 ResolveMovement(Vector2 currentPosition, Vector2 desiredPosition, float radius)
        {
            if (!Finite(currentPosition) || !Finite(desiredPosition) || radius < 0f)
                return currentPosition;
            var clamped = new Vector2(
                Math.Max(Definition.Minimum.X + radius, Math.Min(Definition.Maximum.X - radius, desiredPosition.X)),
                Math.Max(Definition.Minimum.Y + radius, Math.Min(Definition.Maximum.Y - radius, desiredPosition.Y)));
            return ResolveObstacles(currentPosition, clamped, radius);
        }

        public override MapEnvironmentSnapshot GetEnvironmentSnapshot()
        {
            return new MapEnvironmentSnapshot(Definition.Id, MapBoundsMode.Finite, Focus, 1, 0L);
        }
    }

    /// <summary>
    /// Minimal deterministic infinite map. Only a square window around Focus is logically active;
    /// chunks outside that window own no runtime entities and therefore require no release work.
    /// </summary>
    public sealed class ChunkedInfiniteMapRuntime : MapRuntimeBase
    {
        public override bool IsWalkable(Vector2 position)
        {
            return Finite(position) && IsOutsideObstacle(position, 0f);
        }

        public override Vector2 ResolveMovement(Vector2 currentPosition, Vector2 desiredPosition, float radius)
        {
            if (!Finite(currentPosition) || !Finite(desiredPosition) || radius < 0f)
                return currentPosition;
            return ResolveObstacles(currentPosition, desiredPosition, radius);
        }

        public long GetChunkSignature(int chunkX, int chunkY)
        {
            unchecked
            {
                var value = Seed ^ 0x4348554E4BUL;
                value ^= (ulong)(uint)chunkX * 0x9E3779B185EBCA87UL;
                value ^= (ulong)(uint)chunkY * 0xC2B2AE3D27D4EB4FUL;
                value ^= value >> 30;
                value *= 0xBF58476D1CE4E5B9UL;
                value ^= value >> 27;
                value *= 0x94D049BB133111EBUL;
                value ^= value >> 31;
                return (long)value;
            }
        }

        public override MapEnvironmentSnapshot GetEnvironmentSnapshot()
        {
            var chunkX = (int)Math.Floor(Focus.X / Definition.ChunkSize);
            var chunkY = (int)Math.Floor(Focus.Y / Definition.ChunkSize);
            var diameter = Definition.ActiveChunkRadius * 2 + 1;
            return new MapEnvironmentSnapshot(
                Definition.Id,
                MapBoundsMode.ChunkedInfinite,
                Focus,
                diameter * diameter,
                GetChunkSignature(chunkX, chunkY));
        }
    }

    public static class MapRuntimeFactory
    {
        public static IMapRuntime Create(RuntimeMapDefinition definition, ulong seed)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            IMapRuntime runtime = definition.BoundsMode == MapBoundsMode.Finite
                ? (IMapRuntime)new FiniteArenaMapRuntime()
                : new ChunkedInfiniteMapRuntime();
            runtime.Initialize(new MapRuntimeContext(definition, seed));
            return runtime;
        }
    }
}
