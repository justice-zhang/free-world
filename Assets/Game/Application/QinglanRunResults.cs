using System;
using System.Collections.Generic;
using Game.Content.Runtime;
using Game.Core;
using Game.Simulation;

namespace Game.Application
{
    /// <summary>Stable terminal outcome used by the Qinglan Demo result transaction.</summary>
    public enum RunOutcome : byte
    {
        Victory = 1,
        Defeat = 2,
        Abandoned = 3,
        RecoveryRejected = 4
    }

    /// <summary>Backward-compatible terminal reason exposed by the original M6 session.</summary>
    public enum RunEndReason : byte
    {
        Completed = 1,
        PlayerDefeated = 2,
        Abandoned = 3,
        RecoveryRejected = 4
    }

    /// <summary>Version and deterministic hash of one pack used to assemble a run.</summary>
    public readonly struct RunPackSnapshot
    {
        public RunPackSnapshot(ContentId packId, ContentVersion version, string contentHash)
        {
            if (!packId.IsValid) throw new ArgumentException("Pack ID must be valid.", nameof(packId));
            if (string.IsNullOrWhiteSpace(contentHash))
                throw new ArgumentException("Content hash must be present.", nameof(contentHash));
            PackId = packId;
            Version = version;
            ContentHash = contentHash;
        }

        public ContentId PackId { get; }
        public ContentVersion Version { get; }
        public string ContentHash { get; }
    }

    /// <summary>Immutable stable-ID identity and completion rule frozen before a run starts.</summary>
    public sealed class RunDescriptor
    {
        private readonly RunPackSnapshot[] packs;
        private readonly IReadOnlyList<RunPackSnapshot> packsView;

        public RunDescriptor(
            ulong runId,
            ulong seed,
            ContentId characterId,
            ContentId mapId,
            ContentId difficultyId,
            int requiredBossDefeats,
            ContentId victoryBossId,
            RunPackSnapshot[] loadedPacks)
        {
            if (runId == 0UL) throw new ArgumentOutOfRangeException(nameof(runId));
            if (!characterId.IsValid) throw new ArgumentException("Character ID must be valid.", nameof(characterId));
            if (!mapId.IsValid) throw new ArgumentException("Map ID must be valid.", nameof(mapId));
            if (!difficultyId.IsValid) throw new ArgumentException("Difficulty ID must be valid.", nameof(difficultyId));
            if (requiredBossDefeats < 0) throw new ArgumentOutOfRangeException(nameof(requiredBossDefeats));
            if (requiredBossDefeats > 0 && !victoryBossId.IsValid)
                throw new ArgumentException("A Boss-gated run requires a victory Boss ID.", nameof(victoryBossId));
            RunId = runId;
            Seed = seed;
            CharacterId = characterId;
            MapId = mapId;
            DifficultyId = difficultyId;
            RequiredBossDefeats = requiredBossDefeats;
            VictoryBossId = victoryBossId;
            packs = loadedPacks == null ? Array.Empty<RunPackSnapshot>() : (RunPackSnapshot[])loadedPacks.Clone();
            packsView = Array.AsReadOnly(packs);
            for (var index = 0; index < packs.Length; index++)
            {
                if (!packs[index].PackId.IsValid || string.IsNullOrWhiteSpace(packs[index].ContentHash))
                    throw new ArgumentException("Every pack snapshot must be valid.", nameof(loadedPacks));
            }
        }

        public ulong RunId { get; }
        public ulong Seed { get; }
        public ContentId CharacterId { get; }
        public ContentId MapId { get; }
        public ContentId DifficultyId { get; }
        public int RequiredBossDefeats { get; }
        public ContentId VictoryBossId { get; }
        public IReadOnlyList<RunPackSnapshot> LoadedPacks => packsView;

        internal static RunDescriptor CreateLegacy() => new RunDescriptor(
            1UL,
            1UL,
            RequireId("legacy.character.unknown"),
            RequireId("legacy.map.unknown"),
            RequireId("base.difficulty.normal"),
            0,
            default,
            Array.Empty<RunPackSnapshot>());

        private static ContentId RequireId(string value) => ContentId.Create(value).Value;
    }

    /// <summary>Stable content identity and level frozen from one build slot.</summary>
    public readonly struct RunInventoryEntry
    {
        public RunInventoryEntry(ContentId contentId, int level)
        {
            if (!contentId.IsValid) throw new ArgumentException("Inventory content ID must be valid.", nameof(contentId));
            if (level < 1) throw new ArgumentOutOfRangeException(nameof(level));
            ContentId = contentId;
            Level = level;
        }

        public ContentId ContentId { get; }
        public int Level { get; }
    }

    /// <summary>Immutable build inventory copied at the run-ending boundary.</summary>
    public sealed class RunBuildSnapshot
    {
        private readonly RunInventoryEntry[] skills;
        private readonly RunInventoryEntry[] passives;
        private readonly RunInventoryEntry[] relics;
        private readonly ContentId[] evolutions;
        private readonly IReadOnlyList<RunInventoryEntry> skillsView;
        private readonly IReadOnlyList<RunInventoryEntry> passivesView;
        private readonly IReadOnlyList<RunInventoryEntry> relicsView;
        private readonly IReadOnlyList<ContentId> evolutionsView;

        internal RunBuildSnapshot(
            RunInventoryEntry[] skillEntries,
            RunInventoryEntry[] passiveEntries,
            RunInventoryEntry[] relicEntries,
            ContentId[] evolutionIds)
        {
            skills = Copy(skillEntries);
            passives = Copy(passiveEntries);
            relics = Copy(relicEntries);
            evolutions = Copy(evolutionIds);
            skillsView = Array.AsReadOnly(skills);
            passivesView = Array.AsReadOnly(passives);
            relicsView = Array.AsReadOnly(relics);
            evolutionsView = Array.AsReadOnly(evolutions);
        }

        public IReadOnlyList<RunInventoryEntry> Skills => skillsView;
        public IReadOnlyList<RunInventoryEntry> Passives => passivesView;
        public IReadOnlyList<RunInventoryEntry> Relics => relicsView;
        public IReadOnlyList<ContentId> Evolutions => evolutionsView;

        internal static RunBuildSnapshot Empty { get; } = new RunBuildSnapshot(null, null, null, null);

        private static RunInventoryEntry[] Copy(RunInventoryEntry[] source) =>
            source == null ? Array.Empty<RunInventoryEntry>() : (RunInventoryEntry[])source.Clone();

        private static ContentId[] Copy(ContentId[] source) =>
            source == null ? Array.Empty<ContentId>() : (ContentId[])source.Clone();
    }

    /// <summary>Immutable completed map content copied from the simulation owner.</summary>
    public sealed class RunExplorationSnapshot
    {
        private readonly ContentId[] objectives;
        private readonly ContentId[] events;
        private readonly ContentId[] discoveredLandmarks;
        private readonly ContentId[] claimedLandmarks;
        private readonly IReadOnlyList<ContentId> objectivesView;
        private readonly IReadOnlyList<ContentId> eventsView;
        private readonly IReadOnlyList<ContentId> discoveredView;
        private readonly IReadOnlyList<ContentId> claimedView;

        internal RunExplorationSnapshot(
            ContentId[] completedObjectiveIds,
            ContentId[] completedEventIds,
            ContentId[] discoveredLandmarkIds,
            ContentId[] claimedLandmarkIds)
        {
            objectives = Copy(completedObjectiveIds);
            events = Copy(completedEventIds);
            discoveredLandmarks = Copy(discoveredLandmarkIds);
            claimedLandmarks = Copy(claimedLandmarkIds);
            objectivesView = Array.AsReadOnly(objectives);
            eventsView = Array.AsReadOnly(events);
            discoveredView = Array.AsReadOnly(discoveredLandmarks);
            claimedView = Array.AsReadOnly(claimedLandmarks);
        }

        public IReadOnlyList<ContentId> CompletedObjectiveIds => objectivesView;
        public IReadOnlyList<ContentId> CompletedEventIds => eventsView;
        public IReadOnlyList<ContentId> DiscoveredLandmarkIds => discoveredView;
        public IReadOnlyList<ContentId> ClaimedLandmarkIds => claimedView;

        internal static RunExplorationSnapshot Empty { get; } =
            new RunExplorationSnapshot(null, null, null, null);

        private static ContentId[] Copy(ContentId[] source) =>
            source == null ? Array.Empty<ContentId>() : (ContentId[])source.Clone();
    }

    /// <summary>Immutable run result assembled without persistence or presentation dependencies.</summary>
    public readonly struct RunResult
    {
        internal RunResult(
            RunEndReason reason,
            RunDescriptor descriptor,
            long completedTicks,
            int level,
            int activeSynergyCount,
            in RunStatisticsSnapshot statistics,
            RunBuildSnapshot build,
            RunExplorationSnapshot exploration,
            RunResultDelta delta,
            ulong spawnChecksum,
            ulong objectiveChecksum,
            ulong bossChecksum)
        {
            Reason = reason;
            Outcome = ToOutcome(reason);
            Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
            CompletedTicks = completedTicks;
            DurationSeconds = completedTicks * SimulationClock.TickDurationSeconds;
            Level = level;
            Build = build ?? RunBuildSnapshot.Empty;
            SkillCount = Build.Skills.Count;
            PassiveCount = Build.Passives.Count;
            RelicCount = Build.Relics.Count;
            EvolutionCount = Build.Evolutions.Count;
            ActiveSynergyCount = activeSynergyCount;
            Statistics = statistics;
            Exploration = exploration ?? RunExplorationSnapshot.Empty;
            Delta = delta ?? throw new ArgumentNullException(nameof(delta));
            SpawnChecksum = spawnChecksum;
            ObjectiveChecksum = objectiveChecksum;
            BossChecksum = bossChecksum;
        }

        internal RunResult(
            RunEndReason reason,
            long completedTicks,
            int level,
            int skillCount,
            int passiveCount,
            int activeSynergyCount,
            in RunStatisticsSnapshot statistics)
            : this(
                reason,
                RunDescriptor.CreateLegacy(),
                completedTicks,
                level,
                activeSynergyCount,
                statistics,
                LegacyBuild(skillCount, passiveCount),
                RunExplorationSnapshot.Empty,
                new RunResultDelta(RequireId("run.result.legacy")),
                0UL,
                0UL,
                0UL)
        {
        }

        public RunEndReason Reason { get; }
        public RunOutcome Outcome { get; }
        public RunDescriptor Descriptor { get; }
        public long CompletedTicks { get; }
        public double DurationSeconds { get; }
        public int Level { get; }
        public int SkillCount { get; }
        public int PassiveCount { get; }
        public int RelicCount { get; }
        public int EvolutionCount { get; }
        public int ActiveSynergyCount { get; }
        public RunStatisticsSnapshot Statistics { get; }
        public RunBuildSnapshot Build { get; }
        public RunExplorationSnapshot Exploration { get; }
        public RunResultDelta Delta { get; }
        public ulong SpawnChecksum { get; }
        public ulong ObjectiveChecksum { get; }
        public ulong BossChecksum { get; }
        public bool IsVictory => Outcome == RunOutcome.Victory;

        internal static RunResult RecoveryRejected(RunDescriptor descriptor)
        {
            var transactionId = RunResultBuilder.CreateTransactionId(descriptor.RunId);
            return new RunResult(
                RunEndReason.RecoveryRejected,
                descriptor,
                0,
                1,
                0,
                default,
                RunBuildSnapshot.Empty,
                RunExplorationSnapshot.Empty,
                new RunResultDelta(transactionId),
                0UL,
                0UL,
                0UL);
        }

        private static RunOutcome ToOutcome(RunEndReason reason)
        {
            switch (reason)
            {
                case RunEndReason.Completed: return RunOutcome.Victory;
                case RunEndReason.PlayerDefeated: return RunOutcome.Defeat;
                case RunEndReason.RecoveryRejected: return RunOutcome.RecoveryRejected;
                default: return RunOutcome.Abandoned;
            }
        }

        private static RunBuildSnapshot LegacyBuild(int skillCount, int passiveCount)
        {
            var skills = new RunInventoryEntry[Math.Max(0, skillCount)];
            var passives = new RunInventoryEntry[Math.Max(0, passiveCount)];
            for (var index = 0; index < skills.Length; index++)
                skills[index] = new RunInventoryEntry(RequireId("legacy.skill.slot_" + index), 1);
            for (var index = 0; index < passives.Length; index++)
                passives[index] = new RunInventoryEntry(RequireId("legacy.passive.slot_" + index), 1);
            return new RunBuildSnapshot(skills, passives, null, null);
        }

        private static ContentId RequireId(string value) => ContentId.Create(value).Value;
    }
}
