using System;
using System.Collections.Generic;
using Game.Content.Authoring;
using Game.Content.Runtime;
using Game.Core;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>Creates the checked-in G1.5 enemy and elite-affix slice.</summary>
    public static class QinglanG15ContentSetup
    {
        private const string Folder = QinglanG12ContentSetup.Folder;

        [MenuItem("Tools/Free World/Qinglan/G1.5 Configure Enemies and Affixes")]
        public static void Configure()
        {
            var pack = Require<ContentPackAuthoring>(QinglanG12ContentSetup.PackPath);

            var puppetBrace = Status(
                "EnemyPuppetBrace",
                "qinglan.status.enemy.puppet_brace",
                5f,
                new RuntimeStatusModifier(
                    Stat("base.stat.knockback_resistance"),
                    ModifierOperation.AddFlat,
                    0.8f,
                    300,
                    Id("qinglan.stack.enemy.puppet_brace")),
                0f);
            var windBellWard = Status(
                "EnemyWindBellWard",
                "qinglan.status.enemy.wind_bell_ward",
                2f,
                default,
                8f);
            var barrierWard = Status(
                "EliteBarrierWard",
                "qinglan.status.elite.barrier_ward",
                2.5f,
                default,
                18f);

            var grassAttack = Skill(
                "EnemyGrassSpiritAura",
                "qinglan.skill.enemy.grass_spirit_aura",
                new[] { "skill.enemy", "enemy.contact", "delivery.aura" },
                0.65f,
                Module("base.targeting.self"),
                Module(
                    "base.delivery.aura",
                    value0: 1.2f,
                    value1: 0.6f,
                    value2: 0.5f,
                    presentation: "placeholder.presentation.qinglan.enemy.grass_spirit_aura"),
                Damage(2f, DamageType.Physical, DamageTags.Direct));
            var craneAttack = Skill(
                "EnemyPaperCraneDive",
                "qinglan.skill.enemy.paper_crane_dive",
                new[] { "skill.enemy", "enemy.charge", "skill.contact" },
                0.45f,
                Module("base.targeting.nearest", value0: 1.1f, int0: 1),
                Module("base.delivery.instant"),
                Damage(4f, DamageType.Physical, DamageTags.Direct),
                ValueEffect("base.effect.knockback", 1.5f));
            var puppetSlash = Skill(
                "EnemyWoodenPuppetHeavySlash",
                "qinglan.skill.enemy.wooden_puppet_heavy_slash",
                new[] { "skill.enemy", "enemy.heavy", "delivery.area" },
                1f,
                Module("base.targeting.circle", value0: 1.75f, int0: 8),
                Module("base.delivery.instant"),
                Damage(8f, DamageType.Physical, DamageTags.Direct),
                ValueEffect("base.effect.knockback", 2.5f));
            var puppetAttack = Skill(
                "EnemyWoodenPuppetAttack",
                "qinglan.skill.enemy.wooden_puppet_attack",
                new[] { "skill.enemy", "enemy.heavy", "skill.composite" },
                1.2f,
                Module("base.targeting.self"),
                Module("base.delivery.instant"),
                ApplyStatus(puppetBrace.ContentIdText),
                Spawn(puppetSlash.ContentIdText));
            var lanternAttack = Skill(
                "EnemyStoneLanternBolt",
                "qinglan.skill.enemy.stone_lantern_bolt",
                new[] { "skill.enemy", "enemy.ranged", "delivery.projectile" },
                2f,
                Module("base.targeting.nearest", value0: 14f, int0: 1),
                Module(
                    "base.delivery.projectile",
                    value0: 7f,
                    value1: 0.35f,
                    value2: 2.5f,
                    int0: 1,
                    presentation: "placeholder.presentation.qinglan.enemy.stone_lantern_bolt"),
                Damage(5f, DamageType.Fire, DamageTags.Direct));
            var windBellAttack = Skill(
                "EnemyWindBellSupport",
                "qinglan.skill.enemy.wind_bell_support",
                new[] { "skill.enemy", "enemy.support", "skill.shield" },
                2.1f,
                Module("base.targeting.allies_circle", value0: 5f, int0: 6),
                Module("base.delivery.instant"),
                ApplyStatus(windBellWard.ContentIdText));
            var seedAttack = Skill(
                "EnemyExplosiveSeedBurst",
                "qinglan.skill.enemy.explosive_seed_burst",
                new[] { "skill.enemy", "enemy.explosive", "delivery.area" },
                2.8f,
                Module("base.targeting.self"),
                Module(
                    "base.delivery.area",
                    value0: 2.6f,
                    value1: 0.25f,
                    value2: 0.25f,
                    presentation: "placeholder.presentation.qinglan.enemy.explosive_seed_burst"),
                Damage(8f, DamageType.Fire, DamageTags.Direct),
                ValueEffect("base.effect.knockback", 2f));

            var grass = Enemy(
                "GrassSpirit", "qinglan.enemy.grass_spirit",
                new[] { "enemy.normal", "enemy.swarm", "enemy.ground" },
                grassAttack, 18f, 0.4f, 2.8f, 2f, 1.2f, 1f, 0.05f,
                EnemyMovementMode.Chase, 1f, 0.12f, 0f, 0f, 1f, 0.6f, 1f, 0.55f);
            var crane = Enemy(
                "PaperCraneSpirit", "qinglan.enemy.paper_crane_spirit",
                new[] { "enemy.normal", "enemy.fast", "enemy.flying" },
                craneAttack, 22f, 0.35f, 3.3f, 4f, 6f, 1.5f, 0.08f,
                EnemyMovementMode.Charge, 1f, 0.1f, 0.45f, 0.55f, 2.8f, 1.5f, 0.8f, 0.35f);
            var puppet = Enemy(
                "WoodenSwordPuppet", "qinglan.enemy.wooden_sword_puppet",
                new[] { "enemy.normal", "enemy.armored", "enemy.ground" },
                puppetAttack, 65f, 0.65f, 1.7f, 7f, 1.55f, 3f, 0.12f,
                EnemyMovementMode.Chase, 1.2f, 0.15f, 0f, 0f, 1f, 1.2f, 1.4f, 0.8f);
            var lantern = Enemy(
                "StoneLanternGuard", "qinglan.enemy.stone_lantern_guard",
                new[] { "enemy.normal", "enemy.ranged", "enemy.ground" },
                lanternAttack, 35f, 0.55f, 1.6f, 5f, 14f, 2.5f, 0.1f,
                EnemyMovementMode.Ranged, 9f, 0.2f, 0f, 0f, 1f, 2f, 1.2f, 0.45f);
            var bell = Enemy(
                "WindBellSpirit", "qinglan.enemy.wind_bell_spirit",
                new[] { "enemy.normal", "enemy.support", "enemy.flying" },
                windBellAttack, 28f, 0.45f, 2f, 0f, 6f, 2f, 0.1f,
                EnemyMovementMode.KeepDistance, 6f, 0.2f, 0f, 0f, 1f, 2.1f, 1.2f, 0.5f);
            var seed = Enemy(
                "ExplosiveSeedPod", "qinglan.enemy.explosive_seed_pod",
                new[] { "enemy.normal", "enemy.environment", "enemy.explosive", "enemy.ground" },
                seedAttack, 26f, 0.55f, 1.3f, 8f, 1.5f, 2f, 0.06f,
                EnemyMovementMode.Chase, 1f, 0.2f, 0f, 0f, 1f, 2.8f, 1.3f, 0.45f);

            var rampagingTrait = Trait(
                "EliteRampagingTrait",
                "qinglan.trait.elite.rampaging",
                Modifier("base.stat.move_speed", ModifierOperation.AddPercent, 0.35f, 320, "qinglan.stack.elite.rampaging.speed"),
                Modifier("base.stat.attack_speed", ModifierOperation.AddPercent, 0.2f, 321, "qinglan.stack.elite.rampaging.attack"));
            var barrierSkill = Skill(
                "EliteBarrierPulse",
                "qinglan.skill.elite.barrier_pulse",
                new[] { "skill.enemy", "skill.elite", "skill.shield" },
                2.6f,
                Module("base.targeting.self"),
                Module("base.delivery.instant"),
                ApplyStatus(barrierWard.ContentIdText));
            var quakingSkill = Skill(
                "EliteQuakingPulse",
                "qinglan.skill.elite.quaking_pulse",
                new[] { "skill.enemy", "skill.elite", "delivery.area" },
                3.5f,
                Module("base.targeting.self"),
                Module(
                    "base.delivery.area",
                    value0: 3f,
                    value1: 0.3f,
                    value2: 0.3f,
                    presentation: "placeholder.presentation.qinglan.elite.quaking_pulse"),
                Damage(4f, DamageType.Physical, DamageTags.Secondary),
                ValueEffect("base.effect.knockback", 1.75f));

            var afflictedCore = Reward(
                "EliteAfflictedCoreReward",
                "qinglan.reward.elite.afflicted_core",
                new QinglanRewardOperationDto
                {
                    code = (int)RewardOperationCode.AddCurrency,
                    integerValue = 1,
                    eligibilityTag = "reward.afflicted_core"
                });
            var splittingReward = Reward(
                "EliteSplittingReward",
                "qinglan.reward.elite.splitting",
                new QinglanRewardOperationDto
                {
                    code = (int)RewardOperationCode.AddCurrency,
                    integerValue = 1,
                    eligibilityTag = "reward.afflicted_core"
                },
                new QinglanRewardOperationDto
                {
                    code = (int)RewardOperationCode.SpawnEnemy,
                    value = 0.35f,
                    integerValue = 2
                });

            var rampaging = Affix(
                "EliteAffixRampaging", "qinglan.affix.rampaging",
                new[] { "affix.elite", "affix.rampaging", "affix.speed" },
                new[] { "enemy.normal" }, new[] { "enemy.fast", "enemy.boss" },
                rampagingTrait.ContentIdText, string.Empty, afflictedCore.ContentIdText, 0, 1.1f);
            var barrier = Affix(
                "EliteAffixBarrier", "qinglan.affix.barrier",
                new[] { "affix.elite", "affix.barrier", "affix.defense" },
                new[] { "enemy.normal" }, new[] { "enemy.support", "enemy.boss" },
                string.Empty, barrierSkill.ContentIdText, afflictedCore.ContentIdText, 0, 1.15f);
            var splitting = Affix(
                "EliteAffixSplitting", "qinglan.affix.splitting",
                new[] { "affix.elite", "affix.splitting", "affix.spawn" },
                new[] { "enemy.normal" }, new[] { "enemy.boss", "enemy.environment", "enemy.explosive" },
                string.Empty, string.Empty, splittingReward.ContentIdText, 1, 1.1f);
            var quaking = Affix(
                "EliteAffixQuaking", "qinglan.affix.quaking",
                new[] { "affix.elite", "affix.quaking", "affix.area" },
                new[] { "enemy.normal" }, new[] { "enemy.boss", "enemy.environment", "enemy.explosive" },
                string.Empty, quakingSkill.ContentIdText, afflictedCore.ContentIdText, 0, 1.15f);

            var additions = new ContentAuthoringBase[]
            {
                puppetBrace, windBellWard, barrierWard,
                grassAttack, craneAttack, puppetSlash, puppetAttack, lanternAttack, windBellAttack, seedAttack,
                grass, crane, puppet, lantern, bell, seed,
                rampagingTrait, barrierSkill, quakingSkill, afflictedCore, splittingReward,
                rampaging, barrier, splitting, quaking
            };
            var definitions = new List<ContentAuthoringBase>(pack.Definitions.Count + additions.Length);
            for (var index = 0; index < pack.Definitions.Count; index++) definitions.Add(pack.Definitions[index]);
            for (var index = 0; index < additions.Length; index++)
                if (!definitions.Contains(additions[index])) definitions.Add(additions[index]);
            pack.Configure(
                "qinglan.pack.demo",
                "0.4.0",
                ContentPackTopology.QinglanDemoSchemaVersion,
                "0.1.0",
                string.Empty,
                Array.Empty<ContentPackDependencyAuthoring>(),
                "packs/qinglan.demo/catalog",
                "pack.qinglan.demo",
                false,
                definitions.ToArray());

            EnsureLocalization();
            for (var index = 0; index < additions.Length; index++) EditorUtility.SetDirty(additions[index]);
            EditorUtility.SetDirty(pack);
            AssetDatabase.SaveAssets();
            var bake = ContentBakeUtility.Bake(pack);
            if (!bake.IsSuccess) throw new UnityException(bake.Error.ToString());
            ContentBakeUtility.WriteCatalog(QinglanG12ContentSetup.PackPath, bake.Value);
            AssetDatabase.SaveAssets();
            Debug.Log("[Qinglan G1.5] Enemy pack baked: entries=" +
                      bake.Value.Definitions.Count + ", hash=" + bake.Value.ContentHash + ".");
        }

        public static void RunFromCommandLine()
        {
            try
            {
                Configure();
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static EnemyAuthoring Enemy(
            string file, string id, string[] tags, SkillAuthoring attack,
            float health, float radius, float speed, float damage, float range, float xp, float loot,
            EnemyMovementMode movement, float preferredDistance, float decisionInterval,
            float windup, float chargeDuration, float chargeMultiplier, float attackCooldown,
            float separationRadius, float separationWeight)
        {
            var enemy = LoadOrCreate<EnemyAuthoring>(Folder + "/" + file + ".asset");
            Identity(enemy, id, tags);
            enemy.ConfigureM5(
                health, radius, speed, damage, range, attack, xp, loot,
                "placeholder.presentation." + id,
                movement, preferredDistance, decisionInterval, windup, chargeDuration,
                chargeMultiplier, attackCooldown, separationRadius, separationWeight, 1f);
            return enemy;
        }

        private static SkillAuthoring Skill(
            string file, string id, string[] tags, float cooldown,
            SkillModuleAuthoringData targeting, SkillModuleAuthoringData delivery,
            params SkillEffectAuthoringData[] effects)
        {
            var skill = LoadOrCreate<SkillAuthoring>(Folder + "/" + file + ".asset");
            Identity(skill, id, tags);
            skill.ConfigureRuntime(
                cooldown,
                0f,
                Module("base.trigger.timer"),
                Module("base.condition.always"),
                targeting,
                delivery,
                effects,
                Array.Empty<SkillLevelPatchAuthoringData>());
            return skill;
        }

        private static StatusEffectAuthoring Status(
            string file, string id, float duration,
            RuntimeStatusModifier modifier, float shield)
        {
            var status = LoadOrCreate<StatusEffectAuthoring>(Folder + "/" + file + ".asset");
            Identity(status, id, "status.enemy", "status.beneficial");
            status.Configure(
                StatusStackingPolicy.RefreshDuration,
                duration,
                1,
                0f,
                new[] { "status.dispel.beneficial" },
                Array.Empty<string>());
            status.ConfigureBehavior(modifier, default, shield);
            return status;
        }

        private static TraitAuthoring Trait(
            string file, string id, params BuildModifierAuthoringData[] modifiers)
        {
            var trait = LoadOrCreate<TraitAuthoring>(Folder + "/" + file + ".asset");
            Identity(trait, id, "trait.elite");
            trait.Configure(modifiers);
            return trait;
        }

        private static QinglanDefinitionAuthoring Reward(
            string file, string id, params QinglanRewardOperationDto[] operations)
        {
            var reward = LoadOrCreate<QinglanDefinitionAuthoring>(Folder + "/" + file + ".asset");
            Identity(reward, id, "reward.elite", "reward.afflicted_core");
            reward.ConfigureRuntime(
                RuntimeContentKinds.Reward,
                new QinglanRuntimeDefinitionDto
                {
                    enum0 = (int)RewardRepeatPolicy.Repeatable,
                    rewardOperations = operations,
                    presentationProfileId = "placeholder.presentation.qinglan.reward.afflicted_core"
                });
            return reward;
        }

        private static QinglanDefinitionAuthoring Affix(
            string file, string id, string[] tags,
            string[] required, string[] excluded,
            string modifierId, string skillId, string deathRewardId,
            int maximumGeneration, float rewardMultiplier)
        {
            var affix = LoadOrCreate<QinglanDefinitionAuthoring>(Folder + "/" + file + ".asset");
            Identity(affix, id, tags);
            affix.ConfigureRuntime(
                RuntimeContentKinds.EliteAffix,
                new QinglanRuntimeDefinitionDto
                {
                    value0 = rewardMultiplier,
                    int0 = maximumGeneration,
                    tags0 = required,
                    tags1 = excluded,
                    referenceId0 = modifierId,
                    referenceId1 = skillId,
                    referenceId2 = deathRewardId,
                    presentationProfileId = "placeholder.presentation." + id
                });
            return affix;
        }

        private static SkillModuleAuthoringData Module(
            string id, float value0 = 0f, float value1 = 0f, float value2 = 0f,
            int int0 = 0, string presentation = "") =>
            new SkillModuleAuthoringData
            {
                moduleId = id,
                value0 = value0,
                value1 = value1,
                value2 = value2,
                int0 = int0,
                presentationId = presentation
            };

        private static SkillEffectAuthoringData Damage(float value, DamageType type, DamageTags tags) =>
            new SkillEffectAuthoringData
            {
                moduleId = "base.effect.damage",
                value0 = value,
                int0 = (int)type,
                int1 = unchecked((int)(uint)tags)
            };

        private static SkillEffectAuthoringData ValueEffect(string id, float value) =>
            new SkillEffectAuthoringData { moduleId = id, value0 = value };

        private static SkillEffectAuthoringData ApplyStatus(string id) =>
            new SkillEffectAuthoringData
            {
                moduleId = "base.effect.apply_status",
                value0 = 1f,
                referenceId0 = id
            };

        private static SkillEffectAuthoringData Spawn(string id) =>
            new SkillEffectAuthoringData
            {
                moduleId = "base.effect.spawn_secondary_skill",
                referenceId0 = id
            };

        private static BuildModifierAuthoringData Modifier(
            string stat, ModifierOperation operation, float value, int priority, string group) =>
            new BuildModifierAuthoringData
            {
                statId = stat,
                operation = operation,
                value = value,
                priority = priority,
                stackingGroup = group
            };

        private static ContentId Id(string value)
        {
            var result = ContentId.Create(value);
            if (!result.IsSuccess) throw new UnityException(result.Error.ToString());
            return result.Value;
        }

        private static StatId Stat(string value)
        {
            var result = StatId.Create(value);
            if (!result.IsSuccess) throw new UnityException(result.Error.ToString());
            return result.Value;
        }

        private static void Identity(ContentAuthoringBase content, string id, params string[] tags)
        {
            var merged = new string[(tags == null ? 0 : tags.Length) + 1];
            merged[0] = "content.placeholder";
            if (tags != null && tags.Length > 0) Array.Copy(tags, 0, merged, 1, tags.Length);
            content.ConfigureIdentity(
                id,
                "content." + id + ".name",
                "content." + id + ".description",
                merged);
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            if (AssetDatabase.LoadMainAssetAtPath(path) != null) AssetDatabase.DeleteAsset(path);
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static T Require<T>(string path) where T : UnityEngine.Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) throw new UnityException("Required Qinglan content is missing: " + path);
            return asset;
        }

        private static void EnsureLocalization()
        {
            Localize("qinglan.enemy.grass_spirit", "Grass Spirit", "A swarming spirit with a contact aura.", "草灵", "以接触灵气成群压迫走位的低阶草木精怪。");
            Localize("qinglan.enemy.paper_crane_spirit", "Paper Crane Spirit", "Telegraphs and locks a fast diving charge.", "纸鹤符灵", "短暂前摇后锁定方向俯冲的追袭符灵。");
            Localize("qinglan.enemy.wooden_sword_puppet", "Wooden Sword Puppet", "A heavy puppet resistant to knockback.", "木制剑傀", "持重剑近域挥砍并以架势抵抗击退的剑傀。");
            Localize("qinglan.enemy.stone_lantern_guard", "Stone Lantern Guard", "Keeps range and fires bounded stone-flame bolts.", "石灯守卫", "保持距离并发射有寿命上限的石火弹。");
            Localize("qinglan.enemy.wind_bell_spirit", "Wind Bell Spirit", "Maintains distance and wards nearby allies.", "鸣风铃灵", "游离在战团边缘，为附近同伴施加不叠加的风铃护持。");
            Localize("qinglan.enemy.explosive_seed_pod", "Explosive Seed Pod", "Creates a delayed, short-lived burst hazard.", "爆裂种囊", "逼近后周期形成短寿命爆裂危险区。");
            Localize("qinglan.affix.rampaging", "Rampaging", "Increases movement and attack cadence.", "狂奔", "提高移动速度与攻击节奏；高速纸鹤不可获得。");
            Localize("qinglan.affix.barrier", "Barrier", "Periodically establishes a bounded ward.", "结界", "周期建立有明确容量与刷新间隙的护罩。");
            Localize("qinglan.affix.splitting", "Splitting", "Spawns two weaker, non-inheriting children on death.", "分裂", "死亡时生成两名一代弱化子体，子体不继承词缀。");
            Localize("qinglan.affix.quaking", "Quaking", "Emits a bounded periodic ground shock.", "震地", "周期释放带预警轮廓的有限范围地震冲击。");
            Localize("qinglan.reward.elite.afflicted_core", "Afflicted Core", "Queues one elite reward token for the reward slice.", "异相灵核", "排队一个精英奖励令牌，完整三选一在奖励切片结算。");
            Localize("qinglan.reward.elite.splitting", "Splitting Core", "Queues the elite token and bounded child spawn.", "分裂灵核", "排队异相灵核并执行有代数上限的分裂生成。");
            Localize("qinglan.status.enemy.puppet_brace", "Puppet Brace", "Temporary knockback resistance stance.", "剑傀架势", "短时提高击退抗性且不改变碰撞边界。");
            Localize("qinglan.status.enemy.wind_bell_ward", "Wind Bell Ward", "A non-stacking temporary ally shield.", "风铃护持", "不可无限叠加的短时友军护盾。");
            Localize("qinglan.status.elite.barrier_ward", "Elite Barrier", "A bounded elite shield with a refresh gap.", "精英结界", "带刷新间隙与明确容量的精英护罩。");
            Localize("qinglan.skill.enemy.grass_spirit_aura", "Grass Contact Aura", "Contact pressure around the grass spirit.", "草灵触域", "草灵周身持续造成接触压迫。");
            Localize("qinglan.skill.enemy.paper_crane_dive", "Paper Crane Dive", "Contact hit for the locked charge.", "纸鹤俯冲", "锁定冲刺接触时造成伤害与轻微击退。");
            Localize("qinglan.skill.enemy.wooden_puppet_heavy_slash", "Puppet Heavy Slash", "A short heavy area slash.", "剑傀重斩", "短范围重斩并击退目标。");
            Localize("qinglan.skill.enemy.wooden_puppet_attack", "Puppet Stance Attack", "Combines brace stance with a heavy slash.", "剑傀架势斩", "以通用状态与次级技能组合架势和重斩。");
            Localize("qinglan.skill.enemy.stone_lantern_bolt", "Stone-Flame Bolt", "A finite-lifetime swept projectile.", "石火弹", "具有最大寿命与扫掠碰撞的远程石火弹。");
            Localize("qinglan.skill.enemy.wind_bell_support", "Wind Bell Support", "Wards up to six nearby allies.", "鸣风护阵", "为距离最近的至多六名友军施加有限护持。");
            Localize("qinglan.skill.enemy.explosive_seed_burst", "Seed Burst", "A delayed short-lived area burst.", "种囊爆裂", "周期生成短寿命爆裂区。");
            Localize("qinglan.skill.elite.barrier_pulse", "Barrier Pulse", "Refreshes one bounded elite ward.", "结界脉冲", "按间隔重建单个有界护罩。");
            Localize("qinglan.skill.elite.quaking_pulse", "Quaking Pulse", "Creates a bounded ground shock area.", "震地脉冲", "形成短寿命地面冲击区。");
            Localize("qinglan.trait.elite.rampaging", "Rampaging Output", "Reusable speed and cadence modifiers.", "狂奔修正", "可复用的移动与攻击节奏修正。");
        }

        private static void Localize(
            string id, string englishName, string englishDescription,
            string chineseName, string chineseDescription)
        {
            M9LocalizationUtility.EnsureContentEntries(
                "content." + id + ".name",
                "content." + id + ".description",
                englishName,
                englishDescription,
                chineseName,
                chineseDescription);
        }
    }
}
