using System;
using System.Numerics;
using Game.Content.Runtime;
using Game.Core;

namespace Game.Simulation
{
    /// <summary>Immutable run statistics exposed to results and deterministic tests.</summary>
    public readonly struct RunStatisticsSnapshot
    {
        internal RunStatisticsSnapshot(
            long enemyDefeats,
            long pickupsCollected,
            double experienceCollected,
            int offersSelected,
            int offersRerolled,
            int offersBanished,
            int offersSkipped,
            ulong decisionChecksum)
        {
            EnemyDefeats = enemyDefeats;
            PickupsCollected = pickupsCollected;
            ExperienceCollected = experienceCollected;
            OffersSelected = offersSelected;
            OffersRerolled = offersRerolled;
            OffersBanished = offersBanished;
            OffersSkipped = offersSkipped;
            DecisionChecksum = decisionChecksum;
        }

        public long EnemyDefeats { get; }
        public long PickupsCollected { get; }
        public double ExperienceCollected { get; }
        public int OffersSelected { get; }
        public int OffersRerolled { get; }
        public int OffersBanished { get; }
        public int OffersSkipped { get; }
        public ulong DecisionChecksum { get; }
    }

    internal struct ExperiencePickupRecord
    {
        public ushort Generation;
        public float Value;
        public bool Collected;
    }

    internal readonly struct PendingExperiencePickup
    {
        public PendingExperiencePickup(Vector2 position, float value)
        {
            Position = position;
            Value = value;
        }

        public Vector2 Position { get; }
        public float Value { get; }
    }

    /// <summary>
    /// Run-local M6 coordinator for XP pickups, build truth, deterministic offers,
    /// and level-up requests. Entity creation remains deferred to CleanupSystem.
    /// </summary>
    public sealed class ProgressionRuntime
    {
        private const float PickupLifetimeSeconds = 120f;
        private const float PickupAttractionSpeed = 12f;
        private const float PickupCollectionDistanceSquared = 0.16f;
        private ExperiencePickupRecord[] pickups;
        private PendingExperiencePickup[] pendingPickups;
        private int pendingPickupCount;
        private float pendingExperience;
        private long enemyDefeats;
        private long pickupsCollected;
        private double experienceCollected;
        private int offersSelected;
        private int offersRerolled;
        private int offersBanished;
        private int offersSkipped;
        private ulong decisionChecksum = 1469598103934665603UL;

        internal ProgressionRuntime(
            BuildRuntimeCatalog catalog,
            ActorStore actors,
            SkillRuntime skills,
            EntityHandle player,
            ulong runSeed,
            ExperienceCurve? curve,
            int skillSlots,
            int passiveSlots,
            ContentTag[] mapTags,
            int initialCapacity)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (initialCapacity <= 0) throw new ArgumentOutOfRangeException(nameof(initialCapacity));
            Player = new SpatialEntity(EntityKind.Actor, player);
            Build = new BuildState(catalog, actors, skills, Player, skillSlots, passiveSlots, mapTags);
            Offers = new OfferGenerator(catalog, runSeed, initialCapacity);
            Experience = new ExperienceProgression(curve);
            pickups = new ExperiencePickupRecord[initialCapacity];
            pendingPickups = new PendingExperiencePickup[initialCapacity];
        }

        public SpatialEntity Player { get; }
        public BuildState Build { get; }
        public OfferGenerator Offers { get; }
        public ExperienceProgression Experience { get; }
        public UpgradeOfferSet CurrentOffers { get; private set; }
        public bool HasPendingChoice => CurrentOffers != null;
        public bool PauseRequested { get; private set; }
        public float PickupAttractionSpeedUnitsPerSecond => PickupAttractionSpeed;

        public RunStatisticsSnapshot Statistics => new RunStatisticsSnapshot(
            enemyDefeats,
            pickupsCollected,
            experienceCollected,
            offersSelected,
            offersRerolled,
            offersBanished,
            offersSkipped,
            decisionChecksum);

        public bool SelectOffer(ContentId offerId)
        {
            if (!TryFindCurrentOffer(offerId, out var offer) || !Build.ApplyOffer(offer)) return false;
            Offers.RecordSelection(CurrentOffers, offerId);
            Experience.ConsumeLevelUpRequest();
            offersSelected++;
            MixDecision(offerId, OfferHistoryAction.Select);
            ClearChoice();
            return true;
        }

        public bool SkipOffer()
        {
            if (CurrentOffers == null) return false;
            Offers.RecordSkip(CurrentOffers);
            Experience.ConsumeLevelUpRequest();
            offersSkipped++;
            MixDecision(default, OfferHistoryAction.Skip);
            ClearChoice();
            return true;
        }

        public bool RerollOffers(int count = 3)
        {
            if (CurrentOffers == null) return false;
            CurrentOffers = Offers.Reroll(Build, count);
            offersRerolled++;
            MixDecision(default, OfferHistoryAction.Reroll);
            return true;
        }

        public bool BanishOffer(ContentId offerId, int count = 3)
        {
            if (!TryFindCurrentOffer(offerId, out _)) return false;
            CurrentOffers = Offers.Banish(Build, offerId, count);
            offersBanished++;
            MixDecision(offerId, OfferHistoryAction.Banish);
            return true;
        }

        internal void RecordEnemyDefeat(float experienceReward, Vector2 position)
        {
            enemyDefeats++;
            if (experienceReward > 0f && !float.IsNaN(experienceReward) && !float.IsInfinity(experienceReward))
                QueueExperiencePickup(position, experienceReward);
        }

        internal void TickPickups(SimulationWorld world)
        {
            if (!world.Actors.TryRead(Player.Handle, out var playerState) ||
                !world.Actors.TryReadStat(Player.Handle, BuiltInStatIndices.PickupRange, out var pickupRange))
                return;
            var attractionRangeSquared = pickupRange * pickupRange;
            for (var index = 0; index < world.Pickups.Count; index++)
            {
                var handle = world.Pickups.GetHandleAt(index);
                if (!TryGetPickup(handle, out var record) || record.Collected) continue;
                var state = world.Pickups.GetStateAt(index);
                var offset = playerState.Position - state.Position;
                var distanceSquared = offset.LengthSquared();
                if (distanceSquared <= PickupCollectionDistanceSquared)
                {
                    record.Collected = true;
                    pickups[handle.Index] = record;
                    pendingExperience += record.Value;
                    experienceCollected += record.Value;
                    pickupsCollected++;
                    world.Commands.Remove(EntityKind.Pickup, handle);
                    world.QueueSkillTrigger(new SkillTriggerContext(
                        SkillTriggerEventType.OnPickup,
                        Player,
                        new SpatialEntity(EntityKind.Pickup, handle),
                        state.Position,
                        Vector2.Zero,
                        default,
                        default,
                        0));
                }
                else if (distanceSquared <= attractionRangeSquared && distanceSquared > 0f)
                {
                    state.Velocity = Vector2.Normalize(offset) * PickupAttractionSpeed;
                    world.Pickups.SetStateAt(index, state);
                }
                else
                {
                    state.Velocity = Vector2.Zero;
                    world.Pickups.SetStateAt(index, state);
                }
            }
        }

        internal void ApplyPendingExperience()
        {
            if (pendingExperience <= 0f) return;
            Experience.Gain(pendingExperience);
            pendingExperience = 0f;
        }

        internal void RequestNextChoice()
        {
            if (CurrentOffers != null || Experience.PendingLevelUps <= 0) return;
            CurrentOffers = Offers.Generate(Build);
            if (CurrentOffers.Count == 0)
            {
                Offers.RecordSkip(CurrentOffers);
                Experience.ConsumeLevelUpRequest();
                offersSkipped++;
                MixDecision(default, OfferHistoryAction.Skip);
                CurrentOffers = null;
                return;
            }
            PauseRequested = true;
        }

        internal void ApplyPendingPickups(SimulationWorld world)
        {
            for (var index = 0; index < pendingPickupCount; index++)
            {
                var request = pendingPickups[index];
                var state = SimulationEntityState.Create(
                    request.Position,
                    Vector2.Zero,
                    0f,
                    PickupLifetimeSeconds);
                var handle = world.CreatePickup(state);
                EnsurePickupCapacity(handle.Index + 1);
                pickups[handle.Index] = new ExperiencePickupRecord
                {
                    Generation = handle.Generation,
                    Value = request.Value,
                    Collected = false
                };
                world.EmitEvent(SimulationEventType.Created, EntityKind.Pickup, handle, request.Position);
            }
            Array.Clear(pendingPickups, 0, pendingPickupCount);
            pendingPickupCount = 0;
        }

        internal void OnPickupRemoved(EntityHandle handle)
        {
            if (handle.IsValid && handle.Index < pickups.Length && pickups[handle.Index].Generation == handle.Generation)
                pickups[handle.Index] = default;
        }

        private void QueueExperiencePickup(Vector2 position, float value)
        {
            EnsurePendingCapacity(pendingPickupCount + 1);
            pendingPickups[pendingPickupCount++] = new PendingExperiencePickup(position, value);
        }

        private bool TryGetPickup(EntityHandle handle, out ExperiencePickupRecord record)
        {
            if (handle.IsValid && handle.Index < pickups.Length)
            {
                record = pickups[handle.Index];
                if (record.Generation == handle.Generation && record.Value > 0f) return true;
            }
            record = default;
            return false;
        }

        private bool TryFindCurrentOffer(ContentId offerId, out CompiledUpgradeOfferDefinition offer)
        {
            if (CurrentOffers != null)
            {
                for (var index = 0; index < CurrentOffers.Count; index++)
                {
                    var candidate = CurrentOffers.GetAt(index);
                    if (candidate.Source.Id == offerId)
                    {
                        offer = candidate;
                        return true;
                    }
                }
            }
            offer = null;
            return false;
        }

        private void ClearChoice()
        {
            CurrentOffers = null;
            PauseRequested = false;
        }

        private void MixDecision(ContentId id, OfferHistoryAction action)
        {
            unchecked
            {
                decisionChecksum ^= (byte)action;
                decisionChecksum *= 1099511628211UL;
                var text = id.Value ?? string.Empty;
                for (var index = 0; index < text.Length; index++)
                {
                    decisionChecksum ^= text[index];
                    decisionChecksum *= 1099511628211UL;
                }
            }
        }

        private void EnsurePickupCapacity(int required)
        {
            if (required <= pickups.Length) return;
            var size = pickups.Length * 2;
            while (size < required) size *= 2;
            Array.Resize(ref pickups, size);
        }

        private void EnsurePendingCapacity(int required)
        {
            if (required <= pendingPickups.Length) return;
            var size = pendingPickups.Length * 2;
            while (size < required) size *= 2;
            Array.Resize(ref pendingPickups, size);
        }
    }
}
