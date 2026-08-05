using System;
using System.Collections.Generic;
using System.Numerics;
using System.Reflection;
using Game.Application;
using Game.Content.Authoring;
using Game.Content.Runtime;
using Game.Core;
using Game.Infrastructure;
using Game.Simulation;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public sealed class QinglanG11ContractsTests
    {
        private static readonly ContentVersion GameVersion = new ContentVersion(0, 1, 0);
        private static readonly double TickSeconds = SimulationClock.TickDurationSeconds;

        [Test]
        public void BakerCanonicalizesSetSemanticContentIds()
        {
            var a = Id("test.reference.a");
            var b = Id("test.reference.b");
            var method = typeof(ContentBaker).GetMethod(
                "CanonicalizeSet",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);
            var canonical = (ContentId[])method.Invoke(
                null,
                new object[] { new[] { b, a, b, a } });

            Assert.That(canonical, Is.EqualTo(new[] { a, b }));
        }

        [Test]
        public void SchemaSixRoundTripsAllFourteenKindsAndValidatesReferences()
        {
            var definitions = CreateSchemaSixDefinitions();
            var qinglanCount = 0;
            for (var index = 0; index < definitions.Length; index++)
                if (definitions[index] is RuntimeQinglanDefinition) qinglanCount++;
            Assert.That(qinglanCount, Is.EqualTo(14));

            var catalog = Catalog(6, "test.pack.qinglan_g11", definitions);
            var dto = catalog.ToDto();
            var restored = dto.ToCatalog();

            Assert.That(restored.IsSuccess, Is.True, restored.Error.ToString());
            Assert.That(restored.Value.ContentHash, Is.EqualTo(catalog.ContentHash));
            var report = ContentValidator.ValidateCatalogs(new[] { restored.Value }, GameVersion);
            Assert.That(report.IsValid, Is.True, JoinErrors(report));
            for (var index = 0; index < definitions.Length; index++)
                Assert.That(restored.Value.Definitions[index].Kind, Is.EqualTo(definitions[index].Kind));
        }

        [Test]
        public void SchemaSixRejectsWrongTypedAndMissingReferences()
        {
            var skill = ExecutableSkill("test.skill.not_reward");
            var wrongPickup = new RuntimePickupDefinition(
                Id("test.pickup.wrong_reference"),
                "content.test.pickup.wrong_reference.name",
                "content.test.pickup.wrong_reference.description",
                "Assets/Test/WrongPickup.asset",
                Array.Empty<ContentTag>(),
                skill.Id,
                1f,
                10f,
                default,
                Id("placeholder.presentation.pickup"));
            var wrongReport = ContentValidator.ValidateCatalogs(
                new[] { Catalog(6, "test.pack.wrong_reference", skill, wrongPickup) },
                GameVersion);
            Assert.That(wrongReport.IsValid, Is.False);
            Assert.That(JoinErrors(wrongReport), Does.Contain("must resolve to a Reward"));

            var reward = new RuntimeRewardDefinition(
                Id("test.reward.reference_target"), "n", "d",
                "Assets/Test/ReferenceReward.asset", Array.Empty<ContentTag>(),
                new[] { new RewardOperation(RewardOperationCode.Heal, 1f) },
                RewardRepeatPolicy.OncePerTransaction,
                default,
                "reference.reward",
                Id("placeholder.presentation.reference"));
            var pickup = new RuntimePickupDefinition(
                Id("test.pickup.reference_target"), "n", "d",
                "Assets/Test/ReferencePickup.asset", Array.Empty<ContentTag>(),
                reward.Id,
                1f,
                10f,
                default,
                Id("placeholder.presentation.reference"));
            var transitions = FullTransitions();
            var wrongTyped = new RuntimeContentDefinition[]
            {
                skill,
                reward,
                pickup,
                new RuntimeCharacterMechanicDefinition(
                    Id("test.mechanic.wrong_kind"), "n", "d", "Assets/Test/WrongMechanic.asset",
                    Array.Empty<ContentTag>(), Id("test.resource.wrong_kind"), 1f, 1f,
                    new[] { new CharacterMechanicTier(1f, pickup.Id) },
                    Id("placeholder.presentation.reference")),
                new RuntimeRewardDefinition(
                    Id("test.reward.wrong_status"), "n", "d", "Assets/Test/WrongReward.asset",
                    Array.Empty<ContentTag>(),
                    new[] { new RewardOperation(RewardOperationCode.ApplyStatus, referenceId: skill.Id) },
                    RewardRepeatPolicy.OncePerTransaction,
                    default,
                    "wrong.status",
                    Id("placeholder.presentation.reference")),
                new RuntimeRelicDefinition(
                    Id("test.relic.wrong_kinds"), "n", "d", "Assets/Test/WrongRelic.asset",
                    Array.Empty<ContentTag>(), 1,
                    new[] { pickup.Id }, new[] { reward.Id }, new[] { skill.Id },
                    Id("placeholder.presentation.reference")),
                new RuntimeMapObjectiveDefinition(
                    Id("test.objective.wrong_output"), "n", "d", "Assets/Test/WrongObjective.asset",
                    Array.Empty<ContentTag>(), new[] { Id("test.anchor.wrong_objective") }, transitions,
                    skill.Id, Id("placeholder.presentation.reference")),
                new RuntimeMapEventDefinition(
                    Id("test.event.wrong_output"), "n", "d", "Assets/Test/WrongEvent.asset",
                    Array.Empty<ContentTag>(), new[] { Id("test.anchor.wrong_event") }, transitions,
                    0f, 1f, skill.Id, Id("placeholder.presentation.reference")),
                new RuntimeLandmarkDefinition(
                    Id("test.landmark.wrong_outputs"), "n", "d", "Assets/Test/WrongLandmark.asset",
                    Array.Empty<ContentTag>(), Id("test.anchor.wrong_landmark"), skill.Id, skill.Id,
                    false, Id("placeholder.presentation.reference")),
                new RuntimeBossDefinition(
                    Id("test.boss.wrong_kinds"), "n", "d", "Assets/Test/WrongBoss.asset",
                    Array.Empty<ContentTag>(), skill.Id,
                    new[]
                    {
                        new RuntimeBossPhase(
                            0.5f,
                            new[] { reward.Id },
                            BossPhaseCleanupPolicy.ExpireOnPhaseExit)
                    },
                    skill.Id,
                    1f,
                    Id("placeholder.presentation.reference")),
                new RuntimeEliteAffixDefinition(
                    Id("test.affix.wrong_kinds"), "n", "d", "Assets/Test/WrongAffix.asset",
                    Array.Empty<ContentTag>(), Array.Empty<ContentTag>(), Array.Empty<ContentTag>(),
                    reward.Id, reward.Id, skill.Id, Id("placeholder.presentation.reference")),
                new RuntimeMetaNodeDefinition(
                    Id("test.meta_node.wrong_kinds"), "n", "d", "Assets/Test/WrongMetaNode.asset",
                    Array.Empty<ContentTag>(), MetaNodeKind.Branch, 1,
                    new[] { reward.Id }, new[] { reward.Id }, new[] { reward.Id },
                    Id("placeholder.presentation.reference")),
                new RuntimeMetaInsertDefinition(
                    Id("test.meta_insert.wrong_output"), "n", "d", "Assets/Test/WrongMetaInsert.asset",
                    Array.Empty<ContentTag>(), 1, new[] { Tag("test.meta.slot.wrong") },
                    new[] { reward.Id }, Id("placeholder.presentation.reference")),
                new RuntimeMetaFacilityDefinition(
                    Id("test.meta_facility.wrong_unlock"), "n", "d", "Assets/Test/WrongFacility.asset",
                    Array.Empty<ContentTag>(), reward.Id, Id("test.page.wrong_facility"),
                    Id("placeholder.presentation.reference")),
                new RuntimeStoryDefinition(
                    Id("test.story.wrong_unlock"), "n", "d", "Assets/Test/WrongStory.asset",
                    Array.Empty<ContentTag>(), new[] { "story.wrong.line" }, reward.Id,
                    "wrong.story", Id("placeholder.presentation.reference")),
                new RuntimeCollectibleDefinition(
                    Id("test.collectible.wrong_kinds"), "n", "d", "Assets/Test/WrongCollectible.asset",
                    Array.Empty<ContentTag>(), Id("test.topic.wrong_collectible"), reward.Id,
                    "collectible.wrong.body", skill.Id, Id("placeholder.presentation.reference"))
            };
            var typedReport = ContentValidator.ValidateCatalogs(
                new[] { Catalog(6, "test.pack.all_wrong_types", wrongTyped) },
                GameVersion);
            var typedErrors = JoinErrors(typedReport);
            Assert.That(typedReport.IsValid, Is.False);
            Assert.That(typedErrors, Does.Contain("Reward, Skill, Passive, or Trait output"));
            Assert.That(typedErrors, Does.Contain("a Status"));
            Assert.That(typedErrors, Does.Contain("a Relic mutex"));
            Assert.That(typedErrors, Does.Contain("a Reward or generic rule output"));
            Assert.That(typedErrors, Does.Contain("a MapObjective, Reward, or generic rule output"));
            Assert.That(typedErrors, Does.Contain("a Story"));
            Assert.That(typedErrors, Does.Contain("a schema-4 Enemy"));
            Assert.That(typedErrors, Does.Contain("a Passive, Trait, or Synergy modifier output"));
            Assert.That(typedErrors, Does.Contain("a MetaNode prerequisite"));
            Assert.That(typedErrors, Does.Contain("a Trait, Synergy rule, or UpgradeOffer output"));
            Assert.That(typedErrors, Does.Contain("a MetaNode or MapObjective unlock condition"));
            Assert.That(typedErrors, Does.Contain("a Landmark, MapObjective, Story, or MetaNode acquire rule"));

            var missingMechanic = new RuntimeCharacterMechanicDefinition(
                Id("test.mechanic.missing_output"),
                "content.test.mechanic.missing_output.name",
                "content.test.mechanic.missing_output.description",
                "Assets/Test/MissingMechanic.asset",
                Array.Empty<ContentTag>(),
                Id("test.resource.wind"),
                1f,
                1f,
                new[] { new CharacterMechanicTier(1f, Id("test.reward.missing")) },
                Id("placeholder.presentation.mechanic"));
            var missingReport = ContentValidator.ValidateCatalogs(
                new[] { Catalog(6, "test.pack.missing_reference", missingMechanic) },
                GameVersion);
            Assert.That(missingReport.IsValid, Is.False);
            Assert.That(missingReport.Errors[0].Code, Is.EqualTo(ErrorCode.MissingReference));
        }

        [Test]
        public void SchemaSixRejectsInvalidLifecycleMutexAndUniqueRules()
        {
            var invalidUnique = new RuntimeRewardDefinition(
                Id("test.reward.invalid_unique"), "n", "d", "Assets/Test/InvalidUnique.asset",
                Array.Empty<ContentTag>(),
                new[] { new RewardOperation(RewardOperationCode.Heal, 1f) },
                RewardRepeatPolicy.OncePerRun,
                default,
                string.Empty,
                Id("placeholder.presentation.validation"));
            var invalidOperation = new RuntimeRewardDefinition(
                Id("test.reward.invalid_operation"), "n", "d", "Assets/Test/InvalidOperation.asset",
                Array.Empty<ContentTag>(),
                new[] { new RewardOperation(RewardOperationCode.ApplyStatus) },
                RewardRepeatPolicy.OncePerTransaction,
                default,
                "invalid.operation",
                Id("placeholder.presentation.validation"));
            var invalidObjective = new RuntimeMapObjectiveDefinition(
                Id("test.objective.invalid_lifecycle"), "n", "d",
                "Assets/Test/InvalidLifecycle.asset", Array.Empty<ContentTag>(),
                new[] { Id("test.anchor.invalid_lifecycle") },
                new[]
                {
                    new ObjectiveStateTransition(
                        ObjectiveState.Hidden,
                        ObjectiveState.Completed)
                },
                invalidUnique.Id,
                Id("placeholder.presentation.validation"));
            var trait = new RuntimeTraitDefinition(
                Id("test.trait.validation_output"), "n", "d",
                "Assets/Test/ValidationTrait.asset", Array.Empty<ContentTag>(),
                new[]
                {
                    new RuntimeBuildModifier(
                        BuiltInStatIds.Damage,
                        ModifierOperation.AddPercent,
                        0.1f,
                        0,
                        default)
                });
            var metaId = Id("test.meta_node.self_mutex");
            var invalidMeta = new RuntimeMetaNodeDefinition(
                metaId, "n", "d", "Assets/Test/SelfMutex.asset", Array.Empty<ContentTag>(),
                MetaNodeKind.Branch,
                1,
                new[] { metaId },
                new[] { metaId },
                new[] { trait.Id },
                Id("placeholder.presentation.validation"));

            var report = ContentValidator.ValidateCatalogs(
                new[]
                {
                    Catalog(
                        6,
                        "test.pack.invalid_schema6_rules",
                        invalidUnique,
                        invalidOperation,
                        invalidObjective,
                        trait,
                        invalidMeta)
                },
                GameVersion);
            var errors = JoinErrors(report);
            Assert.That(report.IsValid, Is.False);
            Assert.That(errors, Does.Contain("Once-per-run rewards require a unique key"));
            Assert.That(errors, Does.Contain("Reward operation requires a stable content reference"));
            Assert.That(errors, Does.Contain("outside the approved objective lifecycle"));
            Assert.That(errors, Does.Contain("cannot reference self"));
        }

        [Test]
        public void ExistingSchemaHashesRemainStableAndSixIsTheOnlyNewSupportedVersion()
        {
            var goldenHashes = new[]
            {
                "bcbdd8a688d1565b2513bda48220d9ae4d2650a8cfac2e1c2127bb71b5e4ffad",
                "297c88d01d444670f3e7ceb8c93ec314b872f6a83a9761bb93a4eaa5a0fe1eb1",
                "383d93c75d8f54bb30a0badab000b195ddde5a29254fd6f50c80ac47f09f8669",
                "3d2a4f8c1c5bcd04fa4935f160bbc9287d9e07c917f47f3228e65bf4f315434e",
                "d4c36e1f38868346d225c8c9f095761b59510bdb4908798e71ee4f7a1098fcff"
            };
            for (var schema = 1; schema <= 5; schema++)
            {
                var skill = schema >= ContentPackTopology.ModularSkillSchemaVersion
                    ? ExecutableSkill("test.skill.schema_" + schema)
                    : new RuntimeSkillDefinition(
                        Id("test.skill.schema_" + schema),
                        "content.test.skill.schema.name",
                        "content.test.skill.schema.description",
                        "Assets/Test/Schema" + schema + ".asset",
                        Array.Empty<ContentTag>(),
                        1f);
                var first = Catalog(schema, "test.pack.schema_" + schema, skill);
                var restored = first.ToDto().ToCatalog();
                Assert.That(
                    first.ContentHash,
                    Is.EqualTo(goldenHashes[schema - 1]),
                    "schema " + schema + " pre-Schema-6 golden");
                Assert.That(restored.IsSuccess, Is.True, "schema " + schema);
                Assert.That(restored.Value.ContentHash, Is.EqualTo(first.ContentHash), "schema " + schema);
            }

#pragma warning disable CS0618
            Assert.That(ContentPackTopology.SupportedSchemaVersion, Is.EqualTo(5));
#pragma warning restore CS0618
            Assert.That(ContentPackTopology.LatestSupportedSchemaVersion, Is.EqualTo(6));
            Assert.That(ContentPackTopology.IsSchemaVersionSupported(6), Is.True);
            Assert.That(ContentPackTopology.IsSchemaVersionSupported(7), Is.False);
        }

        [Test]
        public void NewStatIndicesDefaultsDomainsAndConsumersAreStable()
        {
            var catalog = StatCatalog.Default;
            Assert.That(catalog.Count, Is.EqualTo(18));
            Assert.That(catalog.GetId(BuiltInStatIndices.Regeneration), Is.EqualTo(BuiltInStatIds.Regeneration));
            Assert.That(BuiltInStatIndices.ProjectileSpeed.Value, Is.EqualTo(14));
            Assert.That(BuiltInStatIndices.CriticalMultiplier.Value, Is.EqualTo(15));
            Assert.That(BuiltInStatIndices.ExperienceGain.Value, Is.EqualTo(16));
            Assert.That(BuiltInStatIndices.KnockbackResistance.Value, Is.EqualTo(17));
            var defaults = StatBaseValues.CreateDefault();
            Assert.That(defaults.ProjectileSpeed, Is.EqualTo(1f));
            Assert.That(defaults.CriticalMultiplier, Is.EqualTo(2f));
            Assert.That(defaults.ExperienceGain, Is.EqualTo(1f));
            Assert.That(defaults.KnockbackResistance, Is.Zero);
            Assert.That(catalog.ClampToDomain(BuiltInStatIndices.ProjectileSpeed, 0f), Is.GreaterThan(0f));
            Assert.That(catalog.ClampToDomain(BuiltInStatIndices.CriticalMultiplier, 0f), Is.EqualTo(1f));
            Assert.That(catalog.ClampToDomain(BuiltInStatIndices.ExperienceGain, 0f), Is.GreaterThan(0f));
            Assert.That(catalog.ClampToDomain(BuiltInStatIndices.KnockbackResistance, 2f), Is.EqualTo(1f));
        }

        [Test]
        public void AllFourNewStatsDriveTheirOwningRuntimeSystems()
        {
            var criticalWorld = new SimulationWorld(
                pipeline: new SimulationPipeline(new DamageResolutionSystem()));
            var criticalStats = StatBaseValues.CreateDefault(100f, 0f);
            criticalStats.CriticalChance = 1f;
            criticalStats.CriticalMultiplier = 3f;
            var criticalSource = ActorWithStats(criticalWorld, Vector2.Zero, criticalStats);
            var criticalTarget = Actor(criticalWorld, Vector2.Zero, 100f);
            criticalWorld.QueueDamage(new DamagePacket(
                criticalSource,
                criticalTarget,
                Id("test.damage.critical_multiplier"),
                DamageType.True,
                DamageTags.Direct,
                10f,
                true,
                1f,
                Vector2.Zero,
                Vector2.Zero,
                0));
            new FixedTickRunner(criticalWorld).Advance(TickSeconds);
            Assert.That(
                criticalWorld.CombatEvents.GetDamageResolvedAt(0).HealthDamage,
                Is.EqualTo(30f));

            var projectileSkill = new RuntimeSkillDefinition(
                Id("test.skill.projectile_speed_consumer"), "n", "d",
                "Assets/Test/ProjectileSpeedConsumer.asset", Array.Empty<ContentTag>(),
                0f, 0f,
                Module(SkillModuleIds.TriggerOnHit),
                Module(SkillModuleIds.ConditionAlways),
                Module(SkillModuleIds.TargetingNearest, 20f, int0: 1),
                Module(
                    SkillModuleIds.DeliveryProjectile,
                    2f,
                    0.1f,
                    1f,
                    0f,
                    1,
                    Id("placeholder.presentation.projectile")),
                new[] { new EffectOp(EffectOpCode.GainResource, 1f) },
                Array.Empty<SkillLevelPatch>());
            var projectileRegistry = Registry(projectileSkill);
            var projectileRuntime = SkillRuntime(projectileRegistry);
            var projectileWorld = new SimulationWorld(
                pipeline: new SimulationPipeline(new SkillTriggerSystem(), new CleanupSystem()),
                skillRuntime: projectileRuntime);
            var projectileStats = StatBaseValues.CreateDefault(100f, 0f);
            projectileStats.ProjectileSpeed = 3f;
            var projectileOwner = ActorWithStats(projectileWorld, Vector2.Zero, projectileStats);
            Actor(projectileWorld, new Vector2(10f, 0f));
            Assert.That(
                projectileRuntime.AddInstance(
                    projectileOwner,
                    IndexOf(projectileRegistry, projectileSkill.Id)).IsSuccess,
                Is.True);
            projectileRuntime.QueueTrigger(new SkillTriggerContext(
                SkillTriggerEventType.OnHit,
                projectileOwner,
                default,
                new Vector2(10f, 0f),
                Vector2.UnitX,
                projectileSkill.Id,
                default,
                0));
            new FixedTickRunner(projectileWorld).Advance(TickSeconds);
            Assert.That(projectileWorld.Projectiles.Count, Is.EqualTo(1));
            Assert.That(
                projectileWorld.Projectiles.GetStateAt(0).Velocity.Length(),
                Is.EqualTo(6f).Within(0.0001f));

            var knockbackSkill = new RuntimeSkillDefinition(
                Id("test.skill.knockback_resistance_consumer"), "n", "d",
                "Assets/Test/KnockbackResistanceConsumer.asset", Array.Empty<ContentTag>(),
                0f, 0f,
                Module(SkillModuleIds.TriggerOnHit),
                Module(SkillModuleIds.ConditionAlways),
                Module(SkillModuleIds.TargetingNearest, 20f, int0: 1),
                Module(SkillModuleIds.DeliveryInstant),
                new[] { new EffectOp(EffectOpCode.Knockback, 10f) },
                Array.Empty<SkillLevelPatch>());
            var knockbackRegistry = Registry(knockbackSkill);
            var knockbackRuntime = SkillRuntime(knockbackRegistry);
            var knockbackWorld = new SimulationWorld(
                pipeline: new SimulationPipeline(
                    new SkillTriggerSystem(),
                    new SkillEffectResolutionSystem()),
                skillRuntime: knockbackRuntime);
            var knockbackOwner = Actor(knockbackWorld, Vector2.Zero);
            var resistantStats = StatBaseValues.CreateDefault(100f, 0f);
            resistantStats.KnockbackResistance = 0.75f;
            var resistantTarget = ActorWithStats(
                knockbackWorld,
                new Vector2(1f, 0f),
                resistantStats);
            Assert.That(
                knockbackRuntime.AddInstance(
                    knockbackOwner,
                    IndexOf(knockbackRegistry, knockbackSkill.Id)).IsSuccess,
                Is.True);
            knockbackRuntime.QueueTrigger(new SkillTriggerContext(
                SkillTriggerEventType.OnHit,
                knockbackOwner,
                resistantTarget,
                new Vector2(1f, 0f),
                Vector2.UnitX,
                knockbackSkill.Id,
                default,
                0));
            new FixedTickRunner(knockbackWorld).Advance(TickSeconds);
            Assert.That(knockbackWorld.Actors.TryRead(resistantTarget.Handle, out var moved), Is.True);
            Assert.That(moved.Velocity.X, Is.EqualTo(2.5f).Within(0.0001f));

            var progressionFixture = M6TestFactory.Create(10f);
            var progressionWorld = M6TestFactory.World(
                progressionFixture,
                17UL,
                out var player);
            Assert.That(progressionWorld.Actors.TryGetCombat(player, out var playerRecord), Is.True);
            playerRecord.Stats.SetBase(BuiltInStatIndices.ExperienceGain, 2f);
            progressionWorld.Progression.RecordEnemyDefeat(10f, Vector2.Zero);
            var progressionRunner = new FixedTickRunner(progressionWorld);
            progressionRunner.Advance(TickSeconds);
            progressionRunner.Advance(TickSeconds);
            Assert.That(
                progressionWorld.Progression.Experience.TotalExperience,
                Is.EqualTo(20d));
        }

        [Test]
        public void AtomicStatusConsumeDoesNotPartiallyMutateAndSupportsTags()
        {
            var tag = Tag("test.status.family.wind");
            var definition = new RuntimeStatusDefinition(
                Id("test.status.wind_mark"),
                "content.test.status.wind_mark.name",
                "content.test.status.wind_mark.description",
                "Assets/Test/WindMark.asset",
                new[] { tag },
                StatusStackingPolicy.AddStacks,
                10f,
                5,
                0f,
                Array.Empty<ContentTag>(),
                Array.Empty<ContentTag>());
            var statusIndex = new RuntimeContentIndex(0);
            var statusCatalog = new RuntimeStatusCatalog();
            statusCatalog.Register(statusIndex, definition);
            var world = new SimulationWorld(
                pipeline: new SimulationPipeline(new StatusTickSystem()),
                statusCatalog: statusCatalog);
            var source = Actor(world, Vector2.Zero);
            var target = Actor(world, Vector2.Zero);
            var request = new StatusApplicationRequest(
                source,
                target,
                Id("test.skill.apply_mark"),
                statusIndex,
                1f,
                0);
            world.QueueStatus(request);
            world.QueueStatus(request);
            new FixedTickRunner(world).Advance(TickSeconds);

            var query = world.StatusTransactions.Query(world, target, default, tag);
            Assert.That(query.MatchedInstances, Is.EqualTo(1));
            Assert.That(query.TotalStacks, Is.EqualTo(2));
            var rejected = world.StatusTransactions.Consume(
                world, target, default, tag, 3, true);
            Assert.That(rejected.Committed, Is.False);
            Assert.That(world.StatusTransactions.Query(world, target, statusIndex, default).TotalStacks, Is.EqualTo(2));
            var committed = world.StatusTransactions.Consume(
                world, target, statusIndex, default, 2, true);
            Assert.That(committed.Committed, Is.True);
            Assert.That(committed.ConsumedStacks, Is.EqualTo(2));
            Assert.That(world.StatusTransactions.Query(world, target, statusIndex, default).TotalStacks, Is.Zero);
        }

        [Test]
        public void DetonateStatusConsumesActualStacksAndQueuesOneDamagePacket()
        {
            var status = new RuntimeStatusDefinition(
                Id("test.status.detonate_mark"), "n", "d",
                "Assets/Test/DetonateMark.asset", Array.Empty<ContentTag>(),
                StatusStackingPolicy.AddStacks,
                10f,
                5,
                0f,
                Array.Empty<ContentTag>(),
                Array.Empty<ContentTag>());
            var skill = new RuntimeSkillDefinition(
                Id("test.skill.detonate_mark"), "n", "d",
                "Assets/Test/DetonateMarkSkill.asset", Array.Empty<ContentTag>(),
                0f, 0f,
                Module(SkillModuleIds.TriggerOnHit),
                Module(SkillModuleIds.ConditionAlways),
                Module(SkillModuleIds.TargetingNearest, 10f, int0: 1),
                Module(SkillModuleIds.DeliveryInstant),
                new[]
                {
                    new EffectOp(
                        EffectOpCode.DetonateStatus,
                        value0: 5f,
                        int0: 3,
                        referenceId0: status.Id)
                },
                Array.Empty<SkillLevelPatch>());
            var registry = Registry(status, skill);
            var runtime = SkillRuntime(registry);
            var world = new SimulationWorld(
                pipeline: new SimulationPipeline(
                    new SkillTriggerSystem(),
                    new SkillEffectResolutionSystem(),
                    new DamageResolutionSystem(),
                    new StatusTickSystem()),
                statusCatalog: new RuntimeStatusCatalog(registry),
                skillRuntime: runtime);
            var owner = Actor(world, Vector2.Zero);
            var target = Actor(world, new Vector2(1f, 0f));
            var statusIndex = IndexOf(registry, status.Id);
            Assert.That(runtime.AddInstance(owner, IndexOf(registry, skill.Id)).IsSuccess, Is.True);
            var apply = new StatusApplicationRequest(
                owner,
                target,
                skill.Id,
                statusIndex,
                1f,
                0);
            world.QueueStatus(apply);
            world.QueueStatus(apply);
            var runner = new FixedTickRunner(world);
            runner.Advance(TickSeconds);
            runtime.QueueTrigger(new SkillTriggerContext(
                SkillTriggerEventType.OnHit,
                owner,
                target,
                position: new Vector2(1f, 0f),
                direction: Vector2.UnitX,
                sourceContentId: skill.Id,
                referenceIndex: statusIndex,
                procDepth: 0));
            runner.Advance(TickSeconds);

            Assert.That(world.CombatEvents.DamageResolvedCount, Is.EqualTo(1));
            Assert.That(world.CombatEvents.GetDamageResolvedAt(0).Requested, Is.EqualTo(10f));
            Assert.That(world.StatusTransactions.Query(world, target, statusIndex, default).TotalStacks, Is.Zero);
        }

        [Test]
        public void TriggerPositionPreservesContextAndUnknownTokensAreRejected()
        {
            var skill = new RuntimeSkillDefinition(
                Id("test.skill.trigger_position"),
                "content.test.skill.trigger_position.name",
                "content.test.skill.trigger_position.description",
                "Assets/Test/TriggerPosition.asset",
                Array.Empty<ContentTag>(),
                0f,
                0f,
                Module(SkillModuleIds.TriggerOnHit),
                Module(SkillModuleIds.ConditionAlways),
                Module(SkillModuleIds.TargetingTriggerPosition, value0: 0f, int0: 0),
                Module(SkillModuleIds.DeliveryInstant),
                new[] { new EffectOp(EffectOpCode.GainResource, 1f) },
                Array.Empty<SkillLevelPatch>());
            var registry = Registry(skill);
            var runtime = SkillRuntime(registry);
            var world = new SimulationWorld(
                pipeline: new SimulationPipeline(new SkillTriggerSystem()),
                skillRuntime: runtime);
            var owner = Actor(world, Vector2.Zero);
            Assert.That(runtime.AddInstance(owner, IndexOf(registry, skill.Id)).IsSuccess, Is.True);
            var position = new Vector2(7f, -2f);
            runtime.QueueTrigger(new SkillTriggerContext(
                SkillTriggerEventType.OnHit,
                owner,
                owner,
                position,
                Vector2.UnitX,
                skill.Id,
                default,
                0));

            new FixedTickRunner(world).Advance(TickSeconds);

            Assert.That(runtime.Commands.Count, Is.EqualTo(1));
            Assert.That(runtime.Commands.GetAt(0).Context.Position, Is.EqualTo(position));

            var invalid = new RuntimeSkillDefinition(
                Id("test.skill.unknown_condition"),
                "content.test.skill.unknown_condition.name",
                "content.test.skill.unknown_condition.description",
                "Assets/Test/UnknownCondition.asset",
                Array.Empty<ContentTag>(),
                1f,
                0f,
                Module(SkillModuleIds.TriggerTimer),
                Module(Id("test.condition.unknown")),
                Module(SkillModuleIds.TargetingSelf),
                Module(SkillModuleIds.DeliveryInstant),
                new[] { new EffectOp(EffectOpCode.GainResource, 1f) },
                Array.Empty<SkillLevelPatch>());
            var report = ContentValidator.ValidateCatalogs(
                new[] { Catalog(6, "test.pack.unknown_token", invalid) },
                GameVersion);
            Assert.That(report.IsValid, Is.False);
            Assert.That(JoinErrors(report), Does.Contain("condition module ID is not explicitly registered"));
        }

        [Test]
        public void OutboundReturnHasTwoDeduplicatedHitPhasesAndCleansUp()
        {
            var skill = new RuntimeSkillDefinition(
                Id("test.skill.outbound_return"),
                "content.test.skill.outbound_return.name",
                "content.test.skill.outbound_return.description",
                "Assets/Test/OutboundReturn.asset",
                Array.Empty<ContentTag>(),
                0f,
                0f,
                Module(SkillModuleIds.TriggerOnHit),
                Module(SkillModuleIds.ConditionAlways),
                Module(SkillModuleIds.TargetingTriggerPosition, int0: 0),
                Module(
                    SkillModuleIds.DeliveryOutboundReturn,
                    30f,
                    30f,
                    0.2f,
                    3f,
                    2,
                    Id("placeholder.presentation.outbound_return")),
                new[] { new EffectOp(EffectOpCode.GainResource, 1f) },
                Array.Empty<SkillLevelPatch>());
            var registry = Registry(skill);
            var runtime = SkillRuntime(registry);
            var pipeline = new SimulationPipeline(
                new SkillTriggerSystem(),
                new MovementSystem(),
                new SkillDeliverySystem(),
                new SkillEffectResolutionSystem(),
                new LifetimeSystem(),
                new CleanupSystem());
            var world = new SimulationWorld(pipeline: pipeline, skillRuntime: runtime);
            var owner = Actor(world, Vector2.Zero);
            Actor(world, new Vector2(1f, 0f));
            Assert.That(runtime.AddInstance(owner, IndexOf(registry, skill.Id)).IsSuccess, Is.True);
            runtime.SetResource(owner, 0f);
            runtime.QueueTrigger(new SkillTriggerContext(
                SkillTriggerEventType.OnHit,
                owner,
                default,
                new Vector2(3f, 0f),
                Vector2.UnitX,
                skill.Id,
                default,
                0));
            var runner = new FixedTickRunner(world);
            for (var tick = 0; tick < 10; tick++) runner.Advance(TickSeconds);

            Assert.That(runtime.GetResource(owner), Is.EqualTo(2f));
            Assert.That(runtime.HitCount, Is.EqualTo(2));
            Assert.That(runtime.ActiveDeliveryCount, Is.Zero);
        }

        [Test]
        public void DemoPipelineIsExactAndLegacyPipelinesRemainUnchanged()
        {
            AssertPipeline(
                SimulationPipeline.CreateQinglanDemo(),
                SimulationSystemId.InputCommand,
                SimulationSystemId.SpawnScheduler,
                SimulationSystemId.MapObjectiveAndEvent,
                SimulationSystemId.BossPhase,
                SimulationSystemId.EnemyDecision,
                SimulationSystemId.SkillTrigger,
                SimulationSystemId.Movement,
                SimulationSystemId.CharacterMechanicAccumulate,
                SimulationSystemId.SkillDelivery,
                SimulationSystemId.SkillEffectResolution,
                SimulationSystemId.DamageResolution,
                SimulationSystemId.RewardResolution,
                SimulationSystemId.CharacterMechanicReaction,
                SimulationSystemId.StatusTick,
                SimulationSystemId.Regeneration,
                SimulationSystemId.Death,
                SimulationSystemId.LootAndReward,
                SimulationSystemId.Pickup,
                SimulationSystemId.Experience,
                SimulationSystemId.LevelUpRequest,
                SimulationSystemId.Lifetime,
                SimulationSystemId.Cleanup,
                SimulationSystemId.EventFlush,
                SimulationSystemId.SnapshotBuild);
            AssertPipeline(SimulationPipeline.CreateM2Default(),
                SimulationSystemId.Movement, SimulationSystemId.Lifetime,
                SimulationSystemId.Cleanup, SimulationSystemId.SnapshotBuild);
            AssertPipeline(SimulationPipeline.CreateM3Default(),
                SimulationSystemId.Movement, SimulationSystemId.DamageResolution,
                SimulationSystemId.StatusTick, SimulationSystemId.Death,
                SimulationSystemId.Lifetime, SimulationSystemId.Cleanup,
                SimulationSystemId.EventFlush, SimulationSystemId.SnapshotBuild);
            Assert.That(CountSystem(SimulationPipeline.CreateQinglanDemo(), SimulationSystemId.Cleanup), Is.EqualTo(1));
        }

        [Test]
        public void DamageChannelsCooldownImmunityBarrierAndZeroOutcomesAreExplicit()
        {
            var world = new SimulationWorld(
                pipeline: new SimulationPipeline(new DamageResolutionSystem()));
            var source = Actor(world, Vector2.Zero, 100f);
            var target = Actor(world, Vector2.Zero, 100f);
            var channels = new[]
            {
                BuiltInDamageChannels.Direct,
                BuiltInDamageChannels.Contact,
                BuiltInDamageChannels.Periodic,
                BuiltInDamageChannels.Hazard,
                BuiltInDamageChannels.BossHazard
            };
            for (var index = 0; index < channels.Length; index++)
                world.QueueDamage(Packet(source, target, channels[index], 1f));
            new FixedTickRunner(world).Advance(TickSeconds);
            Assert.That(world.CombatEvents.DamageResolvedCount, Is.EqualTo(5));
            Assert.That(world.CombatEvents.DamageAppliedCount, Is.EqualTo(5));
            for (var index = 0; index < 5; index++)
                Assert.That(world.CombatEvents.GetDamageResolvedAt(index).ChannelId, Is.EqualTo(channels[index]));

            var policyWorld = new SimulationWorld(
                pipeline: new SimulationPipeline(new DamageResolutionSystem()));
            source = Actor(policyWorld, Vector2.Zero, 100f);
            target = Actor(policyWorld, Vector2.Zero, 100f);
            policyWorld.DamageChannels.SetImmune(target.Handle, BuiltInDamageChannels.Hazard, true);
            policyWorld.DamageChannels.SetBarrier(target.Handle, BuiltInDamageChannels.BossHazard, 10f);
            policyWorld.QueueDamage(Packet(source, target, BuiltInDamageChannels.Direct, 1f, 2));
            policyWorld.QueueDamage(Packet(source, target, BuiltInDamageChannels.Direct, 1f, 2));
            policyWorld.QueueDamage(Packet(source, target, BuiltInDamageChannels.Contact, 1f));
            policyWorld.QueueDamage(Packet(source, target, BuiltInDamageChannels.Hazard, 1f));
            policyWorld.QueueDamage(Packet(source, target, BuiltInDamageChannels.BossHazard, 10f));
            policyWorld.QueueDamage(Packet(source, target, BuiltInDamageChannels.Periodic, 0f));
            new FixedTickRunner(policyWorld).Advance(TickSeconds);

            Assert.That(policyWorld.CombatEvents.DamageResolvedCount, Is.EqualTo(6));
            Assert.That(policyWorld.CombatEvents.GetDamageResolvedAt(0).Outcome, Is.EqualTo(DamageResolutionOutcome.Applied));
            Assert.That(policyWorld.CombatEvents.GetDamageResolvedAt(1).Outcome, Is.EqualTo(DamageResolutionOutcome.ChannelCooldown));
            Assert.That(policyWorld.CombatEvents.GetDamageResolvedAt(2).Outcome, Is.EqualTo(DamageResolutionOutcome.Applied));
            Assert.That(policyWorld.CombatEvents.GetDamageResolvedAt(3).Outcome, Is.EqualTo(DamageResolutionOutcome.Immune));
            Assert.That(policyWorld.CombatEvents.GetDamageResolvedAt(4).BarrierAbsorbed, Is.EqualTo(10f));
            Assert.That(policyWorld.CombatEvents.GetDamageResolvedAt(4).ShieldDamage, Is.Zero);
            Assert.That(policyWorld.CombatEvents.GetDamageResolvedAt(4).HealthDamage, Is.Zero);
            Assert.That(policyWorld.CombatEvents.GetDamageResolvedAt(5).Outcome, Is.EqualTo(DamageResolutionOutcome.Zero));
            Assert.That(policyWorld.CombatEvents.DamageAppliedCount, Is.EqualTo(3));
            Assert.That(
                policyWorld.CombatEvents.GetDamageAppliedAt(2).Context.FinalDamage,
                Is.Zero,
                "Legacy DamageApplied consumers retain an explicit zero-value event.");

            var splitWorld = new SimulationWorld(
                pipeline: new SimulationPipeline(new DamageResolutionSystem()));
            source = Actor(splitWorld, Vector2.Zero, 100f);
            var splitStats = StatBaseValues.CreateDefault(100f, 0f);
            var splitHandle = splitWorld.CreateActor(
                SimulationEntityState.Create(Vector2.Zero, Vector2.Zero),
                new ActorCombatInitialization(splitStats, 100f, 5f, 5f, default));
            target = new SpatialEntity(EntityKind.Actor, splitHandle);
            splitWorld.QueueDamage(Packet(source, target, BuiltInDamageChannels.Direct, 8f));
            new FixedTickRunner(splitWorld).Advance(TickSeconds);
            var split = splitWorld.CombatEvents.GetDamageResolvedAt(0);
            Assert.That(split.ShieldDamage, Is.EqualTo(5f));
            Assert.That(split.HealthDamage, Is.EqualTo(3f));
            Assert.That(splitWorld.CombatEvents.ShieldChangedCount, Is.EqualTo(1));
            Assert.That(splitWorld.CombatEvents.DamageAppliedCount, Is.EqualTo(1));

            var evictionWorld = new SimulationWorld(initialEntityCapacity: 1);
            var evictionTarget = Actor(evictionWorld, Vector2.Zero).Handle;
            for (var index = 0; index < DamageChannelPolicyRuntime.MaximumChannelsPerActor + 1; index++)
            {
                var channel = DamageChannelId.Create(
                    "test.damage_channel.overflow_" + index).Value;
                Assert.That(evictionWorld.DamageChannels.SetImmune(evictionTarget, channel, true), Is.True);
            }
            Assert.That(evictionWorld.DamageChannels.StableEvictions, Is.EqualTo(1));
        }

        [Test]
        public void GenericRuntimeOwnersHandleTwoFixturesWithoutContentIdBranches()
        {
            var reward = Id("test.reward.runtime_owner");
            var mechanicA = Mechanic("test.mechanic.a", reward, 1f, 2f);
            var mechanicB = Mechanic("test.mechanic.b", reward, 2f, 4f);
            var mechanics = new CharacterMechanicRuntime(2);
            var firstOwner = new EntityHandle(0, 1);
            var secondOwner = new EntityHandle(1, 1);
            Assert.That(mechanics.TryAttach(firstOwner, new RuntimeContentIndex(0), mechanicA), Is.True);
            Assert.That(mechanics.TryAttach(secondOwner, new RuntimeContentIndex(1), mechanicB), Is.True);
            mechanics.Accumulate(new ResolvedMovement(
                new SpatialEntity(EntityKind.Actor, firstOwner), MovementSource.PlayerCommand, 3f));
            mechanics.Accumulate(new ResolvedMovement(
                new SpatialEntity(EntityKind.Actor, secondOwner), MovementSource.Teleport, 10f));
            mechanics.ReactToDamage(firstOwner, 1, 1f, 0f);
            mechanics.ReactToDamage(firstOwner, 1, 0f, 1f);
            Assert.That(mechanics.TryGet(firstOwner, out var first), Is.True);
            Assert.That(first.CurrentValue, Is.GreaterThanOrEqualTo(1f).And.LessThan(2f));
            Assert.That(first.Tier, Is.EqualTo(1));
            Assert.That(mechanics.TryGet(secondOwner, out var second), Is.True);
            Assert.That(second.CurrentValue, Is.Zero);

            var rewards = new RewardRuntime(4);
            var transactionA = new RewardTransactionId(1, reward, 0);
            var transactionB = new RewardTransactionId(1, reward, 1);
            Assert.That(rewards.TryCommit(transactionA), Is.True);
            Assert.That(rewards.TryCommit(transactionA), Is.False);
            Assert.That(rewards.TryCommit(transactionB), Is.True);

            var objectives = new MapObjectiveRuntime(2);
            var objectiveA = Id("test.objective.a");
            var objectiveB = Id("test.objective.b");
            Assert.That(objectives.TryAdd(objectiveA), Is.True);
            Assert.That(objectives.TryAdd(objectiveB, ObjectiveState.Available), Is.True);
            Assert.That(objectives.TryTransition(objectiveA, ObjectiveState.Hidden, ObjectiveState.Revealed), Is.True);
            Assert.That(objectives.TryTransition(objectiveB, ObjectiveState.Available, ObjectiveState.Completed), Is.False);

            var bosses = new BossPhaseRuntime();
            var bossA = Boss("test.boss.a", reward, 0.75f, 0.5f);
            var bossB = Boss("test.boss.b", reward, 0.6f, 0.2f);
            Assert.That(bosses.ResolvePhase(bossA, 0, 0.4f, false), Is.EqualTo(2));
            Assert.That(bosses.ResolvePhase(bossB, 0, 1f, true), Is.EqualTo(2));

            var required = Tag("test.enemy.tag.armored");
            var excluded = Tag("test.enemy.tag.flying");
            var affixes = new EliteAffixRuntime();
            var eligible = new RuntimeEliteAffixDefinition(
                Id("test.affix.eligible"), "n", "d", "Assets/Test/AffixEligible.asset",
                Array.Empty<ContentTag>(), new[] { required }, new[] { excluded },
                reward, default, default, Id("placeholder.presentation.affix"));
            var rejected = new RuntimeEliteAffixDefinition(
                Id("test.affix.rejected"), "n", "d", "Assets/Test/AffixRejected.asset",
                Array.Empty<ContentTag>(), new[] { excluded }, Array.Empty<ContentTag>(),
                reward, default, default, Id("placeholder.presentation.affix"));
            Assert.That(affixes.IsEligible(eligible, new[] { required }), Is.True);
            Assert.That(affixes.IsEligible(rejected, new[] { required }), Is.False);
        }

        [Test]
        public void CharacterMechanicFiftyFourThousandTicksAllocateZeroBytes()
        {
            var mechanic = Mechanic(
                "test.mechanic.performance",
                Id("test.reward.performance"),
                100f,
                200f);
            var runtime = new CharacterMechanicRuntime(1);
            var owner = new EntityHandle(0, 1);
            runtime.TryAttach(owner, new RuntimeContentIndex(0), mechanic);
            var movement = new ResolvedMovement(
                new SpatialEntity(EntityKind.Actor, owner),
                MovementSource.PlayerCommand,
                0.01f);
            runtime.Accumulate(movement);

            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var tick = 0; tick < 54_000; tick++) runtime.Accumulate(movement);
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.That(allocated, Is.Zero);
        }

        [Test]
        public void SaveKindsRoundTripIndependentlyAndProfileMigratesOneOrTwoToThree()
        {
            Assert.That(SaveSchema.GetCurrentVersion(SaveDocumentKind.Settings), Is.EqualTo(2));
            Assert.That(SaveSchema.GetCurrentVersion(SaveDocumentKind.Profile), Is.EqualTo(3));
            Assert.That(SaveSchema.GetCurrentVersion(SaveDocumentKind.RunRecovery), Is.EqualTo(2));
            var codec = new UnityJsonSaveCodec();
            var a = Id("test.meta.a");
            var b = Id("test.meta.b");
            var profile = new ProfileSaveData(
                "profile-g11",
                Array.Empty<SavePackVersion>(),
                Array.Empty<ContentId>(),
                Array.Empty<SavedContentLevel>(),
                Array.Empty<SavedCounter>(),
                Array.Empty<SavedCounter>(),
                "2026-08-04T00:00:00Z",
                activeMetaLoadoutIds: new[] { b, a, b },
                firstClearMapIds: new[] { a },
                claimedUniqueRewardIds: new[] { b },
                completedStoryIds: new[] { a },
                collectedCollectibleIds: new[] { b },
                committedTransactionIds: new[] { a });
            var settings = new SettingsSaveData(
                "zh-Hans", 0.15f, 1f, true, 1f, true, AutoAimStrategy.Nearest);
            var recovery = new RunRecoverySaveData(
                7, 3, Id("test.character.save"), Id("test.map.save"),
                Array.Empty<SavePackVersion>(), Array.Empty<SavedContentLevel>(), "now");

            var settingsRoundTrip = codec.DecodeSettings(codec.Encode(settings).Data);
            var profileRoundTrip = codec.DecodeProfile(codec.Encode(profile).Data);
            var recoveryRoundTrip = codec.DecodeRunRecovery(codec.Encode(recovery).Data);
            Assert.That(settingsRoundTrip.IsSuccess, Is.True);
            Assert.That(profileRoundTrip.IsSuccess, Is.True);
            Assert.That(recoveryRoundTrip.IsSuccess, Is.True);
            Assert.That(profileRoundTrip.Value.ActiveMetaLoadoutIds.Count, Is.EqualTo(2));
            Assert.That(profileRoundTrip.Value.ActiveMetaLoadoutIds[0], Is.EqualTo(a));
            Assert.That(profileRoundTrip.Value.ActiveMetaLoadoutIds[1], Is.EqualTo(b));

            const string v2 = "{\"schemaVersion\":2,\"gameVersion\":\"0.1.0\",\"profileId\":\"legacy-v2\",\"contentPacks\":[],\"unlockedContentIds\":[],\"metaUpgrades\":[],\"currencies\":[],\"statistics\":[],\"lastWriteUtc\":\"now\"}";
            const string v1 = "{\"schemaVersion\":1,\"profileId\":\"legacy-v1\",\"unlockedContentIds\":[],\"lastWriteUtc\":\"now\"}";
            var migratedV2 = codec.DecodeProfile(codec.EncodeRawPayload(SaveDocumentKind.Profile, 2, v2));
            var migratedV1 = codec.DecodeProfile(codec.EncodeRawPayload(SaveDocumentKind.Profile, 1, v1));
            Assert.That(migratedV2.IsSuccess, Is.True);
            Assert.That(migratedV1.IsSuccess, Is.True);
            Assert.That(migratedV2.Value.SchemaVersion, Is.EqualTo(3));
            Assert.That(migratedV1.Value.SchemaVersion, Is.EqualTo(3));
            Assert.That(migratedV2.Value.CommittedTransactionIds, Is.Empty);
            Assert.That(migratedV1.Value.FirstClearMapIds, Is.Empty);
        }

        private static RuntimeContentDefinition[] CreateSchemaSixDefinitions()
        {
            var skill = ExecutableSkill("test.skill.schema6_attack");
            var enemy = new RuntimeEnemyDefinition(
                Id("test.enemy.schema6_boss"),
                "content.test.enemy.schema6_boss.name",
                "content.test.enemy.schema6_boss.description",
                "Assets/Test/Schema6Enemy.asset",
                Array.Empty<ContentTag>(),
                100f,
                0.5f,
                2f,
                1f,
                1f,
                skill.Id,
                2f,
                1f,
                Id("placeholder.visual.schema6_enemy"),
                new RuntimeEnemyBehavior(
                    EnemyMovementMode.Chase, 1f, 0.1f, 0.1f, 0.2f,
                    2f, 0.4f, 1.25f, 0.5f, 1f));
            var rewardId = Id("test.reward.schema6");
            var storyId = Id("test.story.schema6");
            var objectiveId = Id("test.map_objective.schema6");
            var presentation = Id("placeholder.presentation.schema6");
            var transitions = FullTransitions();
            var trait = new RuntimeTraitDefinition(
                Id("test.trait.schema6_output"), "n", "d",
                "Assets/Test/Schema6Trait.asset", Array.Empty<ContentTag>(),
                new[]
                {
                    new RuntimeBuildModifier(
                        BuiltInStatIds.Damage,
                        ModifierOperation.AddPercent,
                        0.1f,
                        0,
                        default)
                });
            var reward = new RuntimeRewardDefinition(
                rewardId, "n", "d", "Assets/Test/Reward.asset", Array.Empty<ContentTag>(),
                new[] { new RewardOperation(RewardOperationCode.Heal, 1f) },
                RewardRepeatPolicy.OncePerTransaction, default, "reward.schema6", presentation);
            var story = new RuntimeStoryDefinition(
                storyId, "n", "d", "Assets/Test/Story.asset", Array.Empty<ContentTag>(),
                new[] { "story.schema6.line.1" }, objectiveId, "story.schema6", presentation);
            var definitions = new List<RuntimeContentDefinition>
            {
                skill,
                enemy,
                new RuntimeCharacterMechanicDefinition(
                    Id("test.character_mechanic.schema6"), "n", "d", "Assets/Test/Mechanic.asset",
                    Array.Empty<ContentTag>(), Id("test.resource.schema6"), 1f, 1f,
                    new[] { new CharacterMechanicTier(1f, rewardId) }, presentation),
                reward,
                new RuntimePickupDefinition(
                    Id("test.pickup.schema6"), "n", "d", "Assets/Test/Pickup.asset",
                    Array.Empty<ContentTag>(), rewardId, 1f, 10f, default, presentation),
                new RuntimeRelicDefinition(
                    Id("test.relic.schema6"), "n", "d", "Assets/Test/Relic.asset",
                    Array.Empty<ContentTag>(), 1, new[] { rewardId }, Array.Empty<ContentId>(),
                    Array.Empty<ContentId>(), presentation),
                new RuntimeMapObjectiveDefinition(
                    objectiveId, "n", "d", "Assets/Test/Objective.asset",
                    Array.Empty<ContentTag>(), new[] { Id("test.anchor.objective") }, transitions,
                    rewardId, presentation),
                new RuntimeMapEventDefinition(
                    Id("test.map_event.schema6"), "n", "d", "Assets/Test/Event.asset",
                    Array.Empty<ContentTag>(), new[] { Id("test.anchor.event") }, transitions,
                    1f, 2f, rewardId, presentation),
                new RuntimeLandmarkDefinition(
                    Id("test.landmark.schema6"), "n", "d", "Assets/Test/Landmark.asset",
                    Array.Empty<ContentTag>(), Id("test.anchor.landmark"), rewardId, storyId,
                    false, presentation),
                new RuntimeBossDefinition(
                    Id("test.boss.schema6"), "n", "d", "Assets/Test/Boss.asset",
                    Array.Empty<ContentTag>(), enemy.Id,
                    new[] { new RuntimeBossPhase(0.5f, Array.Empty<ContentId>(), BossPhaseCleanupPolicy.ExpireOnPhaseExit) },
                    rewardId, 1f, presentation),
                new RuntimeEliteAffixDefinition(
                    Id("test.elite_affix.schema6"), "n", "d", "Assets/Test/Affix.asset",
                    Array.Empty<ContentTag>(), Array.Empty<ContentTag>(), Array.Empty<ContentTag>(),
                    trait.Id, default, rewardId, presentation),
                new RuntimeMetaNodeDefinition(
                    Id("test.meta_node.schema6"), "n", "d", "Assets/Test/MetaNode.asset",
                    Array.Empty<ContentTag>(), MetaNodeKind.Branch, 1, Array.Empty<ContentId>(),
                    Array.Empty<ContentId>(), new[] { trait.Id }, presentation),
                new RuntimeMetaInsertDefinition(
                    Id("test.meta_insert.schema6"), "n", "d", "Assets/Test/MetaInsert.asset",
                    Array.Empty<ContentTag>(), 1, new[] { Tag("test.meta.slot.wind") },
                    new[] { trait.Id }, presentation),
                new RuntimeMetaFacilityDefinition(
                    Id("test.meta_facility.schema6"), "n", "d", "Assets/Test/Facility.asset",
                    Array.Empty<ContentTag>(), objectiveId, Id("test.page.schema6"), presentation),
                story,
                trait,
                new RuntimeCollectibleDefinition(
                    Id("test.collectible.schema6"), "n", "d", "Assets/Test/Collectible.asset",
                    Array.Empty<ContentTag>(), Id("test.topic.schema6"), objectiveId,
                    "collectible.schema6.body", rewardId, presentation)
            };
            return definitions.ToArray();
        }

        private static RuntimeSkillDefinition ExecutableSkill(string id)
        {
            return new RuntimeSkillDefinition(
                Id(id), "content." + id + ".name", "content." + id + ".description",
                "Assets/Test/" + id + ".asset", Array.Empty<ContentTag>(),
                1f, 0f,
                Module(SkillModuleIds.TriggerTimer),
                Module(SkillModuleIds.ConditionAlways),
                Module(SkillModuleIds.TargetingSelf),
                Module(SkillModuleIds.DeliveryInstant),
                new[] { new EffectOp(EffectOpCode.GainResource, 1f) },
                Array.Empty<SkillLevelPatch>());
        }

        private static RuntimeCharacterMechanicDefinition Mechanic(
            string id,
            ContentId output,
            float firstThreshold,
            float secondThreshold)
        {
            return new RuntimeCharacterMechanicDefinition(
                Id(id), "n", "d", "Assets/Test/" + id + ".asset",
                Array.Empty<ContentTag>(), Id("test.resource." + id.Replace('.', '_')),
                1f, 1f,
                new[]
                {
                    new CharacterMechanicTier(firstThreshold, output),
                    new CharacterMechanicTier(secondThreshold, output)
                },
                Id("placeholder.presentation.mechanic"));
        }

        private static RuntimeBossDefinition Boss(
            string id,
            ContentId reward,
            float first,
            float second)
        {
            return new RuntimeBossDefinition(
                Id(id), "n", "d", "Assets/Test/" + id + ".asset",
                Array.Empty<ContentTag>(), Id("test.enemy." + id.Replace('.', '_')),
                new[]
                {
                    new RuntimeBossPhase(first, Array.Empty<ContentId>(), BossPhaseCleanupPolicy.ExpireOnPhaseExit),
                    new RuntimeBossPhase(second, Array.Empty<ContentId>(), BossPhaseCleanupPolicy.Persist)
                },
                reward, 1f, Id("placeholder.presentation.boss"));
        }

        private static ObjectiveStateTransition[] FullTransitions()
        {
            return new[]
            {
                new ObjectiveStateTransition(ObjectiveState.Hidden, ObjectiveState.Revealed),
                new ObjectiveStateTransition(ObjectiveState.Revealed, ObjectiveState.Available),
                new ObjectiveStateTransition(ObjectiveState.Available, ObjectiveState.Activating),
                new ObjectiveStateTransition(ObjectiveState.Activating, ObjectiveState.Defending),
                new ObjectiveStateTransition(ObjectiveState.Defending, ObjectiveState.Completed)
            };
        }

        private static SkillModuleDefinition Module(
            ContentId id,
            float value0 = 0f,
            float value1 = 0f,
            float value2 = 0f,
            float value3 = 0f,
            int int0 = 0,
            ContentId presentation = default)
        {
            return new SkillModuleDefinition(
                id, value0, value1, value2, value3, int0, 0, presentation);
        }

        private static BakedContentCatalog Catalog(
            int schema,
            string pack,
            params RuntimeContentDefinition[] definitions)
        {
            return BakedContentCatalog.Create(
                new ContentPackManifest(
                    Id(pack), GameVersion, schema, GameVersion, null,
                    Array.Empty<ContentPackDependency>(),
                    "packs/" + pack, "pack." + pack, false,
                    "Assets/Test/" + pack + ".asset"),
                definitions);
        }

        private static ContentRegistry Registry(params RuntimeContentDefinition[] definitions)
        {
            var registry = new ContentRegistry();
            var load = registry.Load(
                new[] { Catalog(6, "test.pack.skill_g11", definitions) },
                GameVersion);
            Assert.That(load.IsSuccess, Is.True, load.Error.ToString());
            return registry;
        }

        private static SkillRuntime SkillRuntime(ContentRegistry registry)
        {
            var catalog = SkillRuntimeCatalog.Build(registry, SkillModuleRegistry.CreateDefault());
            Assert.That(catalog.IsSuccess, Is.True, catalog.Error.ToString());
            return new SkillRuntime(catalog.Value, 3UL, 8);
        }

        private static RuntimeContentIndex IndexOf(ContentRegistry registry, ContentId id)
        {
            Assert.That(registry.TryGet(id, out var entry), Is.True);
            return entry.Index;
        }

        private static SpatialEntity Actor(
            SimulationWorld world,
            Vector2 position,
            float health = 100f)
        {
            var handle = world.CreateActor(
                SimulationEntityState.Create(position, Vector2.Zero),
                ActorCombatInitialization.CreateDefault(health, 5f));
            return new SpatialEntity(EntityKind.Actor, handle);
        }

        private static SpatialEntity ActorWithStats(
            SimulationWorld world,
            Vector2 position,
            StatBaseValues stats)
        {
            var handle = world.CreateActor(
                SimulationEntityState.Create(position, Vector2.Zero),
                new ActorCombatInitialization(
                    stats,
                    stats.Health,
                    0f,
                    0f,
                    default));
            return new SpatialEntity(EntityKind.Actor, handle);
        }

        private static DamagePacket Packet(
            SpatialEntity source,
            SpatialEntity target,
            DamageChannelId channel,
            float value,
            int cooldownTicks = 0)
        {
            return new DamagePacket(
                source, target, Id("test.damage.source"), DamageType.True,
                DamageTags.Direct, value, false, 1f, Vector2.Zero, Vector2.Zero,
                0, channel, cooldownTicks);
        }

        private static void AssertPipeline(
            SimulationPipeline pipeline,
            params SimulationSystemId[] expected)
        {
            Assert.That(pipeline.Count, Is.EqualTo(expected.Length));
            for (var index = 0; index < expected.Length; index++)
                Assert.That(pipeline.GetSystemId(index), Is.EqualTo(expected[index]), "system " + index);
        }

        private static int CountSystem(SimulationPipeline pipeline, SimulationSystemId id)
        {
            var count = 0;
            for (var index = 0; index < pipeline.Count; index++)
                if (pipeline.GetSystemId(index) == id) count++;
            return count;
        }

        private static string JoinErrors(ContentValidationReport report)
        {
            var value = string.Empty;
            for (var index = 0; index < report.Errors.Count; index++)
                value += report.Errors[index].Message + "\n";
            return value;
        }

        private static ContentId Id(string value) => ContentId.Create(value).Value;
        private static ContentTag Tag(string value) => ContentTag.Create(value).Value;
    }
}
