using Game.Content.Runtime;
using Game.Core;
using UnityEngine;

namespace Game.Content.Authoring
{
    /// <summary>
    /// Minimal M1 skill authoring metadata. No execution behavior is implemented.
    /// </summary>
    [CreateAssetMenu(menuName = "Free World/Content/Skill", fileName = "Skill")]
    public sealed class SkillAuthoring : ContentAuthoringBase
    {
        [SerializeField] private float cooldownSeconds = 1f;

        /// <summary>
        /// Configures the metadata cooldown used by later skill milestones.
        /// </summary>
        public void Configure(float cooldown)
        {
            cooldownSeconds = cooldown;
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
            if (cooldownSeconds < 0f)
            {
                return Result<RuntimeContentDefinition>.Failure(
                    new Error(
                        ErrorCode.InvalidAuthoringData,
                        "Skill cooldown cannot be negative.",
                        common.Id,
                        packId,
                        authorAssetPath));
            }

            return Result<RuntimeContentDefinition>.Success(
                new RuntimeSkillDefinition(
                    common.Id,
                    common.LocalizedNameKey,
                    common.LocalizedDescriptionKey,
                    common.AuthorAssetPath,
                    common.Tags,
                    cooldownSeconds));
        }
    }
}
