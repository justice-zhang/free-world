using System;
using Game.Core;

namespace Game.Content.Runtime
{
    /// <summary>Serialized schema-3 module definition.</summary>
    [Serializable]
    public sealed class SkillModuleDefinitionDto
    {
        public string moduleId;
        public float value0;
        public float value1;
        public float value2;
        public float value3;
        public int int0;
        public int int1;
        public string presentationId;
        public string referenceId0;
        public string referenceId1;
        public string tag0;
        public string tag1;

        internal Result<SkillModuleDefinition> ToDefinition(
            ContentId packId,
            ContentId ownerId,
            string sourceAssetPath,
            string label)
        {
            var idResult = CatalogDtoParsing.ParseCanonicalId(
                moduleId,
                packId,
                sourceAssetPath,
                label + " module ID");
            if (!idResult.IsSuccess)
            {
                return Result<SkillModuleDefinition>.Failure(idResult.Error);
            }

            var presentation = default(ContentId);
            if (!string.IsNullOrEmpty(presentationId))
            {
                var presentationResult = CatalogDtoParsing.ParseCanonicalId(
                    presentationId,
                    packId,
                    sourceAssetPath,
                    label + " presentation ID");
                if (!presentationResult.IsSuccess)
                {
                    return Result<SkillModuleDefinition>.Failure(presentationResult.Error);
                }

                presentation = presentationResult.Value;
            }

            var firstResult = ParseOptionalId(referenceId0, packId, sourceAssetPath, label + " reference 0");
            if (!firstResult.IsSuccess) return Result<SkillModuleDefinition>.Failure(firstResult.Error);
            var secondResult = ParseOptionalId(referenceId1, packId, sourceAssetPath, label + " reference 1");
            if (!secondResult.IsSuccess) return Result<SkillModuleDefinition>.Failure(secondResult.Error);
            var firstTag = ParseOptionalTag(tag0, packId, sourceAssetPath, label + " tag 0");
            if (!firstTag.IsSuccess) return Result<SkillModuleDefinition>.Failure(firstTag.Error);
            var secondTag = ParseOptionalTag(tag1, packId, sourceAssetPath, label + " tag 1");
            if (!secondTag.IsSuccess) return Result<SkillModuleDefinition>.Failure(secondTag.Error);

            return Result<SkillModuleDefinition>.Success(
                SkillModuleDefinition.CreateReferenced(
                    idResult.Value,
                    value0,
                    value1,
                    value2,
                    value3,
                    int0,
                    int1,
                    presentation,
                    firstResult.Value,
                    secondResult.Value,
                    firstTag.Value,
                    secondTag.Value));
        }

        internal static SkillModuleDefinitionDto FromDefinition(
            in SkillModuleDefinition definition)
        {
            return new SkillModuleDefinitionDto
            {
                moduleId = definition.ModuleId.Value,
                value0 = definition.Value0,
                value1 = definition.Value1,
                value2 = definition.Value2,
                value3 = definition.Value3,
                int0 = definition.Int0,
                int1 = definition.Int1,
                presentationId = definition.PresentationId.Value,
                referenceId0 = definition.ReferenceId0.Value,
                referenceId1 = definition.ReferenceId1.Value,
                tag0 = definition.Tag0.Value,
                tag1 = definition.Tag1.Value
            };
        }

        private static Result<ContentId> ParseOptionalId(string value, ContentId packId, string path, string label)
        {
            return string.IsNullOrEmpty(value)
                ? Result<ContentId>.Success(default)
                : CatalogDtoParsing.ParseCanonicalId(value, packId, path, label);
        }

        private static Result<ContentTag> ParseOptionalTag(string value, ContentId packId, string path, string label)
        {
            if (string.IsNullOrEmpty(value)) return Result<ContentTag>.Success(default);
            if (!ContentId.IsCanonical(value))
                return Result<ContentTag>.Failure(new Error(ErrorCode.InvalidCatalog, label + " must be canonical.", default, packId, path));
            return ContentTag.Create(value, packId, path);
        }
    }

    /// <summary>Serialized schema-3 effect operation.</summary>
    [Serializable]
    public sealed class SkillEffectOpDto
    {
        public string moduleId;
        public float value0;
        public float value1;
        public float value2;
        public int int0;
        public int int1;
        public string referenceId0;
        public string referenceId1;
        public string tag0;
        public string statId0;
        public uint flags;

        internal Result<EffectOp> ToEffect(
            ContentId packId,
            ContentId ownerId,
            string sourceAssetPath)
        {
            var moduleResult = CatalogDtoParsing.ParseCanonicalId(
                moduleId,
                packId,
                sourceAssetPath,
                "effect module ID");
            if (!moduleResult.IsSuccess)
            {
                return Result<EffectOp>.Failure(moduleResult.Error);
            }

            if (!SkillModuleIds.TryGetEffectCode(moduleResult.Value, out var code))
            {
                return Result<EffectOp>.Failure(
                    Failure(
                        "Serialized effect module ID '" + moduleResult.Value +
                        "' is not explicitly registered.",
                        ownerId,
                        packId,
                        sourceAssetPath));
            }

            var firstResult = ParseOptionalId(
                referenceId0,
                "effect reference 0",
                packId,
                ownerId,
                sourceAssetPath);
            if (!firstResult.IsSuccess) return Result<EffectOp>.Failure(firstResult.Error);
            var secondResult = ParseOptionalId(
                referenceId1,
                "effect reference 1",
                packId,
                ownerId,
                sourceAssetPath);
            if (!secondResult.IsSuccess) return Result<EffectOp>.Failure(secondResult.Error);

            var tag = default(ContentTag);
            if (!string.IsNullOrEmpty(tag0))
            {
                if (!ContentId.IsCanonical(tag0))
                {
                    return Result<EffectOp>.Failure(
                        Failure("Serialized effect tag is not canonical.", ownerId, packId, sourceAssetPath));
                }

                var tagResult = ContentTag.Create(tag0, packId, sourceAssetPath);
                if (!tagResult.IsSuccess) return Result<EffectOp>.Failure(tagResult.Error);
                tag = tagResult.Value;
            }

            var stat = default(StatId);
            if (!string.IsNullOrEmpty(statId0))
            {
                if (!ContentId.IsCanonical(statId0))
                {
                    return Result<EffectOp>.Failure(
                        Failure("Serialized effect StatId is not canonical.", ownerId, packId, sourceAssetPath));
                }

                var statResult = StatId.Create(statId0, packId, sourceAssetPath);
                if (!statResult.IsSuccess) return Result<EffectOp>.Failure(statResult.Error);
                stat = statResult.Value;
            }

            return Result<EffectOp>.Success(
                new EffectOp(
                    code,
                    value0,
                    value1,
                    value2,
                    int0,
                    int1,
                    firstResult.Value,
                    secondResult.Value,
                    tag,
                    stat,
                    (EffectOpFlags)flags));
        }

        internal static SkillEffectOpDto FromEffect(in EffectOp effect)
        {
            return new SkillEffectOpDto
            {
                moduleId = SkillModuleIds.GetEffectId(effect.Code).Value,
                value0 = effect.Value0,
                value1 = effect.Value1,
                value2 = effect.Value2,
                int0 = effect.Int0,
                int1 = effect.Int1,
                referenceId0 = effect.ReferenceId0.Value,
                referenceId1 = effect.ReferenceId1.Value,
                tag0 = effect.Tag0.Value,
                statId0 = effect.StatId0.Value,
                flags = (uint)effect.Flags
            };
        }

        private static Result<ContentId> ParseOptionalId(
            string value,
            string label,
            ContentId packId,
            ContentId ownerId,
            string sourceAssetPath)
        {
            if (string.IsNullOrEmpty(value))
            {
                return Result<ContentId>.Success(default);
            }

            return CatalogDtoParsing.ParseCanonicalId(
                value,
                packId,
                sourceAssetPath,
                label);
        }

        private static Error Failure(
            string message,
            ContentId ownerId,
            ContentId packId,
            string sourceAssetPath)
        {
            return new Error(
                ErrorCode.InvalidCatalog,
                message,
                ownerId,
                packId,
                sourceAssetPath);
        }
    }

    /// <summary>Serialized schema-3 level patch with an explicit path token.</summary>
    [Serializable]
    public sealed class SkillLevelPatchDto
    {
        public int level;
        public string path;
        public string valueType;
        public string operation;
        public float floatValue;
        public int integerValue;

        internal Result<SkillLevelPatch> ToPatch(
            int effectCount,
            ContentId packId,
            ContentId ownerId,
            string sourceAssetPath)
        {
            if (!SkillLevelPatchPath.TryResolve(
                    path,
                    effectCount,
                    out var target,
                    out var targetIndex,
                    out var requiredType))
            {
                return Result<SkillLevelPatch>.Failure(
                    Failure("Serialized Skill LevelPatch path is invalid.", ownerId, packId, sourceAssetPath));
            }

            SkillPatchValueType parsedType;
            if (valueType == "float") parsedType = SkillPatchValueType.Float;
            else if (valueType == "integer") parsedType = SkillPatchValueType.Integer;
            else
            {
                return Result<SkillLevelPatch>.Failure(
                    Failure("Serialized Skill LevelPatch value type is invalid.", ownerId, packId, sourceAssetPath));
            }

            SkillPatchOperation parsedOperation;
            if (operation == "add") parsedOperation = SkillPatchOperation.Add;
            else if (operation == "multiply") parsedOperation = SkillPatchOperation.Multiply;
            else if (operation == "override") parsedOperation = SkillPatchOperation.Override;
            else
            {
                return Result<SkillLevelPatch>.Failure(
                    Failure("Serialized Skill LevelPatch operation is invalid.", ownerId, packId, sourceAssetPath));
            }

            if (parsedType != requiredType)
            {
                return Result<SkillLevelPatch>.Failure(
                    Failure("Serialized Skill LevelPatch value type does not match its path.", ownerId, packId, sourceAssetPath));
            }

            return Result<SkillLevelPatch>.Success(
                new SkillLevelPatch(
                    level,
                    target,
                    targetIndex,
                    parsedType,
                    parsedOperation,
                    floatValue,
                    integerValue));
        }

        internal static SkillLevelPatchDto FromPatch(in SkillLevelPatch patch)
        {
            return new SkillLevelPatchDto
            {
                level = patch.Level,
                path = SkillLevelPatchPath.GetPath(patch.Target, patch.TargetIndex),
                valueType = patch.ValueType == SkillPatchValueType.Float ? "float" : "integer",
                operation = patch.Operation == SkillPatchOperation.Add
                    ? "add"
                    : patch.Operation == SkillPatchOperation.Multiply
                        ? "multiply"
                        : "override",
                floatValue = patch.FloatValue,
                integerValue = patch.IntegerValue
            };
        }

        private static Error Failure(
            string message,
            ContentId ownerId,
            ContentId packId,
            string sourceAssetPath)
        {
            return new Error(
                ErrorCode.InvalidCatalog,
                message,
                ownerId,
                packId,
                sourceAssetPath);
        }
    }
}
