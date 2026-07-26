using System;
using Game.Content.Runtime;
using Game.Core;
using Game.Simulation;

namespace Game.Tests.EditMode
{
    internal static class SkillTestFactory
    {
        public static readonly ContentVersion GameVersion = new ContentVersion(0, 1, 0);

        public static ContentId Id(string value)
        {
            var result = ContentId.Create(value);
            if (!result.IsSuccess) throw new InvalidOperationException(result.Error.ToString());
            return result.Value;
        }

        public static SkillModuleDefinition Module(
            ContentId id,
            float value0 = 0f,
            float value1 = 0f,
            float value2 = 0f,
            float value3 = 0f,
            int int0 = 0,
            ContentId presentation = default)
        {
            return new SkillModuleDefinition(
                id,
                value0,
                value1,
                value2,
                value3,
                int0,
                0,
                presentation);
        }

        public static EffectOp Damage(float value = 10f)
        {
            return new EffectOp(
                EffectOpCode.Damage,
                value,
                1f,
                0f,
                (int)DamageType.Physical,
                (int)DamageTags.Direct,
                flags: EffectOpFlags.None);
        }

        public static EffectOp GainResource(float value = 1f)
        {
            return new EffectOp(EffectOpCode.GainResource, value);
        }

        public static RuntimeSkillDefinition Skill(
            string id,
            ContentId trigger,
            in SkillModuleDefinition targeting,
            in SkillModuleDefinition delivery,
            EffectOp[] effects,
            float cooldown = 1f,
            float resourceCost = 0f,
            SkillLevelPatch[] patches = null)
        {
            return new RuntimeSkillDefinition(
                Id(id),
                "content." + id + ".name",
                "content." + id + ".description",
                "Assets/Test/" + id + ".asset",
                Array.Empty<ContentTag>(),
                cooldown,
                resourceCost,
                Module(trigger),
                Module(SkillModuleIds.ConditionAlways),
                targeting,
                delivery,
                effects,
                patches ?? Array.Empty<SkillLevelPatch>());
        }

        public static ContentRegistry Registry(params RuntimeContentDefinition[] definitions)
        {
            var packId = Id("test.pack.m4_runtime");
            var manifest = new ContentPackManifest(
                packId,
                GameVersion,
                ContentPackTopology.ModularSkillSchemaVersion,
                GameVersion,
                null,
                Array.Empty<ContentPackDependency>(),
                "packs/test/m4_runtime",
                "pack.test.m4_runtime",
                false,
                "Assets/Test/M4RuntimePack.asset");
            var catalog = BakedContentCatalog.Create(manifest, definitions);
            var registry = new ContentRegistry();
            var result = registry.Load(new[] { catalog }, GameVersion);
            if (!result.IsSuccess) throw new InvalidOperationException(result.Error.ToString());
            return registry;
        }

        public static SkillRuntime Runtime(ContentRegistry registry, ulong seed = 1UL)
        {
            var compiled = SkillRuntimeCatalog.Build(
                registry,
                SkillModuleRegistry.CreateDefault());
            if (!compiled.IsSuccess) throw new InvalidOperationException(compiled.Error.ToString());
            return new SkillRuntime(compiled.Value, seed);
        }

        public static RuntimeContentIndex IndexOf(ContentRegistry registry, ContentId id)
        {
            if (!registry.TryGet(id, out var entry))
            {
                throw new InvalidOperationException("Missing test content " + id + ".");
            }

            return entry.Index;
        }

        public static ContentId Placeholder(string suffix)
        {
            return Id("placeholder.presentation." + suffix);
        }
    }
}
