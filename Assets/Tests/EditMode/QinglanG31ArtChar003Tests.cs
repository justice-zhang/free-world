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
    public sealed class QinglanG31ArtChar003Tests
    {
        private const string BatchRoot =
            "Assets/GameAssets/FirstParty/QinglanDemo/ART-CHAR-003";

        private static readonly string[] FinalPaths =
        {
            BatchRoot + "/final/riding-wind-tier-0-static.png",
            BatchRoot + "/final/riding-wind-tier-1-breeze.png",
            BatchRoot + "/final/riding-wind-tier-2-swift.png",
            BatchRoot + "/final/riding-wind-tier-3-riding.png"
        };

        private static readonly string[] Addresses =
        {
            "qinglan/character/lu-qingye/riding-wind/tier-0",
            "qinglan/character/lu-qingye/riding-wind/tier-1",
            "qinglan/character/lu-qingye/riding-wind/tier-2",
            "qinglan/character/lu-qingye/riding-wind/tier-3"
        };

        [Test]
        public void FourRuntimeOverlaysHaveApprovedTextureAndReleaseContracts()
        {
            for (var index = 0; index < FinalPaths.Length; index++)
            {
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(FinalPaths[index]);
                Assert.That(texture, Is.Not.Null, FinalPaths[index]);
                Assert.That(texture.width, Is.EqualTo(512));
                Assert.That(texture.height, Is.EqualTo(512));

                var importer = AssetImporter.GetAtPath(FinalPaths[index]) as TextureImporter;
                Assert.That(importer, Is.Not.Null);
                Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Sprite));
                Assert.That(importer.spriteImportMode, Is.EqualTo(SpriteImportMode.Multiple));
                Assert.That(importer.mipmapEnabled, Is.False);
                Assert.That(importer.alphaIsTransparency, Is.True);
                Assert.That(importer.maxTextureSize, Is.EqualTo(512));
                var standalone = importer.GetPlatformTextureSettings("Standalone");
                Assert.That(standalone.overridden, Is.True);
                Assert.That(standalone.format, Is.EqualTo(TextureImporterFormat.BC7));

                var settings = AddressableAssetSettingsDefaultObject.GetSettings(false);
                var entry = settings?.FindAssetEntry(AssetDatabase.AssetPathToGUID(FinalPaths[index]));
                Assert.That(entry, Is.Not.Null);
                Assert.That(entry.parentGroup.Name, Is.EqualTo(AssetProvenanceValidator.QinglanVisualGroup));
                Assert.That(entry.address, Is.EqualTo(Addresses[index]));
                Assert.That(entry.labels, Does.Contain(AssetProvenanceValidator.QinglanPackLabel));
                Assert.That(entry.labels, Does.Contain(AssetProvenanceValidator.ReleaseLabel));
                Assert.That(entry.labels, Does.Contain(AssetProvenanceValidator.VisualReleaseLabel));

                var projectRoot = Directory.GetParent(UnityEngine.Application.dataPath)?.FullName;
                var issues = AssetProvenanceValidator.ValidateReleaseInput(
                    projectRoot,
                    FinalPaths[index],
                    entry.labels,
                    entry.parentGroup.Name);
                Assert.That(issues, Is.Empty, JoinIssues(issues));
            }
        }

        [Test]
        public void SourceAndRuntimeGeometryMeetTierAccessibilityContract()
        {
            var absoluteRoot = Directory.GetParent(UnityEngine.Application.dataPath)?.FullName ?? string.Empty;
            for (var index = 0; index < FinalPaths.Length; index++)
            {
                var sourceName = index == 0 ? "riding-wind-tier-0-static-source.png" :
                    index == 1 ? "riding-wind-tier-1-breeze-source.png" :
                    index == 2 ? "riding-wind-tier-2-swift-source.png" :
                    "riding-wind-tier-3-riding-source.png";
                var sourcePath = Path.Combine(
                    absoluteRoot,
                    BatchRoot,
                    "source",
                    sourceName);
                var source = LoadPng(sourcePath);
                try
                {
                    Assert.That(source.width, Is.EqualTo(1024));
                    Assert.That(source.height, Is.EqualTo(1024));
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(source);
                }
            }

            var coverage = new int[FinalPaths.Length];
            var gold = new Color32(242, 210, 123, 255);
            var deep = new Color32(22, 61, 69, 255);
            for (var index = 0; index < FinalPaths.Length; index++)
            {
                var absolute = Path.Combine(absoluteRoot, FinalPaths[index]);
                var texture = LoadPng(absolute);
                try
                {
                    var pixels = texture.GetPixels32();
                    Assert.That(pixels[0].a, Is.Zero);
                    Assert.That(pixels[texture.width - 1].a, Is.Zero);
                    Assert.That(pixels[(texture.height - 1) * texture.width].a, Is.Zero);
                    Assert.That(pixels[pixels.Length - 1].a, Is.Zero);
                    var bounds = AlphaBounds(texture, pixels, out coverage[index], out var opaqueColors);
                    Assert.That(bounds.xMin, Is.GreaterThanOrEqualTo(40));
                    Assert.That(bounds.yMin, Is.GreaterThanOrEqualTo(40));
                    Assert.That(texture.width - bounds.xMax, Is.GreaterThanOrEqualTo(40));
                    Assert.That(texture.height - bounds.yMax, Is.GreaterThanOrEqualTo(40));
                    Assert.That(opaqueColors, Does.Contain(deep));
                    Assert.That(opaqueColors.Contains(gold), Is.EqualTo(index == 3));
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(texture);
                }
            }

            Assert.That(coverage[0], Is.LessThan(coverage[1]));
            Assert.That(coverage[1], Is.LessThan(coverage[2]));
            Assert.That(coverage[2], Is.LessThan(coverage[3]));
            Assert.That(coverage[3], Is.LessThan(512 * 512 * 0.21));
        }

        [Test]
        public void OnlyFinalFilesAreAddressableWithinBatch()
        {
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(false);
            Assert.That(settings, Is.Not.Null);
            var guids = AssetDatabase.FindAssets(string.Empty, new[] { BatchRoot });
            var addressableCount = 0;
            for (var index = 0; index < guids.Length; index++)
            {
                var entry = settings.FindAssetEntry(guids[index]);
                if (entry == null) continue;
                var path = AssetDatabase.GUIDToAssetPath(guids[index]).Replace('\\', '/');
                Assert.That(path, Does.Contain("/final/"));
                addressableCount++;
            }
            Assert.That(addressableCount, Is.EqualTo(4));
        }

        private static Texture2D LoadPng(string path)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            Assert.That(ImageConversion.LoadImage(texture, File.ReadAllBytes(path), false), Is.True, path);
            return texture;
        }

        private static RectInt AlphaBounds(
            Texture2D texture,
            Color32[] pixels,
            out int coverage,
            out HashSet<Color32> opaqueColors)
        {
            var minimumX = texture.width;
            var minimumY = texture.height;
            var maximumX = -1;
            var maximumY = -1;
            coverage = 0;
            opaqueColors = new HashSet<Color32>();
            for (var y = 0; y < texture.height; y++)
            {
                for (var x = 0; x < texture.width; x++)
                {
                    var pixel = pixels[y * texture.width + x];
                    if (pixel.a == 255) opaqueColors.Add(pixel);
                    if (pixel.a < 16) continue;
                    coverage++;
                    minimumX = Math.Min(minimumX, x);
                    minimumY = Math.Min(minimumY, y);
                    maximumX = Math.Max(maximumX, x);
                    maximumY = Math.Max(maximumY, y);
                }
            }
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
