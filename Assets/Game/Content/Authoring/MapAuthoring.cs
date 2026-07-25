using Game.Content.Runtime;
using Game.Core;
using UnityEngine;

namespace Game.Content.Authoring
{
    /// <summary>
    /// Minimal M1 map authoring metadata. No map runtime is instantiated.
    /// </summary>
    [CreateAssetMenu(menuName = "Free World/Content/Map", fileName = "Map")]
    public sealed class MapAuthoring : ContentAuthoringBase
    {
        [SerializeField] private string runtimeProviderId = string.Empty;
        [SerializeField] private string sceneAddress = string.Empty;

        /// <summary>
        /// Configures the deferred runtime provider and scene address.
        /// </summary>
        public void Configure(string providerId, string address)
        {
            runtimeProviderId = providerId ?? string.Empty;
            sceneAddress = address ?? string.Empty;
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
            if (string.IsNullOrWhiteSpace(runtimeProviderId) ||
                string.IsNullOrWhiteSpace(sceneAddress))
            {
                return Result<RuntimeContentDefinition>.Failure(
                    new Error(
                        ErrorCode.InvalidAuthoringData,
                        "Map runtime provider ID and scene address are required.",
                        common.Id,
                        packId,
                        authorAssetPath));
            }

            return Result<RuntimeContentDefinition>.Success(
                new RuntimeMapDefinition(
                    common.Id,
                    common.LocalizedNameKey,
                    common.LocalizedDescriptionKey,
                    common.AuthorAssetPath,
                    common.Tags,
                    runtimeProviderId,
                    sceneAddress));
        }
    }
}
