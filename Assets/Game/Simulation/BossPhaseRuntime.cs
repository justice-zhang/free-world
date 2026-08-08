using System;
using System.Collections.Generic;
using Game.Content.Runtime;
using Game.Core;

namespace Game.Simulation
{
    public enum BossOwnedEffectKind : byte
    {
        Skill = 1,
        Projectile = 2,
        Area = 3
    }

    public enum BossOwnedEffectState : byte
    {
        Active = 1,
        TelegraphOnly = 2,
        Expired = 3
    }

    public readonly struct BossOwnedEffectHandle : IEquatable<BossOwnedEffectHandle>
    {
        public BossOwnedEffectHandle(int value) { Value = value; }
        public int Value { get; }
        public bool IsValid => Value > 0;
        public bool Equals(BossOwnedEffectHandle other) => Value == other.Value;
        public override bool Equals(object obj) => obj is BossOwnedEffectHandle other && Equals(other);
        public override int GetHashCode() => Value;
    }

    public readonly struct BossOwnedEffectSnapshot
    {
        internal BossOwnedEffectSnapshot(
            BossOwnedEffectKind kind,
            BossOwnedEffectState state,
            int originPhase,
            BossPhaseCleanupPolicy cleanupPolicy,
            bool damageEnabled)
        {
            Kind = kind;
            State = state;
            OriginPhase = originPhase;
            CleanupPolicy = cleanupPolicy;
            DamageEnabled = damageEnabled;
        }

        public BossOwnedEffectKind Kind { get; }
        public BossOwnedEffectState State { get; }
        public int OriginPhase { get; }
        public BossPhaseCleanupPolicy CleanupPolicy { get; }
        public bool DamageEnabled { get; }
    }

    public readonly struct BossRuleModifierSnapshot
    {
        internal BossRuleModifierSnapshot(
            byte activeRuleMask,
            float spatialLoadMultiplier,
            float deceptionMultiplier,
            float cadenceIntervalMultiplier,
            bool bonusOutputEligible)
        {
            ActiveRuleMask = activeRuleMask;
            SpatialLoadMultiplier = spatialLoadMultiplier;
            DeceptionMultiplier = deceptionMultiplier;
            CadenceIntervalMultiplier = cadenceIntervalMultiplier;
            BonusOutputEligible = bonusOutputEligible;
        }

        public byte ActiveRuleMask { get; }
        public float SpatialLoadMultiplier { get; }
        public float DeceptionMultiplier { get; }
        public float CadenceIntervalMultiplier { get; }
        public bool BonusOutputEligible { get; }
    }

    public readonly struct BossSnapshot
    {
        internal BossSnapshot(
            ContentId bossId,
            int phase,
            int phaseCount,
            byte activeRuleMask,
            bool deathFinalized)
        {
            BossId = bossId;
            Phase = phase;
            PhaseCount = phaseCount;
            ActiveRuleMask = activeRuleMask;
            DeathFinalized = deathFinalized;
        }

        public ContentId BossId { get; }
        public int Phase { get; }
        public int PhaseCount { get; }
        public byte ActiveRuleMask { get; }
        public bool DeathFinalized { get; }
    }

    public readonly struct BossPhaseTransition
    {
        internal BossPhaseTransition(
            int fromPhase,
            int toPhase,
            int expiredEffects,
            int telegraphOnlyEffects)
        {
            FromPhase = fromPhase;
            ToPhase = toPhase;
            ExpiredEffects = expiredEffects;
            TelegraphOnlyEffects = telegraphOnlyEffects;
        }

        public int FromPhase { get; }
        public int ToPhase { get; }
        public int CrossedPhaseCount => Math.Max(0, ToPhase - FromPhase);
        public int ExpiredEffects { get; }
        public int TelegraphOnlyEffects { get; }
        public bool Changed => ToPhase != FromPhase;
    }

    /// <summary>
    /// Fixed-capacity, content-driven Boss phase owner. It never branches on a concrete Boss ID;
    /// phase skill and objective-rule meanings are resolved from the loaded registry at attach time.
    /// </summary>
    public sealed class BossPhaseRuntime
    {
        private const int MaximumBoundRules = 3;
        private const int MaximumBoundSkills = 64;
        private static readonly ContentTag ControlTag = CreateTag("status.control");

        private struct BossEntry
        {
            public EntityHandle Owner;
            public RuntimeBossDefinition Definition;
            public ContentRegistry Registry;
            public SkillRuntime SkillRuntime;
            public int Phase;
            public byte ActiveRuleMask;
            public byte BoundRuleCount;
            public byte BoundSkillCount;
            public bool DeathFinalized;
        }

        private struct OwnedEffectEntry
        {
            public BossOwnedEffectHandle Handle;
            public EntityHandle Owner;
            public BossOwnedEffectKind Kind;
            public BossOwnedEffectState State;
            public BossPhaseCleanupPolicy CleanupPolicy;
            public int OriginPhase;
            public bool DamageEnabled;
            public bool TelegraphPending;
        }

        private readonly BossEntry[] entries;
        private readonly ContentId[] boundRuleIds;
        private readonly ContentId[] boundSkillIds;
        private readonly SkillInstanceHandle[] boundSkillHandles;
        private readonly OwnedEffectEntry[] effects;
        private int nextEffectHandle = 1;

        public BossPhaseRuntime()
            : this(4, 128)
        {
        }

        public BossPhaseRuntime(int bossCapacity, int ownedEffectCapacity)
        {
            if (bossCapacity < 1) throw new ArgumentOutOfRangeException(nameof(bossCapacity));
            if (ownedEffectCapacity < 1) throw new ArgumentOutOfRangeException(nameof(ownedEffectCapacity));
            entries = new BossEntry[bossCapacity];
            boundRuleIds = new ContentId[bossCapacity * MaximumBoundRules];
            boundSkillIds = new ContentId[bossCapacity * MaximumBoundSkills];
            boundSkillHandles = new SkillInstanceHandle[bossCapacity * MaximumBoundSkills];
            effects = new OwnedEffectEntry[ownedEffectCapacity];
        }

        public int ActiveCount { get; private set; }
        public int CompletedCount { get; private set; }

        public int ResolvePhase(RuntimeBossDefinition definition, int currentPhase, float healthFraction, bool lethal)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (float.IsNaN(healthFraction) || float.IsInfinity(healthFraction))
                throw new ArgumentOutOfRangeException(nameof(healthFraction));
            if (lethal) return definition.Phases.Count;
            var phase = Math.Max(0, currentPhase);
            while (phase < definition.Phases.Count &&
                   healthFraction <= definition.Phases[phase].HealthThreshold)
            {
                phase++;
            }
            return phase;
        }

        public bool TryAttach(
            EntityHandle owner,
            RuntimeBossDefinition definition,
            ContentRegistry registry)
        {
            if (!owner.IsValid || definition == null || registry == null || Find(owner) >= 0) return false;
            var slot = FindFree();
            if (slot < 0) return false;
            entries[slot] = new BossEntry
            {
                Owner = owner,
                Definition = definition,
                Registry = registry,
                Phase = 0
            };
            BindObjectiveRules(slot);
            ActiveCount++;
            return true;
        }

        internal bool TryAttachWorld(
            EntityHandle owner,
            RuntimeBossDefinition definition,
            ContentRegistry registry,
            SkillRuntime skills,
            SkillInstanceHandle baseSkillHandle)
        {
            if (skills == null || !TryAttach(owner, definition, registry)) return false;
            var slot = Find(owner);
            var entry = entries[slot];
            entry.SkillRuntime = skills;
            entries[slot] = entry;
            if (!BindPhaseSkills(slot, baseSkillHandle))
            {
                Detach(owner);
                return false;
            }
            SyncPhaseSkills(slot);
            return true;
        }

        public bool TryGet(EntityHandle owner, out BossSnapshot snapshot)
        {
            var slot = Find(owner);
            if (slot < 0)
            {
                snapshot = default;
                return false;
            }
            var entry = entries[slot];
            snapshot = new BossSnapshot(
                entry.Definition.Id,
                entry.Phase,
                entry.Definition.Phases.Count,
                entry.ActiveRuleMask,
                entry.DeathFinalized);
            return true;
        }

        public bool TrySetRuleState(EntityHandle owner, ContentId ruleId, bool active)
        {
            var slot = Find(owner);
            if (slot < 0 || !ruleId.IsValid) return false;
            var entry = entries[slot];
            for (var index = 0; index < entry.BoundRuleCount; index++)
            {
                if (boundRuleIds[slot * MaximumBoundRules + index] != ruleId) continue;
                var bit = (byte)(1 << index);
                entry.ActiveRuleMask = active
                    ? (byte)(entry.ActiveRuleMask | bit)
                    : (byte)(entry.ActiveRuleMask & ~bit);
                entries[slot] = entry;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Synchronizes every content-bound objective rule from the run-owned map runtime.
        /// This keeps Boss modifiers data-driven and lets objectives completed before or
        /// during a fight affect the next Boss tick without a concrete Boss ID branch.
        /// </summary>
        internal void SyncObjectiveRules(MapObjectiveRuntime objectives)
        {
            if (objectives == null || !objectives.IsInitialized) return;
            for (var slot = 0; slot < entries.Length; slot++)
            {
                var entry = entries[slot];
                if (!entry.Owner.IsValid || entry.Definition == null) continue;
                byte mask = 0;
                for (var rule = 0; rule < entry.BoundRuleCount; rule++)
                {
                    var id = boundRuleIds[slot * MaximumBoundRules + rule];
                    if (objectives.IsObjectiveCompleted(id)) mask |= (byte)(1 << rule);
                }
                entry.ActiveRuleMask = mask;
                entries[slot] = entry;
            }
        }

        public bool TryAdvance(
            EntityHandle owner,
            float healthFraction,
            bool lethal,
            out BossPhaseTransition transition)
        {
            var slot = Find(owner);
            if (slot < 0)
            {
                transition = default;
                return false;
            }
            var entry = entries[slot];
            var previous = entry.Phase;
            var next = ResolvePhase(entry.Definition, previous, healthFraction, lethal);
            var expired = 0;
            var telegraphs = 0;
            if (next > previous)
            {
                CleanupExitedPhases(owner, previous, next, lethal, ref expired, ref telegraphs);
                entry.Phase = next;
                entries[slot] = entry;
                SyncPhaseSkills(slot);
            }
            transition = new BossPhaseTransition(previous, next, expired, telegraphs);
            return true;
        }

        public bool TryFinalizeDeath(
            EntityHandle owner,
            ulong runId,
            out RewardTransactionId transaction,
            out ContentId rewardId)
        {
            transaction = default;
            rewardId = default;
            var slot = Find(owner);
            if (slot < 0 || entries[slot].DeathFinalized) return false;
            TryAdvance(owner, 0f, true, out _);
            var entry = entries[slot];
            entry.DeathFinalized = true;
            entries[slot] = entry;
            CompletedCount++;
            rewardId = entry.Definition.RewardId;
            if (rewardId.IsValid)
                transaction = new RewardTransactionId(runId, entry.Definition.Id, 0);
            return true;
        }

        public bool TryGetModifierSnapshot(EntityHandle owner, out BossRuleModifierSnapshot snapshot)
        {
            var slot = Find(owner);
            if (slot < 0)
            {
                snapshot = default;
                return false;
            }
            var entry = entries[slot];
            var effective = EffectiveRuleMask(slot, entry);
            snapshot = new BossRuleModifierSnapshot(
                effective,
                (effective & 1) != 0 ? 0.70f : 1f,
                (effective & 2) != 0 ? 0.65f : 1f,
                (effective & 4) != 0 ? 1.25f : 1f,
                effective == 7);
            return true;
        }

        public int GetCurrentPhaseSkillCount(EntityHandle owner)
        {
            var slot = Find(owner);
            if (slot < 0) return 0;
            var entry = entries[slot];
            if (entry.Phase >= entry.Definition.Phases.Count) return 0;
            var rules = entry.Definition.Phases[entry.Phase].AcceptedRuleIds;
            var count = 0;
            for (var index = 0; index < rules.Count; index++)
                if (entry.Registry.TryGet(rules[index], out RuntimeSkillDefinition _)) count++;
            return count;
        }

        public bool TryGetCurrentPhaseSkill(EntityHandle owner, int skillIndex, out ContentId skillId)
        {
            skillId = default;
            if (skillIndex < 0) return false;
            var slot = Find(owner);
            if (slot < 0) return false;
            var entry = entries[slot];
            if (entry.Phase >= entry.Definition.Phases.Count) return false;
            var rules = entry.Definition.Phases[entry.Phase].AcceptedRuleIds;
            var current = 0;
            for (var index = 0; index < rules.Count; index++)
            {
                if (!entry.Registry.TryGet(rules[index], out RuntimeSkillDefinition _)) continue;
                if (current++ != skillIndex) continue;
                skillId = rules[index];
                return true;
            }
            return false;
        }

        public float ResolveStatusDuration(
            EntityHandle owner,
            RuntimeStatusDefinition status,
            float unmodifiedDuration)
        {
            var slot = Find(owner);
            if (slot < 0 || status == null || !Contains(status.Tags, ControlTag)) return unmodifiedDuration;
            return Math.Max(0.1f, unmodifiedDuration * entries[slot].Definition.ResistanceMultiplier);
        }

        public bool TryTrackOwnedEffect(
            EntityHandle owner,
            BossOwnedEffectKind kind,
            bool telegraphPending,
            out BossOwnedEffectHandle handle)
        {
            handle = default;
            var ownerSlot = Find(owner);
            if (ownerSlot < 0 || kind < BossOwnedEffectKind.Skill || kind > BossOwnedEffectKind.Area)
                return false;
            for (var index = 0; index < effects.Length; index++)
            {
                if (effects[index].Handle.IsValid && effects[index].State != BossOwnedEffectState.Expired)
                    continue;
                handle = new BossOwnedEffectHandle(nextEffectHandle++);
                var phase = entries[ownerSlot].Phase;
                effects[index] = new OwnedEffectEntry
                {
                    Handle = handle,
                    Owner = owner,
                    Kind = kind,
                    State = BossOwnedEffectState.Active,
                    CleanupPolicy = phase < entries[ownerSlot].Definition.Phases.Count
                        ? entries[ownerSlot].Definition.Phases[phase].CleanupPolicy
                        : BossPhaseCleanupPolicy.ExpireOnPhaseExit,
                    OriginPhase = phase,
                    DamageEnabled = true,
                    TelegraphPending = telegraphPending
                };
                return true;
            }
            return false;
        }

        public bool TryGetOwnedEffect(
            BossOwnedEffectHandle handle,
            out BossOwnedEffectSnapshot snapshot)
        {
            var slot = FindEffect(handle);
            if (slot < 0)
            {
                snapshot = default;
                return false;
            }
            var effect = effects[slot];
            snapshot = new BossOwnedEffectSnapshot(
                effect.Kind,
                effect.State,
                effect.OriginPhase,
                effect.CleanupPolicy,
                effect.DamageEnabled);
            return true;
        }

        public bool TryFinishTelegraph(BossOwnedEffectHandle handle)
        {
            var slot = FindEffect(handle);
            if (slot < 0 || effects[slot].State != BossOwnedEffectState.TelegraphOnly) return false;
            var effect = effects[slot];
            effect.State = BossOwnedEffectState.Expired;
            effect.DamageEnabled = false;
            effects[slot] = effect;
            return true;
        }

        internal void Detach(EntityHandle owner)
        {
            var slot = Find(owner);
            if (slot < 0) return;
            for (var index = 0; index < effects.Length; index++)
            {
                if (effects[index].Owner != owner) continue;
                var effect = effects[index];
                effect.State = BossOwnedEffectState.Expired;
                effect.DamageEnabled = false;
                effects[index] = effect;
            }
            var entry = entries[slot];
            if (entry.SkillRuntime != null)
            {
                for (var index = 0; index < entry.BoundSkillCount; index++)
                    entry.SkillRuntime.RemoveInstance(
                        boundSkillHandles[slot * MaximumBoundSkills + index]);
            }
            Array.Clear(boundRuleIds, slot * MaximumBoundRules, MaximumBoundRules);
            Array.Clear(boundSkillIds, slot * MaximumBoundSkills, MaximumBoundSkills);
            Array.Clear(boundSkillHandles, slot * MaximumBoundSkills, MaximumBoundSkills);
            entries[slot] = default;
            ActiveCount--;
        }

        internal void Tick(SimulationWorld world)
        {
            for (var index = 0; index < entries.Length; index++)
            {
                var entry = entries[index];
                if (entry.Definition == null || entry.DeathFinalized ||
                    !world.Actors.TryGetCombat(entry.Owner, out var combat))
                    continue;
                var health = combat.GetHealth();
                var fraction = health.Maximum <= 0f ? 0f : health.Current / health.Maximum;
                if (TryAdvance(
                        entry.Owner,
                        fraction,
                        combat.DeathPending || combat.Dead,
                        out var transition) && transition.Changed)
                    CleanupPhaseDeliveries(world, index, transition);
            }
        }

        private bool BindPhaseSkills(int slot, SkillInstanceHandle baseSkillHandle)
        {
            var entry = entries[slot];
            RuntimeContentIndex baseIndex = default;
            if (baseSkillHandle.IsValid &&
                entry.SkillRuntime.TryGetInstance(baseSkillHandle, out var baseInstance))
                baseIndex = baseInstance.Definition.Index;

            for (var phaseIndex = 0; phaseIndex < entry.Definition.Phases.Count; phaseIndex++)
            {
                var rules = entry.Definition.Phases[phaseIndex].AcceptedRuleIds;
                for (var ruleIndex = 0; ruleIndex < rules.Count; ruleIndex++)
                {
                    var id = rules[ruleIndex];
                    if (!entry.Registry.TryGet(id, out var registryEntry) ||
                        !(registryEntry.Definition is RuntimeSkillDefinition))
                        continue;
                    if (FindBoundSkill(slot, entry.BoundSkillCount, id) >= 0) continue;
                    if (entry.BoundSkillCount >= MaximumBoundSkills) return false;
                    SkillInstanceHandle handle;
                    if (registryEntry.Index == baseIndex)
                    {
                        handle = baseSkillHandle;
                    }
                    else
                    {
                        var added = entry.SkillRuntime.AddInstance(
                            new SpatialEntity(EntityKind.Actor, entry.Owner),
                            registryEntry.Index);
                        if (!added.IsSuccess) return false;
                        handle = added.Value;
                    }
                    if (!handle.IsValid) return false;
                    var offset = slot * MaximumBoundSkills + entry.BoundSkillCount;
                    boundSkillIds[offset] = id;
                    boundSkillHandles[offset] = handle;
                    entry.BoundSkillCount++;
                    // Publish progress incrementally so the failure path can detach every
                    // instance that was already created before a later capacity error.
                    entries[slot] = entry;
                }
            }
            entries[slot] = entry;
            return entry.BoundSkillCount > 0;
        }

        private void SyncPhaseSkills(int slot)
        {
            var entry = entries[slot];
            if (entry.SkillRuntime == null) return;
            for (var index = 0; index < entry.BoundSkillCount; index++)
            {
                var offset = slot * MaximumBoundSkills + index;
                entry.SkillRuntime.SetSuppressed(
                    boundSkillHandles[offset],
                    !IsSkillAccepted(entry, boundSkillIds[offset]));
            }
        }

        private void CleanupPhaseDeliveries(
            SimulationWorld world,
            int slot,
            in BossPhaseTransition transition)
        {
            var entry = entries[slot];
            if (entry.SkillRuntime == null) return;
            var lastExited = Math.Min(transition.ToPhase, entry.Definition.Phases.Count);
            for (var phase = transition.FromPhase; phase < lastExited; phase++)
            {
                var cleanup = entry.Definition.Phases[phase].CleanupPolicy;
                if (cleanup == BossPhaseCleanupPolicy.Persist) continue;
                var rules = entry.Definition.Phases[phase].AcceptedRuleIds;
                for (var ruleIndex = 0; ruleIndex < rules.Count; ruleIndex++)
                {
                    var skill = FindBoundSkill(slot, entry.BoundSkillCount, rules[ruleIndex]);
                    if (skill < 0) continue;
                    var handle = boundSkillHandles[slot * MaximumBoundSkills + skill];
                    if (cleanup == BossPhaseCleanupPolicy.FinishCurrentTelegraph)
                        entry.SkillRuntime.DisableDeliveries(world, handle);
                    else
                        entry.SkillRuntime.ExpireDeliveries(world, handle);
                }
            }
        }

        private static bool IsSkillAccepted(in BossEntry entry, ContentId id)
        {
            if (entry.Phase >= entry.Definition.Phases.Count) return false;
            var accepted = entry.Definition.Phases[entry.Phase].AcceptedRuleIds;
            for (var index = 0; index < accepted.Count; index++)
                if (accepted[index] == id) return true;
            return false;
        }

        private int FindBoundSkill(int slot, int count, ContentId id)
        {
            for (var index = 0; index < count; index++)
                if (boundSkillIds[slot * MaximumBoundSkills + index] == id) return index;
            return -1;
        }

        private void BindObjectiveRules(int slot)
        {
            var entry = entries[slot];
            for (var phaseIndex = 0; phaseIndex < entry.Definition.Phases.Count; phaseIndex++)
            {
                var rules = entry.Definition.Phases[phaseIndex].AcceptedRuleIds;
                for (var ruleIndex = 0; ruleIndex < rules.Count; ruleIndex++)
                {
                    var id = rules[ruleIndex];
                    if (!entry.Registry.TryGet(id, out RuntimeMapObjectiveDefinition _)) continue;
                    if (IsBound(slot, entry.BoundRuleCount, id)) continue;
                    if (entry.BoundRuleCount >= MaximumBoundRules)
                        throw new InvalidOperationException("Boss definitions support at most three bound map rules.");
                    boundRuleIds[slot * MaximumBoundRules + entry.BoundRuleCount++] = id;
                }
            }
            entries[slot] = entry;
        }

        private byte EffectiveRuleMask(int slot, in BossEntry entry)
        {
            if (entry.Phase >= entry.Definition.Phases.Count) return 0;
            var result = 0;
            var accepted = entry.Definition.Phases[entry.Phase].AcceptedRuleIds;
            for (var rule = 0; rule < entry.BoundRuleCount; rule++)
            {
                var bit = 1 << rule;
                if ((entry.ActiveRuleMask & bit) == 0) continue;
                var id = boundRuleIds[slot * MaximumBoundRules + rule];
                for (var index = 0; index < accepted.Count; index++)
                {
                    if (accepted[index] != id) continue;
                    result |= bit;
                    break;
                }
            }
            return (byte)result;
        }

        private void CleanupExitedPhases(
            EntityHandle owner,
            int fromPhase,
            int toPhase,
            bool lethal,
            ref int expired,
            ref int telegraphs)
        {
            for (var index = 0; index < effects.Length; index++)
            {
                var effect = effects[index];
                if (effect.Owner != owner || effect.State == BossOwnedEffectState.Expired ||
                    effect.OriginPhase < fromPhase || effect.OriginPhase >= toPhase)
                    continue;
                if (!lethal && effect.CleanupPolicy == BossPhaseCleanupPolicy.Persist) continue;
                if (!lethal && effect.CleanupPolicy == BossPhaseCleanupPolicy.FinishCurrentTelegraph &&
                    effect.TelegraphPending)
                {
                    effect.State = BossOwnedEffectState.TelegraphOnly;
                    effect.DamageEnabled = false;
                    effects[index] = effect;
                    telegraphs++;
                    continue;
                }
                effect.State = BossOwnedEffectState.Expired;
                effect.DamageEnabled = false;
                effects[index] = effect;
                expired++;
            }
        }

        private bool IsBound(int slot, int count, ContentId id)
        {
            for (var index = 0; index < count; index++)
                if (boundRuleIds[slot * MaximumBoundRules + index] == id) return true;
            return false;
        }

        private int Find(EntityHandle owner)
        {
            for (var index = 0; index < entries.Length; index++)
                if (entries[index].Owner == owner && entries[index].Definition != null) return index;
            return -1;
        }

        private int FindFree()
        {
            for (var index = 0; index < entries.Length; index++)
                if (entries[index].Definition == null) return index;
            return -1;
        }

        private int FindEffect(BossOwnedEffectHandle handle)
        {
            if (!handle.IsValid) return -1;
            for (var index = 0; index < effects.Length; index++)
                if (effects[index].Handle.Equals(handle)) return index;
            return -1;
        }

        private static bool Contains(IReadOnlyList<ContentTag> tags, ContentTag expected)
        {
            for (var index = 0; index < tags.Count; index++)
                if (tags[index] == expected) return true;
            return false;
        }

        private static ContentTag CreateTag(string value)
        {
            var result = ContentTag.Create(value);
            if (!result.IsSuccess) throw new InvalidOperationException(result.Error.Message);
            return result.Value;
        }
    }
}
