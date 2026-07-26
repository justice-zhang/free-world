using System;
using Game.Content.Authoring;
using Game.Content.Runtime;
using Game.Core;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Creates the isolated programmatic Placeholder status pack used by M3 verification.
    /// </summary>
    public static class M3TestStatusSetup
    {
        /// <summary>Gets the project folder containing the M3 status fixture.</summary>
        public const string Folder =
            "Assets/GameAssets/Placeholder/TestStatusContent";

        /// <summary>Gets the M3 fixture pack authoring path.</summary>
        public const string PackPath = Folder + "/TestM3StatusContentPack.asset";

        /// <summary>Gets the M3 fixture baked catalog path.</summary>
        public const string BakedCatalogPath =
            Folder + "/TestM3StatusContentPack.baked.json";

        /// <summary>
        /// Creates or updates the three M3 status definitions and their schema-v2 pack.
        /// </summary>
        [MenuItem("Tools/Free World/M3/Configure Test Status Content")]
        public static void Configure()
        {
            EnsureFolder("Assets/GameAssets", "Placeholder");
            EnsureFolder("Assets/GameAssets/Placeholder", "TestStatusContent");

            var burning = LoadOrCreate<StatusEffectAuthoring>(
                Folder + "/TestBurningStatus.asset");
            burning.ConfigureIdentity(
                "test.status.burning",
                "content.test.status.burning.name",
                "content.test.status.burning.description",
                new[]
                {
                    "content.placeholder",
                    "status.debuff",
                    "damage.dot",
                    "element.fire"
                });
            burning.Configure(
                StatusStackingPolicy.AddStacks,
                3f,
                5,
                1f,
                new[] { "dispel.debuff", "dispel.fire" },
                new[] { "immunity.fire" });
            var burningDamage = new RuntimeStatusPeriodicDamage(
                DamageType.Fire,
                DamageTags.DamageOverTime | DamageTags.Status,
                6f,
                false,
                0.25f,
                System.Numerics.Vector2.Zero);
            burning.ConfigureBehavior(default, burningDamage, 0f);

            var slow = LoadOrCreate<StatusEffectAuthoring>(
                Folder + "/TestSlowStatus.asset");
            slow.ConfigureIdentity(
                "test.status.slow",
                "content.test.status.slow.name",
                "content.test.status.slow.description",
                new[]
                {
                    "content.placeholder",
                    "status.debuff",
                    "control.slow"
                });
            slow.Configure(
                StatusStackingPolicy.RefreshDuration,
                2f,
                1,
                0f,
                new[] { "dispel.debuff", "dispel.movement" },
                new[] { "immunity.slow" });
            var slowModifier = new RuntimeStatusModifier(
                BuiltInStatIds.MoveSpeed,
                ModifierOperation.Multiply,
                0.7f,
                10,
                CreateId("test.stack.slow"));
            slow.ConfigureBehavior(slowModifier, default, 0f);

            var shielded = LoadOrCreate<StatusEffectAuthoring>(
                Folder + "/TestShieldedStatus.asset");
            shielded.ConfigureIdentity(
                "test.status.shielded",
                "content.test.status.shielded.name",
                "content.test.status.shielded.description",
                new[]
                {
                    "content.placeholder",
                    "status.buff",
                    "defense.shield"
                });
            shielded.Configure(
                StatusStackingPolicy.ReplaceIfStronger,
                5f,
                1,
                0f,
                new[] { "dispel.buff" },
                new[] { "immunity.shield" });
            shielded.ConfigureBehavior(default, default, 10f);

            var pack = LoadOrCreate<ContentPackAuthoring>(PackPath);
            pack.Configure(
                "test.pack.m3_status",
                "0.1.0",
                ContentPackTopology.StatusDefinitionSchemaVersion,
                "0.1.0",
                string.Empty,
                Array.Empty<ContentPackDependencyAuthoring>(),
                "packs/test.m3_status/catalog",
                "pack.test.m3_status",
                false,
                new ContentAuthoringBase[] { burning, slow, shielded });

            EditorUtility.SetDirty(burning);
            EditorUtility.SetDirty(slow);
            EditorUtility.SetDirty(shielded);
            EditorUtility.SetDirty(pack);
            AssetDatabase.SaveAssets();

            var bakeResult = ContentBakeUtility.Bake(pack);
            if (!bakeResult.IsSuccess)
            {
                throw new UnityException(bakeResult.Error.ToString());
            }

            ContentBakeUtility.WriteCatalog(PackPath, bakeResult.Value);
            AssetDatabase.SaveAssets();
            Debug.Log(
                "[M3 Setup] Test status pack baked: entries=" +
                bakeResult.Value.Definitions.Count + ", hash=" +
                bakeResult.Value.ContentHash + ".");
        }

        /// <summary>
        /// Command-line setup entry point used to create deterministic committed fixtures.
        /// </summary>
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

        private static T LoadOrCreate<T>(string path)
            where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
            {
                return asset;
            }

            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void EnsureFolder(string parent, string name)
        {
            var path = parent + "/" + name;
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, name);
            }
        }

        private static ContentId CreateId(string value)
        {
            var result = ContentId.Create(value);
            if (!result.IsSuccess)
            {
                throw new UnityException(result.Error.ToString());
            }

            return result.Value;
        }
    }
}
