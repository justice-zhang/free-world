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
    public sealed class StatusContentTests
    {
        private static readonly ContentVersion GameVersion =
            new ContentVersion(0, 1, 0);

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
        public void SchemaTwoStatusAuthoringRoundTripsWithDeterministicHash()
        {
            var fixture = CreateAuthoringFixture(
                ContentPackTopology.StatusDefinitionSchemaVersion);

            var first = ContentBaker.Bake(fixture.Pack, fixture.PathResolver);
            var second = ContentBaker.Bake(fixture.Pack, fixture.PathResolver);

            Assert.That(first.IsSuccess, Is.True, first.Error.ToString());
            Assert.That(second.IsSuccess, Is.True, second.Error.ToString());

            var json = JsonUtility.ToJson(first.Value.ToDto(), true);
            var restoredDto = JsonUtility.FromJson<BakedContentCatalogDto>(json);
            var restored = restoredDto.ToCatalog();

            Assert.That(restored.IsSuccess, Is.True, restored.Error.ToString());
            Assert.That(restored.Value.ContentHash, Is.EqualTo(first.Value.ContentHash));
            Assert.That(second.Value.ContentHash, Is.EqualTo(first.Value.ContentHash));
            Assert.That(restored.Value.Definitions, Has.Count.EqualTo(1));
            Assert.That(
                restored.Value.Definitions[0],
                Is.TypeOf<RuntimeStatusDefinition>());

            var status = (RuntimeStatusDefinition)restored.Value.Definitions[0];
            Assert.That(status.Id.Value, Is.EqualTo("test.status.burning"));
            Assert.That(
                status.StackingPolicy,
                Is.EqualTo(StatusStackingPolicy.AddStacks));
            Assert.That(status.DurationSeconds, Is.EqualTo(3f));
            Assert.That(status.MaxStacks, Is.EqualTo(5));
            Assert.That(status.TickIntervalSeconds, Is.EqualTo(1f));
            Assert.That(
                status.DispelTags[0].Value,
                Is.EqualTo("dispel.debuff"));
            Assert.That(
                status.ImmunityTags[0].Value,
                Is.EqualTo("immunity.fire"));
            Assert.That(status.Behavior.PeriodicDamage.Enabled, Is.True);
            Assert.That(
                status.Behavior.PeriodicDamage.DamageType,
                Is.EqualTo(DamageType.Fire));
            Assert.That(status.Behavior.PeriodicDamage.BaseValue, Is.EqualTo(6f));
            Assert.That(
                status.Behavior.PeriodicDamage.ProcCoefficient,
                Is.EqualTo(0.25f));

            var validation = ContentValidator.ValidateCatalogs(
                new[] { restored.Value },
                GameVersion);
            Assert.That(validation.IsValid, Is.True);
        }

        [TestCase(StatusStackingPolicy.RefreshDuration, "refresh_duration")]
        [TestCase(StatusStackingPolicy.AddStacks, "add_stacks")]
        [TestCase(StatusStackingPolicy.ReplaceIfStronger, "replace_if_stronger")]
        [TestCase(StatusStackingPolicy.IndependentInstances, "independent_instances")]
        public void StackingPoliciesUseStableWireTokens(
            StatusStackingPolicy policy,
            string expectedToken)
        {
            var maximumStacks =
                policy == StatusStackingPolicy.RefreshDuration ||
                policy == StatusStackingPolicy.ReplaceIfStronger
                    ? 1
                    : 2;
            var status = Status(
                "test.status.policy",
                policy,
                1f,
                maximumStacks,
                0f);
            var catalog = Catalog(2, "test.pack.policy", status);
            var dto = catalog.ToDto();

            var json = JsonUtility.ToJson(dto);
            var restored =
                JsonUtility.FromJson<BakedContentCatalogDto>(json).ToCatalog();

            Assert.That(dto.definitions[0].stackingPolicy, Is.EqualTo(expectedToken));
            Assert.That(restored.IsSuccess, Is.True, restored.Error.ToString());
            Assert.That(
                ((RuntimeStatusDefinition)restored.Value.Definitions[0]).StackingPolicy,
                Is.EqualTo(policy));
        }

        [Test]
        public void StatusSpecificFieldsParticipateInDeterministicHash()
        {
            var first = Catalog(
                2,
                "test.pack.hash",
                Status(
                    "test.status.hash",
                    StatusStackingPolicy.AddStacks,
                    3f,
                    5,
                    1f));
            var second = Catalog(
                2,
                "test.pack.hash",
                Status(
                    "test.status.hash",
                    StatusStackingPolicy.AddStacks,
                    4f,
                    5,
                    1f));

            Assert.That(first.ContentHash, Is.Not.EqualTo(second.ContentHash));
        }

        [Test]
        public void StatusBehaviorRoundTripsAndParticipatesInDeterministicHash()
        {
            var modifier = new RuntimeStatusModifier(
                BuiltInStatIds.MoveSpeed,
                ModifierOperation.Multiply,
                0.7f,
                10,
                Id("test.stack.slow"));
            var firstBehavior = new RuntimeStatusBehavior(modifier, default, 4f);
            var secondBehavior = new RuntimeStatusBehavior(modifier, default, 5f);
            var first = Catalog(
                2,
                "test.pack.behavior",
                Status(
                    "test.status.behavior",
                    StatusStackingPolicy.RefreshDuration,
                    2f,
                    1,
                    0f,
                    firstBehavior));
            var second = Catalog(
                2,
                "test.pack.behavior",
                Status(
                    "test.status.behavior",
                    StatusStackingPolicy.RefreshDuration,
                    2f,
                    1,
                    0f,
                    secondBehavior));

            var dto = first.ToDto();
            var restored = dto.ToCatalog();

            Assert.That(first.ContentHash, Is.Not.EqualTo(second.ContentHash));
            Assert.That(dto.definitions[0].statusModifierOperation, Is.EqualTo("multiply"));
            Assert.That(restored.IsSuccess, Is.True, restored.Error.ToString());
            var status = (RuntimeStatusDefinition)restored.Value.Definitions[0];
            Assert.That(status.Behavior.Modifier.StatId, Is.EqualTo(BuiltInStatIds.MoveSpeed));
            Assert.That(status.Behavior.Modifier.StackingGroup.Value, Is.EqualTo("test.stack.slow"));
            Assert.That(status.Behavior.ShieldCapacity, Is.EqualTo(4f));
        }

        [Test]
        public void SchemaOneCatalogWithoutStatusesRemainsSupported()
        {
            var skill = new RuntimeSkillDefinition(
                Id("test.skill.schema_one"),
                "content.test.skill.schema_one.name",
                "content.test.skill.schema_one.description",
                "Assets/Test/SchemaOneSkill.asset",
                Array.Empty<ContentTag>(),
                1f);
            var catalog = Catalog(1, "test.pack.schema_one", skill);
            var registry = new ContentRegistry();

            var load = registry.Load(new[] { catalog }, GameVersion);

            Assert.That(ContentPackTopology.IsSchemaVersionSupported(1), Is.True);
            Assert.That(ContentPackTopology.IsSchemaVersionSupported(2), Is.True);
            Assert.That(ContentPackTopology.IsSchemaVersionSupported(0), Is.False);
            Assert.That(ContentPackTopology.IsSchemaVersionSupported(3), Is.True);
            Assert.That(ContentPackTopology.IsSchemaVersionSupported(4), Is.True);
            Assert.That(ContentPackTopology.IsSchemaVersionSupported(5), Is.False);
            Assert.That(load.IsSuccess, Is.True, load.Error.ToString());
            Assert.That(
                registry.TryGet<RuntimeSkillDefinition>(skill.Id, out var restored),
                Is.True);
            Assert.That(restored, Is.SameAs(skill));
        }

        [Test]
        public void SchemaOneAuthoringRejectsStatusWithSourceProvenance()
        {
            var fixture = CreateAuthoringFixture(1);

            var bake = ContentBaker.Bake(fixture.Pack, fixture.PathResolver);

            Assert.That(bake.IsSuccess, Is.False);
            Assert.That(
                bake.Error.Code,
                Is.EqualTo(ErrorCode.UnsupportedSchemaVersion));
            Assert.That(bake.Error.ContentId.Value, Is.EqualTo("test.status.burning"));
            Assert.That(bake.Error.PackId.Value, Is.EqualTo("test.pack.status_baker"));
            Assert.That(
                bake.Error.AuthorAssetPath,
                Is.EqualTo("Assets/Test/Burning.asset"));
        }

        [Test]
        public void SchemaOneSerializedCatalogRejectsStatusKindBeforeHashVerification()
        {
            var dto = StatusCatalogDto(1, "add_stacks");

            var result = dto.ToCatalog();

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(
                result.Error.Code,
                Is.EqualTo(ErrorCode.UnsupportedSchemaVersion));
            Assert.That(result.Error.ContentId.Value, Is.EqualTo("test.status.serialized"));
            Assert.That(result.Error.PackId.Value, Is.EqualTo("test.pack.serialized"));
        }

        [Test]
        public void SchemaOneRuntimeCatalogRejectsStatusDuringValidation()
        {
            var status = Status(
                "test.status.schema_one_runtime",
                StatusStackingPolicy.AddStacks,
                1f,
                2,
                0.5f);
            var catalog = Catalog(1, "test.pack.schema_one_runtime", status);

            var report = ContentValidator.ValidateCatalogs(
                new[] { catalog },
                GameVersion);
            var load = new ContentRegistry().Load(new[] { catalog }, GameVersion);

            Assert.That(report.IsValid, Is.False);
            Assert.That(
                ContainsError(report, ErrorCode.UnsupportedSchemaVersion),
                Is.True);
            Assert.That(load.IsSuccess, Is.False);
            Assert.That(
                load.Error.Code,
                Is.EqualTo(ErrorCode.UnsupportedSchemaVersion));
        }

        [Test]
        public void SchemaTwoSerializedCatalogRejectsUnknownStackingPolicy()
        {
            var dto = StatusCatalogDto(2, "unknown_policy");

            var result = dto.ToCatalog();

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error.Code, Is.EqualTo(ErrorCode.InvalidCatalog));
            Assert.That(result.Error.Message, Does.Contain("unknown_policy"));
        }

        [Test]
        public void StatusValidatorRejectsInvalidPolicyAndBounds()
        {
            var invalidDefinitions = new[]
            {
                Status(
                    "test.status.invalid_policy",
                    StatusStackingPolicy.Invalid,
                    1f,
                    1,
                    0f),
                Status(
                    "test.status.invalid_duration",
                    StatusStackingPolicy.AddStacks,
                    0f,
                    2,
                    0f),
                Status(
                    "test.status.invalid_stacks",
                    StatusStackingPolicy.AddStacks,
                    1f,
                    0,
                    0f),
                Status(
                    "test.status.invalid_single_stack_policy",
                    StatusStackingPolicy.RefreshDuration,
                    1f,
                    2,
                    0f),
                Status(
                    "test.status.invalid_interval",
                    StatusStackingPolicy.IndependentInstances,
                    1f,
                    2,
                    -0.1f),
                Status(
                    "test.status.nan_duration",
                    StatusStackingPolicy.AddStacks,
                    float.NaN,
                    2,
                    0f)
            };

            for (var index = 0; index < invalidDefinitions.Length; index++)
            {
                var catalog = Catalog(
                    2,
                    "test.pack.invalid_status_" + index,
                    invalidDefinitions[index]);

                var report = ContentValidator.ValidateCatalogs(
                    new[] { catalog },
                    GameVersion);

                Assert.That(
                    report.IsValid,
                    Is.False,
                    invalidDefinitions[index].Id.Value);
                Assert.That(
                    ContainsError(report, ErrorCode.InvalidAuthoringData),
                    Is.True,
                    invalidDefinitions[index].Id.Value);
            }
        }

        [Test]
        public void StatusValidatorRejectsInvalidBakedBehavior()
        {
            var invalidPeriodic = new RuntimeStatusPeriodicDamage(
                DamageType.Fire,
                DamageTags.DamageOverTime,
                1f,
                false,
                float.NaN,
                System.Numerics.Vector2.Zero);
            var status = Status(
                "test.status.invalid_behavior",
                StatusStackingPolicy.AddStacks,
                1f,
                2,
                0.5f,
                new RuntimeStatusBehavior(default, invalidPeriodic, -1f));

            var report = ContentValidator.ValidateCatalogs(
                new[] { Catalog(2, "test.pack.invalid_behavior", status) },
                GameVersion);

            Assert.That(report.IsValid, Is.False);
            Assert.That(
                ContainsError(report, ErrorCode.InvalidAuthoringData),
                Is.True);
        }

        [Test]
        public void RuntimeStatusDefinitionIsPureAndDoesNotExposeMutableArrays()
        {
            var sourceDispelTags = new[] { Tag("dispel.debuff") };
            var sourceImmunityTags = new[] { Tag("immunity.fire") };
            var status = new RuntimeStatusDefinition(
                Id("test.status.immutable"),
                "content.test.status.immutable.name",
                "content.test.status.immutable.description",
                "Assets/Test/ImmutableStatus.asset",
                Array.Empty<ContentTag>(),
                StatusStackingPolicy.AddStacks,
                3f,
                5,
                1f,
                sourceDispelTags,
                sourceImmunityTags);

            sourceDispelTags[0] = Tag("dispel.changed");
            sourceImmunityTags[0] = Tag("immunity.changed");

            AssertNoUnityObjectFields(typeof(RuntimeStatusDefinition));
            Assert.That(status.DispelTags, Is.Not.InstanceOf<ContentTag[]>());
            Assert.That(status.ImmunityTags, Is.Not.InstanceOf<ContentTag[]>());
            Assert.That(status.DispelTags[0].Value, Is.EqualTo("dispel.debuff"));
            Assert.That(status.ImmunityTags[0].Value, Is.EqualTo("immunity.fire"));
        }

        private AuthoringFixture CreateAuthoringFixture(int schemaVersion)
        {
            var status = Create<StatusEffectAuthoring>("Burning");
            status.ConfigureIdentity(
                "test.status.burning",
                "content.test.status.burning.name",
                "content.test.status.burning.description",
                new[] { "content.placeholder", "status.debuff", "element.fire" });
            status.Configure(
                StatusStackingPolicy.AddStacks,
                3f,
                5,
                1f,
                new[] { "dispel.debuff", "dispel.fire" },
                new[] { "immunity.fire" });
            var periodic = new RuntimeStatusPeriodicDamage(
                DamageType.Fire,
                DamageTags.DamageOverTime | DamageTags.Status,
                6f,
                false,
                0.25f,
                System.Numerics.Vector2.Zero);
            status.ConfigureBehavior(default, periodic, 0f);

            var pack = Create<ContentPackAuthoring>("StatusPack");
            pack.Configure(
                "test.pack.status_baker",
                "0.1.0",
                schemaVersion,
                "0.1.0",
                string.Empty,
                Array.Empty<ContentPackDependencyAuthoring>(),
                "packs/test.status_baker/catalog",
                "pack.test.status_baker",
                false,
                new ContentAuthoringBase[] { status });

            var resolver = new DictionaryPathResolver();
            resolver.Add(pack, "Assets/Test/StatusPack.asset");
            resolver.Add(status, "Assets/Test/Burning.asset");
            return new AuthoringFixture(pack, resolver);
        }

        private T Create<T>(string name)
            where T : ScriptableObject
        {
            var value = ScriptableObject.CreateInstance<T>();
            value.name = name;
            createdObjects.Add(value);
            return value;
        }

        private static BakedContentCatalogDto StatusCatalogDto(
            int schemaVersion,
            string policy)
        {
            return new BakedContentCatalogDto
            {
                manifest = new ContentPackManifestDto
                {
                    packId = "test.pack.serialized",
                    version = "0.1.0",
                    schemaVersion = schemaVersion,
                    minimumGameVersion = "0.1.0",
                    maximumGameVersion = string.Empty,
                    dependencies = Array.Empty<ContentPackDependencyDto>(),
                    catalogAddress = "packs/test.serialized/catalog",
                    assetLabel = "pack.test.serialized",
                    official = false,
                    sourceAssetPath = "Assets/Test/SerializedPack.asset"
                },
                definitions = new[]
                {
                    new RuntimeContentDefinitionDto
                    {
                        kind = RuntimeContentKinds.Status,
                        id = "test.status.serialized",
                        localizedNameKey = "content.test.status.serialized.name",
                        localizedDescriptionKey =
                            "content.test.status.serialized.description",
                        sourceAssetPath = "Assets/Test/SerializedStatus.asset",
                        tags = Array.Empty<string>(),
                        stackingPolicy = policy,
                        durationSeconds = 1f,
                        maxStacks = 2,
                        tickIntervalSeconds = 0.5f,
                        dispelTags = new[] { "dispel.debuff" },
                        immunityTags = new[] { "immunity.fire" }
                    }
                },
                contentHash = string.Empty
            };
        }

        private static RuntimeStatusDefinition Status(
            string id,
            StatusStackingPolicy policy,
            float duration,
            int maxStacks,
            float tickInterval,
            RuntimeStatusBehavior behavior = default)
        {
            return new RuntimeStatusDefinition(
                Id(id),
                "content." + id + ".name",
                "content." + id + ".description",
                "Assets/Test/" + id + ".asset",
                Array.Empty<ContentTag>(),
                policy,
                duration,
                maxStacks,
                tickInterval,
                Array.Empty<ContentTag>(),
                Array.Empty<ContentTag>(),
                behavior);
        }

        private static BakedContentCatalog Catalog(
            int schemaVersion,
            string packId,
            params RuntimeContentDefinition[] definitions)
        {
            return BakedContentCatalog.Create(
                new ContentPackManifest(
                    Id(packId),
                    GameVersion,
                    schemaVersion,
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

        private static ContentTag Tag(string value)
        {
            return ContentTag.Create(value).Value;
        }

        private static bool ContainsError(
            ContentValidationReport report,
            ErrorCode code)
        {
            for (var index = 0; index < report.Errors.Count; index++)
            {
                if (report.Errors[index].Code == code)
                {
                    return true;
                }
            }

            return false;
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

        private readonly struct AuthoringFixture
        {
            public AuthoringFixture(
                ContentPackAuthoring pack,
                DictionaryPathResolver pathResolver)
            {
                Pack = pack;
                PathResolver = pathResolver;
            }

            public ContentPackAuthoring Pack { get; }

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
