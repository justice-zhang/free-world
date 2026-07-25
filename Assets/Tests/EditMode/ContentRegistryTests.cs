using System;
using System.Text;
using Game.Content.Runtime;
using Game.Core;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public sealed class ContentRegistryTests
    {
        private static readonly ContentVersion GameVersion = new ContentVersion(0, 1, 0);

        [Test]
        public void DuplicateIdFailsAndNamesBothPackSources()
        {
            var duplicate = Id("test.skill.duplicate");
            var first = Catalog(
                "test.pack.first",
                Skill(duplicate, "Assets/Test/FirstSkill.asset"));
            var second = Catalog(
                "test.pack.second",
                Skill(duplicate, "Assets/Test/SecondSkill.asset"));

            var result = new ContentRegistry().Load(
                new[] { first, second },
                GameVersion);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error.Code, Is.EqualTo(ErrorCode.DuplicateContentId));
            Assert.That(result.Error.Message, Does.Contain("test.pack.first"));
            Assert.That(result.Error.Message, Does.Contain("test.pack.second"));
            Assert.That(result.Error.Message, Does.Contain("FirstSkill.asset"));
            Assert.That(result.Error.Message, Does.Contain("SecondSkill.asset"));
        }

        [Test]
        public void MissingReferencedContentFailsWithOwnerProvenance()
        {
            var character = new RuntimeCharacterDefinition(
                Id("test.character.owner"),
                "content.test.character.owner.name",
                "content.test.character.owner.description",
                "Assets/Test/Owner.asset",
                Array.Empty<ContentTag>(),
                100f,
                5f,
                new[] { Id("test.skill.missing") });
            var catalog = Catalog("test.pack.owner", character);

            var report = ContentValidator.ValidateCatalogs(
                new[] { catalog },
                GameVersion);

            Assert.That(report.IsValid, Is.False);
            Assert.That(report.Errors[0].Code, Is.EqualTo(ErrorCode.MissingReference));
            Assert.That(report.Errors[0].ContentId.Value, Is.EqualTo("test.character.owner"));
            Assert.That(report.Errors[0].PackId.Value, Is.EqualTo("test.pack.owner"));
            Assert.That(report.Errors[0].AuthorAssetPath, Is.EqualTo("Assets/Test/Owner.asset"));
        }

        [Test]
        public void RuntimeIndicesAreStableForTheSameLoadOrder()
        {
            var firstCatalog = Catalog(
                "test.pack.first",
                Skill(Id("test.skill.one"), "Assets/Test/One.asset"),
                Skill(Id("test.skill.two"), "Assets/Test/Two.asset"));
            var secondCatalog = Catalog(
                "test.pack.second",
                Skill(Id("test.skill.three"), "Assets/Test/Three.asset"));
            var catalogs = new[] { secondCatalog, firstCatalog };
            var firstRegistry = new ContentRegistry();
            var secondRegistry = new ContentRegistry();

            var firstLoad = firstRegistry.Load(catalogs, GameVersion);
            var secondLoad = secondRegistry.Load(catalogs, GameVersion);

            Assert.That(firstLoad.IsSuccess, Is.True, firstLoad.Error.ToString());
            Assert.That(secondLoad.IsSuccess, Is.True, secondLoad.Error.ToString());
            AssertSameIndex(firstRegistry, secondRegistry, "test.skill.one");
            AssertSameIndex(firstRegistry, secondRegistry, "test.skill.two");
            AssertSameIndex(firstRegistry, secondRegistry, "test.skill.three");
        }

        [Test]
        public void RegistryAcceptsNewDefinitionSubtypeWithoutHardcodedTypeList()
        {
            var custom = new TestRuntimeDefinition(
                Id("test.custom.definition"),
                "Assets/Test/Custom.asset");
            var registry = new ContentRegistry();

            var load = registry.Load(
                new[] { Catalog("test.pack.custom", custom) },
                GameVersion);

            Assert.That(load.IsSuccess, Is.True, load.Error.ToString());
            Assert.That(
                registry.TryGet<TestRuntimeDefinition>(custom.Id, out var restored),
                Is.True);
            Assert.That(restored, Is.SameAs(custom));
        }

        [Test]
        public void RuntimeCollectionsDoNotExposeMutableBackingArrays()
        {
            var skillId = Id("test.skill.immutable");
            var tag = ContentTag.Create("test.tag.immutable").Value;
            var skill = new RuntimeSkillDefinition(
                skillId,
                "content.test.skill.immutable.name",
                "content.test.skill.immutable.description",
                "Assets/Test/ImmutableSkill.asset",
                new[] { tag },
                1f);
            var character = new RuntimeCharacterDefinition(
                Id("test.character.immutable"),
                "content.test.character.immutable.name",
                "content.test.character.immutable.description",
                "Assets/Test/ImmutableCharacter.asset",
                Array.Empty<ContentTag>(),
                100f,
                5f,
                new[] { skillId });
            var catalog = Catalog("test.pack.immutable", skill, character);
            var registry = new ContentRegistry();

            var load = registry.Load(new[] { catalog }, GameVersion);

            Assert.That(load.IsSuccess, Is.True, load.Error.ToString());
            Assert.That(
                catalog.Definitions,
                Is.Not.InstanceOf<RuntimeContentDefinition[]>());
            Assert.That(
                catalog.Manifest.Dependencies,
                Is.Not.InstanceOf<ContentPackDependency[]>());
            Assert.That(skill.Tags, Is.Not.InstanceOf<ContentTag[]>());
            Assert.That(
                character.ReferencedContentIds,
                Is.Not.InstanceOf<ContentId[]>());
            Assert.That(
                character.StartingSkillIds,
                Is.Not.InstanceOf<ContentId[]>());
            Assert.That(registry.LoadedPackIds, Is.Not.InstanceOf<ContentId[]>());
        }

        private static void AssertSameIndex(
            ContentRegistry first,
            ContentRegistry second,
            string rawId)
        {
            Assert.That(first.TryGet(Id(rawId), out var firstEntry), Is.True);
            Assert.That(second.TryGet(Id(rawId), out var secondEntry), Is.True);
            Assert.That(firstEntry.Index, Is.EqualTo(secondEntry.Index));
        }

        private static RuntimeSkillDefinition Skill(ContentId id, string path)
        {
            return new RuntimeSkillDefinition(
                id,
                "content." + id.Value + ".name",
                "content." + id.Value + ".description",
                path,
                Array.Empty<ContentTag>(),
                1f);
        }

        private static BakedContentCatalog Catalog(
            string packId,
            params RuntimeContentDefinition[] definitions)
        {
            return BakedContentCatalog.Create(
                new ContentPackManifest(
                    Id(packId),
                    GameVersion,
                    1,
                    GameVersion,
                    null,
                    Array.Empty<ContentPackDependency>(),
                    "packs/" + packId + "/catalog",
                    "pack." + packId,
                    false,
                    "Assets/Test/" + packId + ".asset"),
                definitions);
        }

        private static ContentId Id(string value)
        {
            return ContentId.Create(value).Value;
        }

        private sealed class TestRuntimeDefinition : RuntimeContentDefinition
        {
            public TestRuntimeDefinition(ContentId id, string path)
                : base(
                    id,
                    "content.test.custom.name",
                    "content.test.custom.description",
                    path,
                    Array.Empty<ContentTag>(),
                    Array.Empty<ContentId>())
            {
            }

            public override string Kind => "test_custom";

            protected override void AppendTypeSpecificDeterministicData(StringBuilder builder)
            {
                builder.Append("custom");
            }
        }
    }
}
