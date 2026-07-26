using System;
using Game.Content.Authoring;
using Game.Content.Runtime;
using Game.Core;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>Creates the four programmatic Placeholder skills used by M4 verification.</summary>
    public static class M4TestSkillSetup
    {
        /// <summary>Gets the project folder containing M4 skill fixtures.</summary>
        public const string Folder = "Assets/GameAssets/Placeholder/TestSkillContent";
        /// <summary>Gets the M4 fixture pack path.</summary>
        public const string PackPath = Folder + "/TestM4SkillContentPack.asset";
        /// <summary>Gets the baked M4 fixture catalog path.</summary>
        public const string BakedCatalogPath = Folder + "/TestM4SkillContentPack.baked.json";

        /// <summary>Creates or updates four generic modular skill fixtures and their pack.</summary>
        [MenuItem("Tools/Free World/M4/Configure Test Skill Content")]
        public static void Configure()
        {
            EnsureFolder("Assets/GameAssets", "Placeholder");
            EnsureFolder("Assets/GameAssets/Placeholder", "TestSkillContent");

            var projectile = LoadOrCreate<SkillAuthoring>(Folder + "/TestSingleProjectile.asset");
            ConfigureIdentity(
                projectile,
                "test.skill.single_projectile",
                new[] { "content.placeholder", "delivery.projectile", "damage.direct" });
            projectile.ConfigureRuntime(
                0.5f,
                0f,
                Module("base.trigger.timer"),
                Module("base.condition.always"),
                Module("base.targeting.nearest", 10f, int0: 1),
                Module(
                    "base.delivery.projectile",
                    18f,
                    0.3f,
                    1f,
                    int0: 1,
                    presentationId: "placeholder.presentation.single_projectile"),
                new[] { Damage(12f) },
                new[]
                {
                    FloatPatch(2, "effects[0].value0", SkillPatchOperation.Add, 4f),
                    FloatPatch(2, "cooldown", SkillPatchOperation.Multiply, 0.9f)
                });

            var orbit = LoadOrCreate<SkillAuthoring>(Folder + "/TestOrbit.asset");
            ConfigureIdentity(
                orbit,
                "test.skill.orbit",
                new[] { "content.placeholder", "delivery.orbit", "damage.contact" });
            orbit.ConfigureRuntime(
                10f,
                0f,
                Module("base.trigger.timer"),
                Module("base.condition.always"),
                Module("base.targeting.self"),
                Module(
                    "base.delivery.orbit",
                    1.5f,
                    0.45f,
                    8f,
                    2f,
                    int0: 3,
                    presentationId: "placeholder.presentation.orbit"),
                new[] { Damage(5f) },
                Array.Empty<SkillLevelPatchAuthoringData>());

            var area = LoadOrCreate<SkillAuthoring>(Folder + "/TestGroundArea.asset");
            ConfigureIdentity(
                area,
                "test.skill.ground_area",
                new[] { "content.placeholder", "delivery.area", "damage.area" });
            area.ConfigureRuntime(
                1f,
                0f,
                Module("base.trigger.timer"),
                Module("base.condition.always"),
                Module("base.targeting.random_point_around_player", 1f, 4f),
                Module(
                    "base.delivery.area",
                    2f,
                    1.5f,
                    0.25f,
                    presentationId: "placeholder.presentation.ground_area"),
                new[] { Damage(6f) },
                Array.Empty<SkillLevelPatchAuthoringData>());

            var aura = LoadOrCreate<SkillAuthoring>(Folder + "/TestDamageAura.asset");
            ConfigureIdentity(
                aura,
                "test.skill.damage_aura",
                new[] { "content.placeholder", "delivery.aura", "damage.area" });
            aura.ConfigureRuntime(
                2f,
                0f,
                Module("base.trigger.timer"),
                Module("base.condition.always"),
                Module("base.targeting.self"),
                Module(
                    "base.delivery.aura",
                    3f,
                    1.5f,
                    0.25f,
                    presentationId: "placeholder.presentation.damage_aura"),
                new[] { Damage(3f) },
                Array.Empty<SkillLevelPatchAuthoringData>());

            var pack = LoadOrCreate<ContentPackAuthoring>(PackPath);
            pack.Configure(
                "test.pack.m4_skills",
                "0.1.0",
                ContentPackTopology.ModularSkillSchemaVersion,
                "0.1.0",
                string.Empty,
                Array.Empty<ContentPackDependencyAuthoring>(),
                "packs/test.m4_skills/catalog",
                "pack.test.m4_skills",
                false,
                new ContentAuthoringBase[] { projectile, orbit, area, aura });

            EditorUtility.SetDirty(projectile);
            EditorUtility.SetDirty(orbit);
            EditorUtility.SetDirty(area);
            EditorUtility.SetDirty(aura);
            EditorUtility.SetDirty(pack);
            AssetDatabase.SaveAssets();

            var bakeResult = ContentBakeUtility.Bake(pack);
            if (!bakeResult.IsSuccess) throw new UnityException(bakeResult.Error.ToString());
            ContentBakeUtility.WriteCatalog(PackPath, bakeResult.Value);
            AssetDatabase.SaveAssets();
            Debug.Log(
                "[M4 Setup] Test skill pack baked: entries=" +
                bakeResult.Value.Definitions.Count + ", hash=" +
                bakeResult.Value.ContentHash + ".");
        }

        /// <summary>Command-line setup entry point for committed fixtures.</summary>
        public static void RunFromCommandLine()
        {
            try
            {
                Configure();
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static void ConfigureIdentity(
            SkillAuthoring skill,
            string id,
            string[] tags)
        {
            skill.ConfigureIdentity(
                id,
                "content." + id + ".name",
                "content." + id + ".description",
                tags);
        }

        private static SkillModuleAuthoringData Module(
            string id,
            float value0 = 0f,
            float value1 = 0f,
            float value2 = 0f,
            float value3 = 0f,
            int int0 = 0,
            string presentationId = "")
        {
            return new SkillModuleAuthoringData
            {
                moduleId = id,
                value0 = value0,
                value1 = value1,
                value2 = value2,
                value3 = value3,
                int0 = int0,
                presentationId = presentationId
            };
        }

        private static SkillEffectAuthoringData Damage(float value)
        {
            return new SkillEffectAuthoringData
            {
                moduleId = "base.effect.damage",
                value0 = value,
                value1 = 1f,
                int0 = (int)DamageType.Physical,
                int1 = (int)DamageTags.Direct
            };
        }

        private static SkillLevelPatchAuthoringData FloatPatch(
            int level,
            string path,
            SkillPatchOperation operation,
            float value)
        {
            return new SkillLevelPatchAuthoringData
            {
                level = level,
                path = path,
                valueType = SkillPatchValueType.Float,
                operation = operation,
                floatValue = value
            };
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void EnsureFolder(string parent, string name)
        {
            var path = parent + "/" + name;
            if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, name);
        }
    }
}
