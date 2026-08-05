using System;
using System.Collections.Generic;
using Game.Content.Authoring;
using Game.Content.Runtime;
using Game.Core;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>Creates the checked-in G1.3 Qinglan weapon and hidden-skill slice.</summary>
    public static class QinglanG13ContentSetup
    {
        private const string Folder = QinglanG12ContentSetup.Folder;

        [MenuItem("Tools/Free World/Qinglan/G1.3 Configure Weapon Skill Slice")]
        public static void Configure()
        {
            var pack = AssetDatabase.LoadAssetAtPath<ContentPackAuthoring>(QinglanG12ContentSetup.PackPath);
            var character = AssetDatabase.LoadAssetAtPath<CharacterAuthoring>(Folder + "/LuQingye.asset");
            var mechanic = AssetDatabase.LoadAssetAtPath<QinglanDefinitionAuthoring>(Folder + "/RidingWindMechanic.asset");
            if (pack == null || character == null || mechanic == null)
                throw new InvalidOperationException("Run the checked-in G1.2 setup before G1.3.");

            var ridingWindBlade = Skill(
                "RidingWindBlade",
                "qinglan.skill.hidden.riding_wind_blade",
                new[] { "skill.hidden", "weapon.sword", "delivery.projectile", "damage.secondary" },
                0f,
                Module("base.trigger.on_hit"),
                Module("base.condition.always"),
                Module("base.targeting.nearest", value0: 15f, int0: 1),
                Module(
                    "base.delivery.projectile",
                    value0: 22f,
                    value1: 0.28f,
                    value2: 1.5f,
                    int0: 1,
                    presentation: "placeholder.presentation.qinglan.skill.riding_wind_blade"),
                new[] { Damage(7f, DamageType.Physical, DamageTags.Direct | DamageTags.Secondary) },
                DamagePatches(0, 1f));
            var yufengReturn = Skill(
                "YufengReturn",
                "qinglan.skill.hidden.yufeng_return",
                new[] { "skill.hidden", "weapon.sword", "mechanic.return_complete" },
                0f,
                Module("base.trigger.on_hit"),
                Module("base.condition.always"),
                Module("base.targeting.trigger_position", int0: 0),
                Module("base.delivery.instant"),
                new[] { Spawn("qinglan.skill.hidden.riding_wind_blade") },
                Array.Empty<SkillLevelPatchAuthoringData>());
            var talismanDetonation = Skill(
                "TalismanDetonation",
                "qinglan.skill.hidden.talisman_detonation",
                new[] { "skill.hidden", "weapon.talisman", "mechanic.mark", "damage.burst" },
                0f,
                Module("base.trigger.on_hit"),
                Module(
                    "base.condition.status_count_at_least",
                    int0: 3,
                    int1: (int)StatusQueryTarget.Target,
                    reference0: "qinglan.status.marked"),
                Module("base.targeting.trigger_position", value0: 4f, int0: 12),
                Module("base.delivery.instant"),
                new[] { Detonate("qinglan.status.marked", 5f, 6) },
                DamagePatches(0, 1f));
            var lihuoReturnExplosion = Skill(
                "LihuoReturnExplosion",
                "qinglan.skill.hidden.lihuo_return_explosion",
                new[] { "skill.hidden", "weapon.mechanism", "mechanic.return", "damage.burst" },
                0f,
                Module("base.trigger.on_hit"),
                Module("base.condition.always"),
                Module("base.targeting.trigger_position", value0: 3f, int0: 12),
                Module("base.delivery.instant"),
                new[] { Damage(8f, DamageType.Fire, DamageTags.Direct | DamageTags.Secondary) },
                DamagePatches(0, 1.25f));
            var tideRising = Skill(
                "TideRising",
                "qinglan.skill.hidden.tide_rising",
                new[] { "skill.hidden", "weapon.artifact", "mechanic.cycle.rising", "control.pull" },
                0f,
                Module("base.trigger.on_hit"),
                Module("base.condition.always"),
                Module("base.targeting.trigger_position", value0: 5f, int0: 16),
                Module("base.delivery.instant"),
                new[]
                {
                    Motion("base.effect.pull", 1.5f),
                    ApplyStatus("qinglan.status.slowed", 1f)
                },
                ValuePatches(0, "effects[0].value0", 0.15f));
            var tideFalling = Skill(
                "TideFalling",
                "qinglan.skill.hidden.tide_falling",
                new[] { "skill.hidden", "weapon.artifact", "mechanic.cycle.falling", "control.knockback" },
                0f,
                Module("base.trigger.on_hit"),
                Module("base.condition.always"),
                Module("base.targeting.trigger_position", value0: 5f, int0: 16),
                Module("base.delivery.instant"),
                new[]
                {
                    Damage(10f, DamageType.Cold, DamageTags.Direct),
                    Motion("base.effect.knockback", 2f)
                },
                DamagePatches(0, 1.5f));
            var zhenyueGuard = Skill(
                "ZhenyueGuardDomain",
                "qinglan.skill.hidden.zhenyue_guard_domain",
                new[] { "skill.hidden", "weapon.artifact", "skill.defense", "skill.shield" },
                0f,
                Module("base.trigger.on_hit"),
                Module("base.condition.always"),
                Module("base.targeting.self"),
                Module("base.delivery.instant"),
                new[] { ValueEffect("base.effect.grant_shield", 6f) },
                ValuePatches(0, "effects[0].value0", 1f));
            var zhenyueCountershock = Skill(
                "ZhenyueCountershock",
                "qinglan.skill.hidden.zhenyue_countershock",
                new[] { "skill.hidden", "weapon.artifact", "skill.defense.counter", "control.knockback" },
                1.2f,
                Module("base.trigger.on_damage_taken"),
                Module("base.condition.always"),
                Module("base.targeting.circle", value0: 4f, int0: 12),
                Module("base.delivery.instant"),
                new[]
                {
                    Damage(5f, DamageType.Physical, DamageTags.Direct | DamageTags.Secondary),
                    Motion("base.effect.knockback", 2f)
                },
                DamagePatches(0, 1f));
            var vineGrowth = Skill(
                "VineGrowth",
                "qinglan.skill.hidden.vine_growth",
                new[] { "skill.hidden", "weapon.plant", "mechanic.growth", "damage.dot" },
                0f,
                Module("base.trigger.on_kill"),
                Module("base.condition.always"),
                Module("base.targeting.trigger_position", int0: 0),
                Module(
                    "base.delivery.area",
                    value0: 2.5f,
                    value1: 3f,
                    value2: 1f,
                    presentation: "placeholder.presentation.qinglan.skill.vine_growth"),
                new[]
                {
                    Damage(3f, DamageType.Poison, DamageTags.DamageOverTime),
                    ApplyStatus("qinglan.status.poisoned", 1f)
                },
                DamagePatches(0, 0.5f));
            var vinePropagation = Skill(
                "VinePropagation",
                "qinglan.skill.hidden.vine_propagation",
                new[] { "skill.hidden", "weapon.plant", "mechanic.growth.propagation", "damage.dot" },
                0f,
                Module("base.trigger.on_hit"),
                Module(
                    "base.condition.target_has_status",
                    int0: (int)StatusQueryTarget.Target,
                    reference0: "qinglan.status.poisoned"),
                Module("base.targeting.trigger_position", value0: 5f, int0: 2),
                Module(
                    "base.delivery.area",
                    value0: 2f,
                    value1: 2f,
                    value2: 1f,
                    presentation: "placeholder.presentation.qinglan.skill.vine_propagation"),
                new[]
                {
                    Damage(2f, DamageType.Poison, DamageTags.DamageOverTime | DamageTags.Secondary),
                    ApplyStatus("qinglan.status.poisoned", 1f)
                },
                DamagePatches(0, 0.5f));

            var yufengSword = Skill(
                "YufengSword",
                "qinglan.skill.weapon.yufeng_sword",
                new[] { "skill.player", "weapon.sword", "delivery.projectile", "mechanic.return", "mechanic.riding_wind_affinity" },
                1.8f,
                Module("base.trigger.timer"),
                Module("base.condition.always"),
                Module("base.targeting.nearest", value0: 18f, int0: 1),
                Module(
                    "base.delivery.outbound_return",
                    value0: 14f,
                    value1: 18f,
                    value2: 0.35f,
                    value3: 7f,
                    int0: 2,
                    presentation: "placeholder.presentation.qinglan.skill.yufeng_sword",
                    reference0: yufengReturn.ContentIdText,
                    reference1: "qinglan.trait.lu_qingye.riding_wind"),
                new[] { Damage(16f, DamageType.Physical, DamageTags.Direct, 0.08f, true) },
                new[]
                {
                    Patch(2, "effects[0].value0", 4f),
                    Patch(3, "cooldown", 0.9f, SkillPatchOperation.Multiply),
                    IntPatch(4, "delivery.int0", 1),
                    Patch(5, "delivery.value0", 2f),
                    Patch(6, "effects[0].value0", 6f),
                    Patch(7, "delivery.value3", 2f),
                    Patch(8, "cooldown", 0.85f, SkillPatchOperation.Multiply),
                    Patch(8, "effects[0].value0", 8f)
                });
            var yellowTalisman = Skill(
                "YellowTalisman",
                "qinglan.skill.weapon.yellow_talisman",
                new[] { "skill.player", "weapon.talisman", "delivery.projectile", "mechanic.mark", "damage.burst" },
                1.2f,
                Module("base.trigger.timer"),
                Module("base.condition.always"),
                Module("base.targeting.nearest", value0: 20f, int0: 1),
                Module(
                    "base.delivery.projectile",
                    value0: 16f,
                    value1: 0.25f,
                    value2: 2f,
                    int0: 1,
                    presentation: "placeholder.presentation.qinglan.skill.yellow_talisman"),
                new[]
                {
                    Damage(8f, DamageType.Lightning, DamageTags.Direct, 0.05f, true),
                    ApplyStatus("qinglan.status.marked", 1f),
                    Spawn(talismanDetonation.ContentIdText)
                },
                new[]
                {
                    Patch(2, "effects[0].value0", 2f),
                    Patch(3, "cooldown", 0.9f, SkillPatchOperation.Multiply),
                    IntPatch(4, "delivery.int0", 1),
                    Patch(5, "delivery.value0", 2f),
                    IntPatch(6, "targeting.int0", 1),
                    Patch(7, "effects[0].value0", 3f),
                    Patch(8, "cooldown", 0.85f, SkillPatchOperation.Multiply)
                });
            var lihuoWheel = Skill(
                "LihuoWheel",
                "qinglan.skill.weapon.lihuo_wheel",
                new[] { "skill.player", "weapon.mechanism", "delivery.projectile", "mechanic.return", "mechanic.chain" },
                2.4f,
                Module("base.trigger.timer"),
                Module("base.condition.always"),
                Module("base.targeting.nearest", value0: 17f, int0: 1),
                Module(
                    "base.delivery.outbound_return",
                    value0: 12f,
                    value1: 14f,
                    value2: 0.45f,
                    value3: 6f,
                    int0: 3,
                    presentation: "placeholder.presentation.qinglan.skill.lihuo_wheel"),
                new[] { Damage(12f, DamageType.Fire, DamageTags.Direct, 0.04f, true) },
                new[]
                {
                    Patch(2, "effects[0].value0", 3f),
                    Patch(3, "delivery.value0", 2f),
                    IntPatch(4, "targeting.int0", 1),
                    IntPatch(5, "delivery.int0", 1),
                    Patch(6, "cooldown", 0.88f, SkillPatchOperation.Multiply),
                    Patch(7, "delivery.value3", 2f),
                    Patch(8, "effects[0].value0", 7f)
                });
            var tideOrb = Skill(
                "TideOrb",
                "qinglan.skill.weapon.tide_orb",
                new[] { "skill.player", "weapon.artifact", "delivery.aura", "mechanic.cycle", "skill.control" },
                2f,
                Module("base.trigger.timer"),
                Module("base.condition.always"),
                Module("base.targeting.self"),
                Module("base.delivery.instant"),
                new[] { Spawn(tideRising.ContentIdText, tideFalling.ContentIdText, true) },
                new[]
                {
                    Patch(2, "cooldown", 0.95f, SkillPatchOperation.Multiply),
                    Patch(3, "cooldown", 0.95f, SkillPatchOperation.Multiply),
                    Patch(4, "cooldown", 0.95f, SkillPatchOperation.Multiply),
                    Patch(5, "cooldown", 0.95f, SkillPatchOperation.Multiply),
                    Patch(6, "cooldown", 0.95f, SkillPatchOperation.Multiply),
                    Patch(7, "cooldown", 0.95f, SkillPatchOperation.Multiply),
                    Patch(8, "cooldown", 0.9f, SkillPatchOperation.Multiply)
                });
            var zhenyueSeal = Skill(
                "ZhenyueSeal",
                "qinglan.skill.weapon.zhenyue_seal",
                new[] { "skill.player", "weapon.artifact", "delivery.area", "skill.control", "skill.defense" },
                2.8f,
                Module("base.trigger.timer"),
                Module("base.condition.always"),
                Module("base.targeting.nearest", value0: 10f, int0: 1),
                Module(
                    "base.delivery.area",
                    value0: 3f,
                    value1: 0.25f,
                    value2: 0.25f,
                    presentation: "placeholder.presentation.qinglan.skill.zhenyue_seal"),
                new[]
                {
                    Damage(18f, DamageType.Physical, DamageTags.Direct),
                    Motion("base.effect.knockback", 3f)
                },
                new[]
                {
                    Patch(2, "effects[0].value0", 4f),
                    Patch(3, "delivery.value0", 0.5f),
                    Patch(4, "cooldown", 0.9f, SkillPatchOperation.Multiply),
                    Patch(5, "effects[1].value0", 0.5f),
                    Patch(6, "effects[0].value0", 6f),
                    Patch(7, "targeting.value0", 2f),
                    Patch(8, "delivery.value0", 1f)
                });
            var spiritVineSeed = Skill(
                "SpiritVineSeed",
                "qinglan.skill.weapon.spirit_vine_seed",
                new[] { "skill.player", "weapon.plant", "delivery.area", "damage.dot", "mechanic.growth" },
                0.75f,
                Module("base.trigger.on_kill"),
                Module("base.condition.always"),
                Module("base.targeting.trigger_position", int0: 0),
                Module(
                    "base.delivery.area",
                    value0: 2.5f,
                    value1: 4f,
                    value2: 1f,
                    presentation: "placeholder.presentation.qinglan.skill.spirit_vine_seed"),
                new[]
                {
                    Damage(4f, DamageType.Poison, DamageTags.DamageOverTime),
                    ApplyStatus("qinglan.status.poisoned", 1f)
                },
                new[]
                {
                    Patch(2, "effects[0].value0", 1f),
                    Patch(3, "delivery.value1", 1f),
                    Patch(4, "delivery.value0", 0.5f),
                    Patch(5, "cooldown", 0.9f, SkillPatchOperation.Multiply),
                    Patch(6, "effects[0].value0", 2f),
                    Patch(7, "delivery.value2", 0.85f, SkillPatchOperation.Multiply),
                    Patch(8, "delivery.value0", 0.75f)
                });

            character.Configure(120f, 6f, new[] { yufengSword });
            character.ConfigureMechanics(new[] { mechanic });

            var additions = new ContentAuthoringBase[]
            {
                yufengSword,
                yellowTalisman,
                lihuoWheel,
                tideOrb,
                zhenyueSeal,
                spiritVineSeed,
                yufengReturn,
                ridingWindBlade,
                talismanDetonation,
                lihuoReturnExplosion,
                tideRising,
                tideFalling,
                zhenyueGuard,
                zhenyueCountershock,
                vineGrowth,
                vinePropagation
            };
            var definitions = new List<ContentAuthoringBase>(pack.Definitions.Count + additions.Length);
            for (var index = 0; index < pack.Definitions.Count; index++) definitions.Add(pack.Definitions[index]);
            for (var index = 0; index < additions.Length; index++)
                if (!definitions.Contains(additions[index])) definitions.Add(additions[index]);
            pack.Configure(
                "qinglan.pack.demo",
                "0.2.0",
                ContentPackTopology.QinglanDemoSchemaVersion,
                "0.1.0",
                string.Empty,
                Array.Empty<ContentPackDependencyAuthoring>(),
                "packs/qinglan.demo/catalog",
                "pack.qinglan.demo",
                false,
                definitions.ToArray());

            EnsureLocalization();
            EditorUtility.SetDirty(character);
            for (var index = 0; index < additions.Length; index++) EditorUtility.SetDirty(additions[index]);
            EditorUtility.SetDirty(pack);
            AssetDatabase.SaveAssets();
            var bake = ContentBakeUtility.Bake(pack);
            if (!bake.IsSuccess) throw new UnityException(bake.Error.ToString());
            ContentBakeUtility.WriteCatalog(QinglanG12ContentSetup.PackPath, bake.Value);
            AssetDatabase.SaveAssets();
            Debug.Log("[Qinglan G1.3] Weapon pack baked: entries=" +
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

        private static SkillAuthoring Skill(
            string fileName,
            string id,
            string[] tags,
            float cooldown,
            SkillModuleAuthoringData trigger,
            SkillModuleAuthoringData condition,
            SkillModuleAuthoringData targeting,
            SkillModuleAuthoringData delivery,
            SkillEffectAuthoringData[] effects,
            SkillLevelPatchAuthoringData[] patches)
        {
            var skill = LoadOrCreate<SkillAuthoring>(Folder + "/" + fileName + ".asset");
            Identity(skill, id, tags);
            skill.ConfigureRuntime(cooldown, 0f, trigger, condition, targeting, delivery, effects, patches);
            return skill;
        }

        private static SkillModuleAuthoringData Module(
            string id,
            float value0 = 0f,
            float value1 = 0f,
            float value2 = 0f,
            float value3 = 0f,
            int int0 = 0,
            int int1 = 0,
            string presentation = "",
            string reference0 = "",
            string reference1 = "")
        {
            return new SkillModuleAuthoringData
            {
                moduleId = id,
                value0 = value0,
                value1 = value1,
                value2 = value2,
                value3 = value3,
                int0 = int0,
                int1 = int1,
                presentationId = presentation,
                referenceId0 = reference0,
                referenceId1 = reference1
            };
        }

        private static SkillEffectAuthoringData Damage(
            float amount,
            DamageType type,
            DamageTags tags,
            float criticalChance = 0f,
            bool canCritical = false)
        {
            return new SkillEffectAuthoringData
            {
                moduleId = "base.effect.damage",
                value0 = amount,
                value1 = criticalChance,
                int0 = (int)type,
                int1 = unchecked((int)(uint)tags),
                flags = canCritical ? EffectOpFlags.CanCritical : EffectOpFlags.None
            };
        }

        private static SkillEffectAuthoringData ApplyStatus(string id, float strength)
        {
            return new SkillEffectAuthoringData
            {
                moduleId = "base.effect.apply_status",
                value0 = strength,
                referenceId0 = id
            };
        }

        private static SkillEffectAuthoringData Spawn(string id, string alternate = "", bool alternating = false)
        {
            return new SkillEffectAuthoringData
            {
                moduleId = "base.effect.spawn_secondary_skill",
                referenceId0 = id,
                referenceId1 = alternate,
                int0 = alternating ? 1 : 0
            };
        }

        private static SkillEffectAuthoringData Detonate(string statusId, float perStack, int maximumStacks)
        {
            return new SkillEffectAuthoringData
            {
                moduleId = "base.effect.detonate_status",
                value0 = perStack,
                int0 = maximumStacks,
                referenceId0 = statusId
            };
        }

        private static SkillEffectAuthoringData Motion(string id, float magnitude) =>
            ValueEffect(id, magnitude);

        private static SkillEffectAuthoringData ValueEffect(string id, float value)
        {
            return new SkillEffectAuthoringData { moduleId = id, value0 = value };
        }

        private static SkillLevelPatchAuthoringData[] DamagePatches(int effectIndex, float amount) =>
            ValuePatches(effectIndex, "effects[" + effectIndex + "].value0", amount);

        private static SkillLevelPatchAuthoringData[] ValuePatches(
            int ignoredEffectIndex,
            string path,
            float amount)
        {
            var patches = new SkillLevelPatchAuthoringData[7];
            for (var level = 2; level <= 8; level++) patches[level - 2] = Patch(level, path, amount);
            return patches;
        }

        private static SkillLevelPatchAuthoringData Patch(
            int level,
            string path,
            float value,
            SkillPatchOperation operation = SkillPatchOperation.Add)
        {
            return new SkillLevelPatchAuthoringData
            {
                level = level,
                path = path,
                valueType = SkillPatchValueType.Float,
                operation = operation,
                floatValue = value
            };
        }

        private static SkillLevelPatchAuthoringData IntPatch(
            int level,
            string path,
            int value,
            SkillPatchOperation operation = SkillPatchOperation.Add)
        {
            return new SkillLevelPatchAuthoringData
            {
                level = level,
                path = path,
                valueType = SkillPatchValueType.Integer,
                operation = operation,
                integerValue = value
            };
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

        private static void EnsureLocalization()
        {
            Localize("qinglan.skill.weapon.yufeng_sword", "Wandering Wind Sword", "A sword that strikes on both outbound and return paths.", "游风剑", "飞剑出返皆可命中，满乘风时回收追加风刃。");
            Localize("qinglan.skill.weapon.yellow_talisman", "Warding Yellow Talisman", "Marks targets and detonates accumulated seals.", "镇邪黄符", "符纸标记目标，叠满后引爆周围标记。");
            Localize("qinglan.skill.weapon.lihuo_wheel", "Lihuo Wheel", "A fiery mechanism wheel that damages on outbound and return paths.", "离火飞轮", "离火机轮往返切割敌人，擅长多目标穿行。");
            Localize("qinglan.skill.weapon.tide_orb", "Tide-Listening Orb", "Alternates rising pull and falling knockback phases.", "听潮珠", "涨潮吸附、退潮击退，两相确定性交替。");
            Localize("qinglan.skill.weapon.zhenyue_seal", "Mountain-Shaking Seal", "Drops a heavy area strike with knockback.", "震岳印", "镇印砸落造成范围伤害与击退。");
            Localize("qinglan.skill.weapon.spirit_vine_seed", "Spirit Vine Seed", "Kills plant damaging poisonous vine fields at the trigger position.", "灵藤种", "击杀时在触发位置生长持续伤害的毒藤区域。");
            Localize("qinglan.skill.hidden.yufeng_return", "Wind Sword Return", "Return-complete helper for the innate sword.", "游风回返", "游风剑回收完成时使用的隐藏辅助技能。");
            Localize("qinglan.skill.hidden.riding_wind_blade", "Riding Wind Blade", "A lesser blade emitted by a full-wind return.", "乘风刃", "满风势回返后发出的一道弱风刃。");
            Localize("qinglan.skill.hidden.talisman_detonation", "Talisman Detonation", "Consumes marks atomically and deals scaled damage.", "符印引爆", "原子消费标记并按实际层数造成伤害。");
            Localize("qinglan.skill.hidden.lihuo_return_explosion", "Lihuo Return Burst", "Bounded return explosion helper for evolution content.", "离火回爆", "供显化内容组合的有限回程爆裂。");
            Localize("qinglan.skill.hidden.tide_rising", "Rising Tide", "Pulls nearby enemies and slows them.", "涨潮", "吸附并减速附近敌人。");
            Localize("qinglan.skill.hidden.tide_falling", "Falling Tide", "Damages and pushes nearby enemies away.", "退潮", "伤害并击退附近敌人。");
            Localize("qinglan.skill.hidden.zhenyue_guard_domain", "Zhenyue Guard", "Grants a bounded defensive shield.", "震岳护域", "提供有上限的防御护盾。");
            Localize("qinglan.skill.hidden.zhenyue_countershock", "Zhenyue Countershock", "A cooldown-limited reaction to damage taken.", "震岳反震", "受伤时按冷却触发的反震。");
            Localize("qinglan.skill.hidden.vine_growth", "Vine Growth", "Creates a bounded poisonous growth area.", "灵藤生长", "生成有生命周期上限的毒藤区域。");
            Localize("qinglan.skill.hidden.vine_propagation", "Vine Propagation", "Propagates growth to a bounded number of poisoned neighbors.", "灵藤蔓延", "向有限数量的中毒邻居传播藤丛。");
        }

        private static void Localize(
            string id,
            string englishName,
            string englishDescription,
            string chineseName,
            string chineseDescription)
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
