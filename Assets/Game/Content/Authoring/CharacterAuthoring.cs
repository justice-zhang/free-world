using System;
using Game.Content.Runtime;
using Game.Core;
using UnityEngine;

namespace Game.Content.Authoring
{
    /// <summary>
    /// Minimal M1 character authoring data.
    /// </summary>
    [CreateAssetMenu(menuName = "Free World/Content/Character", fileName = "Character")]
    public sealed class CharacterAuthoring : ContentAuthoringBase
    {
        [SerializeField] private float baseMaxHealth = 100f;
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private SkillAuthoring[] startingSkills = Array.Empty<SkillAuthoring>();

        /// <summary>
        /// Configures M1 character values.
        /// </summary>
        public void Configure(
            float maximumHealth,
            float movementSpeed,
            SkillAuthoring[] skills)
        {
            baseMaxHealth = maximumHealth;
            moveSpeed = movementSpeed;
            startingSkills = skills == null
                ? Array.Empty<SkillAuthoring>()
                : (SkillAuthoring[])skills.Clone();
        }

        internal override Result<RuntimeContentDefinition> Bake(
            ContentId packId,
            string authorAssetPath)
        {
            var commonResult = BakeCommon(packId, authorAssetPath);
            if (!commonResult.IsSuccess)
            {
                return Result<RuntimeContentDefinition>.Failure(commonResult.Error);
            }

            var common = commonResult.Value;
            if (baseMaxHealth <= 0f || moveSpeed < 0f)
            {
                return Result<RuntimeContentDefinition>.Failure(
                    new Error(
                        ErrorCode.InvalidAuthoringData,
                        "Character health must be positive and move speed cannot be negative.",
                        common.Id,
                        packId,
                        authorAssetPath));
            }

            var skillIds = new ContentId[startingSkills.Length];
            for (var index = 0; index < startingSkills.Length; index++)
            {
                if (startingSkills[index] == null)
                {
                    return Result<RuntimeContentDefinition>.Failure(
                        new Error(
                            ErrorCode.MissingReference,
                            "Character starting skill reference is null at index " + index + ".",
                            common.Id,
                            packId,
                            authorAssetPath));
                }

                var skillIdResult = ContentValidator.ValidateAuthoringId(
                    startingSkills[index].ContentIdText,
                    packId,
                    authorAssetPath);
                if (!skillIdResult.IsSuccess)
                {
                    return Result<RuntimeContentDefinition>.Failure(skillIdResult.Error);
                }

                skillIds[index] = skillIdResult.Value;
            }

            return Result<RuntimeContentDefinition>.Success(
                new RuntimeCharacterDefinition(
                    common.Id,
                    common.LocalizedNameKey,
                    common.LocalizedDescriptionKey,
                    common.AuthorAssetPath,
                    common.Tags,
                    baseMaxHealth,
                    moveSpeed,
                    skillIds));
        }
    }
}
