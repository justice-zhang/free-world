using System;

namespace Game.Application
{
    /// <summary>UI-safe build slot copied from run truth without exposing Simulation objects.</summary>
    public readonly struct RunUiBuildEntry
    {
        public RunUiBuildEntry(string contentId, int level, int maximumLevel, byte kind)
        {
            ContentId = contentId ?? string.Empty;
            Level = level;
            MaximumLevel = maximumLevel;
            Kind = kind;
        }

        public string ContentId { get; }
        public int Level { get; }
        public int MaximumLevel { get; }
        public byte Kind { get; }
    }

    /// <summary>UI-safe objective, event, or landmark state copied from the map owner.</summary>
    public readonly struct RunUiMapEntry
    {
        public RunUiMapEntry(string contentId, byte kind, byte state, float progress)
        {
            ContentId = contentId ?? string.Empty;
            Kind = kind;
            State = state;
            Progress = progress;
        }

        public string ContentId { get; }
        public byte Kind { get; }
        public byte State { get; }
        public float Progress { get; }
    }

    /// <summary>
    /// Reusable UI-safe run projection. Application fills fixed buffers so the HUD can
    /// refresh without retaining Simulation stores or allocating a new snapshot.
    /// </summary>
    public sealed class RunUiSnapshot
    {
        private readonly RunUiBuildEntry[] buildEntries;
        private readonly RunUiMapEntry[] mapEntries;

        public RunUiSnapshot(int buildCapacity = 24, int mapCapacity = 24)
        {
            if (buildCapacity < 1) throw new ArgumentOutOfRangeException(nameof(buildCapacity));
            if (mapCapacity < 1) throw new ArgumentOutOfRangeException(nameof(mapCapacity));
            buildEntries = new RunUiBuildEntry[buildCapacity];
            mapEntries = new RunUiMapEntry[mapCapacity];
        }

        public long Tick { get; internal set; }
        public double DurationSeconds { get; internal set; }
        public float Health { get; internal set; }
        public float MaximumHealth { get; internal set; }
        public float Shield { get; internal set; }
        public float MaximumShield { get; internal set; }
        public int Level { get; internal set; }
        public float Experience { get; internal set; }
        public float RequiredExperience { get; internal set; }
        public int MechanicTier { get; internal set; }
        public float MechanicValue { get; internal set; }
        public bool HasBoss { get; internal set; }
        public string BossId { get; internal set; } = string.Empty;
        public int BossPhase { get; internal set; }
        public int BossPhaseCount { get; internal set; }
        public float BossHealth { get; internal set; }
        public float BossMaximumHealth { get; internal set; }
        public int BuildCount { get; private set; }
        public int MapCount { get; private set; }

        public RunUiBuildEntry GetBuildAt(int index)
        {
            if (index < 0 || index >= BuildCount) throw new ArgumentOutOfRangeException(nameof(index));
            return buildEntries[index];
        }

        public RunUiMapEntry GetMapAt(int index)
        {
            if (index < 0 || index >= MapCount) throw new ArgumentOutOfRangeException(nameof(index));
            return mapEntries[index];
        }

        internal void Reset()
        {
            Tick = 0;
            DurationSeconds = 0d;
            Health = MaximumHealth = Shield = MaximumShield = 0f;
            Level = 0;
            Experience = RequiredExperience = 0f;
            MechanicTier = 0;
            MechanicValue = 0f;
            HasBoss = false;
            BossId = string.Empty;
            BossPhase = BossPhaseCount = 0;
            BossHealth = BossMaximumHealth = 0f;
            BuildCount = 0;
            MapCount = 0;
        }

        internal void AddBuild(string contentId, int level, int maximumLevel, byte kind)
        {
            if (BuildCount >= buildEntries.Length) return;
            buildEntries[BuildCount++] = new RunUiBuildEntry(contentId, level, maximumLevel, kind);
        }

        internal void AddMap(string contentId, byte kind, byte state, float progress)
        {
            if (MapCount >= mapEntries.Length) return;
            mapEntries[MapCount++] = new RunUiMapEntry(contentId, kind, state, progress);
        }
    }
}
