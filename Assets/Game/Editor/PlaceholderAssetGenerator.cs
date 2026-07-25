using System;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Generates the programmatic textures allowed during framework development.
    /// </summary>
    public static class PlaceholderAssetGenerator
    {
        /// <summary>
        /// Project-relative folder used for generated placeholder assets.
        /// </summary>
        public const string OutputFolder = "Assets/GameAssets/Placeholder";

        /// <summary>
        /// Addressables label applied to every placeholder asset.
        /// </summary>
        public const string PlaceholderLabel = "placeholder";

        /// <summary>
        /// Addressables label that prevents placeholder assets from shipping.
        /// </summary>
        public const string DevelopmentOnlyLabel = "development-only";

        /// <summary>
        /// Project-relative path for the generated circle texture.
        /// </summary>
        public const string CirclePath = OutputFolder + "/circle.png";

        /// <summary>
        /// Project-relative path for the generated square texture.
        /// </summary>
        public const string SquarePath = OutputFolder + "/square.png";

        /// <summary>
        /// Project-relative path for the generated line texture.
        /// </summary>
        public const string LinePath = OutputFolder + "/line.png";

        private const int TextureSize = 64;
        private static readonly Color32 Foreground = new Color32(255, 255, 255, 255);
        private static readonly Color32 Transparent = new Color32(0, 0, 0, 0);

        /// <summary>
        /// Generates or replaces all M0 placeholder textures and assigns their labels.
        /// </summary>
        [MenuItem("Tools/Free World/M0/Generate Placeholder Assets")]
        public static void GenerateAll()
        {
            Directory.CreateDirectory(GetAbsolutePath(OutputFolder));

            GenerateTexture(CirclePath, IsInsideCircle);
            GenerateTexture(SquarePath, IsInsideSquare);
            GenerateTexture(LinePath, IsInsideLine);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Debug.Log("[M0] Generated circle, square, and line placeholder textures.");
        }

        private static void GenerateTexture(string assetPath, Func<int, int, bool> fillPredicate)
        {
            var texture = new Texture2D(
                TextureSize,
                TextureSize,
                TextureFormat.RGBA32,
                false,
                true);

            texture.name = Path.GetFileNameWithoutExtension(assetPath);
            var pixels = new Color32[TextureSize * TextureSize];
            var index = 0;

            for (var y = 0; y < TextureSize; y++)
            {
                for (var x = 0; x < TextureSize; x++)
                {
                    pixels[index++] = fillPredicate(x, y) ? Foreground : Transparent;
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            File.WriteAllBytes(GetAbsolutePath(assetPath), texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(
                assetPath,
                ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            ConfigureTextureImporter(assetPath);
            ConfigureAddressableEntry(assetPath);
        }

        private static void ConfigureTextureImporter(string assetPath)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                throw new InvalidOperationException("Texture importer was not created for " + assetPath);
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        private static void ConfigureAddressableEntry(string assetPath)
        {
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            if (settings == null || settings.DefaultGroup == null)
            {
                throw new InvalidOperationException("Addressables settings could not be created.");
            }

            settings.AddLabel(PlaceholderLabel);
            settings.AddLabel(DevelopmentOnlyLabel);

            var guid = AssetDatabase.AssetPathToGUID(assetPath);
            var entry = settings.CreateOrMoveEntry(guid, settings.DefaultGroup, false, false);
            entry.address = assetPath;
            entry.SetLabel(PlaceholderLabel, true, false, false);
            entry.SetLabel(DevelopmentOnlyLabel, true, false, false);
            EditorUtility.SetDirty(settings);
        }

        private static bool IsInsideCircle(int x, int y)
        {
            var center = (TextureSize - 1) * 0.5f;
            var radius = TextureSize * 0.42f;
            var deltaX = x - center;
            var deltaY = y - center;
            return (deltaX * deltaX) + (deltaY * deltaY) <= radius * radius;
        }

        private static bool IsInsideSquare(int x, int y)
        {
            const int border = 8;
            return x >= border &&
                   x < TextureSize - border &&
                   y >= border &&
                   y < TextureSize - border;
        }

        private static bool IsInsideLine(int x, int y)
        {
            const int halfThickness = 3;
            var center = TextureSize / 2;
            return y >= center - halfThickness && y <= center + halfThickness;
        }

        private static string GetAbsolutePath(string projectRelativePath)
        {
            var projectRoot = Directory.GetParent(UnityEngine.Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot))
            {
                throw new InvalidOperationException("Unable to resolve the Unity project root.");
            }

            return Path.GetFullPath(Path.Combine(projectRoot, projectRelativePath));
        }
    }
}
