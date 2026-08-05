using System;
using System.Numerics;
using Game.Content.Runtime;
using Game.Core;

namespace Game.Simulation
{
    /// <summary>Read-only health state exposed outside combat systems.</summary>
    public readonly struct Health
    {
        /// <summary>Initializes a health snapshot.</summary>
        public Health(float current, float maximum)
        {
            Current = current;
            Maximum = maximum;
        }

        /// <summary>Gets current health.</summary>
        public float Current { get; }

        /// <summary>Gets current calculated maximum health.</summary>
        public float Maximum { get; }

        /// <summary>Gets whether current health is positive.</summary>
        public bool IsAlive => Current > 0f;
    }

    /// <summary>Read-only shield state exposed outside combat systems.</summary>
    public readonly struct Shield
    {
        /// <summary>Initializes a shield snapshot.</summary>
        public Shield(float current, float maximum)
        {
            Current = current;
            Maximum = maximum;
        }

        /// <summary>Gets current absorbable shield.</summary>
        public float Current { get; }

        /// <summary>Gets the shield capacity.</summary>
        public float Maximum { get; }
    }

    /// <summary>Typed elemental resistances stored independently from the Armor statistic.</summary>
    public readonly struct ResistanceProfile
    {
        /// <summary>Initializes normalized resistance fractions.</summary>
        public ResistanceProfile(
            float fire,
            float cold,
            float lightning,
            float poison)
        {
            Fire = fire;
            Cold = cold;
            Lightning = lightning;
            Poison = poison;
        }

        /// <summary>Gets fire resistance.</summary>
        public float Fire { get; }

        /// <summary>Gets cold resistance.</summary>
        public float Cold { get; }

        /// <summary>Gets lightning resistance.</summary>
        public float Lightning { get; }

        /// <summary>Gets poison resistance.</summary>
        public float Poison { get; }

        /// <summary>Gets resistance for an elemental damage type.</summary>
        public float Get(DamageType damageType)
        {
            switch (damageType)
            {
                case DamageType.Fire:
                    return Fire;
                case DamageType.Cold:
                    return Cold;
                case DamageType.Lightning:
                    return Lightning;
                case DamageType.Poison:
                    return Poison;
                default:
                    return 0f;
            }
        }
    }

    /// <summary>Initial combat state installed atomically when an actor is created.</summary>
    public readonly struct ActorCombatInitialization
    {
        /// <summary>Initializes actor combat state.</summary>
        public ActorCombatInitialization(
            in StatBaseValues baseStats,
            float currentHealth,
            float currentShield,
            float maximumShield,
            in ResistanceProfile resistances)
        {
            if (float.IsNaN(currentHealth) ||
                float.IsInfinity(currentHealth) ||
                currentHealth < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(currentHealth));
            }

            if (float.IsNaN(currentShield) ||
                float.IsInfinity(currentShield) ||
                currentShield < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(currentShield));
            }

            if (float.IsNaN(maximumShield) ||
                float.IsInfinity(maximumShield) ||
                maximumShield < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumShield));
            }

            BaseStats = baseStats;
            CurrentHealth = currentHealth;
            CurrentShield = currentShield;
            MaximumShield = maximumShield;
            Resistances = resistances;
        }

        /// <summary>Gets base statistic values.</summary>
        public StatBaseValues BaseStats { get; }

        /// <summary>Gets initial current health.</summary>
        public float CurrentHealth { get; }

        /// <summary>Gets initial shield.</summary>
        public float CurrentShield { get; }

        /// <summary>Gets initial shield capacity.</summary>
        public float MaximumShield { get; }

        /// <summary>Gets typed resistances.</summary>
        public ResistanceProfile Resistances { get; }

        /// <summary>Creates a conventional combat-ready actor state.</summary>
        public static ActorCombatInitialization CreateDefault(
            float health = 100f,
            float moveSpeed = 5f)
        {
            var stats = StatBaseValues.CreateDefault(health, moveSpeed);
            return new ActorCombatInitialization(
                stats,
                health,
                0f,
                0f,
                default);
        }
    }

    /// <summary>Immutable request passed through the centralized damage pipeline.</summary>
    public readonly struct DamagePacket
    {
        /// <summary>Initializes one damage packet.</summary>
        public DamagePacket(
            SpatialEntity source,
            SpatialEntity target,
            ContentId sourceContentId,
            DamageType damageType,
            DamageTags tags,
            float baseValue,
            bool canCritical,
            float procCoefficient,
            Vector2 knockback,
            Vector2 position,
            int procDepth)
            : this(
                source, target, sourceContentId, damageType, tags, baseValue, canCritical,
                procCoefficient, knockback, position, procDepth, BuiltInDamageChannels.Direct, 0)
        {
        }

        /// <summary>Initializes a damage packet with an explicit policy channel and target cooldown.</summary>
        public DamagePacket(
            SpatialEntity source,
            SpatialEntity target,
            ContentId sourceContentId,
            DamageType damageType,
            DamageTags tags,
            float baseValue,
            bool canCritical,
            float procCoefficient,
            Vector2 knockback,
            Vector2 position,
            int procDepth,
            DamageChannelId channelId,
            int channelCooldownTicks)
        {
            Source = source;
            Target = target;
            SourceContentId = sourceContentId;
            DamageType = damageType;
            Tags = tags;
            BaseValue = baseValue;
            CanCritical = canCritical;
            ProcCoefficient = procCoefficient;
            Knockback = knockback;
            Position = position;
            ProcDepth = procDepth;
            ChannelId = channelId.IsValid ? channelId : BuiltInDamageChannels.Direct;
            ChannelCooldownTicks = Math.Max(0, channelCooldownTicks);
        }

        /// <summary>Gets the source entity, which may be invalid for environment damage.</summary>
        public SpatialEntity Source { get; }

        /// <summary>Gets the target entity.</summary>
        public SpatialEntity Target { get; }

        /// <summary>Gets the stable content source.</summary>
        public ContentId SourceContentId { get; }

        /// <summary>Gets the damage category.</summary>
        public DamageType DamageType { get; }

        /// <summary>Gets mechanic tags.</summary>
        public DamageTags Tags { get; }

        /// <summary>Gets the unmodified requested value.</summary>
        public float BaseValue { get; }

        /// <summary>Gets whether a source critical roll is eligible.</summary>
        public bool CanCritical { get; }

        /// <summary>Gets the coefficient inherited by later proc consumers.</summary>
        public float ProcCoefficient { get; }

        /// <summary>Gets requested knockback; M3 reports but does not integrate it.</summary>
        public Vector2 Knockback { get; }

        /// <summary>Gets the impact position.</summary>
        public Vector2 Position { get; }

        /// <summary>Gets the trigger-chain depth.</summary>
        public int ProcDepth { get; }

        /// <summary>Gets the target-local damage policy channel.</summary>
        public DamageChannelId ChannelId { get; }

        /// <summary>Gets cooldown ticks installed after accepted resolution.</summary>
        public int ChannelCooldownTicks { get; }
    }

    public enum DamageResolutionOutcome : byte
    {
        Applied = 1,
        Immune = 2,
        ChannelCooldown = 3,
        Invalid = 4,
        Zero = 5
    }

    /// <summary>Complete deterministic result of one valid damage packet.</summary>
    public readonly struct DamageContext
    {
        internal DamageContext(
            in DamagePacket packet,
            float normalizedBaseValue,
            float sourceModifiedValue,
            float criticalModifiedValue,
            float mitigatedValue,
            float finalDamage,
            float shieldAbsorbed,
            float healthDamage,
            bool wasCritical)
        {
            Packet = packet;
            NormalizedBaseValue = normalizedBaseValue;
            SourceModifiedValue = sourceModifiedValue;
            CriticalModifiedValue = criticalModifiedValue;
            MitigatedValue = mitigatedValue;
            FinalDamage = finalDamage;
            ShieldAbsorbed = shieldAbsorbed;
            HealthDamage = healthDamage;
            WasCritical = wasCritical;
        }

        /// <summary>Gets the original packet and all source/target metadata.</summary>
        public DamagePacket Packet { get; }

        /// <summary>Gets normalized damage before source attributes.</summary>
        public float NormalizedBaseValue { get; }

        /// <summary>Gets damage after the source Damage statistic.</summary>
        public float SourceModifiedValue { get; }

        /// <summary>Gets damage after a possible critical multiplier.</summary>
        public float CriticalModifiedValue { get; }

        /// <summary>Gets damage after armor or resistance.</summary>
        public float MitigatedValue { get; }

        /// <summary>Gets bounded damage offered to shield and health.</summary>
        public float FinalDamage { get; }

        /// <summary>Gets the shield amount consumed.</summary>
        public float ShieldAbsorbed { get; }

        /// <summary>Gets actual health lost.</summary>
        public float HealthDamage { get; }

        /// <summary>Gets whether the packet critically hit.</summary>
        public bool WasCritical { get; }
    }

    /// <summary>Validated constants governing deterministic combat resolution.</summary>
    public readonly struct CombatRules
    {
        /// <summary>Initializes combat boundaries and coefficients.</summary>
        public CombatRules(
            float minimumDamage,
            float maximumDamage,
            float criticalMultiplier,
            float armorScale,
            float maximumResistance,
            int maximumProcDepth)
        {
            if (float.IsNaN(minimumDamage) ||
                float.IsInfinity(minimumDamage) ||
                minimumDamage < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(minimumDamage));
            }

            if (float.IsNaN(maximumDamage) ||
                float.IsInfinity(maximumDamage) ||
                maximumDamage < minimumDamage)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumDamage));
            }

            if (!(criticalMultiplier >= 1f) || float.IsInfinity(criticalMultiplier))
            {
                throw new ArgumentOutOfRangeException(nameof(criticalMultiplier));
            }

            if (!(armorScale > 0f) || float.IsInfinity(armorScale))
            {
                throw new ArgumentOutOfRangeException(nameof(armorScale));
            }

            if (float.IsNaN(maximumResistance) ||
                maximumResistance < 0f ||
                maximumResistance >= 1f)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumResistance));
            }

            if (maximumProcDepth < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumProcDepth));
            }

            MinimumDamage = minimumDamage;
            MaximumDamage = maximumDamage;
            CriticalMultiplier = criticalMultiplier;
            ArmorScale = armorScale;
            MaximumResistance = maximumResistance;
            MaximumProcDepth = maximumProcDepth;
        }

        /// <summary>Gets the minimum normalized damage.</summary>
        public float MinimumDamage { get; }

        /// <summary>Gets the maximum damage per packet.</summary>
        public float MaximumDamage { get; }

        /// <summary>Gets the critical multiplier.</summary>
        public float CriticalMultiplier { get; }

        /// <summary>Gets armor corresponding to fifty percent mitigation.</summary>
        public float ArmorScale { get; }

        /// <summary>Gets the maximum elemental resistance fraction.</summary>
        public float MaximumResistance { get; }

        /// <summary>Gets the maximum allowed packet depth.</summary>
        public int MaximumProcDepth { get; }

        /// <summary>Gets production M3 defaults.</summary>
        public static CombatRules Default =>
            new CombatRules(0f, 1_000_000f, 2f, 100f, 0.95f, 8);
    }

    /// <summary>Buffered request to apply a runtime status definition.</summary>
    public readonly struct StatusApplicationRequest
    {
        /// <summary>Initializes a status application request.</summary>
        public StatusApplicationRequest(
            SpatialEntity source,
            SpatialEntity target,
            ContentId sourceContentId,
            RuntimeContentIndex statusIndex,
            float strength,
            int procDepth)
        {
            Source = source;
            Target = target;
            SourceContentId = sourceContentId;
            StatusIndex = statusIndex;
            Strength = strength;
            ProcDepth = procDepth;
        }

        /// <summary>Gets the source entity.</summary>
        public SpatialEntity Source { get; }

        /// <summary>Gets the target actor.</summary>
        public SpatialEntity Target { get; }

        /// <summary>Gets the stable source content ID.</summary>
        public ContentId SourceContentId { get; }

        /// <summary>Gets the status definition runtime index.</summary>
        public RuntimeContentIndex StatusIndex { get; }

        /// <summary>Gets explicit strength used by ReplaceIfStronger and payload scaling.</summary>
        public float Strength { get; }

        /// <summary>Gets the inherited proc depth.</summary>
        public int ProcDepth { get; }
    }

    /// <summary>Buffered request to dispel statuses matching one canonical tag.</summary>
    public readonly struct StatusDispelRequest
    {
        /// <summary>Initializes a dispel request.</summary>
        public StatusDispelRequest(SpatialEntity target, ContentTag dispelTag)
        {
            Target = target;
            DispelTag = dispelTag;
        }

        /// <summary>Gets the target actor.</summary>
        public SpatialEntity Target { get; }

        /// <summary>Gets the tag matched against RuntimeStatusDefinition.DispelTags.</summary>
        public ContentTag DispelTag { get; }
    }

    /// <summary>Read-only active status data exposed to tests and later snapshots.</summary>
    public readonly struct ActiveStatus
    {
        internal ActiveStatus(
            RuntimeContentIndex statusIndex,
            int stacks,
            float strength,
            float remainingDuration,
            long tickCount)
        {
            StatusIndex = statusIndex;
            Stacks = stacks;
            Strength = strength;
            RemainingDuration = remainingDuration;
            TickCount = tickCount;
        }

        /// <summary>Gets the runtime definition index.</summary>
        public RuntimeContentIndex StatusIndex { get; }

        /// <summary>Gets aggregate stacks for this instance.</summary>
        public int Stacks { get; }

        /// <summary>Gets application strength.</summary>
        public float Strength { get; }

        /// <summary>Gets remaining duration in seconds.</summary>
        public float RemainingDuration { get; }

        /// <summary>Gets the number of interval ticks produced by this instance.</summary>
        public long TickCount { get; }
    }

    /// <summary>
    /// RuntimeContentIndex lookup table for pure status definitions.
    /// </summary>
    public sealed class RuntimeStatusCatalog
    {
        private RuntimeStatusDefinition[] definitions;

        /// <summary>Initializes an empty table for explicit registration.</summary>
        public RuntimeStatusCatalog(int initialCapacity = 8)
        {
            if (initialCapacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(initialCapacity));
            }

            definitions = new RuntimeStatusDefinition[initialCapacity];
        }

        /// <summary>Builds a status table from one fully loaded content registry.</summary>
        public RuntimeStatusCatalog(ContentRegistry registry)
        {
            if (registry == null)
            {
                throw new ArgumentNullException(nameof(registry));
            }

            definitions = new RuntimeStatusDefinition[Math.Max(1, registry.Count)];
            for (var index = 0; index < registry.Count; index++)
            {
                var result = registry.Get(new RuntimeContentIndex(index));
                if (result.IsSuccess &&
                    result.Value.Definition is RuntimeStatusDefinition status)
                {
                    definitions[index] = status;
                }
            }
        }

        /// <summary>Registers a pure definition at its load-local index.</summary>
        public void Register(
            RuntimeContentIndex index,
            RuntimeStatusDefinition definition)
        {
            if (!index.IsValid)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            if (index.Value >= definitions.Length)
            {
                var capacity = definitions.Length;
                while (capacity <= index.Value)
                {
                    capacity *= 2;
                }

                Array.Resize(ref definitions, capacity);
            }

            if (definitions[index.Value] != null)
            {
                throw new InvalidOperationException(
                    "A status definition is already registered at runtime index " + index + ".");
            }

            definitions[index.Value] = definition;
        }

        /// <summary>Resolves a pure status definition without allocating.</summary>
        public bool TryGet(
            RuntimeContentIndex index,
            out RuntimeStatusDefinition definition)
        {
            if (!index.IsValid || index.Value >= definitions.Length)
            {
                definition = null;
                return false;
            }

            definition = definitions[index.Value];
            return definition != null;
        }
    }

    internal sealed class ActorCombatRecord
    {
        public ActorCombatRecord(in ActorCombatInitialization initialization)
        {
            Stats = new ActorStatBlock(initialization.BaseStats);
            Statuses = new StatusCollection();
            ApplyInitialization(initialization);
        }

        public ActorStatBlock Stats { get; }

        public StatusCollection Statuses { get; }

        public float HealthCurrent;

        public float ShieldCurrent;

        public float ShieldMaximum;

        public ResistanceProfile Resistances;

        public bool DeathPending;

        public bool Dead;

        public void Reset(in ActorCombatInitialization initialization)
        {
            Stats.Reset(initialization.BaseStats);
            Statuses.Clear();
            ApplyInitialization(initialization);
        }

        public void ReconcileHealthMaximum()
        {
            var maximum = Stats.Get(BuiltInStatIndices.Health);
            if (HealthCurrent > maximum)
            {
                HealthCurrent = maximum;
            }
        }

        public Health GetHealth()
        {
            ReconcileHealthMaximum();
            return new Health(HealthCurrent, Stats.Get(BuiltInStatIndices.Health));
        }

        public Shield GetShield()
        {
            return new Shield(ShieldCurrent, ShieldMaximum);
        }

        private static float Clamp(float value, float minimum, float maximum)
        {
            if (value < minimum)
            {
                return minimum;
            }

            return value > maximum ? maximum : value;
        }

        private void ApplyInitialization(in ActorCombatInitialization initialization)
        {
            var maximumHealth = Stats.Get(BuiltInStatIndices.Health);
            HealthCurrent = Clamp(initialization.CurrentHealth, 0f, maximumHealth);
            ShieldMaximum = Math.Max(initialization.MaximumShield, initialization.CurrentShield);
            ShieldCurrent = Clamp(initialization.CurrentShield, 0f, ShieldMaximum);
            Resistances = initialization.Resistances;
            DeathPending = false;
            Dead = false;
        }
    }

    internal struct StatusInstance
    {
        public long InstanceId;
        public RuntimeContentIndex StatusIndex;
        public RuntimeStatusDefinition Definition;
        public SpatialEntity Source;
        public ContentId SourceContentId;
        public float Strength;
        public int Stacks;
        public float RemainingDuration;
        public float TickAccumulator;
        public long TickCount;
        public int ProcDepth;
        public ModifierHandle ModifierHandle;
        public float ShieldContribution;
    }

    internal sealed class StatusCollection
    {
        private StatusInstance[] entries = new StatusInstance[2];
        private long nextInstanceId = 1;

        public int Count { get; private set; }

        public void Clear()
        {
            Array.Clear(entries, 0, Count);
            Count = 0;
        }

        public StatusInstance GetAt(int index)
        {
            if (index < 0 || index >= Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return entries[index];
        }

        public void SetAt(int index, in StatusInstance instance)
        {
            if (index < 0 || index >= Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            entries[index] = instance;
        }

        public int Add(in StatusInstance instance)
        {
            if (Count == entries.Length)
            {
                Array.Resize(ref entries, entries.Length * 2);
            }

            var stored = instance;
            stored.InstanceId = nextInstanceId++;
            entries[Count] = stored;
            return Count++;
        }

        public void RemoveAt(int index)
        {
            if (index < 0 || index >= Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            var lastIndex = Count - 1;
            if (index != lastIndex)
            {
                entries[index] = entries[lastIndex];
            }

            entries[lastIndex] = default;
            Count--;
        }

        public int FindFirst(RuntimeContentIndex statusIndex)
        {
            for (var index = 0; index < Count; index++)
            {
                if (entries[index].StatusIndex == statusIndex)
                {
                    return index;
                }
            }

            return -1;
        }

        public int CountInstances(RuntimeContentIndex statusIndex)
        {
            var count = 0;
            for (var index = 0; index < Count; index++)
            {
                if (entries[index].StatusIndex == statusIndex)
                {
                    count++;
                }
            }

            return count;
        }
    }

    internal sealed class ActorCombatStorage
    {
        private ActorCombatRecord[] records;
        private ushort[] generations;

        public ActorCombatStorage(int initialCapacity)
        {
            var capacity = Math.Max(1, initialCapacity);
            records = new ActorCombatRecord[capacity];
            generations = new ushort[capacity];
        }

        public void Attach(
            EntityHandle handle,
            in ActorCombatInitialization initialization)
        {
            EnsureCapacity(handle.Index + 1);
            var record = records[handle.Index];
            if (record == null)
            {
                record = new ActorCombatRecord(initialization);
                records[handle.Index] = record;
            }
            else
            {
                record.Reset(initialization);
            }

            generations[handle.Index] = handle.Generation;
        }

        public bool TryGet(EntityHandle handle, out ActorCombatRecord record)
        {
            if (handle.Index < 0 ||
                handle.Index >= records.Length ||
                handle.Generation == 0 ||
                generations[handle.Index] != handle.Generation)
            {
                record = null;
                return false;
            }

            record = records[handle.Index];
            return record != null;
        }

        public void Detach(EntityHandle handle)
        {
            if (TryGet(handle, out _))
            {
                generations[handle.Index] = 0;
            }
        }

        private void EnsureCapacity(int required)
        {
            if (required <= records.Length)
            {
                return;
            }

            var capacity = records.Length * 2;
            while (capacity < required)
            {
                capacity *= 2;
            }

            Array.Resize(ref records, capacity);
            Array.Resize(ref generations, capacity);
        }
    }
}
