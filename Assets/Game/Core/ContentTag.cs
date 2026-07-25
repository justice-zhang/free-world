using System;

namespace Game.Core
{
    /// <summary>
    /// Represents a canonical namespaced tag used for content composition.
    /// </summary>
    [Serializable]
    public readonly struct ContentTag :
        IEquatable<ContentTag>,
        IComparable<ContentTag>
    {
        private readonly ContentId identifier;

        private ContentTag(ContentId identifier)
        {
            this.identifier = identifier;
        }

        /// <summary>
        /// Gets the canonical tag text.
        /// </summary>
        public string Value => identifier.Value;

        /// <summary>
        /// Gets whether this value contains a valid tag.
        /// </summary>
        public bool IsValid => identifier.IsValid;

        /// <summary>
        /// Creates a tag from an input after canonical normalization.
        /// </summary>
        public static Result<ContentTag> Create(
            string input,
            ContentId packId = default,
            string authorAssetPath = "")
        {
            var idResult = ContentId.Create(input, packId, authorAssetPath);
            if (!idResult.IsSuccess)
            {
                return Result<ContentTag>.Failure(
                    new Error(
                        ErrorCode.InvalidContentTag,
                        idResult.Error.Message.Replace("ContentId", "ContentTag"),
                        default,
                        packId,
                        authorAssetPath));
            }

            return Result<ContentTag>.Success(new ContentTag(idResult.Value));
        }

        /// <summary>
        /// Attempts to create a tag from an input.
        /// </summary>
        public static bool TryCreate(string input, out ContentTag tag)
        {
            var result = Create(input);
            tag = result.IsSuccess ? result.Value : default;
            return result.IsSuccess;
        }

        /// <inheritdoc />
        public bool Equals(ContentTag other)
        {
            return identifier.Equals(other.identifier);
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            return obj is ContentTag other && Equals(other);
        }

        /// <inheritdoc />
        public int CompareTo(ContentTag other)
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

        /// <summary>
        /// Compares two tags by canonical string.
        /// </summary>
        public static bool operator ==(ContentTag left, ContentTag right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Compares two tags by canonical string.
        /// </summary>
        public static bool operator !=(ContentTag left, ContentTag right)
        {
            return !left.Equals(right);
        }
    }
}
