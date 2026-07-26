using System;
using Game.Core;

namespace Game.Simulation
{
    public enum InventoryReplacementPolicy : byte
    {
        Reject = 0,
        ReplaceLowestLevel = 1,
        ReplaceSpecifiedSlot = 2
    }

    public enum InventoryAcquireResult : byte
    {
        Rejected = 0,
        Added = 1,
        Leveled = 2,
        Replaced = 3
    }

    /// <summary>Immutable view of one skill or passive inventory slot.</summary>
    public readonly struct InventoryEntry
    {
        public InventoryEntry(
            ContentId contentId,
            RuntimeContentIndex contentIndex,
            int level,
            int maximumLevel)
        {
            ContentId = contentId;
            ContentIndex = contentIndex;
            Level = level;
            MaximumLevel = maximumLevel;
        }

        public ContentId ContentId { get; }
        public RuntimeContentIndex ContentIndex { get; }
        public int Level { get; }
        public int MaximumLevel { get; }
        public bool IsMaximumLevel => Level >= MaximumLevel;

        internal InventoryEntry WithLevel(int level) =>
            new InventoryEntry(ContentId, ContentIndex, level, MaximumLevel);
    }

    internal sealed class ContentInventory
    {
        private readonly InventoryEntry[] entries;

        public ContentInventory(int slotCount)
        {
            if (slotCount <= 0) throw new ArgumentOutOfRangeException(nameof(slotCount));
            entries = new InventoryEntry[slotCount];
        }

        public int SlotCount => entries.Length;
        public int Count { get; private set; }

        public InventoryEntry GetAt(int slot)
        {
            if (slot < 0 || slot >= Count) throw new ArgumentOutOfRangeException(nameof(slot));
            return entries[slot];
        }

        public bool TryGet(ContentId id, out InventoryEntry entry, out int slot)
        {
            for (var index = 0; index < Count; index++)
            {
                if (entries[index].ContentId == id)
                {
                    entry = entries[index];
                    slot = index;
                    return true;
                }
            }
            entry = default;
            slot = -1;
            return false;
        }

        public InventoryAcquireResult TryAcquire(
            ContentId id,
            RuntimeContentIndex contentIndex,
            int maximumLevel,
            InventoryReplacementPolicy replacementPolicy,
            int replacementSlot,
            out int affectedSlot,
            out InventoryEntry previous)
        {
            if (!id.IsValid || !contentIndex.IsValid || maximumLevel < 1)
            {
                affectedSlot = -1;
                previous = default;
                return InventoryAcquireResult.Rejected;
            }

            if (TryGet(id, out var existing, out affectedSlot))
            {
                previous = existing;
                if (existing.IsMaximumLevel) return InventoryAcquireResult.Rejected;
                entries[affectedSlot] = existing.WithLevel(existing.Level + 1);
                return InventoryAcquireResult.Leveled;
            }

            var added = new InventoryEntry(id, contentIndex, 1, maximumLevel);
            if (Count < entries.Length)
            {
                affectedSlot = Count++;
                previous = default;
                entries[affectedSlot] = added;
                return InventoryAcquireResult.Added;
            }

            if (replacementPolicy == InventoryReplacementPolicy.Reject)
            {
                affectedSlot = -1;
                previous = default;
                return InventoryAcquireResult.Rejected;
            }

            if (replacementPolicy == InventoryReplacementPolicy.ReplaceLowestLevel)
            {
                replacementSlot = 0;
                for (var index = 1; index < Count; index++)
                {
                    if (entries[index].Level < entries[replacementSlot].Level)
                        replacementSlot = index;
                }
            }

            if (replacementSlot < 0 || replacementSlot >= Count)
            {
                affectedSlot = -1;
                previous = default;
                return InventoryAcquireResult.Rejected;
            }

            affectedSlot = replacementSlot;
            previous = entries[replacementSlot];
            entries[replacementSlot] = added;
            return InventoryAcquireResult.Replaced;
        }

        public bool Transform(
            ContentId sourceId,
            ContentId resultId,
            RuntimeContentIndex resultIndex,
            int resultMaximumLevel,
            out int slot,
            out InventoryEntry previous)
        {
            slot = -1;
            previous = default;
            if (TryGet(resultId, out _, out _) ||
                !resultId.IsValid || !resultIndex.IsValid || resultMaximumLevel < 1 ||
                !TryGet(sourceId, out previous, out slot))
                return false;
            entries[slot] = new InventoryEntry(resultId, resultIndex, 1, resultMaximumLevel);
            return true;
        }

        public bool Remove(ContentId id, out InventoryEntry removed)
        {
            if (!TryGet(id, out removed, out var slot)) return false;
            var last = Count - 1;
            if (slot != last) entries[slot] = entries[last];
            entries[last] = default;
            Count--;
            return true;
        }
    }

    /// <summary>Fixed-slot, duplicate-leveling skill inventory.</summary>
    public sealed class SkillInventory
    {
        private readonly ContentInventory inventory;

        public SkillInventory(int slotCount = 6) { inventory = new ContentInventory(slotCount); }
        public int SlotCount => inventory.SlotCount;
        public int Count => inventory.Count;
        public InventoryEntry GetAt(int slot) => inventory.GetAt(slot);
        public bool TryGet(ContentId id, out InventoryEntry entry, out int slot) => inventory.TryGet(id, out entry, out slot);

        public InventoryAcquireResult TryAcquire(
            ContentId id,
            RuntimeContentIndex index,
            int maximumLevel,
            InventoryReplacementPolicy policy,
            int replacementSlot,
            out int affectedSlot,
            out InventoryEntry previous) =>
            inventory.TryAcquire(id, index, maximumLevel, policy, replacementSlot, out affectedSlot, out previous);

        internal bool Transform(ContentId sourceId, ContentId resultId, RuntimeContentIndex resultIndex, int resultMaximumLevel, out int slot, out InventoryEntry previous) =>
            inventory.Transform(sourceId, resultId, resultIndex, resultMaximumLevel, out slot, out previous);
    }

    /// <summary>Fixed-slot, duplicate-leveling passive inventory.</summary>
    public sealed class PassiveInventory
    {
        private readonly ContentInventory inventory;

        public PassiveInventory(int slotCount = 6) { inventory = new ContentInventory(slotCount); }
        public int SlotCount => inventory.SlotCount;
        public int Count => inventory.Count;
        public InventoryEntry GetAt(int slot) => inventory.GetAt(slot);
        public bool TryGet(ContentId id, out InventoryEntry entry, out int slot) => inventory.TryGet(id, out entry, out slot);

        public InventoryAcquireResult TryAcquire(
            ContentId id,
            RuntimeContentIndex index,
            int maximumLevel,
            InventoryReplacementPolicy policy,
            int replacementSlot,
            out int affectedSlot,
            out InventoryEntry previous) =>
            inventory.TryAcquire(id, index, maximumLevel, policy, replacementSlot, out affectedSlot, out previous);

        internal bool Remove(ContentId id, out InventoryEntry removed) => inventory.Remove(id, out removed);
    }
}
