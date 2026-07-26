using System;
using Game.Content.Runtime;
using Game.Core;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public sealed class M5ContentTests
    {
        [Test]
        public void SchemaFourRoundTripPreservesEnemyMapEncounterAndHash()
        {
            var fixture = M5TestFactory.Create();

            var restored = fixture.Catalog.ToDto().ToCatalog();

            Assert.That(restored.IsSuccess, Is.True, restored.Error.ToString());
            Assert.That(restored.Value.ContentHash, Is.EqualTo(fixture.Catalog.ContentHash));
            Assert.That(restored.Value.Definitions.Count, Is.EqualTo(9));
            var restoredEnemy = (RuntimeEnemyDefinition)restored.Value.Definitions[1];
            var restoredEncounter = (RuntimeEncounterSchedule)restored.Value.Definitions[6];
            var restoredMap = (RuntimeMapDefinition)restored.Value.Definitions[7];
            Assert.That(restoredEnemy.HasM5Data, Is.True);
            Assert.That(restoredEnemy.AttackSkillId, Is.EqualTo(fixture.Skill.Id));
            Assert.That(restoredEncounter.Phases.Count, Is.EqualTo(1));
            Assert.That(restoredMap.EncounterScheduleId, Is.EqualTo(restoredEncounter.Id));
        }

        [Test]
        public void SchemaFourRejectsLegacyEnemyWithoutM5RuntimeData()
        {
            var packId = SkillTestFactory.Id("test.pack.m5_legacy_enemy");
            var enemy = new RuntimeEnemyDefinition(
                SkillTestFactory.Id("test.enemy.legacy_in_schema_four"),
                "content.test.enemy.legacy.name",
                "content.test.enemy.legacy.description",
                "Assets/Test/LegacyEnemy.asset",
                Array.Empty<ContentTag>(),
                10f,
                0.5f);
            var catalog = BakedContentCatalog.Create(
                new ContentPackManifest(
                    packId,
                    SkillTestFactory.GameVersion,
                    ContentPackTopology.EnemyMapEncounterSchemaVersion,
                    SkillTestFactory.GameVersion,
                    null,
                    Array.Empty<ContentPackDependency>(),
                    "packs/test/m5_legacy",
                    "pack.test.m5_legacy",
                    false,
                    "Assets/Test/M5LegacyPack.asset"),
                new RuntimeContentDefinition[] { enemy });

            var report = ContentValidator.ValidateCatalogs(
                new[] { catalog },
                SkillTestFactory.GameVersion);

            Assert.That(report.IsValid, Is.False);
            Assert.That(report.Errors[0].Message, Does.Contain("M5 runtime data"));
        }

        [Test]
        public void SameEncounterDefinitionIsReferencedByBothMapProviders()
        {
            var fixture = M5TestFactory.Create();

            Assert.That(fixture.FiniteMap.EncounterScheduleId, Is.EqualTo(fixture.Encounter.Id));
            Assert.That(fixture.InfiniteMap.EncounterScheduleId, Is.EqualTo(fixture.Encounter.Id));
            Assert.That(fixture.FiniteMap.RuntimeProviderId, Is.Not.EqualTo(fixture.InfiniteMap.RuntimeProviderId));
        }
    }
}
