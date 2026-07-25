using System;
using System.Collections.Generic;
using System.Reflection;
using Game.Content.Authoring;
using Game.Content.Runtime;
using Game.Core;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    public sealed class ContentBakerTests
    {
        private readonly List<UnityEngine.Object> createdObjects =
            new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            for (var index = 0; index < createdObjects.Count; index++)
            {
                UnityEngine.Object.DestroyImmediate(createdObjects[index]);
            }

            createdObjects.Clear();
        }

        [Test]
        public void AuthoringBakesToPureDefinitionsWithoutUnityObjectFields()
        {
            var fixture = CreateFixture();

            var bake = ContentBaker.Bake(fixture.Pack, fixture.PathResolver);

            Assert.That(bake.IsSuccess, Is.True, bake.Error.ToString());
            Assert.That(bake.Value.Definitions.Count, Is.EqualTo(4));
            Assert.That(bake.Value.Definitions[0], Is.TypeOf<RuntimeCharacterDefinition>());
            AssertNoUnityObjectFields(typeof(BakedContentCatalog));
            AssertNoUnityObjectFields(typeof(ContentPackManifest));
            AssertNoUnityObjectFields(typeof(RuntimeContentDefinition));
            AssertNoUnityObjectFields(typeof(RuntimeCharacterDefinition));
            AssertNoUnityObjectFields(typeof(RuntimeSkillDefinition));
            AssertNoUnityObjectFields(typeof(RuntimeEnemyDefinition));
            AssertNoUnityObjectFields(typeof(RuntimeMapDefinition));
        }

        [Test]
        public void SameAuthoringInputProducesSameHashAndSerializableCatalog()
        {
            var fixture = CreateFixture();

            var first = ContentBaker.Bake(fixture.Pack, fixture.PathResolver);
            var second = ContentBaker.Bake(fixture.Pack, fixture.PathResolver);
            var json = JsonUtility.ToJson(first.Value.ToDto(), true);
            var restoredDto = JsonUtility.FromJson<BakedContentCatalogDto>(json);
            var restored = restoredDto.ToCatalog();

            Assert.That(first.IsSuccess, Is.True, first.Error.ToString());
            Assert.That(second.IsSuccess, Is.True, second.Error.ToString());
            Assert.That(first.Value.ContentHash, Is.EqualTo(second.Value.ContentHash));
            Assert.That(restored.IsSuccess, Is.True, restored.Error.ToString());
            Assert.That(restored.Value.ContentHash, Is.EqualTo(first.Value.ContentHash));
            Assert.That(
                restored.Value.Definitions[0].Id.Value,
                Is.EqualTo("test.character.baker"));
        }

        [Test]
        public void AuthoringRejectsNonCanonicalIdWithPackAndAssetPath()
        {
            var fixture = CreateFixture();
            fixture.Skill.ConfigureIdentity(
                "Test.Skill.Uppercase",
                "content.test.skill.name",
                "content.test.skill.description",
                Array.Empty<string>());

            var bake = ContentBaker.Bake(fixture.Pack, fixture.PathResolver);

            Assert.That(bake.IsSuccess, Is.False);
            Assert.That(bake.Error.Code, Is.EqualTo(ErrorCode.InvalidContentId));
            Assert.That(bake.Error.PackId.Value, Is.EqualTo("test.pack.baker"));
            Assert.That(
                bake.Error.AuthorAssetPath,
                Is.EqualTo("Assets/Test/Skill.asset"));
        }

        private Fixture CreateFixture()
        {
            var skill = Create<SkillAuthoring>("Skill");
            skill.ConfigureIdentity(
                "test.skill.baker",
                "content.test.skill.baker.name",
                "content.test.skill.baker.description",
                new[] { "delivery.instant" });
            skill.Configure(1f);

            var character = Create<CharacterAuthoring>("Character");
            character.ConfigureIdentity(
                "test.character.baker",
                "content.test.character.baker.name",
                "content.test.character.baker.description",
                new[] { "actor.player" });
            character.Configure(100f, 5f, new[] { skill });

            var enemy = Create<EnemyAuthoring>("Enemy");
            enemy.ConfigureIdentity(
                "test.enemy.baker",
                "content.test.enemy.baker.name",
                "content.test.enemy.baker.description",
                new[] { "actor.enemy" });
            enemy.Configure(10f, 0.5f);

            var map = Create<MapAuthoring>("Map");
            map.ConfigureIdentity(
                "test.map.baker",
                "content.test.map.baker.name",
                "content.test.map.baker.description",
                new[] { "map.finite" });
            map.Configure("map.provider.test", "scenes/test-map");

            var pack = Create<ContentPackAuthoring>("Pack");
            pack.Configure(
                "test.pack.baker",
                "0.1.0",
                1,
                "0.1.0",
                string.Empty,
                Array.Empty<ContentPackDependencyAuthoring>(),
                "packs/test.baker/catalog",
                "pack.test.baker",
                false,
                new ContentAuthoringBase[] { character, skill, enemy, map });

            var resolver = new DictionaryPathResolver();
            resolver.Add(pack, "Assets/Test/Pack.asset");
            resolver.Add(character, "Assets/Test/Character.asset");
            resolver.Add(skill, "Assets/Test/Skill.asset");
            resolver.Add(enemy, "Assets/Test/Enemy.asset");
            resolver.Add(map, "Assets/Test/Map.asset");
            return new Fixture(pack, skill, resolver);
        }

        private T Create<T>(string name)
            where T : ScriptableObject
        {
            var value = ScriptableObject.CreateInstance<T>();
            value.name = name;
            createdObjects.Add(value);
            return value;
        }

        private static void AssertNoUnityObjectFields(Type type)
        {
            var fields = type.GetFields(
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic |
                BindingFlags.DeclaredOnly);
            for (var index = 0; index < fields.Length; index++)
            {
                Assert.That(
                    typeof(UnityEngine.Object).IsAssignableFrom(fields[index].FieldType),
                    Is.False,
                    type.FullName + "." + fields[index].Name);
            }
        }

        private readonly struct Fixture
        {
            public Fixture(
                ContentPackAuthoring pack,
                SkillAuthoring skill,
                DictionaryPathResolver pathResolver)
            {
                Pack = pack;
                Skill = skill;
                PathResolver = pathResolver;
            }

            public ContentPackAuthoring Pack { get; }
            public SkillAuthoring Skill { get; }
            public DictionaryPathResolver PathResolver { get; }
        }

        private sealed class DictionaryPathResolver : IAuthoringPathResolver
        {
            private readonly Dictionary<UnityEngine.Object, string> paths =
                new Dictionary<UnityEngine.Object, string>();

            public void Add(UnityEngine.Object asset, string path)
            {
                paths.Add(asset, path);
            }

            public string GetPath(UnityEngine.Object authoringAsset)
            {
                return authoringAsset != null &&
                       paths.TryGetValue(authoringAsset, out var path)
                    ? path
                    : string.Empty;
            }
        }
    }
}
