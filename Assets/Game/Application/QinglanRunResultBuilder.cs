using System;
using System.Collections.Generic;
using System.Globalization;
using Game.Content.Runtime;
using Game.Core;
using Game.Simulation;

namespace Game.Application
{
    /// <summary>Low-frequency freezer that copies simulation truth into stable-ID result data.</summary>
    internal static class RunResultBuilder
    {
        private struct CurrencyAccumulator
        {
            public ContentId Id;
            public long Amount;
        }

        public static RunResult Build(
            SimulationWorld world,
            RunDescriptor descriptor,
            RunEndReason reason)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            if (descriptor == null) throw new ArgumentNullException(nameof(descriptor));
            var progression = world.Progression ??
                throw new InvalidOperationException("A run result requires initialized progression.");
            var statistics = progression.Statistics;
            var build = BuildInventory(world, progression.Build);
            var exploration = BuildExploration(world.Qinglan?.MapObjectives, out var objectiveChecksum);
            var delta = BuildDelta(world.Qinglan?.Rewards, descriptor.RunId);
            var bossCompleted = world.Qinglan?.Bosses.CompletedCount ?? 0;
            var bossChecksum = 1469598103934665603UL;
            Mix(ref bossChecksum, bossCompleted);
            Mix(ref bossChecksum, statistics.BossDefeats);

            return new RunResult(
                reason,
                descriptor,
                world.Tick,
                progression.Experience.Level,
                progression.Build.ActiveSynergyCount,
                statistics,
                build,
                exploration,
                delta,
                world.Enemies.SpawnChecksum,
                objectiveChecksum,
                bossChecksum);
        }

        internal static ContentId CreateTransactionId(ulong runId)
        {
            return ContentId.Create(
                "run.result." + runId.ToString("x16", CultureInfo.InvariantCulture)).Value;
        }

        private static RunBuildSnapshot BuildInventory(SimulationWorld world, BuildState state)
        {
            var skills = new RunInventoryEntry[state.Skills.Count];
            for (var index = 0; index < skills.Length; index++)
            {
                var entry = state.Skills.GetAt(index);
                skills[index] = new RunInventoryEntry(entry.ContentId, entry.Level);
            }

            var passives = new RunInventoryEntry[state.Passives.Count];
            for (var index = 0; index < passives.Length; index++)
            {
                var entry = state.Passives.GetAt(index);
                passives[index] = new RunInventoryEntry(entry.ContentId, entry.Level);
            }

            var relicCount = world.Qinglan?.Rewards.Relics?.Count ?? 0;
            var relics = new RunInventoryEntry[relicCount];
            for (var index = 0; index < relics.Length; index++)
            {
                var entry = world.Qinglan.Rewards.Relics.GetAt(index);
                relics[index] = new RunInventoryEntry(entry.RelicId, entry.Level);
            }

            var evolutions = new ContentId[state.AppliedEvolutionCount];
            for (var index = 0; index < evolutions.Length; index++)
                evolutions[index] = state.GetAppliedEvolutionAt(index);
            return new RunBuildSnapshot(skills, passives, relics, evolutions);
        }

        private static RunExplorationSnapshot BuildExploration(
            MapObjectiveRuntime map,
            out ulong checksum)
        {
            checksum = 1469598103934665603UL;
            if (map == null)
                return RunExplorationSnapshot.Empty;

            var objectives = new List<ContentId>(map.ObjectiveCount);
            var events = new List<ContentId>(map.EventCount);
            var discovered = new List<ContentId>(map.LandmarkCount);
            var claimed = new List<ContentId>(map.LandmarkCount);
            for (var index = 0; index < map.ObjectiveCount; index++)
            {
                var item = map.GetObjectiveAt(index);
                Mix(ref checksum, item.Id);
                Mix(ref checksum, (int)item.State);
                if (item.State == ObjectiveState.Completed) objectives.Add(item.Id);
            }
            for (var index = 0; index < map.EventCount; index++)
            {
                var item = map.GetEventAt(index);
                Mix(ref checksum, item.Id);
                Mix(ref checksum, (int)item.State);
                if (item.State == ObjectiveState.Completed) events.Add(item.Id);
            }
            for (var index = 0; index < map.LandmarkCount; index++)
            {
                var item = map.GetLandmarkAt(index);
                Mix(ref checksum, item.Id);
                Mix(ref checksum, (int)item.State);
                Mix(ref checksum, item.ClaimCount);
                if (item.State != LandmarkState.Undiscovered) discovered.Add(item.Id);
                if (item.State == LandmarkState.Claimed || item.ClaimCount > 0) claimed.Add(item.Id);
            }

            return new RunExplorationSnapshot(
                Sort(objectives),
                Sort(events),
                Sort(discovered),
                Sort(claimed));
        }

        private static RunResultDelta BuildDelta(RewardRuntime rewards, ulong runId)
        {
            var unlocks = new List<ContentId>();
            var unique = new List<ContentId>();
            var stories = new List<ContentId>();
            var currencies = new List<CurrencyAccumulator>();
            if (rewards != null)
            {
                for (var index = 0; index < rewards.ResultEntryCount; index++)
                {
                    var entry = rewards.GetResultEntryAt(index);
                    switch (entry.Kind)
                    {
                        case RewardDeltaKind.Currency:
                            AddCurrency(currencies, entry.ContentId, entry.Amount);
                            break;
                        case RewardDeltaKind.UnlockContent:
                            AddUnique(unlocks, entry.ContentId);
                            break;
                        case RewardDeltaKind.Unique:
                            AddUnique(unique, entry.ContentId);
                            break;
                        case RewardDeltaKind.Story:
                            AddUnique(stories, entry.ContentId);
                            break;
                    }
                }
            }

            currencies.Sort(CompareCurrency);
            var currencyDeltas = new SavedCounter[currencies.Count];
            for (var index = 0; index < currencyDeltas.Length; index++)
            {
                currencyDeltas[index] = new SavedCounter(
                    currencies[index].Id.Value,
                    currencies[index].Amount);
            }
            return new RunResultDelta(
                CreateTransactionId(runId),
                Sort(unlocks),
                Sort(unique),
                Sort(stories),
                null,
                currencyDeltas);
        }

        private static void AddCurrency(List<CurrencyAccumulator> values, ContentId id, int amount)
        {
            if (!id.IsValid || amount == 0) return;
            for (var index = 0; index < values.Count; index++)
            {
                if (values[index].Id != id) continue;
                var value = values[index];
                value.Amount += amount;
                values[index] = value;
                return;
            }
            values.Add(new CurrencyAccumulator { Id = id, Amount = amount });
        }

        private static void AddUnique(List<ContentId> values, ContentId id)
        {
            if (!id.IsValid) return;
            for (var index = 0; index < values.Count; index++)
                if (values[index] == id) return;
            values.Add(id);
        }

        private static ContentId[] Sort(List<ContentId> values)
        {
            var result = values.ToArray();
            Array.Sort(result, CompareContentId);
            return result;
        }

        private static int CompareContentId(ContentId left, ContentId right) =>
            string.Compare(left.Value, right.Value, StringComparison.Ordinal);

        private static int CompareCurrency(CurrencyAccumulator left, CurrencyAccumulator right) =>
            CompareContentId(left.Id, right.Id);

        private static void Mix(ref ulong checksum, ContentId value)
        {
            var text = value.IsValid ? value.Value : string.Empty;
            for (var index = 0; index < text.Length; index++)
            {
                checksum ^= text[index];
                checksum *= 1099511628211UL;
            }
        }

        private static void Mix(ref ulong checksum, long value)
        {
            unchecked
            {
                checksum ^= (ulong)value;
                checksum *= 1099511628211UL;
            }
        }
    }
}
