using System;
using System.Collections.Generic;
using Game.Content.Runtime;
using Game.Core;

namespace Game.Simulation
{
    internal interface IBuildEffectProvider
    {
        void QueueAddedEffects(
            RuntimeContentIndex skillIndex,
            SkillExecutionCommandBuffer commands,
            in SkillEffectContext context);
    }

    /// <summary>Centralized evaluator for all five schema-5 build condition operations.</summary>
    public static class BuildConditionEvaluator
    {
        public static bool Evaluate(in CompiledBuildCondition condition, BuildState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            var source = condition.Source;
            if (source.Type == BuildConditionType.OwnsContent)
                return state.OwnsContent(source.ContentId);
            if (source.Type == BuildConditionType.HasTagCount)
                return state.GetTagCount(source.Tag) >= source.IntegerValue;
            if (source.Type == BuildConditionType.SkillLevelAtLeast)
                return state.Skills.TryGet(source.ContentId, out var skill, out _) &&
                       skill.Level >= source.IntegerValue;
            if (source.Type == BuildConditionType.StatAtLeast)
                return state.TryReadStat(condition.StatIndex, out var value) &&
                       value >= source.FloatValue;
            return source.Type == BuildConditionType.MapHasTag && state.MapHasTag(source.Tag);
        }
    }

    /// <summary>
    /// Run-local build truth: inventories, traits, tags, activated synergies,
    /// evolution eligibility, modifiers, and skill effect overlays.
    /// </summary>
    public sealed class BuildState : IBuildEffectProvider
    {
        private struct ModifierBinding
        {
            public ContentId SourceId;
            public ModifierHandle Handle;
        }

        private readonly BuildRuntimeCatalog catalog;
        private readonly ActorStore actors;
        private readonly SkillRuntime skillRuntime;
        private readonly SpatialEntity owner;
        private readonly Dictionary<ContentId, SkillInstanceHandle> skillInstances;
        private ContentId[] traits = new ContentId[4];
        private ContentId[] activeSynergies = new ContentId[4];
        private ContentId[] eligibleEvolutions = new ContentId[4];
        private ContentId[] unlockedOffers = new ContentId[8];
        private ContentTag[] tags = new ContentTag[16];
        private int[] tagCounts = new int[16];
        private ContentTag[] mapTags = Array.Empty<ContentTag>();
        private ModifierBinding[] modifierBindings = new ModifierBinding[16];
        private CompiledSynergyOutput[] addedEffects = new CompiledSynergyOutput[8];
        private int traitCount;
        private int activeSynergyCount;
        private int eligibleEvolutionCount;
        private int unlockedOfferCount;
        private int tagCount;
        private int modifierBindingCount;
        private int addedEffectCount;

        public BuildState(
            BuildRuntimeCatalog runtimeCatalog,
            ActorStore actorStore,
            SkillRuntime skills,
            SpatialEntity player,
            int skillSlots = 6,
            int passiveSlots = 6,
            ContentTag[] runMapTags = null)
        {
            catalog = runtimeCatalog ?? throw new ArgumentNullException(nameof(runtimeCatalog));
            actors = actorStore ?? throw new ArgumentNullException(nameof(actorStore));
            skillRuntime = skills ?? throw new ArgumentNullException(nameof(skills));
            if (player.Kind != EntityKind.Actor || !actors.Contains(player.Handle))
                throw new ArgumentException("Build owner must be a live actor.", nameof(player));
            owner = player;
            Skills = new SkillInventory(skillSlots);
            Passives = new PassiveInventory(passiveSlots);
            skillInstances = new Dictionary<ContentId, SkillInstanceHandle>(skillSlots);
            mapTags = runMapTags == null ? Array.Empty<ContentTag>() : (ContentTag[])runMapTags.Clone();
            for (var index = 0; index < catalog.Offers.Count; index++)
                if (catalog.Offers[index].Source.InitiallyUnlocked)
                    AddUnique(ref unlockedOffers, ref unlockedOfferCount, catalog.Offers[index].Source.Id);
            skillRuntime.SetBuildEffectProvider(this);
            RebuildTags();
            RecomputeEvolutionEligibility();
        }

        public SkillInventory Skills { get; }
        public PassiveInventory Passives { get; }
        public int TraitCount => traitCount;
        public int ActiveSynergyCount => activeSynergyCount;
        public int EligibleEvolutionCount => eligibleEvolutionCount;
        public int UnlockedOfferCount => unlockedOfferCount;
        public int Revision { get; private set; }

        public ContentId GetTraitAt(int index) => GetAt(traits, TraitCount, index);
        public ContentId GetActiveSynergyAt(int index) => GetAt(activeSynergies, ActiveSynergyCount, index);
        public ContentId GetEligibleEvolutionAt(int index) => GetAt(eligibleEvolutions, EligibleEvolutionCount, index);

        public bool OwnsContent(ContentId id)
        {
            if (!id.IsValid) return false;
            if (Skills.TryGet(id, out _, out _) || Passives.TryGet(id, out _, out _)) return true;
            return Contains(traits, TraitCount, id) || Contains(activeSynergies, ActiveSynergyCount, id);
        }

        public int GetTagCount(ContentTag tag)
        {
            for (var index = 0; index < tagCount; index++)
                if (tags[index] == tag) return tagCounts[index];
            return 0;
        }

        public bool MapHasTag(ContentTag tag)
        {
            for (var index = 0; index < mapTags.Length; index++)
                if (mapTags[index] == tag) return true;
            return false;
        }

        public bool IsOfferUnlocked(ContentId offerId) => Contains(unlockedOffers, UnlockedOfferCount, offerId);
        public bool IsEvolutionEligible(ContentId evolutionId) => Contains(eligibleEvolutions, EligibleEvolutionCount, evolutionId);

        public bool CanAcceptOffer(CompiledUpgradeOfferDefinition offer)
        {
            if (offer == null || !IsOfferUnlocked(offer.Source.Id)) return false;
            for (var index = 0; index < offer.Source.MutuallyExclusiveIds.Count; index++)
                if (OwnsContent(offer.Source.MutuallyExclusiveIds[index])) return false;
            if (!EvaluateAll(offer.Prerequisites)) return false;
            if (offer.TargetKind == UpgradeTargetKind.Skill)
            {
                if (Skills.TryGet(offer.Source.TargetContentId, out var existing, out _))
                    return !existing.IsMaximumLevel;
                return Skills.Count < Skills.SlotCount;
            }
            if (offer.TargetKind == UpgradeTargetKind.Passive)
            {
                if (Passives.TryGet(offer.Source.TargetContentId, out var existing, out _))
                    return !existing.IsMaximumLevel;
                return Passives.Count < Passives.SlotCount;
            }
            return IsEvolutionEligible(offer.Source.TargetContentId);
        }

        public bool ApplyOffer(
            CompiledUpgradeOfferDefinition offer,
            InventoryReplacementPolicy replacementPolicy = InventoryReplacementPolicy.Reject,
            int replacementSlot = -1)
        {
            if (offer == null) return false;
            bool changed;
            if (offer.TargetKind == UpgradeTargetKind.Skill)
                changed = AcquireSkill(offer.Source.TargetContentId, offer.TargetIndex, offer.TargetMaximumLevel, replacementPolicy, replacementSlot);
            else if (offer.TargetKind == UpgradeTargetKind.Passive)
                changed = AcquirePassive(offer.Source.TargetContentId, offer.TargetIndex, offer.TargetMaximumLevel, replacementPolicy, replacementSlot);
            else
                changed = ApplyEvolution(offer.Source.TargetContentId);
            if (!changed) return false;
            RefreshDerivedState();
            return true;
        }

        /// <summary>Grants or levels an initial/runtime skill without coupling it to an offer.</summary>
        public bool TryAcquireSkill(
            ContentId skillId,
            InventoryReplacementPolicy replacementPolicy = InventoryReplacementPolicy.Reject,
            int replacementSlot = -1)
        {
            if (!catalog.TryGetDefinition(skillId, out var definition) ||
                !(definition is RuntimeSkillDefinition skill) || !skill.IsExecutable ||
                !catalog.TryGetIndex(skillId, out var index) ||
                !AcquireSkill(skillId, index, skill.MaximumLevel, replacementPolicy, replacementSlot))
                return false;
            RefreshDerivedState();
            return true;
        }

        /// <summary>Grants or levels an initial/runtime passive without coupling it to an offer.</summary>
        public bool TryAcquirePassive(
            ContentId passiveId,
            InventoryReplacementPolicy replacementPolicy = InventoryReplacementPolicy.Reject,
            int replacementSlot = -1)
        {
            if (!catalog.TryGetPassive(passiveId, out var passive) ||
                !AcquirePassive(passiveId, passive.Index, passive.Source.MaximumLevel, replacementPolicy, replacementSlot))
                return false;
            RefreshDerivedState();
            return true;
        }

        public bool GrantTrait(ContentId traitId)
        {
            if (Contains(traits, TraitCount, traitId) || !catalog.TryGetTrait(traitId, out var trait)) return false;
            AddUnique(ref traits, ref traitCount, traitId);
            for (var index = 0; index < trait.Source.Modifiers.Count; index++)
                AddModifier(traitId, trait.Source.Modifiers[index]);
            Revision++;
            RefreshDerivedState();
            return true;
        }

        public void RefreshDerivedState()
        {
            RebuildTags();
            RecomputeEvolutionEligibility();
            ActivateEligibleSynergies();
            RebuildTags();
            RecomputeEvolutionEligibility();
        }

        internal bool TryReadStat(StatIndex index, out float value) =>
            actors.TryReadStat(owner.Handle, index, out value);

        void IBuildEffectProvider.QueueAddedEffects(
            RuntimeContentIndex skillIndex,
            SkillExecutionCommandBuffer commands,
            in SkillEffectContext context)
        {
            for (var index = 0; index < addedEffectCount; index++)
            {
                var output = addedEffects[index];
                if (output.SourceIndex != skillIndex) continue;
                output.EffectExecutor.Queue(commands, context, output.ResolvedEffect);
            }
        }

        private bool AcquireSkill(
            ContentId id,
            RuntimeContentIndex index,
            int maximumLevel,
            InventoryReplacementPolicy policy,
            int replacementSlot)
        {
            if (Skills.TryGet(id, out var existing, out _))
            {
                if (existing.IsMaximumLevel || !skillInstances.TryGetValue(id, out var handle)) return false;
                var result = Skills.TryAcquire(id, index, maximumLevel, policy, replacementSlot, out _, out _);
                return result == InventoryAcquireResult.Leveled && skillRuntime.SetLevel(handle, existing.Level + 1);
            }

            if (Skills.Count >= Skills.SlotCount && policy == InventoryReplacementPolicy.Reject) return false;
            if (!skillRuntime.CanAddInstance(index)) return false;
            var addedInstance = skillRuntime.AddInstance(owner, index);
            if (!addedInstance.IsSuccess) return false;
            var acquire = Skills.TryAcquire(id, index, maximumLevel, policy, replacementSlot, out _, out var previous);
            if (acquire != InventoryAcquireResult.Added && acquire != InventoryAcquireResult.Replaced)
            {
                skillRuntime.RemoveInstance(addedInstance.Value);
                return false;
            }
            if (acquire == InventoryAcquireResult.Replaced && skillInstances.TryGetValue(previous.ContentId, out var oldHandle))
            {
                skillRuntime.RemoveInstance(oldHandle);
                skillInstances.Remove(previous.ContentId);
            }
            skillInstances[id] = addedInstance.Value;
            Revision++;
            return true;
        }

        private bool AcquirePassive(
            ContentId id,
            RuntimeContentIndex index,
            int maximumLevel,
            InventoryReplacementPolicy policy,
            int replacementSlot)
        {
            var result = Passives.TryAcquire(id, index, maximumLevel, policy, replacementSlot, out _, out var previous);
            if (result == InventoryAcquireResult.Rejected) return false;
            if (result == InventoryAcquireResult.Replaced)
            {
                RemoveModifiers(previous.ContentId);
            }
            if (!catalog.TryGetPassive(id, out var passive)) return false;
            Passives.TryGet(id, out var entry, out _);
            RemoveModifiers(id);
            for (var modifierIndex = 0; modifierIndex < passive.Source.LevelModifiers.Count; modifierIndex++)
            {
                var levelModifier = passive.Source.LevelModifiers[modifierIndex];
                if (levelModifier.Level <= entry.Level) AddModifier(id, levelModifier.Modifier);
            }
            Revision++;
            return true;
        }

        private bool ApplyEvolution(ContentId evolutionId)
        {
            if (!IsEvolutionEligible(evolutionId) || !catalog.TryGetEvolution(evolutionId, out var evolution)) return false;
            if (!catalog.TryGetDefinition(evolution.Source.ResultSkillId, out var resultDefinition) ||
                !(resultDefinition is RuntimeSkillDefinition resultSkill) ||
                !skillRuntime.CanAddInstance(evolution.ResultSkillIndex)) return false;
            var added = skillRuntime.AddInstance(owner, evolution.ResultSkillIndex);
            if (!added.IsSuccess) return false;
            if (!Skills.Transform(
                    evolution.Source.RequiredSkillId,
                    evolution.Source.ResultSkillId,
                    evolution.ResultSkillIndex,
                    resultSkill.MaximumLevel,
                    out _,
                    out var previous))
            {
                skillRuntime.RemoveInstance(added.Value);
                return false;
            }
            if (skillInstances.TryGetValue(previous.ContentId, out var oldHandle))
            {
                skillRuntime.RemoveInstance(oldHandle);
                skillInstances.Remove(previous.ContentId);
            }
            skillInstances[evolution.Source.ResultSkillId] = added.Value;
            if (evolution.Source.ConsumePolicy == EvolutionConsumePolicy.ConsumeRequiredPassives)
            {
                for (var index = 0; index < evolution.Source.RequiredPassiveIds.Count; index++)
                {
                    var passiveId = evolution.Source.RequiredPassiveIds[index];
                    if (Passives.Remove(passiveId, out _)) RemoveModifiers(passiveId);
                }
            }
            Revision++;
            return true;
        }

        private void ActivateEligibleSynergies()
        {
            var activated = true;
            var passes = 0;
            while (activated && passes++ <= catalog.Synergies.Count)
            {
                activated = false;
                for (var index = 0; index < catalog.Synergies.Count; index++)
                {
                    var synergy = catalog.Synergies[index];
                    if (Contains(activeSynergies, ActiveSynergyCount, synergy.Source.Id) || !EvaluateAll(synergy.Conditions)) continue;
                    AddUnique(ref activeSynergies, ref activeSynergyCount, synergy.Source.Id);
                    for (var outputIndex = 0; outputIndex < synergy.Outputs.Count; outputIndex++)
                        ApplySynergyOutput(synergy.Source.Id, synergy.Outputs[outputIndex]);
                    Revision++;
                    RebuildTags();
                    RecomputeEvolutionEligibility();
                    activated = true;
                }
            }
        }

        private void ApplySynergyOutput(ContentId synergyId, CompiledSynergyOutput output)
        {
            var source = output.Source;
            if (source.Type == SynergyOutputType.AddModifier)
                AddModifier(synergyId, source.Modifier);
            else if (source.Type == SynergyOutputType.UnlockOffer)
                AddUnique(ref unlockedOffers, ref unlockedOfferCount, source.TargetId);
            else if (source.Type == SynergyOutputType.AddEffectOp)
            {
                EnsureCapacity(ref addedEffects, addedEffectCount + 1);
                addedEffects[addedEffectCount++] = output;
            }
            else if (source.Type == SynergyOutputType.TransformSkill &&
                     catalog.TryGetDefinition(source.TargetId, out var definition) &&
                     definition is RuntimeSkillDefinition skill &&
                     skillRuntime.CanAddInstance(output.TargetIndex))
            {
                var added = skillRuntime.AddInstance(owner, output.TargetIndex);
                if (added.IsSuccess && Skills.Transform(source.SourceId, source.TargetId, output.TargetIndex, skill.MaximumLevel, out _, out var previous))
                {
                    if (skillInstances.TryGetValue(previous.ContentId, out var oldHandle))
                    {
                        skillRuntime.RemoveInstance(oldHandle);
                        skillInstances.Remove(previous.ContentId);
                    }
                    skillInstances[source.TargetId] = added.Value;
                }
                else if (added.IsSuccess) skillRuntime.RemoveInstance(added.Value);
            }
            else if (source.Type == SynergyOutputType.GrantTrait)
                GrantTraitInternal(source.TargetId);
        }

        private void GrantTraitInternal(ContentId traitId)
        {
            if (Contains(traits, TraitCount, traitId) || !catalog.TryGetTrait(traitId, out var trait)) return;
            AddUnique(ref traits, ref traitCount, traitId);
            for (var index = 0; index < trait.Source.Modifiers.Count; index++) AddModifier(traitId, trait.Source.Modifiers[index]);
        }

        private void RecomputeEvolutionEligibility()
        {
            Array.Clear(eligibleEvolutions, 0, eligibleEvolutionCount);
            eligibleEvolutionCount = 0;
            for (var index = 0; index < catalog.Evolutions.Count; index++)
            {
                var evolution = catalog.Evolutions[index];
                if (!Skills.TryGet(evolution.Source.RequiredSkillId, out var skill, out _) ||
                    skill.Level < evolution.Source.RequiredSkillLevel ||
                    !EvaluateAll(evolution.Conditions)) continue;
                var passivesPresent = true;
                for (var passiveIndex = 0; passiveIndex < evolution.Source.RequiredPassiveIds.Count; passiveIndex++)
                {
                    if (!Passives.TryGet(evolution.Source.RequiredPassiveIds[passiveIndex], out _, out _))
                    {
                        passivesPresent = false;
                        break;
                    }
                }
                if (passivesPresent) AddUnique(ref eligibleEvolutions, ref eligibleEvolutionCount, evolution.Source.Id);
            }
        }

        private bool EvaluateAll(IReadOnlyList<CompiledBuildCondition> conditions)
        {
            for (var index = 0; index < conditions.Count; index++)
                if (!BuildConditionEvaluator.Evaluate(conditions[index], this)) return false;
            return true;
        }

        private void RebuildTags()
        {
            Array.Clear(tags, 0, tagCount);
            Array.Clear(tagCounts, 0, tagCount);
            tagCount = 0;
            for (var index = 0; index < Skills.Count; index++) AddDefinitionTags(Skills.GetAt(index).ContentId);
            for (var index = 0; index < Passives.Count; index++) AddDefinitionTags(Passives.GetAt(index).ContentId);
            for (var index = 0; index < TraitCount; index++) AddDefinitionTags(traits[index]);
            for (var index = 0; index < ActiveSynergyCount; index++) AddDefinitionTags(activeSynergies[index]);
        }

        private void AddDefinitionTags(ContentId id)
        {
            if (!catalog.TryGetDefinition(id, out var definition)) return;
            for (var index = 0; index < definition.Tags.Count; index++) AddTag(definition.Tags[index]);
        }

        private void AddTag(ContentTag tag)
        {
            for (var index = 0; index < tagCount; index++)
            {
                if (tags[index] == tag)
                {
                    tagCounts[index]++;
                    return;
                }
            }
            EnsureCapacity(ref tags, tagCount + 1);
            EnsureCapacity(ref tagCounts, tagCount + 1);
            tags[tagCount] = tag;
            tagCounts[tagCount++] = 1;
        }

        private bool AddModifier(ContentId sourceId, in RuntimeBuildModifier source)
        {
            var modifier = new Modifier(
                sourceId,
                source.StatId,
                source.Operation,
                source.Value,
                source.Priority,
                source.StackingGroup,
                float.PositiveInfinity);
            if (!actors.TryAddModifier(owner.Handle, modifier, out var handle)) return false;
            EnsureCapacity(ref modifierBindings, modifierBindingCount + 1);
            modifierBindings[modifierBindingCount++] = new ModifierBinding { SourceId = sourceId, Handle = handle };
            return true;
        }

        private void RemoveModifiers(ContentId sourceId)
        {
            var index = 0;
            while (index < modifierBindingCount)
            {
                if (modifierBindings[index].SourceId != sourceId)
                {
                    index++;
                    continue;
                }
                actors.TryRemoveModifier(owner.Handle, modifierBindings[index].Handle);
                var last = --modifierBindingCount;
                modifierBindings[index] = modifierBindings[last];
                modifierBindings[last] = default;
            }
        }

        private static ContentId GetAt(ContentId[] source, int count, int index)
        {
            if (index < 0 || index >= count) throw new ArgumentOutOfRangeException(nameof(index));
            return source[index];
        }

        private static bool Contains(ContentId[] source, int count, ContentId id)
        {
            for (var index = 0; index < count; index++) if (source[index] == id) return true;
            return false;
        }

        private static void AddUnique(ref ContentId[] source, ref int count, ContentId id)
        {
            if (!id.IsValid || Contains(source, count, id)) return;
            EnsureCapacity(ref source, count + 1);
            source[count++] = id;
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
