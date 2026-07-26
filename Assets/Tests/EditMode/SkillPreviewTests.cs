using Game.Content.Runtime;
using Game.Simulation;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public sealed class SkillPreviewTests
    {
        [Test]
        public void FourPlaceholderSkillsProduceStableFixedSeedPreviews()
        {
            var skills = new[]
            {
                SkillTestFactory.Skill(
                    "test.skill.single_projectile",
                    SkillModuleIds.TriggerTimer,
                    SkillTestFactory.Module(SkillModuleIds.TargetingNearest, 10f, int0: 1),
                    SkillTestFactory.Module(
                        SkillModuleIds.DeliveryProjectile,
                        60f,
                        0.5f,
                        1f,
                        int0: 1,
                        presentation: SkillTestFactory.Placeholder("single_projectile")),
                    new[] { SkillTestFactory.Damage(12f) },
                    cooldown: 0.5f),
                SkillTestFactory.Skill(
                    "test.skill.orbit",
                    SkillModuleIds.TriggerTimer,
                    SkillTestFactory.Module(SkillModuleIds.TargetingSelf),
                    SkillTestFactory.Module(
                        SkillModuleIds.DeliveryOrbit,
                        1f,
                        1.5f,
                        5f,
                        1f,
                        int0: 2,
                        presentation: SkillTestFactory.Placeholder("orbit")),
                    new[] { SkillTestFactory.Damage(4f) },
                    cooldown: 10f),
                SkillTestFactory.Skill(
                    "test.skill.ground_area",
                    SkillModuleIds.TriggerTimer,
                    SkillTestFactory.Module(
                        SkillModuleIds.TargetingRandomPointAroundPlayer,
                        0f,
                        2f),
                    SkillTestFactory.Module(
                        SkillModuleIds.DeliveryArea,
                        2f,
                        1f,
                        0.2f,
                        presentation: SkillTestFactory.Placeholder("ground_area")),
                    new[] { SkillTestFactory.Damage(5f) },
                    cooldown: 0.75f),
                SkillTestFactory.Skill(
                    "test.skill.damage_aura",
                    SkillModuleIds.TriggerTimer,
                    SkillTestFactory.Module(SkillModuleIds.TargetingSelf),
                    SkillTestFactory.Module(
                        SkillModuleIds.DeliveryAura,
                        4f,
                        1f,
                        0.25f,
                        presentation: SkillTestFactory.Placeholder("damage_aura")),
                    new[] { SkillTestFactory.Damage(3f) },
                    cooldown: 1.25f)
            };
            var registry = SkillTestFactory.Registry(skills);
            var expected = new[]
            {
                new SkillPreviewSummary(0x4D34554CUL, 3.0000002f, 23.9999981f, 6, 6),
                new SkillPreviewSummary(0x4D34554CUL, 3.0000002f, 189.333313f, 142, 1),
                new SkillPreviewSummary(0x4D34554CUL, 3.0000002f, 189.999985f, 114, 4),
                new SkillPreviewSummary(0x4D34554CUL, 3.0000002f, 159.999985f, 160, 3)
            };

            for (var index = 0; index < skills.Length; index++)
            {
                var runtimeIndex = SkillTestFactory.IndexOf(registry, skills[index].Id);
                var first = SkillPreviewHarness.Run(registry, runtimeIndex, 0x4D34554CUL, 3f);
                var second = SkillPreviewHarness.Run(registry, runtimeIndex, 0x4D34554CUL, 3f);

                Assert.That(first.IsSuccess, Is.True, first.Error.ToString());
                Assert.That(second.IsSuccess, Is.True, second.Error.ToString());
                TestContext.WriteLine(
                    skills[index].Id.Value + "|" +
                    first.Value.DamagePerSecond.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "|" +
                    first.Value.HitCount + "|" +
                    first.Value.TriggerCount);
                Assert.That(first.Value, Is.EqualTo(expected[index]), skills[index].Id.Value);
                Assert.That(second.Value, Is.EqualTo(first.Value), skills[index].Id.Value);
                Assert.That(first.Value.TriggerCount, Is.GreaterThan(0), skills[index].Id.Value);
                Assert.That(first.Value.HitCount, Is.GreaterThan(0), skills[index].Id.Value);
                Assert.That(first.Value.DamagePerSecond, Is.GreaterThan(0f), skills[index].Id.Value);
            }
        }
    }
}
