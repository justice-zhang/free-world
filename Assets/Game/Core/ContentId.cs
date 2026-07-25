using System;

namespace Game.Core
{
    /// <summary>
    /// Stores a canonical, namespaced content identifier and never substitutes a hash for its text.
    /// </summary>
    [Serializable]
    public readonly struct ContentId :
        IEquatable<ContentId>,
        IComparable<ContentId>
    {
        private const int MaximumLength = 128;
        private readonly string value;
        private readonly int stableHash;

        private ContentId(string canonicalValue)
        {
            value = canonicalValue;
            stableHash = ComputeStableHash(canonicalValue);
        }

        /// <summary>
        /// Gets the canonical identifier text.
        /// </summary>
        public string Value => value ?? string.Empty;

        /// <summary>
        /// Gets a deterministic hash used for lookup acceleration only.
        /// </summary>
        public int StableHash => stableHash;

        /// <summary>
        /// Gets whether this value contains a valid identifier.
        /// </summary>
        public bool IsValid => !string.IsNullOrEmpty(value);

        /// <summary>
        /// Normalizes an input by trimming outer whitespace and applying invariant lowercase.
        /// </summary>
        public static string Normalize(string input)
        {
            return input == null ? string.Empty : input.Trim().ToLowerInvariant();
        }

        /// <summary>
        /// Creates an identifier from an input after canonical normalization.
        /// </summary>
        public static Result<ContentId> Create(
            string input,
            ContentId packId = default,
            string authorAssetPath = "")
        {
            var canonical = Normalize(input);
            var validationMessage = ValidateCanonical(canonical);
            if (validationMessage != null)
            {
                return Result<ContentId>.Failure(
                    new Error(
                        ErrorCode.InvalidContentId,
                        validationMessage + " Raw value: '" + (input ?? string.Empty) + "'.",
                        default,
                        packId,
                        authorAssetPath));
            }

            return Result<ContentId>.Success(new ContentId(canonical));
        }

        /// <summary>
        /// Attempts to create an identifier from an input after canonical normalization.
        /// </summary>
        public static bool TryCreate(string input, out ContentId contentId)
        {
            var result = Create(input);
            contentId = result.IsSuccess ? result.Value : default;
            return result.IsSuccess;
        }

        /// <summary>
        /// Returns whether an input is already canonical and valid.
        /// </summary>
        public static bool IsCanonical(string input)
        {
            if (string.IsNullOrEmpty(input) ||
                !string.Equals(input, Normalize(input), StringComparison.Ordinal))
            {
                return false;
            }

            return ValidateCanonical(input) == null;
        }

        /// <summary>
        /// Serializes the identifier as its canonical string.
        /// </summary>
        public string Serialize()
        {
            return Value;
        }

        /// <summary>
        /// Deserializes a canonical identifier string.
        /// </summary>
        public static Result<ContentId> Deserialize(
            string serializedValue,
            ContentId packId = default,
            string authorAssetPath = "")
        {
            return Create(serializedValue, packId, authorAssetPath);
        }

        /// <inheritdoc />
        public bool Equals(ContentId other)
        {
            return string.Equals(value, other.value, StringComparison.Ordinal);
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            return obj is ContentId other && Equals(other);
        }

        /// <inheritdoc />
        public int CompareTo(ContentId other)
        {
            return string.Compare(value, other.value, StringComparison.Ordinal);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return stableHash;
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return Value;
        }

        /// <summary>
        /// Compares two identifiers by their complete canonical strings.
        /// </summary>
        public static bool operator ==(ContentId left, ContentId right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Compares two identifiers by their complete canonical strings.
        /// </summary>
        public static bool operator !=(ContentId left, ContentId right)
        {
            return !left.Equals(right);
        }

        private static string ValidateCanonical(string canonical)
        {
            if (string.IsNullOrEmpty(canonical))
            {
                return "ContentId is empty";
            }

            if (canonical.Length > MaximumLength)
            {
                return "ContentId exceeds " + MaximumLength + " characters";
            }

            var hasSeparator = false;
            var segmentLength = 0;
            for (var index = 0; index < canonical.Length; index++)
            {
                var character = canonical[index];
                var isAlphaNumeric =
                    (character >= 'a' && character <= 'z') ||
                    (character >= '0' && character <= '9');
                if (isAlphaNumeric)
                {
                    segmentLength++;
                    continue;
                }

                if (character == '_')
                {
                    if (segmentLength == 0 ||
                        index + 1 >= canonical.Length ||
                        canonical[index + 1] == '.' ||
                        canonical[index + 1] == '_')
                    {
                        return "ContentId underscore must separate alphanumeric characters";
                    }

                    segmentLength++;
                    continue;
                }

                if (character == '.')
                {
                    if (segmentLength == 0)
                    {
                        return "ContentId contains an empty namespace segment";
                    }

                    hasSeparator = true;
                    segmentLength = 0;
                    continue;
                }

                return "ContentId contains an invalid character at index " + index;
            }

            if (!hasSeparator)
            {
                return "ContentId must contain at least one namespace separator";
            }

            if (segmentLength == 0)
            {
                return "ContentId contains an empty namespace segment";
            }

            return null;
        }

        private static int ComputeStableHash(string canonical)
        {
            unchecked
            {
                const uint offsetBasis = 2166136261;
                const uint prime = 16777619;
                var hash = offsetBasis;
                for (var index = 0; index < canonical.Length; index++)
                {
                    hash ^= canonical[index];
                    hash *= prime;
                }

                return (int)hash;
            }
        }
    }
}
