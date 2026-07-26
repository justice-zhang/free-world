using System;
using System.Numerics;
using Game.Content.Runtime;
using Game.Core;
using Game.Simulation;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public sealed class SkillTargetingDeliveryTests
    {
        [Test]
        public void AllInitialTargetingModulesUseSpatialGridAndExpectedGeometry()
        {
            var definitions = new[]
            {
                TargetSkill("test.skill.target_self", SkillTestFactory.Module(SkillModuleIds.TargetingSelf)),
                TargetSkill("test.skill.target_nearest", SkillTestFactory.Module(SkillModuleIds.TargetingNearest, 10f, int0: 1)),
                TargetSkill("test.skill.target_random", SkillTestFactory.Module(SkillModuleIds.TargetingRandom, 10f, int0: 1)),
                TargetSkill("test.skill.target_circle", SkillTestFactory.Module(SkillModuleIds.TargetingCircle, 2.1f)),
                TargetSkill("test.skill.target_cone", SkillTestFactory.Module(SkillModuleIds.TargetingCone, 5f, 90f)),
                TargetSkill("test.skill.target_line", SkillTestFactory.Module(SkillModuleIds.TargetingLine, 5f, 0.5f)),
                TargetSkill("test.skill.target_ring", SkillTestFactory.Module(SkillModuleIds.TargetingRing, 2f, 4f)),
                TargetSkill("test.skill.target_point", SkillTestFactory.Module(SkillModuleIds.TargetingRandomPointAroundPlayer, 2f, 3f))
            };
            var registry = SkillTestFactory.Registry(definitions);
            var modules = SkillModuleRegistry.CreateDefault();
            var compiled = SkillRuntimeCatalog.Build(registry, modules);
            Assert.That(compiled.IsSuccess, Is.True, compiled.Error.ToString());
            var skills = new SkillRuntime(compiled.Value, 1234UL);
            var world = new SimulationWorld(skillRuntime: skills);
            var owner = Actor(world, Vector2.Zero);
            var eastNear = Actor(world, new Vector2(1f, 0f));
            var eastFar = Actor(world, new Vector2(3f, 0f));
            var northFar = Actor(world, new Vector2(0f, 3f));
            var westNear = Actor(world, new Vector2(-1f, 0f));
            var context = new SkillTriggerContext(
                SkillTriggerEventType.Timer,
                owner,
                owner,
                Vector2.Zero,
                Vector2.UnitX,
                default,
                default,
                0);
            var spatial = new SpatialQueryBuffer();
            var targets = new SkillTargetResultBuffer();
            var random = new RandomStream(99UL);

            var self = Select(definitions[0], owner, registry, skills, modules, world, context, spatial, targets, ref random);
            Assert.That(self.Count, Is.EqualTo(1));
            Assert.That(self[0].Entity, Is.EqualTo(owner));

            var nearest = Select(definitions[1], owner, registry, skills, modules, world, context, spatial, targets, ref random);
            Assert.That(nearest.Count, Is.EqualTo(1));
            Assert.That(nearest[0].Entity, Is.EqualTo(eastNear));

            var randomTarget = Select(definitions[2], owner, registry, skills, modules, world, context, spatial, targets, ref random);
            Assert.That(randomTarget.Count, Is.EqualTo(1));
            Assert.That(randomTarget[0].Entity, Is.Not.EqualTo(owner));

            var circle = Select(definitions[3], owner, registry, skills, modules, world, context, spatial, targets, ref random);
            Assert.That(circle.Count, Is.EqualTo(2));
            Assert.That(Contains(circle, eastNear), Is.True);
            Assert.That(Contains(circle, westNear), Is.True);

            var cone = Select(definitions[4], owner, registry, skills, modules, world, context, spatial, targets, ref random);
            Assert.That(cone.Count, Is.EqualTo(2));
            Assert.That(Contains(cone, eastNear), Is.True);
            Assert.That(Contains(cone, eastFar), Is.True);

            var line = Select(definitions[5], owner, registry, skills, modules, world, context, spatial, targets, ref random);
            Assert.That(line.Count, Is.EqualTo(2));
            Assert.That(Contains(line, eastNear), Is.True);
            Assert.That(Contains(line, eastFar), Is.True);

            var ring = Select(definitions[6], owner, registry, skills, modules, world, context, spatial, targets, ref random);
            Assert.That(ring.Count, Is.EqualTo(2));
            Assert.That(Contains(ring, eastFar), Is.True);
            Assert.That(Contains(ring, northFar), Is.True);

            var point = Select(definitions[7], owner, registry, skills, modules, world, context, spatial, targets, ref random);
            Assert.That(point.Count, Is.EqualTo(1));
            Assert.That(point[0].HasEntity, Is.False);
            Assert.That(point[0].Position.Length(), Is.InRange(2f, 3f));
        }

        [TestCase("projectile")]
        [TestCase("area")]
        [TestCase("aura")]
        [TestCase("orbit")]
        public void NonInstantDeliveryLifecycleSpawnsHitsAndCleansUp(string deliveryName)
        {
            var targetModule = deliveryName == "projectile"
                ? SkillTestFactory.Module(SkillModuleIds.TargetingNearest, 5f, int0: 1)
                : deliveryName == "area"
                    ? SkillTestFactory.Module(SkillModuleIds.TargetingRandomPointAroundPlayer, 0f, 0f)
                    : SkillTestFactory.Module(SkillModuleIds.TargetingSelf);
            SkillModuleDefinition delivery;
            Vector2 targetPosition;
            if (deliveryName == "projectile")
            {
                delivery = SkillTestFactory.Module(
                    SkillModuleIds.DeliveryProjectile,
                    30f,
                    0.25f,
                    0.2f,
                    int0: 1,
                    presentation: SkillTestFactory.Placeholder("projectile"));
                targetPosition = new Vector2(2f, 0f);
            }
            else if (deliveryName == "area")
            {
                delivery = SkillTestFactory.Module(
                    SkillModuleIds.DeliveryArea,
                    1f,
                    0.07f,
                    (float)SimulationClock.TickDurationSeconds,
                    presentation: SkillTestFactory.Placeholder("area"));
                targetPosition = new Vector2(0.5f, 0f);
            }
            else if (deliveryName == "aura")
            {
                delivery = SkillTestFactory.Module(
                    SkillModuleIds.DeliveryAura,
                    1f,
                    0.07f,
                    (float)SimulationClock.TickDurationSeconds,
                    presentation: SkillTestFactory.Placeholder("aura"));
                targetPosition = new Vector2(0.5f, 0f);
            }
            else
            {
                delivery = SkillTestFactory.Module(
                    SkillModuleIds.DeliveryOrbit,
                    1f,
                    0.5f,
                    0.07f,
                    0f,
                    int0: 1,
                    presentation: SkillTestFactory.Placeholder("orbit"));
                targetPosition = new Vector2(1f, 0f);
            }

            var skill = SkillTestFactory.Skill(
                "test.skill.delivery_" + deliveryName,
                SkillModuleIds.TriggerTimer,
                targetModule,
                delivery,
                new[] { SkillTestFactory.Damage(10f) },
                cooldown: 100f);
            var registry = SkillTestFactory.Registry(skill);
            var skills = SkillTestFactory.Runtime(registry, 222UL);
            var world = new SimulationWorld(skillRuntime: skills);
            var owner = Actor(world, Vector2.Zero);
            var target = Actor(world, targetPosition);
            Assert.That(
                skills.AddInstance(owner, SkillTestFactory.IndexOf(registry, skill.Id)).IsSuccess,
                Is.True);
            var runner = new FixedTickRunner(world);

            runner.Advance(SimulationClock.TickDurationSeconds);
            Assert.That(skills.ActiveDeliveryCount, Is.EqualTo(1));
            Assert.That(
                deliveryName == "projectile" ? world.Projectiles.Count : world.Areas.Count,
                Is.EqualTo(1));
            for (var tick = 0; tick < 7; tick++)
            {
                runner.Advance(SimulationClock.TickDurationSeconds);
            }

            Assert.That(world.Actors.TryReadHealth(target.Handle, out var health), Is.True);
            Assert.That(health.Current, Is.LessThan(100f));
            Assert.That(skills.ActiveDeliveryCount, Is.Zero);
            Assert.That(
                deliveryName == "projectile" ? world.Projectiles.Count : world.Areas.Count,
                Is.Zero);
        }

        private static RuntimeSkillDefinition TargetSkill(
            string id,
            in SkillModuleDefinition targeting)
        {
            return SkillTestFactory.Skill(
                id,
                SkillModuleIds.TriggerTimer,
                targeting,
                SkillTestFactory.Module(SkillModuleIds.DeliveryInstant),
                new[] { SkillTestFactory.GainResource() },
                cooldown: 100f);
        }

        private static SkillTargetResultBuffer Select(
            RuntimeSkillDefinition source,
            SpatialEntity owner,
            ContentRegistry registry,
            SkillRuntime skills,
            SkillModuleRegistry modules,
            SimulationWorld world,
            in SkillTriggerContext context,
            SpatialQueryBuffer spatial,
            SkillTargetResultBuffer targets,
            ref RandomStream random)
        {
            var add = skills.AddInstance(owner, SkillTestFactory.IndexOf(registry, source.Id));
            Assert.That(add.IsSuccess, Is.True, add.Error.ToString());
            Assert.That(skills.TryGetInstance(add.Value, out var instance), Is.True);
            Assert.That(modules.TryGetTargeting(source.Targeting.ModuleId, out var executor), Is.True);
            executor.Select(
                world,
                instance,
                instance.Definition.GetLevel(1),
                context,
                spatial,
                targets,
                ref random);
            return targets;
        }

        private static bool Contains(SkillTargetResultBuffer targets, SpatialEntity entity)
        {
            for (var index = 0; index < targets.Count; index++)
            {
                if (targets[index].Entity == entity) return true;
            }

            return false;
        }

        private static SpatialEntity Actor(SimulationWorld world, Vector2 position)
        {
            return new SpatialEntity(
                EntityKind.Actor,
                world.CreateActor(
                    SimulationEntityState.Create(position, Vector2.Zero),
                    ActorCombatInitialization.CreateDefault()));
        }
    }
}
