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
        [SerializeField] private QinglanDefinitionAuthoring[] mechanics = Array.Empty<QinglanDefinitionAuthoring>();

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

        /// <summary>Configures schema-6 generic mechanic references without changing character logic.</summary>
        public void ConfigureMechanics(QinglanDefinitionAuthoring[] characterMechanics)
        {
            mechanics = characterMechanics == null
                ? Array.Empty<QinglanDefinitionAuthoring>()
                : (QinglanDefinitionAuthoring[])characterMechanics.Clone();
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

            var mechanicIds = new ContentId[mechanics == null ? 0 : mechanics.Length];
            for (var index = 0; index < mechanicIds.Length; index++)
            {
                if (mechanics[index] == null ||
                    mechanics[index].RuntimeKind != RuntimeContentKinds.CharacterMechanic)
                {
                    return Result<RuntimeContentDefinition>.Failure(
                        new Error(
                            ErrorCode.MissingReference,
                            "Character mechanic reference is null or has the wrong kind at index " + index + ".",
                            common.Id,
                            packId,
                            authorAssetPath));
                }

                var mechanicId = ContentValidator.ValidateAuthoringId(
                    mechanics[index].ContentIdText,
                    packId,
                    authorAssetPath);
                if (!mechanicId.IsSuccess)
                    return Result<RuntimeContentDefinition>.Failure(mechanicId.Error);
                mechanicIds[index] = mechanicId.Value;
            }
            mechanicIds = ContentBaker.CanonicalizeSet(mechanicIds);

            return Result<RuntimeContentDefinition>.Success(
                new RuntimeCharacterDefinition(
                    common.Id,
                    common.LocalizedNameKey,
                    common.LocalizedDescriptionKey,
                    common.AuthorAssetPath,
                    common.Tags,
                    baseMaxHealth,
                    moveSpeed,
                    skillIds,
                    mechanicIds));
        }
    }
}
