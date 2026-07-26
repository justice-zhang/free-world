using System;

namespace Game.Core
{
    /// <summary>
    /// Stores a stable, canonical identifier for one simulation statistic.
    /// </summary>
    [Serializable]
    public readonly struct StatId :
        IEquatable<StatId>,
        IComparable<StatId>
    {
        private readonly ContentId identifier;

        private StatId(ContentId identifier)
        {
            this.identifier = identifier;
        }

        /// <summary>Gets the canonical identifier text.</summary>
        public string Value => identifier.Value;

        /// <summary>Gets whether this value contains a valid statistic identifier.</summary>
        public bool IsValid => identifier.IsValid;

        /// <summary>Gets the deterministic lookup hash.</summary>
        public int StableHash => identifier.StableHash;

        /// <summary>Creates a statistic identifier from canonicalizable text.</summary>
        public static Result<StatId> Create(
            string input,
            ContentId packId = default,
            string authorAssetPath = "")
        {
            var result = ContentId.Create(input, packId, authorAssetPath);
            return result.IsSuccess
                ? Result<StatId>.Success(new StatId(result.Value))
                : Result<StatId>.Failure(result.Error);
        }

        /// <summary>Attempts to create a statistic identifier.</summary>
        public static bool TryCreate(string input, out StatId statId)
        {
            var result = Create(input);
            statId = result.IsSuccess ? result.Value : default;
            return result.IsSuccess;
        }

        /// <inheritdoc />
        public bool Equals(StatId other)
        {
            return identifier.Equals(other.identifier);
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            return obj is StatId other && Equals(other);
        }

        /// <inheritdoc />
        public int CompareTo(StatId other)
        {
            return identifier.CompareTo(other.identifier);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return identifier.GetHashCode();
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return Value;
        }

        /// <summary>Compares two statistic identifiers.</summary>
        public static bool operator ==(StatId left, StatId right)
        {
            return left.Equals(right);
        }

        /// <summary>Compares two statistic identifiers.</summary>
        public static bool operator !=(StatId left, StatId right)
        {
            return !left.Equals(right);
        }
    }

    /// <summary>
    /// Stores a compact statistic index assigned for one runtime statistic catalog.
    /// </summary>
    [Serializable]
    public readonly struct StatIndex :
        IEquatable<StatIndex>,
        IComparable<StatIndex>
    {
        private readonly int encodedValue;

        /// <summary>Initializes a valid zero-based statistic index.</summary>
        public StatIndex(int value)
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            encodedValue = checked(value + 1);
        }

        /// <summary>Gets the zero-based value, or -1 when invalid.</summary>
        public int Value => encodedValue - 1;

        /// <summary>Gets whether this index is valid.</summary>
        public bool IsValid => encodedValue > 0;

        /// <inheritdoc />
        public bool Equals(StatIndex other)
        {
            return encodedValue == other.encodedValue;
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            return obj is StatIndex other && Equals(other);
        }

        /// <inheritdoc />
        public int CompareTo(StatIndex other)
        {
            return Value.CompareTo(other.Value);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return encodedValue;
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return IsValid ? Value.ToString() : "Invalid";
        }

        /// <summary>Compares two statistic indices.</summary>
        public static bool operator ==(StatIndex left, StatIndex right)
        {
            return left.Equals(right);
        }

        /// <summary>Compares two statistic indices.</summary>
        public static bool operator !=(StatIndex left, StatIndex right)
        {
            return !left.Equals(right);
        }
    }

    /// <summary>
    /// Operations admitted by the deterministic statistic modifier pipeline.
    /// </summary>
    public enum ModifierOperation : byte
    {
        /// <summary>Adds an absolute value.</summary>
        AddFlat = 1,

        /// <summary>Adds a percentage expressed as a decimal fraction.</summary>
        AddPercent = 2,

        /// <summary>Multiplies by a factor.</summary>
        Multiply = 3,

        /// <summary>Raises the current value to a minimum.</summary>
        ClampMinimum = 4,

        /// <summary>Lowers the current value to a maximum.</summary>
        ClampMaximum = 5,

        /// <summary>Replaces the calculated value after all other modifier stages.</summary>
        Override = 6
    }

    /// <summary>
    /// Stable IDs for the fourteen statistics admitted by the M3 runtime.
    /// </summary>
    public static class BuiltInStatIds
    {
        /// <summary>Maximum health.</summary>
        public static readonly StatId Health = Create("base.stat.health");

        /// <summary>Movement speed.</summary>
        public static readonly StatId MoveSpeed = Create("base.stat.move_speed");

        /// <summary>Outgoing damage multiplier.</summary>
        public static readonly StatId Damage = Create("base.stat.damage");

        /// <summary>Attack-speed multiplier.</summary>
        public static readonly StatId AttackSpeed = Create("base.stat.attack_speed");

        /// <summary>Cooldown multiplier.</summary>
        public static readonly StatId Cooldown = Create("base.stat.cooldown");

        /// <summary>Range multiplier.</summary>
        public static readonly StatId Range = Create("base.stat.range");

        /// <summary>Duration multiplier.</summary>
        public static readonly StatId Duration = Create("base.stat.duration");

        /// <summary>Projectile count.</summary>
        public static readonly StatId ProjectileCount = Create("base.stat.projectile_count");

        /// <summary>Projectile or delivery penetration count.</summary>
        public static readonly StatId Pierce = Create("base.stat.pierce");

        /// <summary>Critical-hit probability in the inclusive range [0, 1].</summary>
        public static readonly StatId CriticalChance = Create("base.stat.critical_chance");

        /// <summary>Physical armor.</summary>
        public static readonly StatId Armor = Create("base.stat.armor");

        /// <summary>Pickup attraction range.</summary>
        public static readonly StatId PickupRange = Create("base.stat.pickup_range");

        /// <summary>Luck used by later deterministic rolls.</summary>
        public static readonly StatId Luck = Create("base.stat.luck");

        /// <summary>Health regeneration per second.</summary>
        public static readonly StatId Regeneration = Create("base.stat.regeneration");

        private static StatId Create(string value)
        {
            var result = StatId.Create(value);
            if (!result.IsSuccess)
            {
                throw new InvalidOperationException(result.Error.ToString());
            }

            return result.Value;
        }
    }
}
