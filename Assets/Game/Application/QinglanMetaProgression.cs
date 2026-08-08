using System;
using System.Collections.Generic;
using Game.Content.Runtime;
using Game.Core;

namespace Game.Application
{
    public enum MetaOperationStatus : byte
    {
        Applied = 1,
        AlreadyApplied = 2,
        InvalidSelection = 3,
        MissingContent = 4,
        Locked = 5,
        InsufficientCurrency = 6,
        SaveFailed = 7
    }

    public readonly struct MetaOperationResult
    {
        internal MetaOperationResult(
            MetaOperationStatus status,
            ProfileSaveData profile,
            SaveDiagnostic diagnostic = default)
        {
            Status = status;
            Profile = profile;
            Diagnostic = diagnostic;
        }

        public MetaOperationStatus Status { get; }
        public ProfileSaveData Profile { get; }
        public SaveDiagnostic Diagnostic { get; }
        public bool IsSuccess =>
            Status == MetaOperationStatus.Applied || Status == MetaOperationStatus.AlreadyApplied;
    }

    public readonly struct MetaLoadoutProjection
    {
        internal MetaLoadoutProjection(
            MetaLoadout loadout,
            bool usedSafeFallback,
            SaveDiagnostic diagnostic)
        {
            Loadout = loadout ?? MetaLoadout.Empty;
            UsedSafeFallback = usedSafeFallback;
            Diagnostic = diagnostic;
        }

        public MetaLoadout Loadout { get; }
        public bool UsedSafeFallback { get; }
        public SaveDiagnostic Diagnostic { get; }
        public bool IsValid => !UsedSafeFallback;
    }

    public enum MetaFacilityState : byte
    {
        Locked = 1,
        Available = 2,
        Visited = 3,
        Updated = 4
    }

    public readonly struct MetaFacilitySnapshot
    {
        public MetaFacilitySnapshot(ContentId facilityId, MetaFacilityState state)
        {
            if (!facilityId.IsValid) throw new ArgumentException("Facility ID must be valid.", nameof(facilityId));
            FacilityId = facilityId;
            State = state;
        }

        public ContentId FacilityId { get; }
        public MetaFacilityState State { get; }
    }

    /// <summary>
    /// Pure low-frequency owner for Qinglan purchases, free loadout reset, and hub projections.
    /// All rules resolve generic definitions and tags rather than concrete content IDs.
    /// </summary>
    public sealed class QinglanMetaProgression
    {
        public const int MaximumBranchNodes = 6;
        public const int MaximumTerminalNodes = 1;
        public const int MaximumInserts = 2;
        public const string SpiritSandCurrency = "qinglan.currency.spirit_sand";

        private const string InitialTag = "meta.initial";
        private const string AnyUpgradeConditionTag = "meta.condition.any_upgrade";
        private const string AnyCollectibleConditionTag = "meta.condition.any_collectible";
        private readonly ContentRegistry content;

        public QinglanMetaProgression(ContentRegistry contentRegistry)
        {
            content = contentRegistry ?? throw new ArgumentNullException(nameof(contentRegistry));
        }

        /// <summary>Parses saved flat IDs; illegal or missing entries yield a safe empty loadout.</summary>
        public MetaLoadoutProjection ProjectLoadout(ProfileSaveData profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            var nodes = new List<ContentId>(MaximumBranchNodes);
            var inserts = new List<ContentId>(MaximumInserts);
            var terminal = default(ContentId);
            for (var index = 0; index < profile.ActiveMetaLoadoutIds.Count; index++)
            {
                var id = profile.ActiveMetaLoadoutIds[index];
                if (content.TryGet(id, out RuntimeMetaNodeDefinition node))
                {
                    if (node.NodeKind == MetaNodeKind.Terminal)
                    {
                        if (terminal.IsValid) return Fallback(id, "meta.error.multiple_terminals");
                        terminal = id;
                    }
                    else nodes.Add(id);
                }
                else if (content.TryGet(id, out RuntimeMetaInsertDefinition _)) inserts.Add(id);
                else return Fallback(id, "meta.error.missing_loadout_content");
            }

            var loadout = terminal.IsValid
                ? new MetaLoadout(nodes.ToArray(), terminal, inserts.ToArray())
                : new MetaLoadout(nodes.ToArray(), inserts.ToArray());
            var validation = Validate(profile, loadout, true);
            return validation.IsSuccess
                ? new MetaLoadoutProjection(loadout, false, default)
                : new MetaLoadoutProjection(MetaLoadout.Empty, true, validation.Diagnostic);
        }

        /// <summary>Validates a frozen run loadout without Profile ownership checks.</summary>
        public MetaOperationResult ValidateStructure(MetaLoadout loadout)
        {
            return Validate(null, loadout, false);
        }

        /// <summary>Purchases one generic node or insert using spirit sand.</summary>
        public MetaOperationResult Purchase(ProfileSaveData profile, ContentId contentId, string writeUtc)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (!content.TryGet(contentId, out ContentRegistryEntry entry) ||
                (!(entry.Definition is RuntimeMetaNodeDefinition) &&
                 !(entry.Definition is RuntimeMetaInsertDefinition)))
                return Failure(MetaOperationStatus.MissingContent, "meta.error.purchase_missing", contentId);
            if (Contains(profile.UnlockedContentIds, contentId))
                return new MetaOperationResult(MetaOperationStatus.AlreadyApplied, profile);

            var definition = (RuntimeMetaDefinition)entry.Definition;
            for (var index = 0; index < definition.PrerequisiteIds.Count; index++)
                if (!Contains(profile.UnlockedContentIds, definition.PrerequisiteIds[index]))
                    return Failure(MetaOperationStatus.Locked, "meta.error.prerequisite_locked", definition.PrerequisiteIds[index]);

            var balance = ProfileDataUtility.GetCounter(profile.Currencies, SpiritSandCurrency);
            if (balance < definition.Cost)
                return Failure(MetaOperationStatus.InsufficientCurrency, "meta.error.insufficient_spirit_sand", contentId);

            var currencies = ProfileDataUtility.SetCounter(
                profile.Currencies,
                SpiritSandCurrency,
                balance - definition.Cost);
            var unlocked = ProfileDataUtility.AddIds(profile.UnlockedContentIds, contentId);
            var upgrades = ProfileDataUtility.AddLevel(profile.MetaUpgrades, contentId, 1);
            var candidate = ProfileDataUtility.Clone(
                profile,
                writeUtc,
                unlockedContentIds: unlocked,
                metaUpgrades: upgrades,
                currencies: currencies);
            var facilities = GetSatisfiedFacilityIds(candidate);
            candidate = ProfileDataUtility.Clone(
                candidate,
                writeUtc,
                unlockedContentIds: ProfileDataUtility.AddIds(candidate.UnlockedContentIds, facilities));
            return new MetaOperationResult(MetaOperationStatus.Applied, candidate);
        }

        /// <summary>Replaces the active selection without refunding or spending currency.</summary>
        public MetaOperationResult ResetLoadout(ProfileSaveData profile, MetaLoadout loadout, string writeUtc)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            var validation = Validate(profile, loadout, true);
            if (!validation.IsSuccess) return validation;
            var ids = new List<ContentId>(
                loadout.EquippedNodeIds.Count + loadout.EquippedInsertIds.Count +
                (loadout.HasTerminalNode ? 1 : 0));
            for (var index = 0; index < loadout.EquippedNodeIds.Count; index++)
                ids.Add(loadout.EquippedNodeIds[index]);
            if (loadout.HasTerminalNode) ids.Add(loadout.TerminalNodeId);
            for (var index = 0; index < loadout.EquippedInsertIds.Count; index++)
                ids.Add(loadout.EquippedInsertIds[index]);
            var candidate = ProfileDataUtility.Clone(
                profile,
                writeUtc,
                activeMetaLoadoutIds: ids.ToArray());
            return new MetaOperationResult(MetaOperationStatus.Applied, candidate);
        }

        /// <summary>Projects all four data-driven facilities from current permanent state.</summary>
        public IReadOnlyList<MetaFacilitySnapshot> ProjectFacilities(ProfileSaveData profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            var facilities = new List<MetaFacilitySnapshot>();
            for (var index = 0; index < content.Count; index++)
            {
                var entry = content.Get(new RuntimeContentIndex(index));
                if (!entry.IsSuccess || !(entry.Value.Definition is RuntimeMetaFacilityDefinition facility)) continue;
                var state = IsConditionSatisfied(profile, facility.UnlockConditionId)
                    ? MetaFacilityState.Available
                    : MetaFacilityState.Locked;
                facilities.Add(new MetaFacilitySnapshot(facility.Id, state));
            }
            facilities.Sort(CompareFacility);
            return facilities.AsReadOnly();
        }

        internal ContentId[] GetSatisfiedFacilityIds(ProfileSaveData profile)
        {
            var ids = new List<ContentId>();
            for (var index = 0; index < content.Count; index++)
            {
                var entry = content.Get(new RuntimeContentIndex(index));
                if (entry.IsSuccess && entry.Value.Definition is RuntimeMetaFacilityDefinition facility &&
                    IsConditionSatisfied(profile, facility.UnlockConditionId))
                    ids.Add(facility.Id);
            }
            ids.Sort(CompareId);
            return ids.ToArray();
        }

        internal bool IsConditionSatisfied(ProfileSaveData profile, ContentId conditionId)
        {
            if (!conditionId.IsValid || !content.TryGet(conditionId, out ContentRegistryEntry condition)) return false;
            if (HasTag(condition.Definition, InitialTag)) return true;
            if (HasTag(condition.Definition, AnyUpgradeConditionTag)) return profile.MetaUpgrades.Count > 0;
            if (HasTag(condition.Definition, AnyCollectibleConditionTag)) return profile.CollectedCollectibleIds.Count > 0;
            return Contains(profile.UnlockedContentIds, conditionId) ||
                   Contains(profile.CompletedStoryIds, conditionId) ||
                   Contains(profile.CollectedCollectibleIds, conditionId) ||
                   Contains(profile.FirstClearMapIds, conditionId) ||
                   Contains(profile.ClaimedUniqueRewardIds, conditionId) ||
                   ContainsLevel(profile.MetaUpgrades, conditionId);
        }

        private MetaOperationResult Validate(
            ProfileSaveData profile,
            MetaLoadout loadout,
            bool requireOwnership)
        {
            if (loadout == null)
                return Failure(MetaOperationStatus.InvalidSelection, "meta.error.loadout_missing", default);
            if (loadout.EquippedNodeIds.Count > MaximumBranchNodes ||
                loadout.EquippedInsertIds.Count > MaximumInserts)
                return Failure(MetaOperationStatus.InvalidSelection, "meta.error.loadout_capacity", default);

            var allNodes = new List<ContentId>(loadout.EquippedNodeIds.Count + 1);
            for (var index = 0; index < loadout.EquippedNodeIds.Count; index++)
            {
                var id = loadout.EquippedNodeIds[index];
                if (!content.TryGet(id, out RuntimeMetaNodeDefinition node) || node.NodeKind != MetaNodeKind.Branch)
                    return Failure(MetaOperationStatus.MissingContent, "meta.error.invalid_branch_node", id);
                if (Contains(allNodes, id))
                    return Failure(MetaOperationStatus.InvalidSelection, "meta.error.duplicate_loadout", id);
                allNodes.Add(id);
            }
            if (loadout.HasTerminalNode)
            {
                if (!content.TryGet(loadout.TerminalNodeId, out RuntimeMetaNodeDefinition terminal) ||
                    terminal.NodeKind != MetaNodeKind.Terminal)
                    return Failure(MetaOperationStatus.MissingContent, "meta.error.invalid_terminal", loadout.TerminalNodeId);
                allNodes.Add(loadout.TerminalNodeId);
            }

            for (var left = 0; left < allNodes.Count; left++)
            {
                if (requireOwnership && !Contains(profile.UnlockedContentIds, allNodes[left]))
                    return Failure(MetaOperationStatus.Locked, "meta.error.loadout_locked", allNodes[left]);
                content.TryGet(allNodes[left], out RuntimeMetaNodeDefinition node);
                for (var prerequisite = 0; requireOwnership && prerequisite < node.PrerequisiteIds.Count; prerequisite++)
                    if (!Contains(profile.UnlockedContentIds, node.PrerequisiteIds[prerequisite]))
                        return Failure(MetaOperationStatus.Locked, "meta.error.prerequisite_locked", node.PrerequisiteIds[prerequisite]);
                for (var mutex = 0; mutex < node.MutuallyExclusiveIds.Count; mutex++)
                    if (Contains(allNodes, node.MutuallyExclusiveIds[mutex]))
                        return Failure(MetaOperationStatus.InvalidSelection, "meta.error.mutually_exclusive", node.MutuallyExclusiveIds[mutex]);
            }

            var inserts = new List<ContentId>(loadout.EquippedInsertIds.Count);
            for (var index = 0; index < loadout.EquippedInsertIds.Count; index++)
            {
                var id = loadout.EquippedInsertIds[index];
                if (!content.TryGet(id, out RuntimeMetaInsertDefinition _))
                    return Failure(MetaOperationStatus.MissingContent, "meta.error.invalid_insert", id);
                if (Contains(inserts, id))
                    return Failure(MetaOperationStatus.InvalidSelection, "meta.error.duplicate_loadout", id);
                if (requireOwnership && !Contains(profile.UnlockedContentIds, id))
                    return Failure(MetaOperationStatus.Locked, "meta.error.loadout_locked", id);
                inserts.Add(id);
            }
            return new MetaOperationResult(MetaOperationStatus.Applied, profile);
        }

        private static MetaLoadoutProjection Fallback(ContentId id, string key) =>
            new MetaLoadoutProjection(
                MetaLoadout.Empty,
                true,
                new SaveDiagnostic(SaveFailureCode.MissingContent, key, id.Value, id));

        private static MetaOperationResult Failure(
            MetaOperationStatus status,
            string key,
            ContentId id) =>
            new MetaOperationResult(
                status,
                null,
                new SaveDiagnostic(
                    status == MetaOperationStatus.MissingContent ? SaveFailureCode.MissingContent : SaveFailureCode.InvalidFormat,
                    key,
                    id.Value,
                    id));

        internal static bool HasTag(RuntimeContentDefinition definition, string value)
        {
            if (definition == null) return false;
            for (var index = 0; index < definition.Tags.Count; index++)
                if (string.Equals(definition.Tags[index].Value, value, StringComparison.Ordinal)) return true;
            return false;
        }

        internal static bool Contains(IReadOnlyList<ContentId> ids, ContentId id)
        {
            for (var index = 0; index < ids.Count; index++) if (ids[index] == id) return true;
            return false;
        }

        private static bool Contains(List<ContentId> ids, ContentId id)
        {
            for (var index = 0; index < ids.Count; index++) if (ids[index] == id) return true;
            return false;
        }

        private static bool ContainsLevel(IReadOnlyList<SavedContentLevel> levels, ContentId id)
        {
            for (var index = 0; index < levels.Count; index++)
                if (levels[index].ContentId == id) return true;
            return false;
        }

        private static int CompareFacility(MetaFacilitySnapshot left, MetaFacilitySnapshot right) =>
            CompareId(left.FacilityId, right.FacilityId);

        private static int CompareId(ContentId left, ContentId right) =>
            string.Compare(left.Value, right.Value, StringComparison.Ordinal);
    }

    internal static class ProfileDataUtility
    {
        public static ProfileSaveData Clone(
            ProfileSaveData source,
            string writeUtc,
            ContentId[] unlockedContentIds = null,
            SavedContentLevel[] metaUpgrades = null,
            SavedCounter[] currencies = null,
            SavedCounter[] statistics = null,
            ContentId[] activeMetaLoadoutIds = null,
            ContentId[] firstClearMapIds = null,
            ContentId[] claimedUniqueRewardIds = null,
            ContentId[] completedStoryIds = null,
            ContentId[] collectedCollectibleIds = null,
            ContentId[] committedTransactionIds = null)
        {
            return new ProfileSaveData(
                source.ProfileId,
                Copy(source.ContentPacks),
                unlockedContentIds ?? Copy(source.UnlockedContentIds),
                metaUpgrades ?? Copy(source.MetaUpgrades),
                currencies ?? Copy(source.Currencies),
                statistics ?? Copy(source.Statistics),
                writeUtc ?? source.LastWriteUtc,
                activeMetaLoadoutIds ?? Copy(source.ActiveMetaLoadoutIds),
                firstClearMapIds ?? Copy(source.FirstClearMapIds),
                claimedUniqueRewardIds ?? Copy(source.ClaimedUniqueRewardIds),
                completedStoryIds ?? Copy(source.CompletedStoryIds),
                collectedCollectibleIds ?? Copy(source.CollectedCollectibleIds),
                committedTransactionIds ?? Copy(source.CommittedTransactionIds),
                SaveSchema.ProfileCurrentVersion,
                source.GameVersion);
        }

        public static long GetCounter(IReadOnlyList<SavedCounter> counters, string key)
        {
            for (var index = 0; index < counters.Count; index++)
                if (string.Equals(counters[index].Key, key, StringComparison.Ordinal)) return counters[index].Value;
            return 0;
        }

        public static SavedCounter[] SetCounter(IReadOnlyList<SavedCounter> source, string key, long value)
        {
            var found = -1;
            for (var index = 0; index < source.Count; index++)
                if (string.Equals(source[index].Key, key, StringComparison.Ordinal)) { found = index; break; }
            var result = new SavedCounter[source.Count + (found < 0 ? 1 : 0)];
            for (var index = 0; index < source.Count; index++) result[index] = source[index];
            if (found < 0) result[source.Count] = new SavedCounter(key, value);
            else result[found] = new SavedCounter(key, value);
            Array.Sort(result, CompareCounter);
            return result;
        }

        public static SavedCounter[] AddCounter(IReadOnlyList<SavedCounter> source, string key, long delta)
        {
            var current = GetCounter(source, key);
            long next;
            try { next = checked(current + delta); }
            catch (OverflowException) { next = delta > 0 ? long.MaxValue : long.MinValue; }
            return SetCounter(source, key, next);
        }

        public static ContentId[] AddIds(IReadOnlyList<ContentId> source, params ContentId[] additions)
        {
            var list = new List<ContentId>(source.Count + (additions?.Length ?? 0));
            for (var index = 0; index < source.Count; index++) AddUnique(list, source[index]);
            if (additions != null)
                for (var index = 0; index < additions.Length; index++) AddUnique(list, additions[index]);
            list.Sort(CompareId);
            return list.ToArray();
        }

        public static SavedContentLevel[] AddLevel(
            IReadOnlyList<SavedContentLevel> source,
            ContentId id,
            int level)
        {
            var result = new List<SavedContentLevel>(source.Count + 1);
            var found = false;
            for (var index = 0; index < source.Count; index++)
            {
                if (source[index].ContentId != id) result.Add(source[index]);
                else
                {
                    result.Add(new SavedContentLevel(id, Math.Max(level, source[index].Level)));
                    found = true;
                }
            }
            if (!found) result.Add(new SavedContentLevel(id, level));
            result.Sort(CompareLevel);
            return result.ToArray();
        }

        public static T[] Copy<T>(IReadOnlyList<T> source)
        {
            var result = new T[source.Count];
            for (var index = 0; index < result.Length; index++) result[index] = source[index];
            return result;
        }

        private static void AddUnique(List<ContentId> ids, ContentId id)
        {
            if (!id.IsValid) return;
            for (var index = 0; index < ids.Count; index++) if (ids[index] == id) return;
            ids.Add(id);
        }

        private static int CompareCounter(SavedCounter left, SavedCounter right) =>
            string.Compare(left.Key, right.Key, StringComparison.Ordinal);

        private static int CompareLevel(SavedContentLevel left, SavedContentLevel right) =>
            CompareId(left.ContentId, right.ContentId);

        private static int CompareId(ContentId left, ContentId right) =>
            string.Compare(left.Value, right.Value, StringComparison.Ordinal);
    }
}
