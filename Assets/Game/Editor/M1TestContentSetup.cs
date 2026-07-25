using System;
using Game.Content.Authoring;
using Game.Infrastructure;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Creates the minimal programmatic Placeholder content pack used by M1 verification.
    /// </summary>
    public static class M1TestContentSetup
    {
        /// <summary>Gets the project folder containing the M1 placeholder fixture.</summary>
        public const string Folder = "Assets/GameAssets/Placeholder/TestContent";

        /// <summary>Gets the M1 fixture pack authoring path.</summary>
        public const string PackPath = Folder + "/TestM1ContentPack.asset";

        /// <summary>Gets the M1 fixture baked catalog path.</summary>
        public const string BakedCatalogPath =
            Folder + "/TestM1ContentPack.baked.json";

        /// <summary>
        /// Creates or updates the M1 test pack, bakes it, and assigns it to Bootstrap.
        /// </summary>
        [MenuItem("Tools/Free World/M1/Configure Test Content")]
        public static void Configure()
        {
            EnsureFolder("Assets/GameAssets", "Placeholder");
            EnsureFolder("Assets/GameAssets/Placeholder", "TestContent");

            var skill = LoadOrCreate<SkillAuthoring>(Folder + "/TestSkill.asset");
            skill.ConfigureIdentity(
                "test.skill.pulse",
                "content.test.skill.pulse.name",
                "content.test.skill.pulse.description",
                new[] { "content.placeholder", "delivery.instant" });
            skill.Configure(1.25f);

            var character = LoadOrCreate<CharacterAuthoring>(
                Folder + "/TestCharacter.asset");
            character.ConfigureIdentity(
                "test.character.runner",
                "content.test.character.runner.name",
                "content.test.character.runner.description",
                new[] { "content.placeholder", "actor.player" });
            character.Configure(100f, 5f, new[] { skill });

            var enemy = LoadOrCreate<EnemyAuthoring>(Folder + "/TestEnemy.asset");
            enemy.ConfigureIdentity(
                "test.enemy.placeholder",
                "content.test.enemy.placeholder.name",
                "content.test.enemy.placeholder.description",
                new[] { "content.placeholder", "actor.enemy" });
            enemy.Configure(10f, 0.5f);

            var map = LoadOrCreate<MapAuthoring>(Folder + "/TestMap.asset");
            map.ConfigureIdentity(
                "test.map.arena",
                "content.test.map.arena.name",
                "content.test.map.arena.description",
                new[] { "content.placeholder", "map.finite" });
            map.Configure("map.provider.test", "scenes/test-placeholder-arena");

            var pack = LoadOrCreate<ContentPackAuthoring>(PackPath);
            pack.Configure(
                "test.pack.m1",
                "0.1.0",
                1,
                "0.1.0",
                string.Empty,
                Array.Empty<ContentPackDependencyAuthoring>(),
                "packs/test.m1/catalog",
                "pack.test.m1",
                false,
                new ContentAuthoringBase[] { character, skill, enemy, map });

            EditorUtility.SetDirty(skill);
            EditorUtility.SetDirty(character);
            EditorUtility.SetDirty(enemy);
            EditorUtility.SetDirty(map);
            EditorUtility.SetDirty(pack);
            AssetDatabase.SaveAssets();

            var bakeResult = ContentBakeUtility.Bake(pack);
            if (!bakeResult.IsSuccess)
            {
                throw new UnityException(bakeResult.Error.ToString());
            }

            var bakedPath = ContentBakeUtility.WriteCatalog(PackPath, bakeResult.Value);
            AssignCatalogToBootstrap(bakedPath);
            AssetDatabase.SaveAssets();
            Debug.Log(
                "[M1 Setup] Test pack baked: entries=" +
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

        private static void AssignCatalogToBootstrap(string bakedPath)
        {
            var catalogAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(bakedPath);
            if (catalogAsset == null)
            {
                throw new UnityException("Unable to load baked catalog at " + bakedPath + ".");
            }

            var scene = EditorSceneManager.OpenScene(
                M0ProjectSetup.BootstrapScenePath,
                OpenSceneMode.Single);
            GameBootstrapper bootstrapper = null;
            var roots = scene.GetRootGameObjects();
            for (var index = 0; index < roots.Length && bootstrapper == null; index++)
            {
                bootstrapper = roots[index].GetComponentInChildren<GameBootstrapper>(true);
            }

            if (bootstrapper == null)
            {
                throw new UnityException("Bootstrap scene has no GameBootstrapper.");
            }

            var serializedBootstrapper = new SerializedObject(bootstrapper);
            var catalogProperty =
                serializedBootstrapper.FindProperty("bakedTestCatalog");
            if (catalogProperty == null)
            {
                throw new UnityException(
                    "GameBootstrapper.bakedTestCatalog is not serialized.");
            }

            catalogProperty.objectReferenceValue = catalogAsset;
            serializedBootstrapper.ApplyModifiedPropertiesWithoutUndo();
            if (!EditorSceneManager.SaveScene(scene))
            {
                throw new UnityException("Unable to save Bootstrap scene.");
            }
        }
    }
}
