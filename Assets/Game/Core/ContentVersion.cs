using System;

namespace Game.Core
{
    /// <summary>
    /// Represents the supported numeric major.minor.patch content version.
    /// </summary>
    [Serializable]
    public readonly struct ContentVersion :
        IEquatable<ContentVersion>,
        IComparable<ContentVersion>
    {
        /// <summary>
        /// Initializes a content version.
        /// </summary>
        public ContentVersion(int major, int minor, int patch)
        {
            if (major < 0 || minor < 0 || patch < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(major),
                    "Version components cannot be negative.");
            }

            Major = major;
            Minor = minor;
            Patch = patch;
        }

        /// <summary>
        /// Gets the major component.
        /// </summary>
        public int Major { get; }

        /// <summary>
        /// Gets the minor component.
        /// </summary>
        public int Minor { get; }

        /// <summary>
        /// Gets the patch component.
        /// </summary>
        public int Patch { get; }

        /// <summary>
        /// Gets version 0.0.0.
        /// </summary>
        public static ContentVersion Zero => default;

        /// <summary>
        /// Parses a strict major.minor.patch version.
        /// </summary>
        public static Result<ContentVersion> Parse(
            string input,
            ContentId packId = default,
            string authorAssetPath = "")
        {
            var components = string.IsNullOrEmpty(input)
                ? Array.Empty<string>()
                : input.Split('.');
            if (components.Length != 3 ||
                !TryParseComponent(components[0], out var major) ||
                !TryParseComponent(components[1], out var minor) ||
                !TryParseComponent(components[2], out var patch))
            {
                return Result<ContentVersion>.Failure(
                    new Error(
                        ErrorCode.InvalidContentVersion,
                        "Version must use non-negative major.minor.patch integers. Raw value: '" +
                        (input ?? string.Empty) + "'.",
                        default,
                        packId,
                        authorAssetPath));
            }

            return Result<ContentVersion>.Success(new ContentVersion(major, minor, patch));
        }

        /// <summary>
        /// Attempts to parse a strict major.minor.patch version.
        /// </summary>
        public static bool TryParse(string input, out ContentVersion version)
        {
            var result = Parse(input);
            version = result.IsSuccess ? result.Value : default;
            return result.IsSuccess;
        }

        /// <inheritdoc />
        public int CompareTo(ContentVersion other)
        {
            var majorComparison = Major.CompareTo(other.Major);
            if (majorComparison != 0)
            {
                return majorComparison;
            }

            var minorComparison = Minor.CompareTo(other.Minor);
            return minorComparison != 0 ? minorComparison : Patch.CompareTo(other.Patch);
        }

        /// <inheritdoc />
        public bool Equals(ContentVersion other)
        {
            return Major == other.Major && Minor == other.Minor && Patch == other.Patch;
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            return obj is ContentVersion other && Equals(other);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                var hash = Major;
                hash = (hash * 397) ^ Minor;
                hash = (hash * 397) ^ Patch;
                return hash;
            }
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return Major + "." + Minor + "." + Patch;
        }

        /// <summary>
        /// Compares versions.
        /// </summary>
        public static bool operator ==(ContentVersion left, ContentVersion right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Compares versions.
        /// </summary>
        public static bool operator !=(ContentVersion left, ContentVersion right)
        {
            return !left.Equals(right);
        }

        /// <summary>
        /// Compares versions.
        /// </summary>
        public static bool operator <(ContentVersion left, ContentVersion right)
        {
            return left.CompareTo(right) < 0;
        }

        /// <summary>
        /// Compares versions.
        /// </summary>
        public static bool operator >(ContentVersion left, ContentVersion right)
        {
            return left.CompareTo(right) > 0;
        }

        /// <summary>
        /// Compares versions.
        /// </summary>
        public static bool operator <=(ContentVersion left, ContentVersion right)
        {
            return left.CompareTo(right) <= 0;
        }

        /// <summary>
        /// Compares versions.
        /// </summary>
        public static bool operator >=(ContentVersion left, ContentVersion right)
        {
            return left.CompareTo(right) >= 0;
        }

        private static bool TryParseComponent(string input, out int value)
        {
            value = 0;
            if (string.IsNullOrEmpty(input) ||
                (input.Length > 1 && input[0] == '0'))
            {
                return false;
            }

            for (var index = 0; index < input.Length; index++)
            {
                var character = input[index];
                if (character < '0' || character > '9')
                {
                    return false;
                }

                try
                {
                    value = checked((value * 10) + (character - '0'));
                }
                catch (OverflowException)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
