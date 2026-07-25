using System;
using System.Collections.Generic;
using Game.Content.Runtime;
using Game.Core;
using UnityEngine;

namespace Game.Content.Authoring
{
    /// <summary>
    /// Base ScriptableObject for authored content identity and localization metadata.
    /// </summary>
    public abstract class ContentAuthoringBase : ScriptableObject
    {
        [SerializeField] private string contentId = string.Empty;
        [SerializeField] private string localizedNameKey = string.Empty;
        [SerializeField] private string localizedDescriptionKey = string.Empty;
        [SerializeField] private string[] tags = Array.Empty<string>();

        /// <summary>
        /// Gets the raw authored content ID.
        /// </summary>
        public string ContentIdText => contentId;

        /// <summary>
        /// Gets the authored localization name key.
        /// </summary>
        public string LocalizedNameKey => localizedNameKey;

        /// <summary>
        /// Gets the authored localization description key.
        /// </summary>
        public string LocalizedDescriptionKey => localizedDescriptionKey;

        /// <summary>
        /// Gets the raw authored tag strings.
        /// </summary>
        public IReadOnlyList<string> Tags => tags;

        /// <summary>
        /// Sets common authoring fields. Intended for editor tools and test fixtures.
        /// </summary>
        public void ConfigureIdentity(
            string id,
            string nameKey,
            string descriptionKey,
            string[] contentTags)
        {
            contentId = id ?? string.Empty;
            localizedNameKey = nameKey ?? string.Empty;
            localizedDescriptionKey = descriptionKey ?? string.Empty;
            tags = contentTags == null ? Array.Empty<string>() : (string[])contentTags.Clone();
        }

        internal abstract Result<RuntimeContentDefinition> Bake(
            ContentId packId,
            string authorAssetPath);

        internal Result<AuthoringCommonData> BakeCommon(
            ContentId packId,
            string authorAssetPath)
        {
            var idResult = ContentValidator.ValidateAuthoringId(
                contentId,
                packId,
                authorAssetPath);
            if (!idResult.IsSuccess)
            {
                return Result<AuthoringCommonData>.Failure(idResult.Error);
            }

            if (string.IsNullOrWhiteSpace(localizedNameKey) ||
                string.IsNullOrWhiteSpace(localizedDescriptionKey))
            {
                return Result<AuthoringCommonData>.Failure(
                    new Error(
                        ErrorCode.InvalidAuthoringData,
                        "Name and description localization keys are required.",
                        idResult.Value,
                        packId,
                        authorAssetPath));
            }

            var runtimeTags = new ContentTag[tags.Length];
            for (var index = 0; index < tags.Length; index++)
            {
                if (!ContentId.IsCanonical(tags[index]))
                {
                    return Result<AuthoringCommonData>.Failure(
                        new Error(
                            ErrorCode.InvalidContentTag,
                            "Authoring ContentTag must already be lowercase canonical text: '" +
                            (tags[index] ?? string.Empty) + "'.",
                            idResult.Value,
                            packId,
                            authorAssetPath));
                }

                var tagResult = ContentTag.Create(tags[index], packId, authorAssetPath);
                if (!tagResult.IsSuccess)
                {
                    return Result<AuthoringCommonData>.Failure(tagResult.Error);
                }

                runtimeTags[index] = tagResult.Value;
            }

            return Result<AuthoringCommonData>.Success(
                new AuthoringCommonData(
                    idResult.Value,
                    localizedNameKey,
                    localizedDescriptionKey,
                    authorAssetPath,
                    runtimeTags));
        }
    }

    internal readonly struct AuthoringCommonData
    {
        public AuthoringCommonData(
            ContentId id,
            string localizedNameKey,
            string localizedDescriptionKey,
            string authorAssetPath,
            ContentTag[] tags)
        {
            Id = id;
            LocalizedNameKey = localizedNameKey;
            LocalizedDescriptionKey = localizedDescriptionKey;
            AuthorAssetPath = authorAssetPath;
            Tags = tags;
        }

        public ContentId Id { get; }

        public string LocalizedNameKey { get; }

        public string LocalizedDescriptionKey { get; }

        public string AuthorAssetPath { get; }

        public ContentTag[] Tags { get; }
    }
}
