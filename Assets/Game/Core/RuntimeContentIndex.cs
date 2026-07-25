using System;

namespace Game.Core
{
    /// <summary>
    /// Stores a compact index assigned only for one registry load.
    /// </summary>
    [Serializable]
    public readonly struct RuntimeContentIndex :
        IEquatable<RuntimeContentIndex>,
        IComparable<RuntimeContentIndex>
    {
        private readonly int encodedValue;

        /// <summary>
        /// Initializes a valid runtime index.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
        public RuntimeContentIndex(int value)
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            encodedValue = checked(value + 1);
        }

        /// <summary>
        /// Gets the zero-based index, or -1 for the default invalid value.
        /// </summary>
        public int Value => encodedValue - 1;

        /// <summary>
        /// Gets whether this value was assigned by a registry.
        /// </summary>
        public bool IsValid => encodedValue > 0;

        /// <inheritdoc />
        public bool Equals(RuntimeContentIndex other)
        {
            return encodedValue == other.encodedValue;
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            return obj is RuntimeContentIndex other && Equals(other);
        }

        /// <inheritdoc />
        public int CompareTo(RuntimeContentIndex other)
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

        /// <summary>
        /// Compares two runtime indices.
        /// </summary>
        public static bool operator ==(RuntimeContentIndex left, RuntimeContentIndex right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Compares two runtime indices.
        /// </summary>
        public static bool operator !=(RuntimeContentIndex left, RuntimeContentIndex right)
        {
            return !left.Equals(right);
        }
    }
}
