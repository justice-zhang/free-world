using System;

namespace Game.Core
{
    /// <summary>
    /// Identifies a stable category of failure returned by core and content services.
    /// </summary>
    public enum ErrorCode
    {
        /// <summary>No failure.</summary>
        None = 0,
        /// <summary>A content identifier is invalid.</summary>
        InvalidContentId = 1,
        /// <summary>A content tag is invalid.</summary>
        InvalidContentTag = 2,
        /// <summary>A content version is invalid.</summary>
        InvalidContentVersion = 3,
        /// <summary>A runtime content index is invalid.</summary>
        InvalidRuntimeContentIndex = 4,
        /// <summary>Authoring data is invalid.</summary>
        InvalidAuthoringData = 5,
        /// <summary>A baked catalog is invalid.</summary>
        InvalidCatalog = 6,
        /// <summary>Multiple definitions declare the same content ID.</summary>
        DuplicateContentId = 7,
        /// <summary>Multiple manifests declare the same pack ID.</summary>
        DuplicatePackId = 8,
        /// <summary>A definition references unavailable content.</summary>
        MissingReference = 9,
        /// <summary>A required content pack is unavailable.</summary>
        MissingDependency = 10,
        /// <summary>Content pack dependencies contain a cycle.</summary>
        DependencyCycle = 11,
        /// <summary>A game or pack version is incompatible.</summary>
        IncompatibleVersion = 12,
        /// <summary>A pack uses an unsupported content schema.</summary>
        UnsupportedSchemaVersion = 13,
        /// <summary>A catalog payload does not match its declared hash.</summary>
        ContentHashMismatch = 14
    }

    /// <summary>
    /// Describes a structured failure and its content provenance.
    /// </summary>
    [Serializable]
    public readonly struct Error : IEquatable<Error>
    {
        /// <summary>
        /// Initializes a structured error.
        /// </summary>
        public Error(
            ErrorCode code,
            string message,
            ContentId contentId = default,
            ContentId packId = default,
            string authorAssetPath = "")
        {
            Code = code;
            Message = message ?? string.Empty;
            ContentId = contentId;
            PackId = packId;
            AuthorAssetPath = authorAssetPath ?? string.Empty;
        }

        /// <summary>
        /// Gets the stable error category.
        /// </summary>
        public ErrorCode Code { get; }

        /// <summary>
        /// Gets the diagnostic message.
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// Gets the content identifier associated with the failure, when available.
        /// </summary>
        public ContentId ContentId { get; }

        /// <summary>
        /// Gets the source pack associated with the failure, when available.
        /// </summary>
        public ContentId PackId { get; }

        /// <summary>
        /// Gets the author asset path associated with the failure, when available.
        /// </summary>
        public string AuthorAssetPath { get; }

        /// <summary>
        /// Gets an empty error value.
        /// </summary>
        public static Error None => default;

        /// <summary>
        /// Returns whether this value represents an error.
        /// </summary>
        public bool IsError => Code != ErrorCode.None;

        /// <inheritdoc />
        public bool Equals(Error other)
        {
            return Code == other.Code &&
                   ContentId.Equals(other.ContentId) &&
                   PackId.Equals(other.PackId) &&
                   string.Equals(Message, other.Message, StringComparison.Ordinal) &&
                   string.Equals(
                       AuthorAssetPath,
                       other.AuthorAssetPath,
                       StringComparison.Ordinal);
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            return obj is Error other && Equals(other);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                var hash = (int)Code;
                hash = (hash * 397) ^ ContentId.GetHashCode();
                hash = (hash * 397) ^ PackId.GetHashCode();
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(Message ?? string.Empty);
                hash = (hash * 397) ^
                       StringComparer.Ordinal.GetHashCode(AuthorAssetPath ?? string.Empty);
                return hash;
            }
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return Code + ": " + Message +
                   " [content=" + ContentId +
                   ", pack=" + PackId +
                   ", asset=" + AuthorAssetPath + "]";
        }
    }
}
