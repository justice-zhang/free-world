using System;
using System.Numerics;
using Game.Content.Runtime;
using Game.Core;

namespace Game.Simulation
{
    internal readonly struct EliteAffixSelection
    {
        public EliteAffixSelection(
            CompiledEliteAffixDefinition first,
            CompiledEliteAffixDefinition second)
        {
            First = first;
            Second = second;
            Count = first == null ? 0 : second == null ? 1 : 2;
        }

        public int Count { get; }
        public CompiledEliteAffixDefinition First { get; }
        public CompiledEliteAffixDefinition Second { get; }

        public CompiledEliteAffixDefinition GetAt(int index)
        {
            if (index < 0 || index >= Count) throw new ArgumentOutOfRangeException(nameof(index));
            return index == 0 ? First : Second;
        }
    }

    /// <summary>One deferred actor creation produced by an encounter scheduler.</summary>
    public readonly struct SpawnRequest
    {
        public SpawnRequest(
            RuntimeContentIndex enemyIndex,
            Vector2 position,
            bool elite,
            bool boss,
            long sequence)
            : this(
                enemyIndex,
                position,
                elite,
                boss,
                sequence,
                default,
                0,
                1f,
                1f)
        {
        }

        internal SpawnRequest(
            RuntimeContentIndex enemyIndex,
            Vector2 position,
            bool elite,
            bool boss,
            long sequence,
            in EliteAffixSelection affixes,
            int splitGeneration,
            float combatScale,
            float rewardScale)
            : this(
                enemyIndex,
                position,
                elite,
                boss,
                sequence,
                affixes,
                splitGeneration,
                combatScale,
                rewardScale,
                null)
        {
        }

        internal SpawnRequest(
            RuntimeContentIndex enemyIndex,
            Vector2 position,
            bool elite,
            bool boss,
            long sequence,
            in EliteAffixSelection affixes,
            int splitGeneration,
            float combatScale,
            float rewardScale,
            RuntimeBossDefinition bossDefinition)
        {
            EnemyIndex = enemyIndex;
            Position = position;
            Elite = elite;
            Boss = boss;
            Sequence = sequence;
            Affixes = affixes;
            SplitGeneration = splitGeneration;
            CombatScale = combatScale;
            RewardScale = rewardScale;
            BossDefinition = bossDefinition;
        }

        public RuntimeContentIndex EnemyIndex { get; }
        public Vector2 Position { get; }
        public bool Elite { get; }
        public bool Boss { get; }
        public long Sequence { get; }
        internal EliteAffixSelection Affixes { get; }
        internal int SplitGeneration { get; }
        internal float CombatScale { get; }
        internal float RewardScale { get; }
        internal RuntimeBossDefinition BossDefinition { get; }
    }

    /// <summary>Reusable FIFO spawn buffer applied only by CleanupSystem.</summary>
    public sealed class SpawnRequestBuffer
    {
        private SpawnRequest[] requests;

        public SpawnRequestBuffer(int initialCapacity = 32)
        {
            if (initialCapacity <= 0) throw new ArgumentOutOfRangeException(nameof(initialCapacity));
            requests = new SpawnRequest[initialCapacity];
        }

        public int Count { get; private set; }

        public SpawnRequest GetAt(int index)
        {
            if (index < 0 || index >= Count) throw new ArgumentOutOfRangeException(nameof(index));
            return requests[index];
        }

        public void Add(in SpawnRequest request)
        {
            if (!request.EnemyIndex.IsValid || !Finite(request.Position) ||
                request.SplitGeneration < 0 ||
                !FinitePositive(request.CombatScale) ||
                !FinitePositive(request.RewardScale))
                throw new ArgumentException("Spawn request is invalid.", nameof(request));
            if (Count == requests.Length) Array.Resize(ref requests, requests.Length * 2);
            requests[Count++] = request;
        }

        internal void Clear()
        {
            Array.Clear(requests, 0, Count);
            Count = 0;
        }

        private static bool Finite(Vector2 value) =>
            !float.IsNaN(value.X) && !float.IsInfinity(value.X) &&
            !float.IsNaN(value.Y) && !float.IsInfinity(value.Y);

        private static bool FinitePositive(float value) =>
            value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
    }

    /// <summary>Allocation-free implementations of the eight M5 spawn patterns.</summary>
    public static class SpawnPatternGenerator
    {
        public static Vector2 Generate(
            SpawnPattern pattern,
            IMapRuntime map,
            Vector2 playerPosition,
            float minimumDistance,
            float maximumDistance,
            ContentId anchorId,
            int groupIndex,
            ref RandomStream random)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            if (pattern == SpawnPattern.Portal || pattern == SpawnPattern.FixedAnchor)
            {
                if (map.TryGetAnchor(anchorId, out var anchor) && map.IsWalkable(anchor))
                    return anchor;
            }

            var angle = random.NextFloat() * 2f * (float)Math.PI;
            var direction = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle));
            Vector2 desired;
            switch (pattern)
            {
                case SpawnPattern.Ring:
                    desired = playerPosition + direction * maximumDistance;
                    break;
                case SpawnPattern.Edge:
                    desired = playerPosition + direction * 100000f;
                    break;
                case SpawnPattern.Cluster:
                {
                    var origin = map.SampleEnemySpawnPosition(
                        playerPosition,
                        minimumDistance,
                        maximumDistance,
                        ref random);
                    var spread = Math.Min(2f, Math.Max(0.25f, maximumDistance * 0.1f));
                    desired = origin + direction * (spread * (groupIndex % 3) / 2f);
                    break;
                }
                case SpawnPattern.Line:
                    desired = playerPosition + direction *
                              Math.Min(maximumDistance, minimumDistance + groupIndex * 0.75f);
                    break;
                case SpawnPattern.Ambush:
                {
                    var side = groupIndex % 2 == 0 ? -1f : 1f;
                    desired = playerPosition + new Vector2(-maximumDistance, side * groupIndex);
                    break;
                }
                case SpawnPattern.Portal:
                case SpawnPattern.FixedAnchor:
                case SpawnPattern.OffscreenRandom:
                    return map.SampleEnemySpawnPosition(
                        playerPosition,
                        minimumDistance,
                        maximumDistance,
                        ref random);
                default:
                    throw new ArgumentOutOfRangeException(nameof(pattern));
            }

            var resolved = map.ResolveMovement(playerPosition, desired, 0f);
            return map.IsWalkable(resolved)
                ? resolved
                : map.SampleEnemySpawnPosition(
                    playerPosition,
                    minimumDistance,
                    maximumDistance,
                    ref random);
        }
    }

    /// <summary>One exact sample of the encounter curves consumed by the runtime scheduler.</summary>
    public readonly struct EncounterTimelineSample
    {
        internal EncounterTimelineSample(
            float phaseFraction,
            float budgetPerSecond,
            float spawnIntervalSeconds)
        {
            PhaseFraction = phaseFraction;
            BudgetPerSecond = budgetPerSecond;
            SpawnIntervalSeconds = spawnIntervalSeconds;
        }

        /// <summary>Gets the clamped normalized phase position.</summary>
        public float PhaseFraction { get; }
        /// <summary>Gets the difficulty-adjusted budget generated per second.</summary>
        public float BudgetPerSecond { get; }
        /// <summary>Gets the difficulty-adjusted delay between spawn attempts.</summary>
        public float SpawnIntervalSeconds { get; }
    }

    /// <summary>
    /// Shared encounter-curve sampler used by both the scheduler and M9 timeline tooling.
    /// </summary>
    public static class EncounterTimelineSampler
    {
        /// <summary>Samples the exact linear curves used by the encounter scheduler.</summary>
        public static EncounterTimelineSample Sample(
            RuntimeEncounterPhase phase,
            float elapsedSeconds,
            float spawnRateMultiplier = 1f)
        {
            if (phase == null) throw new ArgumentNullException(nameof(phase));
            if (float.IsNaN(elapsedSeconds) || float.IsInfinity(elapsedSeconds))
                throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
            if (float.IsNaN(spawnRateMultiplier) || float.IsInfinity(spawnRateMultiplier) ||
                spawnRateMultiplier <= 0f)
                throw new ArgumentOutOfRangeException(nameof(spawnRateMultiplier));

            var duration = phase.EndTimeSeconds - phase.StartTimeSeconds;
            var fraction = duration <= 0f
                ? 0f
                : (elapsedSeconds - phase.StartTimeSeconds) / duration;
            fraction = Math.Max(0f, Math.Min(1f, fraction));
            var budget = Lerp(
                phase.BudgetPerSecondStart,
                phase.BudgetPerSecondEnd,
                fraction) * spawnRateMultiplier;
            var interval = Lerp(
                phase.SpawnIntervalStart,
                phase.SpawnIntervalEnd,
                fraction) / spawnRateMultiplier;
            return new EncounterTimelineSample(fraction, budget, interval);
        }

        private static float Lerp(float start, float end, float fraction) =>
            start + (end - start) * fraction;
    }

    /// <summary>Budgeted deterministic phase scheduler independent of map scenes.</summary>
    public sealed class EncounterScheduler
    {
        private readonly RuntimeEncounterSchedule schedule;
        private readonly IMapRuntime map;
        private readonly DifficultySnapshot difficulty;
        private readonly bool[] eliteTriggered;
        private readonly int[] eliteOffsets;
        private readonly bool[] bossTriggered;
        private readonly int[] bossOffsets;
        private RandomStream random;
        private long elapsedTicks;
        private float accumulatedBudget;
        private float spawnCooldown;
        private long sequence;

        public EncounterScheduler(
            RuntimeEncounterSchedule encounterSchedule,
            IMapRuntime mapRuntime,
            in DifficultySnapshot difficultySnapshot,
            ulong seed)
        {
            schedule = encounterSchedule ?? throw new ArgumentNullException(nameof(encounterSchedule));
            map = mapRuntime ?? throw new ArgumentNullException(nameof(mapRuntime));
            difficulty = difficultySnapshot;
            random = new RandomStream(seed).Derive(0x535041574EUL);
            eliteOffsets = new int[schedule.Phases.Count + 1];
            bossOffsets = new int[schedule.Phases.Count + 1];
            var eliteCount = 0;
            var bossCount = 0;
            for (var index = 0; index < schedule.Phases.Count; index++)
            {
                eliteOffsets[index] = eliteCount;
                bossOffsets[index] = bossCount;
                eliteCount += schedule.Phases[index].EliteRules.Count;
                bossCount += schedule.Phases[index].BossRules.Count;
            }

            eliteOffsets[schedule.Phases.Count] = eliteCount;
            bossOffsets[schedule.Phases.Count] = bossCount;
            eliteTriggered = new bool[eliteCount];
            bossTriggered = new bool[bossCount];
        }

        public float ElapsedSeconds =>
            (float)(elapsedTicks * SimulationClock.TickDurationSeconds);
        public float AccumulatedBudget => accumulatedBudget;
        public long SpawnedRequestCount { get; private set; }
        public int BossRequestCount { get; private set; }
        internal int EliteRequestCount { get; private set; }

        internal void Tick(SimulationWorld world)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            if (!world.TryGetPlayerPosition(out var playerPosition))
            {
                elapsedTicks++;
                return;
            }

            map.UpdateFocus(playerPosition);
            var elapsedSeconds = ElapsedSeconds;
            var phaseIndex = FindPhase(elapsedSeconds);
            if (phaseIndex < 0)
            {
                if (schedule.Phases.Count > 0 &&
                    elapsedSeconds >= schedule.Phases[schedule.Phases.Count - 1].EndTimeSeconds)
                {
                    accumulatedBudget = 0f;
                    spawnCooldown = 0f;
                }
                elapsedTicks++;
                return;
            }

            var phase = schedule.Phases[phaseIndex];
            var curveSample = EncounterTimelineSampler.Sample(
                phase,
                elapsedSeconds,
                difficulty.SpawnRateMultiplier);
            accumulatedBudget += curveSample.BudgetPerSecond * world.DeltaTimeSeconds;
            spawnCooldown -= world.DeltaTimeSeconds;

            TryQueueElites(world, phaseIndex, phase, playerPosition);
            TryQueueBosses(world, phaseIndex, phase, playerPosition);
            var maximumConcurrent = Math.Min(
                schedule.MaximumConcurrentEnemies,
                phase.MaximumConcurrentEnemies);
            var occupied = world.Enemies.Count + world.Enemies.PendingSpawns.Count;
            var normalLimit = Math.Max(
                0,
                maximumConcurrent - CountUntriggeredElites(phaseIndex) - CountUntriggeredBosses(phaseIndex));
            if (spawnCooldown <= 0f && occupied < normalLimit)
            {
                TryQueueGroup(world, phase, playerPosition, normalLimit - occupied);
                spawnCooldown = Math.Max(
                    world.DeltaTimeSeconds,
                    curveSample.SpawnIntervalSeconds);
            }

            elapsedTicks++;
            if (ElapsedSeconds >= schedule.Phases[schedule.Phases.Count - 1].EndTimeSeconds)
            {
                accumulatedBudget = 0f;
                spawnCooldown = 0f;
            }
        }

        private int CountUntriggeredElites(int phaseIndex)
        {
            var count = 0;
            for (var index = eliteOffsets[phaseIndex]; index < eliteOffsets[phaseIndex + 1]; index++)
            {
                if (!eliteTriggered[index]) count++;
            }

            return count;
        }

        private int CountUntriggeredBosses(int phaseIndex)
        {
            var count = 0;
            for (var index = bossOffsets[phaseIndex]; index < bossOffsets[phaseIndex + 1]; index++)
            {
                if (!bossTriggered[index]) count++;
            }

            return count;
        }

        private void TryQueueElites(
            SimulationWorld world,
            int phaseIndex,
            RuntimeEncounterPhase phase,
            Vector2 playerPosition)
        {
            for (var index = 0; index < phase.EliteRules.Count; index++)
            {
                var globalIndex = eliteOffsets[phaseIndex] + index;
                if (eliteTriggered[globalIndex] || ElapsedSeconds < phase.EliteRules[index].SpawnTimeSeconds)
                    continue;
                var cap = Math.Min(schedule.MaximumConcurrentEnemies, phase.MaximumConcurrentEnemies);
                if (world.Enemies.Count + world.Enemies.PendingSpawns.Count >= cap) return;
                var elite = phase.EliteRules[index];
                if (!world.Enemies.Catalog.TryGet(elite.EnemyId, out var enemy)) continue;
                var affixes = world.Enemies.ComposeAffixes(
                    enemy,
                    elite.AffixPoolIds,
                    true,
                    false,
                    ref random);
                var position = SpawnPatternGenerator.Generate(
                    elite.Pattern,
                    map,
                    playerPosition,
                    schedule.MinimumSpawnDistance,
                    schedule.MaximumSpawnDistance,
                    elite.AnchorId,
                    0,
                    ref random);
                world.Enemies.PendingSpawns.Add(
                    new SpawnRequest(
                        enemy.Index,
                        position,
                        true,
                        false,
                        sequence++,
                        affixes,
                        0,
                        1f,
                        1f));
                eliteTriggered[globalIndex] = true;
                SpawnedRequestCount++;
                EliteRequestCount++;
            }
        }

        private void TryQueueBosses(
            SimulationWorld world,
            int phaseIndex,
            RuntimeEncounterPhase phase,
            Vector2 playerPosition)
        {
            for (var index = 0; index < phase.BossRules.Count; index++)
            {
                var globalIndex = bossOffsets[phaseIndex] + index;
                if (bossTriggered[globalIndex] || ElapsedSeconds < phase.BossRules[index].SpawnTimeSeconds)
                    continue;
                var cap = Math.Min(schedule.MaximumConcurrentEnemies, phase.MaximumConcurrentEnemies);
                if (world.Enemies.Count + world.Enemies.PendingSpawns.Count >= cap) return;
                var boss = phase.BossRules[index];
                if (!world.Enemies.Catalog.TryGet(boss.EnemyId, out var enemy)) continue;
                RuntimeBossDefinition bossDefinition = null;
                if (boss.BossDefinitionId.IsValid &&
                    !world.Enemies.Catalog.TryGetBoss(boss.BossDefinitionId, out bossDefinition))
                    continue;
                var position = SpawnPatternGenerator.Generate(
                    boss.Pattern,
                    map,
                    playerPosition,
                    schedule.MinimumSpawnDistance,
                    schedule.MaximumSpawnDistance,
                    boss.AnchorId,
                    0,
                    ref random);
                world.Enemies.PendingSpawns.Add(
                    new SpawnRequest(
                        enemy.Index,
                        position,
                        true,
                        true,
                        sequence++,
                        default,
                        0,
                        1f,
                        1f,
                        bossDefinition));
                bossTriggered[globalIndex] = true;
                SpawnedRequestCount++;
                BossRequestCount++;
            }
        }

        private void TryQueueGroup(
            SimulationWorld world,
            RuntimeEncounterPhase phase,
            Vector2 playerPosition,
            int availableSlots)
        {
            var entry = SelectEntry(phase);
            if (entry.BudgetCost > accumulatedBudget) return;
            var range = entry.MaximumGroupSize - entry.MinimumGroupSize + 1;
            var requested = entry.MinimumGroupSize + (range > 1 ? random.NextInt(range) : 0);
            var groupSize = Math.Min(requested, availableSlots);
            groupSize = Math.Min(groupSize, (int)(accumulatedBudget / entry.BudgetCost));
            for (var index = 0; index < groupSize; index++)
            {
                if (!world.Enemies.Catalog.TryGet(entry.EnemyId, out var enemy)) return;
                var elite = entry.Elite || random.NextFloat() < difficulty.EliteProbability;
                var affixes = world.Enemies.ComposeAffixes(
                    enemy,
                    entry.AffixPoolIds,
                    elite,
                    false,
                    ref random);
                var position = SpawnPatternGenerator.Generate(
                    phase.SpawnPattern,
                    map,
                    playerPosition,
                    schedule.MinimumSpawnDistance,
                    schedule.MaximumSpawnDistance,
                    phase.AnchorId,
                    index,
                    ref random);
                world.Enemies.PendingSpawns.Add(
                    new SpawnRequest(
                        enemy.Index,
                        position,
                        elite,
                        false,
                        sequence++,
                        affixes,
                        0,
                        1f,
                        1f));
                accumulatedBudget -= entry.BudgetCost;
                SpawnedRequestCount++;
            }
        }

        private RuntimeEncounterEnemyEntry SelectEntry(RuntimeEncounterPhase phase)
        {
            var total = 0f;
            for (var index = 0; index < phase.EnemyEntries.Count; index++)
                total += phase.EnemyEntries[index].Weight;
            var selected = random.NextFloat() * total;
            for (var index = 0; index < phase.EnemyEntries.Count; index++)
            {
                selected -= phase.EnemyEntries[index].Weight;
                if (selected <= 0f) return phase.EnemyEntries[index];
            }

            return phase.EnemyEntries[phase.EnemyEntries.Count - 1];
        }

        private int FindPhase(float time)
        {
            for (var index = 0; index < schedule.Phases.Count; index++)
            {
                if (time >= schedule.Phases[index].StartTimeSeconds &&
                    time < schedule.Phases[index].EndTimeSeconds)
                    return index;
            }

            return -1;
        }
    }
}
