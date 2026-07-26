using System;
using Game.Core;

namespace Game.Simulation
{
    public enum OfferHistoryAction : byte
    {
        Generate = 1,
        Reroll = 2,
        Select = 3,
        Banish = 4,
        Skip = 5
    }

    /// <summary>Immutable candidate set returned to Application/UI callers.</summary>
    public sealed class UpgradeOfferSet
    {
        private readonly CompiledUpgradeOfferDefinition[] offers;

        internal UpgradeOfferSet(CompiledUpgradeOfferDefinition[] candidates)
        {
            offers = candidates ?? Array.Empty<CompiledUpgradeOfferDefinition>();
        }

        public int Count => offers.Length;
        public CompiledUpgradeOfferDefinition GetAt(int index)
        {
            if (index < 0 || index >= offers.Length) throw new ArgumentOutOfRangeException(nameof(index));
            return offers[index];
        }
    }

    /// <summary>Compact replay diagnostic for one offer action.</summary>
    public readonly struct OfferHistoryEntry
    {
        internal OfferHistoryEntry(
            int sequence,
            OfferHistoryAction action,
            ulong rootSeed,
            ulong callsBefore,
            ulong callsAfter,
            ContentId first,
            ContentId second,
            ContentId third,
            int offerCount,
            ContentId subject)
        {
            Sequence = sequence;
            Action = action;
            RootSeed = rootSeed;
            CallsBefore = callsBefore;
            CallsAfter = callsAfter;
            FirstOfferId = first;
            SecondOfferId = second;
            ThirdOfferId = third;
            OfferCount = offerCount;
            SubjectId = subject;
        }

        public int Sequence { get; }
        public OfferHistoryAction Action { get; }
        public ulong RootSeed { get; }
        public ulong CallsBefore { get; }
        public ulong CallsAfter { get; }
        public ContentId FirstOfferId { get; }
        public ContentId SecondOfferId { get; }
        public ContentId ThirdOfferId { get; }
        public int OfferCount { get; }
        public ContentId SubjectId { get; }
    }

    /// <summary>Deterministic weighted candidate generator with reroll, banish, skip, and history.</summary>
    public sealed class OfferGenerator
    {
        private const ulong OfferStreamId = 0x4F4646455253UL;
        private readonly BuildRuntimeCatalog catalog;
        private CompiledUpgradeOfferDefinition[] candidates;
        private ContentId[] banished;
        private OfferHistoryEntry[] history;
        private int banishedCount;
        private RandomStream random;

        public OfferGenerator(BuildRuntimeCatalog runtimeCatalog, ulong runSeed, int initialCapacity = 16)
        {
            if (initialCapacity <= 0) throw new ArgumentOutOfRangeException(nameof(initialCapacity));
            catalog = runtimeCatalog ?? throw new ArgumentNullException(nameof(runtimeCatalog));
            candidates = new CompiledUpgradeOfferDefinition[initialCapacity];
            banished = new ContentId[initialCapacity];
            history = new OfferHistoryEntry[initialCapacity];
            random = new RandomStream(runSeed).Derive(OfferStreamId);
        }

        public ulong StreamSeed => random.RootSeed;
        public ulong RandomCalls => random.Calls;
        public int HistoryCount { get; private set; }

        public OfferHistoryEntry GetHistoryAt(int index)
        {
            if (index < 0 || index >= HistoryCount) throw new ArgumentOutOfRangeException(nameof(index));
            return history[index];
        }

        public UpgradeOfferSet Generate(BuildState state, int count = 3)
        {
            return GenerateInternal(state, count, OfferHistoryAction.Generate, default);
        }

        public UpgradeOfferSet Reroll(BuildState state, int count = 3)
        {
            return GenerateInternal(state, count, OfferHistoryAction.Reroll, default);
        }

        public UpgradeOfferSet Banish(BuildState state, ContentId offerId, int count = 3)
        {
            if (offerId.IsValid && !IsBanished(offerId))
            {
                EnsureCapacity(ref banished, banishedCount + 1);
                banished[banishedCount++] = offerId;
            }
            return GenerateInternal(state, count, OfferHistoryAction.Banish, offerId);
        }

        public void RecordSelection(UpgradeOfferSet set, ContentId offerId)
        {
            Record(OfferHistoryAction.Select, set, offerId, random.Calls, random.Calls);
        }

        public void RecordSkip(UpgradeOfferSet set)
        {
            Record(OfferHistoryAction.Skip, set, default, random.Calls, random.Calls);
        }

        public bool IsBanished(ContentId offerId)
        {
            for (var index = 0; index < banishedCount; index++) if (banished[index] == offerId) return true;
            return false;
        }

        private UpgradeOfferSet GenerateInternal(
            BuildState state,
            int count,
            OfferHistoryAction action,
            ContentId subject)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (count <= 0 || count > 3) throw new ArgumentOutOfRangeException(nameof(count));
            EnsureCapacity(ref candidates, catalog.Offers.Count);
            var candidateCount = 0;
            for (var index = 0; index < catalog.Offers.Count; index++)
            {
                var offer = catalog.Offers[index];
                if (!IsBanished(offer.Source.Id) && state.CanAcceptOffer(offer))
                    candidates[candidateCount++] = offer;
            }

            var callsBefore = random.Calls;
            var outputCount = Math.Min(count, candidateCount);
            var output = new CompiledUpgradeOfferDefinition[outputCount];
            for (var outputIndex = 0; outputIndex < outputCount; outputIndex++)
            {
                var totalWeight = 0f;
                for (var index = 0; index < candidateCount; index++) totalWeight += candidates[index].Source.Weight;
                var roll = random.NextFloat() * totalWeight;
                var selected = candidateCount - 1;
                var cursor = 0f;
                for (var index = 0; index < candidateCount; index++)
                {
                    cursor += candidates[index].Source.Weight;
                    if (roll < cursor)
                    {
                        selected = index;
                        break;
                    }
                }
                output[outputIndex] = candidates[selected];
                candidateCount--;
                candidates[selected] = candidates[candidateCount];
                candidates[candidateCount] = null;
            }

            var set = new UpgradeOfferSet(output);
            Record(action, set, subject, callsBefore, random.Calls);
            return set;
        }

        private void Record(
            OfferHistoryAction action,
            UpgradeOfferSet set,
            ContentId subject,
            ulong callsBefore,
            ulong callsAfter)
        {
            EnsureCapacity(ref history, HistoryCount + 1);
            history[HistoryCount] = new OfferHistoryEntry(
                HistoryCount,
                action,
                random.RootSeed,
                callsBefore,
                callsAfter,
                set != null && set.Count > 0 ? set.GetAt(0).Source.Id : default,
                set != null && set.Count > 1 ? set.GetAt(1).Source.Id : default,
                set != null && set.Count > 2 ? set.GetAt(2).Source.Id : default,
                set?.Count ?? 0,
                subject);
            HistoryCount++;
        }

        private static void EnsureCapacity<T>(ref T[] source, int required)
        {
            if (required <= source.Length) return;
            var capacity = source.Length == 0 ? 4 : source.Length * 2;
            while (capacity < required) capacity *= 2;
            Array.Resize(ref source, capacity);
        }
    }
}
