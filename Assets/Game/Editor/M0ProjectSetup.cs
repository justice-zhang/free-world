using Game.Infrastructure;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Editor
{
    /// <summary>
    /// Creates the deterministic M0 scene and generated development assets.
    /// </summary>
    public static class M0ProjectSetup
    {
        /// <summary>
        /// Project-relative path of the only M0 runtime scene.
        /// </summary>
        public const string BootstrapScenePath = "Assets/Scenes/Bootstrap.unity";

        /// <summary>
        /// Configures generated placeholder assets, the bootstrap scene, and build scenes.
        /// </summary>
        [MenuItem("Tools/Free World/M0/Configure Project")]
        public static void Configure()
        {
            PlaceholderAssetGenerator.GenerateAll();
            CreateBootstrapScene();
            EditorBuildSettings.scenes =
                new[] { new EditorBuildSettingsScene(BootstrapScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("[M0] Project configuration completed.");
        }

        private static void CreateBootstrapScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "Bootstrap";

            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.orthographic = true;
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);

            var bootstrapObject = new GameObject("GameBootstrapper");
            bootstrapObject.AddComponent<GameBootstrapper>();

            if (!EditorSceneManager.SaveScene(scene, BootstrapScenePath, false))
            {
                throw new UnityException("Failed to save " + BootstrapScenePath);
            }
        }
    }
}
