using System;
using System.Numerics;
using Game.Content.Runtime;
using Game.Core;
using Game.Simulation;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public sealed class SkillRuntimeTests
    {
        [TestCase("base.trigger.timer", SkillTriggerEventType.Timer, 0)]
        [TestCase("base.trigger.on_hit", SkillTriggerEventType.OnHit, 1)]
        [TestCase("base.trigger.on_kill", SkillTriggerEventType.OnKill, 1)]
        [TestCase("base.trigger.on_damage_taken", SkillTriggerEventType.OnDamageTaken, 2)]
        [TestCase("base.trigger.on_pickup", SkillTriggerEventType.OnPickup, 2)]
        [TestCase("base.trigger.on_status_applied", SkillTriggerEventType.OnStatusApplied, 2)]
        public void InitialTriggerModulesActivateOnlyForMatchingOwnerContext(
            string triggerId,
            SkillTriggerEventType eventType,
            int ownerRole)
        {
            var skill = SkillTestFactory.Skill(
                "test.skill.trigger_" + eventType.ToString().ToLowerInvariant(),
                SkillTestFactory.Id(triggerId),
                SkillTestFactory.Module(SkillModuleIds.TargetingSelf),
                SkillTestFactory.Module(SkillModuleIds.DeliveryInstant),
                new[] { SkillTestFactory.GainResource() },
                cooldown: 10f);
            var registry = SkillTestFactory.Registry(skill);
            var skills = SkillTestFactory.Runtime(registry, 100UL + (ulong)eventType);
            var world = new SimulationWorld(skillRuntime: skills);
            var owner = Actor(world, Vector2.Zero);
            var other = Actor(world, Vector2.UnitX);
            var skillIndex = SkillTestFactory.IndexOf(registry, skill.Id);
            Assert.That(skills.AddInstance(owner, skillIndex).IsSuccess, Is.True);
            skills.SetResource(owner, 0f);

            if (eventType != SkillTriggerEventType.Timer)
            {
                var source = ownerRole == 1 ? owner : other;
                var target = ownerRole == 2 ? owner : other;
                Assert.That(
                    world.QueueSkillTrigger(
                        new SkillTriggerContext(
                            eventType,
                            source,
                            target,
                            target == owner ? Vector2.Zero : Vector2.UnitX,
                            Vector2.UnitX,
                            skill.Id,
                            default,
                            0)),
                    Is.True);
            }

            new FixedTickRunner(world).Advance(SimulationClock.TickDurationSeconds);

            Assert.That(skills.GetResource(owner), Is.EqualTo(1f));
            Assert.That(skills.TriggerCount, Is.EqualTo(1));
        }

        [Test]
        public void LevelPatchProducesTypedImmutableValuesAtEachLevel()
        {
            var patches = new[]
            {
                new SkillLevelPatch(
                    2,
                    SkillPatchTarget.Cooldown,
                    0,
                    SkillPatchValueType.Float,
                    SkillPatchOperation.Override,
                    0.5f,
                    0),
                new SkillLevelPatch(
                    2,
                    SkillPatchTarget.TargetingInt0,
                    0,
                    SkillPatchValueType.Integer,
                    SkillPatchOperation.Override,
                    0f,
                    2),
                new SkillLevelPatch(
                    2,
                    SkillPatchTarget.EffectValue0,
                    0,
                    SkillPatchValueType.Float,
                    SkillPatchOperation.Add,
                    5f,
                    0)
            };
            var skill = SkillTestFactory.Skill(
                "test.skill.level_patch",
                SkillModuleIds.TriggerTimer,
                SkillTestFactory.Module(SkillModuleIds.TargetingNearest, 10f, int0: 1),
                SkillTestFactory.Module(SkillModuleIds.DeliveryInstant),
                new[] { SkillTestFactory.Damage(10f) },
                cooldown: 1f,
                patches: patches);
            var registry = SkillTestFactory.Registry(skill);
            var catalog = SkillRuntimeCatalog.Build(registry, SkillModuleRegistry.CreateDefault());
            Assert.That(catalog.IsSuccess, Is.True, catalog.Error.ToString());
            Assert.That(
                catalog.Value.TryGet(
                    SkillTestFactory.IndexOf(registry, skill.Id),
                    out var compiled),
                Is.True);

            Assert.That(compiled.GetLevel(1).CooldownSeconds, Is.EqualTo(1f));
            Assert.That(compiled.GetLevel(1).Targeting.Int0, Is.EqualTo(1));
            Assert.That(compiled.GetLevel(1).Effects[0].Value0, Is.EqualTo(10f));
            Assert.That(compiled.GetLevel(2).CooldownSeconds, Is.EqualTo(0.5f));
            Assert.That(compiled.GetLevel(2).Targeting.Int0, Is.EqualTo(2));
            Assert.That(compiled.GetLevel(2).Effects[0].Value0, Is.EqualTo(15f));
        }

        [Test]
        public void SameCompiledSkillDefinitionIsReusedByTwoActorInstances()
        {
            var skill = SkillTestFactory.Skill(
                "test.skill.shared_definition",
                SkillModuleIds.TriggerTimer,
                SkillTestFactory.Module(SkillModuleIds.TargetingSelf),
                SkillTestFactory.Module(SkillModuleIds.DeliveryInstant),
                new[] { SkillTestFactory.GainResource() },
                cooldown: 10f);
            var registry = SkillTestFactory.Registry(skill);
            var skills = SkillTestFactory.Runtime(registry);
            var world = new SimulationWorld(skillRuntime: skills);
            var first = Actor(world, Vector2.Zero);
            var second = Actor(world, Vector2.UnitX);
            var index = SkillTestFactory.IndexOf(registry, skill.Id);
            var firstResult = skills.AddInstance(first, index);
            var secondResult = skills.AddInstance(second, index);
            Assert.That(firstResult.IsSuccess && secondResult.IsSuccess, Is.True);
            skills.TryGetInstance(firstResult.Value, out var firstInstance);
            skills.TryGetInstance(secondResult.Value, out var secondInstance);

            new FixedTickRunner(world).Advance(SimulationClock.TickDurationSeconds);

            Assert.That(firstInstance.Definition, Is.SameAs(secondInstance.Definition));
            Assert.That(skills.GetResource(first), Is.EqualTo(1f));
            Assert.That(skills.GetResource(second), Is.EqualTo(1f));
        }

        [Test]
        public void RemovingOwnerReleasesSkillInstancesAndInvalidatesReusedHandles()
        {
            var skill = SkillTestFactory.Skill(
                "test.skill.owner_lifecycle",
                SkillModuleIds.TriggerTimer,
                SkillTestFactory.Module(SkillModuleIds.TargetingSelf),
                SkillTestFactory.Module(SkillModuleIds.DeliveryInstant),
                new[] { SkillTestFactory.GainResource() },
                cooldown: 10f);
            var registry = SkillTestFactory.Registry(skill);
            var skills = SkillTestFactory.Runtime(registry);
            var world = new SimulationWorld(skillRuntime: skills);
            var owner = Actor(world, Vector2.Zero);
            var skillIndex = SkillTestFactory.IndexOf(registry, skill.Id);
            var first = skills.AddInstance(owner, skillIndex);
            Assert.That(first.IsSuccess, Is.True, first.Error.ToString());

            world.Commands.Remove(EntityKind.Actor, owner.Handle);
            new FixedTickRunner(world).Advance(SimulationClock.TickDurationSeconds);

            Assert.That(skills.InstanceCount, Is.Zero);
            Assert.That(skills.TryGetInstance(first.Value, out _), Is.False);

            var replacementOwner = Actor(world, Vector2.UnitX);
            var replacement = skills.AddInstance(replacementOwner, skillIndex);

            Assert.That(replacement.IsSuccess, Is.True, replacement.Error.ToString());
            Assert.That(skills.InstanceCount, Is.EqualTo(1));
            Assert.That(replacement.Value, Is.Not.EqualTo(first.Value));
            Assert.That(skills.TryGetInstance(replacement.Value, out _), Is.True);
            Assert.That(skills.TryGetInstance(first.Value, out _), Is.False);
        }

        [Test]
        public void SpawnSecondarySkillPropagatesProcDepth()
        {
            var secondary = SkillTestFactory.Skill(
                "test.skill.secondary_damage",
                SkillModuleIds.TriggerTimer,
                SkillTestFactory.Module(SkillModuleIds.TargetingSelf),
                SkillTestFactory.Module(SkillModuleIds.DeliveryInstant),
                new[] { SkillTestFactory.Damage(10f) },
                cooldown: 100f);
            var primary = SkillTestFactory.Skill(
                "test.skill.primary_spawn",
                SkillModuleIds.TriggerTimer,
                SkillTestFactory.Module(SkillModuleIds.TargetingSelf),
                SkillTestFactory.Module(SkillModuleIds.DeliveryInstant),
                new[]
                {
                    new EffectOp(
                        EffectOpCode.SpawnSecondarySkill,
                        referenceId0: secondary.Id)
                },
                cooldown: 100f);
            var registry = SkillTestFactory.Registry(primary, secondary);
            var skills = SkillTestFactory.Runtime(registry);
            var world = new SimulationWorld(skillRuntime: skills);
            var owner = Actor(world, Vector2.Zero);
            Assert.That(
                skills.AddInstance(
                    owner,
                    SkillTestFactory.IndexOf(registry, primary.Id)).IsSuccess,
                Is.True);
            var runner = new FixedTickRunner(world);

            runner.Advance(SimulationClock.TickDurationSeconds);
            runner.Advance(SimulationClock.TickDurationSeconds);

            Assert.That(world.CombatEvents.DamageAppliedCount, Is.EqualTo(1));
            Assert.That(
                world.CombatEvents.GetDamageAppliedAt(0).Context.Packet.ProcDepth,
                Is.EqualTo(1));
        }

        [Test]
        public void ChainedSecondarySkillsRegisterTransitivelyAndPropagateProcDepth()
        {
            var tertiary = SkillTestFactory.Skill(
                "test.skill.tertiary_damage",
                SkillModuleIds.TriggerTimer,
                SkillTestFactory.Module(SkillModuleIds.TargetingSelf),
                SkillTestFactory.Module(SkillModuleIds.DeliveryInstant),
                new[] { SkillTestFactory.Damage(10f) },
                cooldown: 100f);
            var secondary = SkillTestFactory.Skill(
                "test.skill.secondary_spawn",
                SkillModuleIds.TriggerTimer,
                SkillTestFactory.Module(SkillModuleIds.TargetingSelf),
                SkillTestFactory.Module(SkillModuleIds.DeliveryInstant),
                new[]
                {
                    new EffectOp(
                        EffectOpCode.SpawnSecondarySkill,
                        referenceId0: tertiary.Id)
                },
                cooldown: 100f);
            var primary = SkillTestFactory.Skill(
                "test.skill.primary_chained_spawn",
                SkillModuleIds.TriggerTimer,
                SkillTestFactory.Module(SkillModuleIds.TargetingSelf),
                SkillTestFactory.Module(SkillModuleIds.DeliveryInstant),
                new[]
                {
                    new EffectOp(
                        EffectOpCode.SpawnSecondarySkill,
                        referenceId0: secondary.Id)
                },
                cooldown: 100f);
            var registry = SkillTestFactory.Registry(primary, secondary, tertiary);
            var skills = SkillTestFactory.Runtime(registry);
            var world = new SimulationWorld(skillRuntime: skills);
            var owner = Actor(world, Vector2.Zero);
            Assert.That(
                skills.AddInstance(
                    owner,
                    SkillTestFactory.IndexOf(registry, primary.Id)).IsSuccess,
                Is.True);
            var runner = new FixedTickRunner(world);

            runner.Advance(SimulationClock.TickDurationSeconds);
            runner.Advance(SimulationClock.TickDurationSeconds);
            runner.Advance(SimulationClock.TickDurationSeconds);

            Assert.That(world.CombatEvents.DamageAppliedCount, Is.EqualTo(1));
            Assert.That(
                world.CombatEvents.GetDamageAppliedAt(0).Context.Packet.ProcDepth,
                Is.EqualTo(2));
        }

        [Test]
        public void DamageAndApplyStatusEffectOpsUseM3RequestPipelines()
        {
            var status = Status("test.status.skill_applied");
            var skill = SkillTestFactory.Skill(
                "test.skill.damage_status",
                SkillModuleIds.TriggerTimer,
                SkillTestFactory.Module(SkillModuleIds.TargetingNearest, 5f, int0: 1),
                SkillTestFactory.Module(SkillModuleIds.DeliveryInstant),
                new[]
                {
                    SkillTestFactory.Damage(10f),
                    new EffectOp(
                        EffectOpCode.ApplyStatus,
                        value0: 1f,
                        referenceId0: status.Id)
                },
                cooldown: 100f);
            var registry = SkillTestFactory.Registry(skill, status);
            var skills = SkillTestFactory.Runtime(registry);
            var world = new SimulationWorld(
                statusCatalog: new RuntimeStatusCatalog(registry),
                skillRuntime: skills);
            var owner = Actor(world, Vector2.Zero);
            var target = Actor(world, Vector2.UnitX);
            Assert.That(
                skills.AddInstance(owner, SkillTestFactory.IndexOf(registry, skill.Id)).IsSuccess,
                Is.True);

            new FixedTickRunner(world).Advance(SimulationClock.TickDurationSeconds);

            Assert.That(world.Actors.TryReadHealth(target.Handle, out var health), Is.True);
            Assert.That(health.Current, Is.EqualTo(90f));
            Assert.That(
                world.Actors.TryReadStatus(
                    target.Handle,
                    SkillTestFactory.IndexOf(registry, status.Id),
                    out _),
                Is.True);
        }

        [Test]
        public void HealShieldMotionModifierAndResourceEffectsResolveCentrally()
        {
            var effects = new[]
            {
                new EffectOp(EffectOpCode.Heal, value0: 5f),
                new EffectOp(EffectOpCode.GrantShield, value0: 3f),
                new EffectOp(EffectOpCode.Knockback, value0: 2f),
                new EffectOp(EffectOpCode.Pull, value0: 0.5f),
                new EffectOp(
                    EffectOpCode.ModifyStat,
                    value0: 1f,
                    value1: 1f,
                    int0: (int)ModifierOperation.AddFlat,
                    statId0: BuiltInStatIds.MoveSpeed),
                new EffectOp(EffectOpCode.GainResource, value0: 2f)
            };
            var skill = SkillTestFactory.Skill(
                "test.skill.central_effects",
                SkillModuleIds.TriggerTimer,
                SkillTestFactory.Module(SkillModuleIds.TargetingSelf),
                SkillTestFactory.Module(SkillModuleIds.DeliveryInstant),
                effects,
                cooldown: 100f);
            var registry = SkillTestFactory.Registry(skill);
            var skills = SkillTestFactory.Runtime(registry);
            var world = new SimulationWorld(skillRuntime: skills);
            var stats = StatBaseValues.CreateDefault(100f, 5f);
            var ownerHandle = world.CreateActor(
                SimulationEntityState.Create(Vector2.Zero, Vector2.Zero),
                new ActorCombatInitialization(stats, 50f, 0f, 0f, default));
            var owner = new SpatialEntity(EntityKind.Actor, ownerHandle);
            Assert.That(
                skills.AddInstance(owner, SkillTestFactory.IndexOf(registry, skill.Id)).IsSuccess,
                Is.True);

            new FixedTickRunner(world).Advance(SimulationClock.TickDurationSeconds);

            world.Actors.TryReadHealth(ownerHandle, out var health);
            world.Actors.TryReadShield(ownerHandle, out var shield);
            world.Actors.TryReadStat(ownerHandle, BuiltInStatIndices.MoveSpeed, out var moveSpeed);
            world.Actors.TryRead(ownerHandle, out var body);
            Assert.That(health.Current, Is.EqualTo(55f));
            Assert.That(shield.Current, Is.EqualTo(3f));
            Assert.That(shield.Maximum, Is.EqualTo(3f));
            Assert.That(moveSpeed, Is.EqualTo(6f));
            Assert.That(body.Velocity.X, Is.EqualTo(1.5f).Within(0.0001f));
            Assert.That(skills.GetResource(owner), Is.EqualTo(2f));
        }

        [Test]
        public void RemoveStatusEffectUsesM3DispelRequest()
        {
            var status = Status("test.status.removable", "dispel.skill_test");
            var skill = SkillTestFactory.Skill(
                "test.skill.remove_status",
                SkillModuleIds.TriggerTimer,
                SkillTestFactory.Module(SkillModuleIds.TargetingSelf),
                SkillTestFactory.Module(SkillModuleIds.DeliveryInstant),
                new[]
                {
                    new EffectOp(
                        EffectOpCode.RemoveStatus,
                        tag0: ContentTag.Create("dispel.skill_test").Value)
                },
                cooldown: 100f);
            var registry = SkillTestFactory.Registry(skill, status);
            var skills = SkillTestFactory.Runtime(registry);
            var world = new SimulationWorld(
                statusCatalog: new RuntimeStatusCatalog(registry),
                skillRuntime: skills);
            var owner = Actor(world, Vector2.Zero);
            var statusIndex = SkillTestFactory.IndexOf(registry, status.Id);
            world.QueueStatus(
                new StatusApplicationRequest(owner, owner, status.Id, statusIndex, 1f, 0));
            var runner = new FixedTickRunner(world);
            runner.Advance(SimulationClock.TickDurationSeconds);
            Assert.That(world.Actors.TryReadStatus(owner.Handle, statusIndex, out _), Is.True);
            Assert.That(
                skills.AddInstance(owner, SkillTestFactory.IndexOf(registry, skill.Id)).IsSuccess,
                Is.True);

            runner.Advance(SimulationClock.TickDurationSeconds);

            Assert.That(world.Actors.TryReadStatus(owner.Handle, statusIndex, out _), Is.False);
        }

        private static SpatialEntity Actor(SimulationWorld world, Vector2 position)
        {
            return new SpatialEntity(
                EntityKind.Actor,
                world.CreateActor(
                    SimulationEntityState.Create(position, Vector2.Zero),
                    ActorCombatInitialization.CreateDefault()));
        }

        private static RuntimeStatusDefinition Status(string id, string dispelTag = null)
        {
            var tags = string.IsNullOrEmpty(dispelTag)
                ? Array.Empty<ContentTag>()
                : new[] { ContentTag.Create(dispelTag).Value };
            return new RuntimeStatusDefinition(
                SkillTestFactory.Id(id),
                "content." + id + ".name",
                "content." + id + ".description",
                "Assets/Test/" + id + ".asset",
                Array.Empty<ContentTag>(),
                StatusStackingPolicy.RefreshDuration,
                5f,
                1,
                0f,
                tags,
                Array.Empty<ContentTag>(),
                default);
        }
    }
}
