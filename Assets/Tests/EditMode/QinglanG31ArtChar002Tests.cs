using System;
using System.Collections.Generic;
using System.IO;
using Game.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;

namespace Game.Tests.EditMode
{
    public sealed class QinglanG31ArtChar002Tests
    {
        private const string PortraitPath =
            "Assets/GameAssets/AI/QinglanDemo/ART-CHAR-002/final/lu-qingye-portrait.png";
        private const string SilhouettePath =
            "Assets/GameAssets/AI/QinglanDemo/ART-CHAR-002/final/lu-qingye-silhouette.png";

        [Test]
        public void PortraitAndSilhouetteHaveApprovedRuntimeContracts()
        {
            AssertTexture(PortraitPath, 1024, "qinglan/character/lu-qingye/portrait");
            AssertTexture(SilhouettePath, 256, "qinglan/character/lu-qingye/silhouette");

            var portraitBounds = ReadAlphaBounds(PortraitPath, out var portraitCorners, out _);
            Assert.That(portraitCorners, Is.EqualTo(new[] { 0, 0, 0, 0 }));
            Assert.That(portraitBounds.xMin, Is.GreaterThanOrEqualTo(48));
            Assert.That(portraitBounds.yMin, Is.GreaterThanOrEqualTo(48));
            Assert.That(1024 - portraitBounds.xMax, Is.GreaterThanOrEqualTo(48));
            Assert.That(1024 - portraitBounds.yMax, Is.GreaterThanOrEqualTo(48));

            var silhouetteBounds = ReadAlphaBounds(SilhouettePath, out var silhouetteCorners, out var colors);
            Assert.That(silhouetteCorners, Is.EqualTo(new[] { 0, 0, 0, 0 }));
            Assert.That(silhouetteBounds.xMin, Is.GreaterThanOrEqualTo(8));
            Assert.That(silhouetteBounds.yMin, Is.GreaterThanOrEqualTo(8));
            Assert.That(256 - silhouetteBounds.xMax, Is.GreaterThanOrEqualTo(8));
            Assert.That(256 - silhouetteBounds.yMax, Is.GreaterThanOrEqualTo(8));
            Assert.That(colors, Does.Contain(new Color32(22, 61, 69, 255)));
            Assert.That(colors, Does.Contain(new Color32(244, 239, 216, 255)));
        }

        [Test]
        public void SourceAndWorkingFilesRemainOutsideAddressables()
        {
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(false);
            Assert.That(settings, Is.Not.Null);
            var guids = AssetDatabase.FindAssets(
                string.Empty,
                new[]
                {
                    "Assets/GameAssets/AI/QinglanDemo/ART-CHAR-002/source",
                    "Assets/GameAssets/AI/QinglanDemo/ART-CHAR-002/working"
                });
            for (var index = 0; index < guids.Length; index++)
                Assert.That(settings.FindAssetEntry(guids[index]), Is.Null, AssetDatabase.GUIDToAssetPath(guids[index]));
        }

        private static void AssertTexture(string path, int size, string address)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            Assert.That(texture, Is.Not.Null);
            Assert.That(texture.width, Is.EqualTo(size));
            Assert.That(texture.height, Is.EqualTo(size));
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            Assert.That(importer, Is.Not.Null);
            Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Sprite));
            Assert.That(importer.spriteImportMode, Is.EqualTo(SpriteImportMode.Multiple));
            Assert.That(importer.mipmapEnabled, Is.False);
            Assert.That(importer.alphaIsTransparency, Is.True);
            Assert.That(importer.maxTextureSize, Is.EqualTo(size));
            var standalone = importer.GetPlatformTextureSettings("Standalone");
            Assert.That(standalone.overridden, Is.True);
            Assert.That(standalone.format, Is.EqualTo(TextureImporterFormat.BC7));

            var settings = AddressableAssetSettingsDefaultObject.GetSettings(false);
            var entry = settings?.FindAssetEntry(AssetDatabase.AssetPathToGUID(path));
            Assert.That(entry, Is.Not.Null);
            Assert.That(entry.parentGroup.Name, Is.EqualTo(AssetProvenanceValidator.QinglanVisualGroup));
            Assert.That(entry.address, Is.EqualTo(address));
            Assert.That(entry.labels, Does.Contain(AssetProvenanceValidator.QinglanPackLabel));
            Assert.That(entry.labels, Does.Contain(AssetProvenanceValidator.ReleaseLabel));
            Assert.That(entry.labels, Does.Contain(AssetProvenanceValidator.VisualReleaseLabel));
            var root = Directory.GetParent(UnityEngine.Application.dataPath)?.FullName;
            var issues = AssetProvenanceValidator.ValidateReleaseInput(root, path, entry.labels, entry.parentGroup.Name);
            Assert.That(issues, Is.Empty, JoinIssues(issues));
        }

        private static RectInt ReadAlphaBounds(
            string assetPath,
            out int[] corners,
            out HashSet<Color32> opaqueColors)
        {
            var absolute = Path.Combine(
                Directory.GetParent(UnityEngine.Application.dataPath)?.FullName ?? string.Empty,
                assetPath);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            Assert.That(ImageConversion.LoadImage(texture, File.ReadAllBytes(absolute), false), Is.True);
            var pixels = texture.GetPixels32();
            var minimumX = texture.width;
            var minimumY = texture.height;
            var maximumX = -1;
            var maximumY = -1;
            opaqueColors = new HashSet<Color32>();
            for (var y = 0; y < texture.height; y++)
            {
                for (var x = 0; x < texture.width; x++)
                {
                    var pixel = pixels[y * texture.width + x];
                    if (pixel.a == 255) opaqueColors.Add(pixel);
                    if (pixel.a == 0) continue;
                    minimumX = Math.Min(minimumX, x);
                    minimumY = Math.Min(minimumY, y);
                    maximumX = Math.Max(maximumX, x);
                    maximumY = Math.Max(maximumY, y);
                }
            }
            corners = new[]
            {
                (int)pixels[0].a,
                (int)pixels[texture.width - 1].a,
                (int)pixels[(texture.height - 1) * texture.width].a,
                (int)pixels[pixels.Length - 1].a
            };
            UnityEngine.Object.DestroyImmediate(texture);
            return new RectInt(
                minimumX,
                minimumY,
                maximumX - minimumX + 1,
                maximumY - minimumY + 1);
        }

        private static string JoinIssues(IReadOnlyList<ValidationIssue> issues)
        {
            var value = string.Empty;
            for (var index = 0; index < issues.Count; index++)
                value += (index == 0 ? string.Empty : "\n") + issues[index];
            return value;
        }
    }
}
