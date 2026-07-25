using Game.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;

namespace Game.Tests.EditMode
{
    public sealed class PlaceholderAssetGeneratorTests
    {
        [Test]
        public void GenerateAllCreatesExpectedTexturesWithDevelopmentLabels()
        {
            PlaceholderAssetGenerator.GenerateAll();
            AssertTextureAndLabels(PlaceholderAssetGenerator.CirclePath);
            AssertTextureAndLabels(PlaceholderAssetGenerator.SquarePath);
            AssertTextureAndLabels(PlaceholderAssetGenerator.LinePath);
        }

        private static void AssertTextureAndLabels(string path)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            Assert.That(texture, Is.Not.Null, path);
            Assert.That(texture.width, Is.EqualTo(64), path);
            Assert.That(texture.height, Is.EqualTo(64), path);

            var settings = AddressableAssetSettingsDefaultObject.GetSettings(false);
            Assert.That(settings, Is.Not.Null);
            var guid = AssetDatabase.AssetPathToGUID(path);
            var entry = settings.FindAssetEntry(guid);
            Assert.That(entry, Is.Not.Null, path);
            Assert.That(
                entry.labels,
                Does.Contain(PlaceholderAssetGenerator.PlaceholderLabel),
                path);
            Assert.That(
                entry.labels,
                Does.Contain(PlaceholderAssetGenerator.DevelopmentOnlyLabel),
                path);
            Assert.That(entry.labels, Does.Not.Contain("release"), path);
        }
    }
}
