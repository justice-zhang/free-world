using System;
using System.Collections.Generic;
using Game.Content.Authoring;
using Game.Content.Runtime;
using Game.Core;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>Creates the G2.2 two-Boss content and binds both one-shot encounter rules.</summary>
    public static class QinglanG22ContentSetup
    {
        private const string Folder = QinglanG12ContentSetup.Folder;

        [MenuItem("Tools/Free World/Qinglan/G2.2 Configure Bosses")]
        public static void Configure()
        {
            var pack = Require<ContentPackAuthoring>(QinglanG12ContentSetup.PackPath);
            var encounter = Require<EncounterScheduleAuthoring>(QinglanG16ContentSetup.EncounterPath);
            var guide = Require<QinglanDefinitionAuthoring>(Folder + "/ObjectiveGuideWindPulse.asset");
            var listen = Require<QinglanDefinitionAuthoring>(Folder + "/ObjectiveListenToWind.asset");
            var stopBalance = Require<QinglanDefinitionAuthoring>(Folder + "/ObjectiveStopWindBalance.asset");

            var horizontalTrial = ProjectileSkill(
                "BossZhezhiHorizontalTrial", "qinglan.skill.boss.zhezhi.horizontal_trial",
                1.6f, 11f, 0.65f, 10f, 1.1f);
            var fallingWoodShadow = AreaSkill(
                "BossZhezhiFallingWoodShadow", "qinglan.skill.boss.zhezhi.falling_wood_shadow",
                2.4f, 3.2f, 0.55f, 14f);
            var trainingDummy = AreaSkill(
                "BossZhezhiTrainingDummy", "qinglan.skill.boss.zhezhi.training_dummy",
                1.8f, 2.4f, 0.35f, 16f);

            var swordQi = ProjectileSkill(
                "BossTingfengSwordQi", "qinglan.skill.boss.tingfeng.sword_qi",
                1.4f, 12f, 0.55f, 12f, 1.25f);
            var charge = InstantSkill(
                "BossTingfengCharge", "qinglan.skill.boss.tingfeng.charge",
                2.8f, 4f, 18f, 3.5f);
            var obscuringWindfield = AreaSkill(
                "BossTingfengObscuringWindfield", "qinglan.skill.boss.tingfeng.obscuring_windfield",
                3.6f, 4.8f, 0.8f, 8f);
            var falseSwordChime = ProjectileSkill(
                "BossTingfengFalseSwordChime", "qinglan.skill.boss.tingfeng.false_sword_chime",
                2.2f, 9f, 0.45f, 13f, 1f);
            var remnantSword = ProjectileSkill(
                "BossTingfengRemnantSword", "qinglan.skill.boss.tingfeng.remnant_sword",
                1.7f, 8f, 0.4f, 10f, 0.9f);
            var crossingWindScar = AreaSkill(
                "BossTingfengCrossingWindScar", "qinglan.skill.boss.tingfeng.crossing_wind_scar",
                2.1f, 5.5f, 0.45f, 17f);
            var undyingOath = AreaSkill(
                "BossTingfengUndyingOath", "qinglan.skill.boss.tingfeng.undying_oath",
                3.8f, 6.5f, 0.65f, 22f);

            var zhezhiEnemy = Enemy(
                "BossZhezhiEnemy", "qinglan.enemy.boss.zhezhi", horizontalTrial,
                1200f, 1.05f, 2.15f, 11f, 12f, 25f,
                EnemyMovementMode.Chase, 2.2f, 0.10f, 0.45f, 0.55f, 2.5f, 1.4f);
            var tingfengEnemy = Enemy(
                "BossTingfengEnemy", "qinglan.enemy.boss.tingfeng", swordQi,
                2600f, 1.2f, 2.35f, 14f, 14f, 50f,
                EnemyMovementMode.KeepDistance, 5.5f, 0.08f, 0.5f, 0.65f, 2.8f, 1.2f);

            var zhezhi = Boss(
                "BossZhezhiDefinition", "qinglan.boss.zhezhi", zhezhiEnemy, 0.35f,
                Phase(0.65f, BossPhaseCleanupPolicy.ExpireOnPhaseExit, horizontalTrial.ContentIdText),
                Phase(0.30f, BossPhaseCleanupPolicy.FinishCurrentTelegraph, fallingWoodShadow.ContentIdText),
                Phase(0f, BossPhaseCleanupPolicy.Persist, trainingDummy.ContentIdText));
            var tingfeng = Boss(
                "BossTingfengDefinition", "qinglan.boss.tingfeng", tingfengEnemy, 0.25f,
                Phase(0.70f, BossPhaseCleanupPolicy.ExpireOnPhaseExit,
                    swordQi.ContentIdText, charge.ContentIdText),
                Phase(0.35f, BossPhaseCleanupPolicy.FinishCurrentTelegraph,
                    guide.ContentIdText, listen.ContentIdText, stopBalance.ContentIdText,
                    obscuringWindfield.ContentIdText, falseSwordChime.ContentIdText, remnantSword.ContentIdText),
                Phase(0f, BossPhaseCleanupPolicy.Persist,
                    guide.ContentIdText, listen.ContentIdText, stopBalance.ContentIdText,
                    crossingWindScar.ContentIdText, remnantSword.ContentIdText, undyingOath.ContentIdText));

            if (!encounter.TryConfigureBossRules(
                    4,
                    new[]
                    {
                        BossRule(
                            zhezhiEnemy,
                            zhezhi,
                            360f,
                            "qinglan.anchor.old_court.zone.central")
                    }) ||
                !encounter.TryConfigureBossRules(
                    8,
                    new[]
                    {
                        BossRule(
                            tingfengEnemy,
                            tingfeng,
                            719.9f,
                            "qinglan.anchor.old_court.zone.north")
                    }))
            {
                throw new UnityException("G2.2 requires the checked-in nine-phase G1.6 encounter.");
            }

            var additions = new ContentAuthoringBase[]
            {
                horizontalTrial, fallingWoodShadow, trainingDummy,
                swordQi, charge, obscuringWindfield, falseSwordChime, remnantSword,
                crossingWindScar, undyingOath,
                zhezhiEnemy, tingfengEnemy, zhezhi, tingfeng
            };
            var definitions = new List<ContentAuthoringBase>(pack.Definitions.Count + additions.Length);
            for (var index = 0; index < pack.Definitions.Count; index++) definitions.Add(pack.Definitions[index]);
            for (var index = 0; index < additions.Length; index++)
                if (!definitions.Contains(additions[index])) definitions.Add(additions[index]);
            pack.Configure(
                "qinglan.pack.demo",
                "0.7.0",
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
            EditorUtility.SetDirty(encounter);
            EditorUtility.SetDirty(pack);
            AssetDatabase.SaveAssets();
            var bake = ContentBakeUtility.Bake(pack);
            if (!bake.IsSuccess) throw new UnityException(bake.Error.ToString());
            ContentBakeUtility.WriteCatalog(QinglanG12ContentSetup.PackPath, bake.Value);
            QinglanG17PackSetup.Configure();
            AssetDatabase.SaveAssets();
            Debug.Log("[Qinglan G2.2] Boss pack baked: entries=" +
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

        private static QinglanBossPhaseDto Phase(
            float threshold,
            BossPhaseCleanupPolicy cleanup,
            params string[] rules) =>
            new QinglanBossPhaseDto
            {
                healthThreshold = threshold,
                acceptedRuleIds = rules,
                cleanupPolicy = (int)cleanup
            };

        private static QinglanDefinitionAuthoring Boss(
            string file,
            string id,
            EnemyAuthoring enemy,
            float controlDurationMultiplier,
            params QinglanBossPhaseDto[] phases)
        {
            var boss = LoadOrCreate<QinglanDefinitionAuthoring>(Folder + "/" + file + ".asset");
            Identity(boss, id, "boss.definition", "boss.three_phase");
            boss.ConfigureRuntime(
                RuntimeContentKinds.Boss,
                new QinglanRuntimeDefinitionDto
                {
                    referenceId0 = enemy.ContentIdText,
                    referenceId1 = string.Empty,
                    value0 = controlDurationMultiplier,
                    bossPhases = phases,
                    presentationProfileId = "placeholder.presentation." + id
                });
            return boss;
        }

        private static EncounterBossRuleAuthoringData BossRule(
            EnemyAuthoring enemy,
            QinglanDefinitionAuthoring definition,
            float time,
            string anchor) =>
            new EncounterBossRuleAuthoringData
            {
                enemy = enemy,
                bossDefinition = definition,
                spawnTimeSeconds = time,
                pattern = SpawnPattern.FixedAnchor,
                anchorId = anchor
            };

        private static EnemyAuthoring Enemy(
            string file,
            string id,
            SkillAuthoring attack,
            float health,
            float radius,
            float speed,
            float damage,
            float range,
            float experience,
            EnemyMovementMode movement,
            float preferredDistance,
            float decisionInterval,
            float windup,
            float chargeDuration,
            float chargeMultiplier,
            float attackCooldown)
        {
            var enemy = LoadOrCreate<EnemyAuthoring>(Folder + "/" + file + ".asset");
            Identity(enemy, id, "enemy.boss", "enemy.ground", "enemy.no_affix");
            enemy.ConfigureM5(
                health, radius, speed, damage, range, attack, experience, 0f,
                "placeholder.presentation." + id,
                movement, preferredDistance, decisionInterval, windup, chargeDuration,
                chargeMultiplier, attackCooldown, radius * 2f, 0.75f, 1f);
            return enemy;
        }

        private static SkillAuthoring ProjectileSkill(
            string file,
            string id,
            float cooldown,
            float speed,
            float radius,
            float damage,
            float lifetime) =>
            Skill(
                file,
                id,
                cooldown,
                Module("base.targeting.nearest", value0: 18f, int0: 1),
                Module(
                    "base.delivery.projectile",
                    value0: speed,
                    value1: radius,
                    value2: lifetime,
                    int0: 1,
                    presentation: "placeholder.presentation." + id),
                Damage(damage),
                Knockback(1.5f));

        private static SkillAuthoring AreaSkill(
            string file,
            string id,
            float cooldown,
            float radius,
            float telegraphSeconds,
            float damage) =>
            Skill(
                file,
                id,
                cooldown,
                Module("base.targeting.self"),
                Module(
                    "base.delivery.area",
                    value0: radius,
                    value1: telegraphSeconds,
                    value2: 0.25f,
                    presentation: "placeholder.presentation." + id),
                Damage(damage));

        private static SkillAuthoring InstantSkill(
            string file,
            string id,
            float cooldown,
            float radius,
            float damage,
            float knockback) =>
            Skill(
                file,
                id,
                cooldown,
                Module("base.targeting.circle", value0: radius, int0: 8),
                Module("base.delivery.instant"),
                Damage(damage),
                Knockback(knockback));

        private static SkillAuthoring Skill(
            string file,
            string id,
            float cooldown,
            SkillModuleAuthoringData targeting,
            SkillModuleAuthoringData delivery,
            params SkillEffectAuthoringData[] effects)
        {
            var skill = LoadOrCreate<SkillAuthoring>(Folder + "/" + file + ".asset");
            Identity(skill, id, "skill.enemy", "skill.boss", "damage.channel.boss_hazard");
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

        private static SkillModuleAuthoringData Module(
            string id,
            float value0 = 0f,
            float value1 = 0f,
            float value2 = 0f,
            int int0 = 0,
            string presentation = "") =>
            new SkillModuleAuthoringData
            {
                moduleId = id,
                value0 = value0,
                value1 = value1,
                value2 = value2,
                int0 = int0,
                presentationId = presentation
            };

        private static SkillEffectAuthoringData Damage(float value) =>
            new SkillEffectAuthoringData
            {
                moduleId = "base.effect.damage",
                value0 = value,
                int0 = (int)DamageType.Physical,
                int1 = unchecked((int)(uint)DamageTags.Direct)
            };

        private static SkillEffectAuthoringData Knockback(float value) =>
            new SkillEffectAuthoringData { moduleId = "base.effect.knockback", value0 = value };

        private static void EnsureLocalization()
        {
            Localize("qinglan.enemy.boss.zhezhi", "Sword Trial Puppet: Zhezhi", "The Old Court's mid-run three-phase sword trial.", "试剑傀·折枝", "旧庭中段三阶段试剑傀儡。");
            Localize("qinglan.enemy.boss.tingfeng", "Court Guardian: Tingfeng", "The final guardian whose rules respond to restored wind altars.", "守庭剑傀·听风", "会响应已修复风脉台规则的最终守庭剑傀。");
            Localize("qinglan.boss.zhezhi", "Zhezhi Phase Rules", "Three ordered phases with deterministic cleanup.", "折枝阶段规则", "三段有序转换与确定性清场规则。");
            Localize("qinglan.boss.tingfeng", "Tingfeng Phase Rules", "Three ordered phases and eight deterministic altar combinations.", "听风阶段规则", "三段有序转换及八种确定性风脉组合。");
            Localize("qinglan.skill.boss.zhezhi.horizontal_trial", "Horizontal Trial", "A clearly telegraphed horizontal sword wave.", "横斩试式", "带清晰前摇的横向剑波。");
            Localize("qinglan.skill.boss.zhezhi.falling_wood_shadow", "Falling Wood Shadow", "An area strike used in Zhezhi's second phase.", "落木剑影", "折枝第二阶段的范围斩击。");
            Localize("qinglan.skill.boss.zhezhi.training_dummy", "Training Dummy Formation", "Accelerated close-area sword formation.", "试剑木人", "加速运转的近身剑阵。");
            Localize("qinglan.skill.boss.tingfeng.sword_qi", "Listening Sword Qi", "A direct opening sword wave.", "听风剑气", "开场使用的直线剑气。");
            Localize("qinglan.skill.boss.tingfeng.charge", "Wind Charge", "A committed charge with a readable windup.", "乘风突进", "具有明确前摇的突进。");
            Localize("qinglan.skill.boss.tingfeng.obscuring_windfield", "Obscuring Windfield", "A persistent spatial pressure field.", "遮蔽风场", "持续施压的空间风场。");
            Localize("qinglan.skill.boss.tingfeng.false_sword_chime", "False Sword Chime", "A deceptive sword-chime projectile.", "假剑鸣", "用于迷惑判断的剑鸣投射。");
            Localize("qinglan.skill.boss.tingfeng.remnant_sword", "Remnant Sword", "A repositioning remnant-sword projectile.", "残剑", "用于封路换位的残剑投射。");
            Localize("qinglan.skill.boss.tingfeng.crossing_wind_scar", "Crossing Wind Scar", "Crossing scars constrain the final arena.", "交错风痕", "在终局场地形成交错限制。");
            Localize("qinglan.skill.boss.tingfeng.undying_oath", "Undying Oath", "Tingfeng's strongest final-phase area oath.", "不灭剑誓", "听风最终阶段的最强范围剑誓。");
        }

        private static void Localize(
            string id,
            string englishName,
            string englishDescription,
            string chineseName,
            string chineseDescription) =>
            M9LocalizationUtility.EnsureContentEntries(
                "content." + id + ".name",
                "content." + id + ".description",
                englishName,
                englishDescription,
                chineseName,
                chineseDescription);

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
    }
}
