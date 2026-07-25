using System;
using System.Collections.Generic;
using System.Numerics;

namespace Game.Simulation
{
    /// <summary>
    /// One result returned by a spatial query.
    /// </summary>
    public readonly struct SpatialQueryResult
    {
        /// <summary>Initializes a spatial query result.</summary>
        public SpatialQueryResult(
            SpatialEntity entity,
            Vector2 position,
            float distanceSquared)
        {
            Entity = entity;
            Position = position;
            DistanceSquared = distanceSquared;
        }

        /// <summary>Gets the matched entity.</summary>
        public SpatialEntity Entity { get; }

        /// <summary>Gets the entity position captured by the query.</summary>
        public Vector2 Position { get; }

        /// <summary>Gets squared distance from the query center.</summary>
        public float DistanceSquared { get; }
    }

    /// <summary>
    /// Caller-owned reusable output storage for spatial queries.
    /// </summary>
    public sealed class SpatialQueryBuffer
    {
        private SpatialQueryResult[] results;

        /// <summary>Initializes a reusable query buffer.</summary>
        public SpatialQueryBuffer(int initialCapacity = 16)
        {
            if (initialCapacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(initialCapacity));
            }

            results = new SpatialQueryResult[initialCapacity];
        }

        /// <summary>Gets the number of results from the latest query.</summary>
        public int Count { get; private set; }

        /// <summary>Gets one query result.</summary>
        public SpatialQueryResult this[int index]
        {
            get
            {
                if (index < 0 || index >= Count)
                {
                    throw new ArgumentOutOfRangeException(nameof(index));
                }

                return results[index];
            }
        }

        internal void Reset()
        {
            Count = 0;
        }

        internal void Add(in SpatialQueryResult result)
        {
            if (Count == results.Length)
            {
                Array.Resize(ref results, results.Length * 2);
            }

            results[Count++] = result;
        }
    }

    /// <summary>
    /// Unified allocation-conscious 2D spatial hash grid for all simulation stores.
    /// </summary>
    public sealed class SpatialGrid
    {
        private struct Entry
        {
            public SpatialEntity Entity;
            public Vector2 Position;
            public long CellKey;
            public int PreviousInCell;
            public int NextInCell;
            public int NextFree;
            public bool Active;
        }

        private readonly Dictionary<long, int> cellHeads;
        private readonly Dictionary<SpatialEntity, int> entryByEntity;
        private Entry[] entries;
        private int entryCount;
        private int freeHead = -1;

        /// <summary>Initializes a spatial grid with fixed cell size.</summary>
        public SpatialGrid(float cellSize, int initialCapacity = 64)
        {
            if (!(cellSize > 0f) || float.IsInfinity(cellSize))
            {
                throw new ArgumentOutOfRangeException(nameof(cellSize));
            }

            if (initialCapacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(initialCapacity));
            }

            CellSize = cellSize;
            entries = new Entry[initialCapacity];
            cellHeads = new Dictionary<long, int>(initialCapacity);
            entryByEntity = new Dictionary<SpatialEntity, int>(initialCapacity);
        }

        /// <summary>Gets the world-unit size of one square cell.</summary>
        public float CellSize { get; }

        /// <summary>Gets the number of indexed entities.</summary>
        public int Count => entryByEntity.Count;

        /// <summary>Inserts an entity if it is not already indexed.</summary>
        public bool Insert(SpatialEntity entity, Vector2 position)
        {
            if (entryByEntity.ContainsKey(entity))
            {
                return false;
            }

            var entryIndex = AllocateEntry();
            var cellKey = GetCellKey(position);
            var previousHead = cellHeads.TryGetValue(cellKey, out var head) ? head : -1;
            entries[entryIndex] = new Entry
            {
                Entity = entity,
                Position = position,
                CellKey = cellKey,
                PreviousInCell = -1,
                NextInCell = previousHead,
                NextFree = -1,
                Active = true
            };

            if (previousHead >= 0)
            {
                entries[previousHead].PreviousInCell = entryIndex;
            }

            cellHeads[cellKey] = entryIndex;
            entryByEntity.Add(entity, entryIndex);
            return true;
        }

        /// <summary>Updates position and cell membership for an indexed entity.</summary>
        public bool Update(SpatialEntity entity, Vector2 position)
        {
            if (!entryByEntity.TryGetValue(entity, out var entryIndex))
            {
                return false;
            }

            var newCellKey = GetCellKey(position);
            if (newCellKey != entries[entryIndex].CellKey)
            {
                UnlinkFromCell(entryIndex);
                entries[entryIndex].CellKey = newCellKey;
                LinkAtCellHead(entryIndex, newCellKey);
            }

            entries[entryIndex].Position = position;
            return true;
        }

        /// <summary>Removes an indexed entity.</summary>
        public bool Remove(SpatialEntity entity)
        {
            if (!entryByEntity.TryGetValue(entity, out var entryIndex))
            {
                return false;
            }

            UnlinkFromCell(entryIndex);
            entryByEntity.Remove(entity);
            entries[entryIndex].Active = false;
            entries[entryIndex].NextFree = freeHead;
            freeHead = entryIndex;
            return true;
        }

        /// <summary>Tries to read the latest indexed position.</summary>
        public bool TryGetPosition(SpatialEntity entity, out Vector2 position)
        {
            if (!entryByEntity.TryGetValue(entity, out var entryIndex))
            {
                position = default;
                return false;
            }

            position = entries[entryIndex].Position;
            return true;
        }

        /// <summary>
        /// Writes all entities within an inclusive radius to a caller-owned buffer.
        /// </summary>
        public void QueryRadius(Vector2 center, float radius, SpatialQueryBuffer results)
        {
            QueryRadiusInternal(center, radius, results, default, false);
        }

        /// <summary>
        /// Writes neighbors of one indexed entity, excluding that source entity.
        /// </summary>
        /// <returns>False when the source entity is not indexed.</returns>
        public bool QueryNearby(
            SpatialEntity source,
            float radius,
            SpatialQueryBuffer results)
        {
            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            if (!entryByEntity.TryGetValue(source, out var entryIndex))
            {
                results.Reset();
                return false;
            }

            QueryRadiusInternal(entries[entryIndex].Position, radius, results, source, true);
            return true;
        }

        private void QueryRadiusInternal(
            Vector2 center,
            float radius,
            SpatialQueryBuffer results,
            SpatialEntity excluded,
            bool hasExcluded)
        {
            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            if (radius < 0f || float.IsNaN(radius) || float.IsInfinity(radius))
            {
                throw new ArgumentOutOfRangeException(nameof(radius));
            }

            results.Reset();
            var minimumX = ToCellCoordinate(center.X - radius);
            var maximumX = ToCellCoordinate(center.X + radius);
            var minimumY = ToCellCoordinate(center.Y - radius);
            var maximumY = ToCellCoordinate(center.Y + radius);
            var radiusSquared = radius * radius;

            for (var cellX = minimumX; cellX <= maximumX; cellX++)
            {
                for (var cellY = minimumY; cellY <= maximumY; cellY++)
                {
                    var key = ComposeCellKey(cellX, cellY);
                    if (!cellHeads.TryGetValue(key, out var entryIndex))
                    {
                        continue;
                    }

                    while (entryIndex >= 0)
                    {
                        ref var entry = ref entries[entryIndex];
                        var nextIndex = entry.NextInCell;
                        if (entry.Active && (!hasExcluded || entry.Entity != excluded))
                        {
                            var offset = entry.Position - center;
                            var distanceSquared = offset.LengthSquared();
                            if (distanceSquared <= radiusSquared)
                            {
                                var result = new SpatialQueryResult(
                                    entry.Entity,
                                    entry.Position,
                                    distanceSquared);
                                results.Add(result);
                            }
                        }

                        entryIndex = nextIndex;
                    }
                }
            }
        }

        private int AllocateEntry()
        {
            if (freeHead >= 0)
            {
                var index = freeHead;
                freeHead = entries[index].NextFree;
                return index;
            }

            if (entryCount == entries.Length)
            {
                Array.Resize(ref entries, entries.Length * 2);
            }

            return entryCount++;
        }

        private void LinkAtCellHead(int entryIndex, long cellKey)
        {
            var previousHead = cellHeads.TryGetValue(cellKey, out var head) ? head : -1;
            entries[entryIndex].PreviousInCell = -1;
            entries[entryIndex].NextInCell = previousHead;
            if (previousHead >= 0)
            {
                entries[previousHead].PreviousInCell = entryIndex;
            }

            cellHeads[cellKey] = entryIndex;
        }

        private void UnlinkFromCell(int entryIndex)
        {
            ref var entry = ref entries[entryIndex];
            if (entry.PreviousInCell >= 0)
            {
                entries[entry.PreviousInCell].NextInCell = entry.NextInCell;
            }
            else if (entry.NextInCell >= 0)
            {
                cellHeads[entry.CellKey] = entry.NextInCell;
            }
            else
            {
                cellHeads.Remove(entry.CellKey);
            }

            if (entry.NextInCell >= 0)
            {
                entries[entry.NextInCell].PreviousInCell = entry.PreviousInCell;
            }

            entry.PreviousInCell = -1;
            entry.NextInCell = -1;
        }

        private long GetCellKey(Vector2 position)
        {
            return ComposeCellKey(
                ToCellCoordinate(position.X),
                ToCellCoordinate(position.Y));
        }

        private int ToCellCoordinate(float coordinate)
        {
            return (int)Math.Floor(coordinate / CellSize);
        }

        private static long ComposeCellKey(int cellX, int cellY)
        {
            return ((long)cellX << 32) ^ (uint)cellY;
        }
    }
}
