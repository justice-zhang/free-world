using System;
using Game.Content.Runtime;
using Game.Core;
using UnityEngine;

namespace Game.Content.Authoring
{
    /// <summary>
    /// Generic schema-6 authoring surface for the fourteen approved Qinglan definition kinds.
    /// The selected kind is still validated by the runtime DTO factory and ContentValidator.
    /// </summary>
    [CreateAssetMenu(
        fileName = "QinglanDefinition",
        menuName = "Game/Content/Qinglan Schema 6 Definition")]
    public sealed class QinglanDefinitionAuthoring : ContentAuthoringBase
    {
        [SerializeField] private string runtimeKind = RuntimeContentKinds.CharacterMechanic;
        [SerializeField] private QinglanRuntimeDefinitionDto runtime = new QinglanRuntimeDefinitionDto();

        public string RuntimeKind => runtimeKind;
        public QinglanRuntimeDefinitionDto RuntimeData => runtime;

        /// <summary>Configures kind-specific values for editor tools and programmatic fixtures.</summary>
        public void ConfigureRuntime(string kind, QinglanRuntimeDefinitionDto data)
        {
            runtimeKind = kind ?? string.Empty;
            runtime = data ?? new QinglanRuntimeDefinitionDto();
        }

        internal override Result<RuntimeContentDefinition> Bake(
            ContentId packId,
            string authorAssetPath)
        {
            var commonResult = BakeCommon(packId, authorAssetPath);
            if (!commonResult.IsSuccess)
                return Result<RuntimeContentDefinition>.Failure(commonResult.Error);
            if (runtime == null)
            {
                return Result<RuntimeContentDefinition>.Failure(
                    new Error(
                        ErrorCode.InvalidAuthoringData,
                        "Schema-6 runtime payload is missing.",
                        commonResult.Value.Id,
                        packId,
                        authorAssetPath));
            }

            var common = commonResult.Value;
            return runtime.ToDefinition(
                runtimeKind,
                packId,
                common.Id,
                common.LocalizedNameKey,
                common.LocalizedDescriptionKey,
                common.AuthorAssetPath,
                common.Tags);
        }
    }
}
