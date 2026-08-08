using System;
using System.Collections.Generic;
using Game.Content.Authoring;
using Game.Content.Runtime;
using Game.Core;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>Creates G2.3 instant pickups, battle relics, and fixed reward sources.</summary>
    public static class QinglanG23ContentSetup
    {
        private const string Folder = QinglanG12ContentSetup.Folder;
        private static readonly string[] EvolutionIds =
        {
            "qinglan.evolution.qinglan_flowing_shadow_sword",
            "qinglan.evolution.taiyi_spirit_sealing_array",
            "qinglan.evolution.chilu_hundred_craft_wheel",
            "qinglan.evolution.mirror_sea_tide_wheel",
            "qinglan.evolution.mountain_boundary_seal",
            "qinglan.evolution.earth_vein_spring_branch"
        };

        [MenuItem("Tools/Free World/Qinglan/G2.3 Configure Rewards")]
        public static void Configure()
        {
            var pack = Require<ContentPackAuthoring>(QinglanG12ContentSetup.PackPath);

            var ridingWindFeatherStatus = Status(
                "RewardRidingWindFeatherStatus",
                "qinglan.status.pickup.riding_wind_feather",
                4f,
                StatusModifier(
                    "base.stat.move_speed",
                    ModifierOperation.Multiply,
                    1.45f,
                    "qinglan.stack.pickup.riding_wind_feather"));

            var greenwoodReward = Reward(
                "RewardGreenwoodDew",
                "qinglan.reward.pickup.greenwood_dew",
                RewardRepeatPolicy.Repeatable,
                string.Empty,
                string.Empty,
                Operation(RewardOperationCode.Heal, 30f, 1500));
            var boundaryReward = Reward(
                "RewardBoundaryTalisman",
                "qinglan.reward.pickup.boundary_talisman",
                RewardRepeatPolicy.Repeatable,
                string.Empty,
                string.Empty,
                Operation(
                    RewardOperationCode.ApplyStatus,
                    1f,
                    60,
                    "qinglan.status.rooted",
                    "reward.target.hostile_area"));
            var thunderReward = Reward(
                "RewardThunderJade",
                "qinglan.reward.pickup.thunder_jade",
                RewardRepeatPolicy.Repeatable,
                string.Empty,
                string.Empty,
                Operation(RewardOperationCode.DamageArea, 90f, 65));
            var gourdReward = Reward(
                "RewardSpiritGourd",
                "qinglan.reward.pickup.spirit_gourd",
                RewardRepeatPolicy.Repeatable,
                string.Empty,
                string.Empty,
                Operation(RewardOperationCode.CollectEligiblePickups));
            var heartReward = Reward(
                "RewardHeartGuardJade",
                "qinglan.reward.pickup.heart_guard_jade",
                RewardRepeatPolicy.Repeatable,
                string.Empty,
                string.Empty,
                Operation(
                    RewardOperationCode.ApplyStatus,
                    1f,
                    0,
                    "qinglan.status.damage_immunity"));
            var featherReward = Reward(
                "RewardRidingWindFeather",
                "qinglan.reward.pickup.riding_wind_feather",
                RewardRepeatPolicy.Repeatable,
                string.Empty,
                string.Empty,
                Operation(
                    RewardOperationCode.ApplyStatus,
                    1f,
                    0,
                    ridingWindFeatherStatus.ContentIdText));

            var greenwood = Pickup("PickupGreenwoodDew", "qinglan.pickup.greenwood_dew", greenwoodReward, 0.65f, "reward.pickup.instant");
            var boundary = Pickup("PickupBoundaryTalisman", "qinglan.pickup.boundary_talisman", boundaryReward, 0.65f, "reward.pickup.instant");
            var thunder = Pickup("PickupThunderJade", "qinglan.pickup.thunder_jade", thunderReward, 0.7f, "reward.pickup.instant");
            var gourd = Pickup("PickupSpiritGourd", "qinglan.pickup.spirit_gourd", gourdReward, 0.75f, "reward.pickup.instant");
            var heart = Pickup("PickupHeartGuardJade", "qinglan.pickup.heart_guard_jade", heartReward, 0.65f, "reward.pickup.instant");
            var feather = Pickup("PickupRidingWindFeather", "qinglan.pickup.riding_wind_feather", featherReward, 0.65f, "reward.pickup.instant");

            var brokenSwordEcho = Skill(
                "RelicBrokenSwordEchoSkill",
                "qinglan.skill.relic.broken_sword_echo",
                2.8f,
                Module("base.targeting.nearest", value0: 14f, int0: 1),
                Module(
                    "base.delivery.projectile",
                    value0: 11f,
                    value1: 0.35f,
                    value2: 1.2f,
                    int0: 1,
                    presentation: "placeholder.presentation.qinglan.skill.relic.broken_sword_echo"),
                Damage(7f, DamageTags.Secondary));
            var listeningWindEcho = Skill(
                "RelicListeningWindEchoSkill",
                "qinglan.skill.relic.listening_wind_echo",
                2.2f,
                Module("base.targeting.circle", value0: 12f, int0: 3),
                Module("base.delivery.instant"),
                Damage(5f, DamageTags.Secondary));
            var oldCourtBellPulse = Skill(
                "RelicOldCourtBellPulseSkill",
                "qinglan.skill.relic.old_court_bell_pulse",
                6f,
                Module("base.targeting.circle", value0: 7f, int0: 12),
                Module("base.delivery.instant"),
                ApplyStatus("qinglan.status.slowed"));

            var windVeinOutput = Trait(
                "RelicWindVeinCopperTrait",
                "qinglan.trait.relic.wind_vein_copper",
                new[] { "trait.relic", "relic.rule.movement_resource" },
                Modifier("base.stat.move_speed", ModifierOperation.AddPercent, 0.10f, 410, "qinglan.stack.relic.wind_vein_copper.speed"),
                Modifier("base.stat.pickup_range", ModifierOperation.AddPercent, 0.20f, 411, "qinglan.stack.relic.wind_vein_copper.pickup"));
            var herbGardenOutput = Trait(
                "RelicHerbGardenSeedPodTrait",
                "qinglan.trait.relic.herb_garden_seed_pod",
                new[] { "trait.relic", "relic.rule.growth" },
                Modifier("base.stat.health", ModifierOperation.AddPercent, 0.10f, 412, "qinglan.stack.relic.herb_garden_seed_pod.health"),
                Modifier("base.stat.regeneration", ModifierOperation.AddFlat, 0.75f, 413, "qinglan.stack.relic.herb_garden_seed_pod.regeneration"));
            var blankTokenCost = Trait(
                "RelicBlankSwordTrialTokenTrait",
                "qinglan.trait.relic.blank_sword_trial_token",
                new[] { "trait.relic", "relic.rule.cost" },
                Modifier("base.stat.armor", ModifierOperation.Multiply, 0.85f, 414, "qinglan.stack.relic.blank_sword_trial_token.armor"));

            var brokenSwordTassel = Relic(
                "RelicBrokenSwordTassel",
                "qinglan.relic.broken_sword_tassel",
                new[] { "relic.battle", "relic.rule.secondary_repeat" },
                brokenSwordEcho.ContentIdText);
            var windVeinCopper = Relic(
                "RelicWindVeinCopper",
                "qinglan.relic.wind_vein_copper",
                new[] { "relic.battle", "relic.rule.movement_resource" },
                windVeinOutput.ContentIdText);
            var herbGardenSeedPod = Relic(
                "RelicHerbGardenSeedPod",
                "qinglan.relic.herb_garden_seed_pod",
                new[] { "relic.battle", "relic.rule.overheal_barrier" },
                herbGardenOutput.ContentIdText);
            var listeningWindCore = Relic(
                "RelicListeningWindCore",
                "qinglan.relic.listening_wind_core",
                new[] { "relic.battle", "relic.rule.stable_multitarget" },
                listeningWindEcho.ContentIdText);
            var oldCourtBell = Relic(
                "RelicOldCourtBell",
                "qinglan.relic.old_court_bell",
                new[] { "relic.battle", "relic.rule.periodic_control" },
                oldCourtBellPulse.ContentIdText);
            var blankSwordTrialToken = Relic(
                "RelicBlankSwordTrialToken",
                "qinglan.relic.blank_sword_trial_token",
                new[] { "relic.battle", "relic.rule.boss_damage", "relic.rule.incoming_risk" },
                blankTokenCost.ContentIdText);

            var fallback = Reward(
                "RewardFallbackSpiritSand",
                "qinglan.reward.fallback.spirit_sand",
                RewardRepeatPolicy.Repeatable,
                string.Empty,
                string.Empty,
                Operation(RewardOperationCode.AddCurrency, 0f, 5, string.Empty, "qinglan.currency.spirit_sand"));
            var relicIds = new[]
            {
                brokenSwordTassel.ContentIdText,
                windVeinCopper.ContentIdText,
                herbGardenSeedPod.ContentIdText,
                listeningWindCore.ContentIdText,
                oldCourtBell.ContentIdText,
                blankSwordTrialToken.ContentIdText
            };
            var relicOperations = ChoiceOperations(RewardOperationCode.GrantRelicChoice, relicIds);
            var manifestation = Reward(
                "RewardManifestationChest",
                "qinglan.reward.manifestation_chest",
                RewardRepeatPolicy.OncePerTransaction,
                fallback.ContentIdText,
                string.Empty,
                ChoiceOperations(RewardOperationCode.GrantEvolutionChoice, EvolutionIds));

            var regionMark = Trait(
                "ProgressQinglanRegionMark",
                "qinglan.progress.region_mark.qinglan",
                new[] { "progress.unique", "progress.region_mark" });
            var firstClear = Reward(
                "RewardTingfengFirstClear",
                "qinglan.reward.first_clear.tingfeng",
                RewardRepeatPolicy.OncePerRun,
                fallback.ContentIdText,
                "qinglan.first_clear.tingfeng",
                Operation(RewardOperationCode.GrantUnique, 0f, 0, regionMark.ContentIdText),
                Operation(RewardOperationCode.AddCurrency, 0f, 25, string.Empty, "qinglan.currency.spirit_sand"));

            ConfigureEliteReward(
                "EliteAfflictedCoreReward",
                fallback.ContentIdText,
                relicOperations,
                false);
            ConfigureEliteReward(
                "EliteSplittingReward",
                fallback.ContentIdText,
                relicOperations,
                true);

            var zhezhi = Require<QinglanDefinitionAuthoring>(Folder + "/BossZhezhiDefinition.asset");
            var tingfeng = Require<QinglanDefinitionAuthoring>(Folder + "/BossTingfengDefinition.asset");
            zhezhi.RuntimeData.referenceId1 = manifestation.ContentIdText;
            tingfeng.RuntimeData.referenceId1 = firstClear.ContentIdText;

            var additions = new ContentAuthoringBase[]
            {
                ridingWindFeatherStatus,
                greenwoodReward, boundaryReward, thunderReward, gourdReward, heartReward, featherReward,
                greenwood, boundary, thunder, gourd, heart, feather,
                brokenSwordEcho, listeningWindEcho, oldCourtBellPulse,
                windVeinOutput, herbGardenOutput, blankTokenCost,
                brokenSwordTassel, windVeinCopper, herbGardenSeedPod,
                listeningWindCore, oldCourtBell, blankSwordTrialToken,
                fallback, manifestation, regionMark, firstClear
            };
            var definitions = new List<ContentAuthoringBase>(pack.Definitions.Count + additions.Length);
            for (var index = 0; index < pack.Definitions.Count; index++) definitions.Add(pack.Definitions[index]);
            for (var index = 0; index < additions.Length; index++)
                if (!definitions.Contains(additions[index])) definitions.Add(additions[index]);
            pack.Configure(
                "qinglan.pack.demo",
                "0.8.0",
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
            EditorUtility.SetDirty(zhezhi);
            EditorUtility.SetDirty(tingfeng);
            EditorUtility.SetDirty(pack);
            AssetDatabase.SaveAssets();
            var bake = ContentBakeUtility.Bake(pack);
            if (!bake.IsSuccess) throw new UnityException(bake.Error.ToString());
            ContentBakeUtility.WriteCatalog(QinglanG12ContentSetup.PackPath, bake.Value);
            QinglanG17PackSetup.Configure();
            AssetDatabase.SaveAssets();
            Debug.Log("[Qinglan G2.3] Reward pack baked: entries=" +
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

        private static void ConfigureEliteReward(
            string file,
            string fallbackId,
            QinglanRewardOperationDto[] choiceOperations,
            bool splitting)
        {
            var reward = Require<QinglanDefinitionAuthoring>(Folder + "/" + file + ".asset");
            var count = choiceOperations.Length + (splitting ? 1 : 0);
            var operations = new QinglanRewardOperationDto[count];
            Array.Copy(choiceOperations, operations, choiceOperations.Length);
            if (splitting)
            {
                operations[count - 1] = Operation(RewardOperationCode.SpawnEnemy, 0.35f, 2);
            }
            reward.ConfigureRuntime(
                RuntimeContentKinds.Reward,
                new QinglanRuntimeDefinitionDto
                {
                    enum0 = (int)RewardRepeatPolicy.Repeatable,
                    referenceId0 = fallbackId,
                    rewardOperations = operations,
                    presentationProfileId = "placeholder.presentation.qinglan.reward.afflicted_core"
                });
            EditorUtility.SetDirty(reward);
        }

        private static QinglanDefinitionAuthoring Reward(
            string file,
            string id,
            RewardRepeatPolicy repeatPolicy,
            string fallbackId,
            string uniqueKey,
            params QinglanRewardOperationDto[] operations)
        {
            var reward = LoadOrCreate<QinglanDefinitionAuthoring>(Folder + "/" + file + ".asset");
            Identity(reward, id, "reward.runtime");
            reward.ConfigureRuntime(
                RuntimeContentKinds.Reward,
                new QinglanRuntimeDefinitionDto
                {
                    enum0 = (int)repeatPolicy,
                    referenceId0 = fallbackId,
                    text0 = uniqueKey,
                    rewardOperations = operations,
                    presentationProfileId = "placeholder.presentation." + id
                });
            return reward;
        }

        private static QinglanDefinitionAuthoring Pickup(
            string file,
            string id,
            QinglanDefinitionAuthoring reward,
            float radius,
            string eligibilityTag)
        {
            var pickup = LoadOrCreate<QinglanDefinitionAuthoring>(Folder + "/" + file + ".asset");
            Identity(pickup, id, "pickup.instant", eligibilityTag);
            pickup.ConfigureRuntime(
                RuntimeContentKinds.Pickup,
                new QinglanRuntimeDefinitionDto
                {
                    referenceId0 = reward.ContentIdText,
                    value0 = radius,
                    value1 = 90f,
                    tags0 = new[] { eligibilityTag },
                    presentationProfileId = "placeholder.presentation." + id
                });
            return pickup;
        }

        private static QinglanDefinitionAuthoring Relic(
            string file,
            string id,
            string[] tags,
            params string[] outputs)
        {
            var relic = LoadOrCreate<QinglanDefinitionAuthoring>(Folder + "/" + file + ".asset");
            Identity(relic, id, tags);
            relic.ConfigureRuntime(
                RuntimeContentKinds.Relic,
                new QinglanRuntimeDefinitionDto
                {
                    int0 = 1,
                    references0 = outputs,
                    references1 = Array.Empty<string>(),
                    references2 = Array.Empty<string>(),
                    presentationProfileId = "placeholder.presentation." + id
                });
            return relic;
        }

        private static TraitAuthoring Trait(
            string file,
            string id,
            string[] tags,
            params BuildModifierAuthoringData[] modifiers)
        {
            var trait = LoadOrCreate<TraitAuthoring>(Folder + "/" + file + ".asset");
            Identity(trait, id, tags);
            trait.Configure(modifiers ?? Array.Empty<BuildModifierAuthoringData>());
            return trait;
        }

        private static StatusEffectAuthoring Status(
            string file,
            string id,
            float duration,
            RuntimeStatusModifier modifier)
        {
            var status = LoadOrCreate<StatusEffectAuthoring>(Folder + "/" + file + ".asset");
            Identity(status, id, "status.beneficial", "status.pickup");
            status.Configure(
                StatusStackingPolicy.RefreshDuration,
                duration,
                1,
                0f,
                new[] { "status.dispel.beneficial" },
                Array.Empty<string>());
            status.ConfigureBehavior(modifier, default, 0f);
            return status;
        }

        private static SkillAuthoring Skill(
            string file,
            string id,
            float cooldown,
            SkillModuleAuthoringData targeting,
            SkillModuleAuthoringData delivery,
            params SkillEffectAuthoringData[] effects)
        {
            var skill = LoadOrCreate<SkillAuthoring>(Folder + "/" + file + ".asset");
            Identity(skill, id, "skill.hidden", "skill.relic", "damage.secondary");
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

        private static SkillEffectAuthoringData Damage(float value, DamageTags tags) =>
            new SkillEffectAuthoringData
            {
                moduleId = "base.effect.damage",
                value0 = value,
                int0 = (int)DamageType.Physical,
                int1 = unchecked((int)(uint)tags)
            };

        private static SkillEffectAuthoringData ApplyStatus(string statusId) =>
            new SkillEffectAuthoringData
            {
                moduleId = "base.effect.apply_status",
                value0 = 1f,
                referenceId0 = statusId
            };

        private static BuildModifierAuthoringData Modifier(
            string stat,
            ModifierOperation operation,
            float value,
            int priority,
            string group) =>
            new BuildModifierAuthoringData
            {
                statId = stat,
                operation = operation,
                value = value,
                priority = priority,
                stackingGroup = group
            };

        private static RuntimeStatusModifier StatusModifier(
            string stat,
            ModifierOperation operation,
            float value,
            string group) =>
            new RuntimeStatusModifier(
                StatId.Create(stat).Value,
                operation,
                value,
                0,
                ContentId.Create(group).Value);

        private static QinglanRewardOperationDto Operation(
            RewardOperationCode code,
            float value = 0f,
            int integerValue = 0,
            string referenceId = "",
            string eligibilityTag = "") =>
            new QinglanRewardOperationDto
            {
                code = (int)code,
                value = value,
                integerValue = integerValue,
                referenceId = referenceId,
                eligibilityTag = eligibilityTag
            };

        private static QinglanRewardOperationDto[] ChoiceOperations(
            RewardOperationCode code,
            string[] ids)
        {
            var operations = new QinglanRewardOperationDto[ids.Length];
            for (var index = 0; index < ids.Length; index++)
                operations[index] = Operation(code, 0f, 0, ids[index]);
            return operations;
        }

        private static void EnsureLocalization()
        {
            Localize("qinglan.status.pickup.riding_wind_feather", "Wind Feather Rush", "A short movement-speed and traversal-tolerance window.", "乘风羽疾行", "短时提升移速与穿阵容错。");
            Localize("qinglan.pickup.greenwood_dew", "Greenwood Dew", "Restores a fixed amount plus fifteen percent maximum health.", "青木露", "恢复固定生命并附加最大生命比例恢复。");
            Localize("qinglan.pickup.boundary_talisman", "Boundary Talisman", "Roots normal enemies in an area; Boss control is resistance-scaled.", "定界符", "范围定身普通敌人，首领控制时长受抗性缩减。");
            Localize("qinglan.pickup.thunder_jade", "Thunder Jade", "Deals high area lightning damage with knockback.", "震霄雷玉", "造成高额范围雷伤并击退。");
            Localize("qinglan.pickup.spirit_gourd", "Spirit Gourd", "Pulls eligible ground pickups while excluding unique and choice rewards.", "聚灵葫芦", "吸取合格地面灵物，排除唯一与选择奖励。");
            Localize("qinglan.pickup.heart_guard_jade", "Heart-Guard Jade", "Grants a short all-channel damage-immunity window.", "护心玉", "短时免疫全部伤害通道。");
            Localize("qinglan.pickup.riding_wind_feather", "Riding-Wind Feather", "Grants a short movement burst without crossing hard map boundaries.", "乘风羽", "短时疾行但不会穿越地图硬边界。");
            Localize("qinglan.relic.broken_sword_tassel", "Broken Sword Tassel", "Periodically emits a bounded secondary sword echo.", "断剑穗", "按冷却释放有界的次级剑气。");
            Localize("qinglan.relic.wind_vein_copper", "Wind-Vein Copper", "Improves movement and route pickup tolerance.", "风脉铜片", "强化移动与路线拾取容错。");
            Localize("qinglan.relic.herb_garden_seed_pod", "Herb-Garden Seed Pod", "Adds growth and converts controlled healing overflow into a capped barrier.", "药圃种囊", "提升生长并将受控治疗溢出转为有上限的屏障。");
            Localize("qinglan.relic.listening_wind_core", "Listening-Wind Core", "Periodically redirects a stable multi-target echo.", "听风木芯", "周期性对稳定多目标释放回响。");
            Localize("qinglan.relic.old_court_bell", "Old Court Bell", "Emits a telegraphed periodic slowing pulse.", "旧庭残钟", "周期释放带前摇的减速脉冲。");
            Localize("qinglan.relic.blank_sword_trial_token", "Blank Sword-Trial Token", "Deals twenty-five percent more damage to Bosses but increases incoming damage and lowers armor.", "无字试剑牌", "对首领增伤百分之二十五，但提高承伤并降低护甲。");
            Localize("qinglan.reward.manifestation_chest", "Manifestation Chest", "Offers up to three currently eligible Evolutions with deterministic fallback.", "显化宝匣", "提供至多三个当前合格显化，无候选时确定性回退。");
            Localize("qinglan.reward.fallback.spirit_sand", "Spirit-Sand Fallback", "A deterministic reward when no choice remains eligible.", "灵砂回退", "无合格候选时发放的确定性奖励。");
            Localize("qinglan.reward.first_clear.tingfeng", "Tingfeng First-Clear Reward", "A fixed unique Qinglan region mark and spirit sand.", "听风首通奖励", "固定发放唯一青岚山河脉印与灵砂。");
            Localize("qinglan.progress.region_mark.qinglan", "Qinglan Region Mark", "The unique fixed proof of the first Tingfeng victory.", "青岚山河脉印", "首次战胜听风的固定唯一凭证。");

            Localize("qinglan.reward.pickup.greenwood_dew", "Greenwood Dew Reward", "Controlled healing operation.", "青木露奖励", "受控治疗操作。");
            Localize("qinglan.reward.pickup.boundary_talisman", "Boundary Talisman Reward", "Controlled hostile-area status operation.", "定界符奖励", "受控敌对范围状态操作。");
            Localize("qinglan.reward.pickup.thunder_jade", "Thunder Jade Reward", "Controlled area-damage operation.", "震霄雷玉奖励", "受控范围伤害操作。");
            Localize("qinglan.reward.pickup.spirit_gourd", "Spirit Gourd Reward", "Eligible-pickup collection operation.", "聚灵葫芦奖励", "合格拾取物吸取操作。");
            Localize("qinglan.reward.pickup.heart_guard_jade", "Heart-Guard Jade Reward", "Controlled immunity-status operation.", "护心玉奖励", "受控免疫状态操作。");
            Localize("qinglan.reward.pickup.riding_wind_feather", "Riding-Wind Feather Reward", "Controlled movement-status operation.", "乘风羽奖励", "受控移动状态操作。");
            Localize("qinglan.skill.relic.broken_sword_echo", "Broken Sword Echo", "A bounded secondary projectile that cannot repeat itself.", "断剑回响", "不会递归重复自身的有界次级投射。");
            Localize("qinglan.skill.relic.listening_wind_echo", "Listening-Wind Echo", "A stable capped multi-target echo.", "听风回响", "稳定且有目标上限的多目标回响。");
            Localize("qinglan.skill.relic.old_court_bell_pulse", "Old Court Bell Pulse", "A periodic control pulse with Boss resistance scaling.", "旧庭钟波", "首领控制时长受抗性缩减的周期脉冲。");
            Localize("qinglan.trait.relic.wind_vein_copper", "Wind-Vein Copper Output", "Movement and pickup-range output.", "风脉铜片输出", "移动与拾取范围输出。");
            Localize("qinglan.trait.relic.herb_garden_seed_pod", "Herb-Garden Growth", "Health and regeneration growth output.", "药圃生长输出", "生命与恢复成长输出。");
            Localize("qinglan.trait.relic.blank_sword_trial_token", "Sword-Trial Cost", "The explicit armor cost of the high-risk token.", "试剑代价", "高风险试剑牌的明确护甲代价。");
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
