using Game.Content.Runtime;
using Game.Core;
using UnityEngine;

namespace Game.Content.Authoring
{
    /// <summary>
    /// Minimal M1 enemy authoring metadata. No entity or spawn behavior is implemented.
    /// </summary>
    [CreateAssetMenu(menuName = "Free World/Content/Enemy", fileName = "Enemy")]
    public sealed class EnemyAuthoring : ContentAuthoringBase
    {
        [SerializeField] private float baseMaxHealth = 10f;
        [SerializeField] private float collisionRadius = 0.5f;

        /// <summary>
        /// Configures M1 enemy values.
        /// </summary>
        public void Configure(float maximumHealth, float radius)
        {
            baseMaxHealth = maximumHealth;
            collisionRadius = radius;
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
            if (baseMaxHealth <= 0f || collisionRadius <= 0f)
            {
                return Result<RuntimeContentDefinition>.Failure(
                    new Error(
                        ErrorCode.InvalidAuthoringData,
                        "Enemy health and collision radius must be positive.",
                        common.Id,
                        packId,
                        authorAssetPath));
            }

            return Result<RuntimeContentDefinition>.Success(
                new RuntimeEnemyDefinition(
                    common.Id,
                    common.LocalizedNameKey,
                    common.LocalizedDescriptionKey,
                    common.AuthorAssetPath,
                    common.Tags,
                    baseMaxHealth,
                    collisionRadius));
        }
    }
}
