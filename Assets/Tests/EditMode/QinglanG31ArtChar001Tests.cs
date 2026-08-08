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
    public sealed class QinglanG31ArtChar001Tests
    {
        private const string AssetPath =
            "Assets/GameAssets/AI/QinglanDemo/ART-CHAR-001/final/lu-qingye-directional-animation-atlas.png";
        private const string ProfilePath =
            "Assets/GameContent/QinglanDemo/Profiles/Visual/ART-CHAR-001/lu-qingye-visual-profile.asset";

        [Test]
        public void FinalAtlasHasApprovedImportAndAddressableContract()
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetPath);
            Assert.That(texture, Is.Not.Null);
            Assert.That(texture.width, Is.EqualTo(1536));
            Assert.That(texture.height, Is.EqualTo(1024));

            var importer = AssetImporter.GetAtPath(AssetPath) as TextureImporter;
            Assert.That(importer, Is.Not.Null);
            Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Sprite));
            Assert.That(importer.spriteImportMode, Is.EqualTo(SpriteImportMode.Multiple));
            Assert.That(importer.mipmapEnabled, Is.False);
            Assert.That(importer.alphaIsTransparency, Is.True);
            Assert.That(importer.maxTextureSize, Is.EqualTo(2048));
            var standalone = importer.GetPlatformTextureSettings("Standalone");
            Assert.That(standalone.overridden, Is.True);
            Assert.That(standalone.format, Is.EqualTo(TextureImporterFormat.BC7));

            var assets = AssetDatabase.LoadAllAssetsAtPath(AssetPath);
            var names = new HashSet<string>(StringComparer.Ordinal);
            var spriteCount = 0;
            for (var index = 0; index < assets.Length; index++)
            {
                if (!(assets[index] is Sprite sprite)) continue;
                spriteCount++;
                names.Add(sprite.name);
                Assert.That(sprite.rect.width, Is.EqualTo(256f));
                Assert.That(sprite.rect.height, Is.EqualTo(256f));
                Assert.That(sprite.pivot.x, Is.EqualTo(128f).Within(0.01f));
                Assert.That(sprite.pivot.y, Is.EqualTo(12f).Within(0.01f));
            }

            Assert.That(spriteCount, Is.EqualTo(24));
            Assert.That(names, Does.Contain("lu-qingye.down.idle"));
            Assert.That(names, Does.Contain("lu-qingye.left.imperial-sword"));
            Assert.That(names, Does.Contain("lu-qingye.right.move"));
            Assert.That(names, Does.Contain("lu-qingye.up.victory"));

            var settings = AddressableAssetSettingsDefaultObject.GetSettings(false);
            var group = settings?.FindGroup(AssetProvenanceValidator.QinglanVisualGroup);
            Assert.That(group, Is.Not.Null);
            var entry = settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(AssetPath));
            Assert.That(entry, Is.Not.Null);
            Assert.That(entry.parentGroup, Is.SameAs(group));
            Assert.That(entry.address, Is.EqualTo("qinglan/character/lu-qingye/directional-animation-atlas"));
            Assert.That(entry.labels, Does.Contain(AssetProvenanceValidator.QinglanPackLabel));
            Assert.That(entry.labels, Does.Contain(AssetProvenanceValidator.ReleaseLabel));
            Assert.That(entry.labels, Does.Contain(AssetProvenanceValidator.VisualReleaseLabel));
            Assert.That(entry.labels, Does.Not.Contain(PlaceholderAssetGenerator.PlaceholderLabel));
            Assert.That(entry.labels, Does.Not.Contain(PlaceholderAssetGenerator.DevelopmentOnlyLabel));

            var projectRoot = Directory.GetParent(UnityEngine.Application.dataPath)?.FullName;
            var provenanceIssues = AssetProvenanceValidator.ValidateReleaseInput(
                projectRoot,
                AssetPath,
                entry.labels,
                group.Name);
            Assert.That(provenanceIssues, Is.Empty, JoinIssues(provenanceIssues));

            var profile = AssetDatabase.LoadAssetAtPath<Game.Presentation.VisualProfile>(ProfilePath);
            Assert.That(profile, Is.Not.Null);
            Assert.That(profile.StableId, Is.EqualTo("qinglan.character.lu_qingye"));
            Assert.That(profile.Sprite, Is.Not.Null);
            Assert.That(profile.Sprite.name, Is.EqualTo("lu-qingye.down.idle"));
            var profileEntry = settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(ProfilePath));
            Assert.That(profileEntry, Is.Not.Null);
            Assert.That(profileEntry.parentGroup, Is.SameAs(group));
            Assert.That(profileEntry.address, Is.EqualTo("qinglan/profile/character/lu-qingye"));
            Assert.That(profileEntry.labels, Does.Contain(AssetProvenanceValidator.QinglanPackLabel));
            Assert.That(profileEntry.labels, Does.Contain(AssetProvenanceValidator.ReleaseLabel));
            Assert.That(profileEntry.labels, Does.Contain(AssetProvenanceValidator.VisualReleaseLabel));
            var profileIssues = AssetProvenanceValidator.ValidateReleaseInput(
                projectRoot,
                ProfilePath,
                profileEntry.labels,
                group.Name);
            Assert.That(profileIssues, Is.Empty, JoinIssues(profileIssues));
        }

        [Test]
        public void SourceAndWorkingFilesAreNotAddressable()
        {
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(false);
            Assert.That(settings, Is.Not.Null);
            var guids = AssetDatabase.FindAssets(
                string.Empty,
                new[] { "Assets/GameAssets/AI/QinglanDemo/ART-CHAR-001/source",
                    "Assets/GameAssets/AI/QinglanDemo/ART-CHAR-001/working" });
            for (var index = 0; index < guids.Length; index++)
                Assert.That(settings.FindAssetEntry(guids[index]), Is.Null, AssetDatabase.GUIDToAssetPath(guids[index]));
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
