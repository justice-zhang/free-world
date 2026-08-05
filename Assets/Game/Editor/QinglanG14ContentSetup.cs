using System;
using System.Collections.Generic;
using Game.Content.Authoring;
using Game.Content.Runtime;
using Game.Core;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>Creates the checked-in G1.4 passives, offers, synergies, and evolution graph.</summary>
    public static class QinglanG14ContentSetup
    {
        private const string Folder = QinglanG12ContentSetup.Folder;

        [MenuItem("Tools/Free World/Qinglan/G1.4 Configure Progression Slice")]
        public static void Configure()
        {
            var pack = Require<ContentPackAuthoring>(QinglanG12ContentSetup.PackPath);
            var yufeng = Require<SkillAuthoring>(Folder + "/YufengSword.asset");
            var talisman = Require<SkillAuthoring>(Folder + "/YellowTalisman.asset");
            var lihuo = Require<SkillAuthoring>(Folder + "/LihuoWheel.asset");
            var tide = Require<SkillAuthoring>(Folder + "/TideOrb.asset");
            var zhenyue = Require<SkillAuthoring>(Folder + "/ZhenyueSeal.asset");
            var vine = Require<SkillAuthoring>(Folder + "/SpiritVineSeed.asset");
            var lihuoReturn = Require<SkillAuthoring>(Folder + "/LihuoReturnExplosion.asset");
            var tideRising = Require<SkillAuthoring>(Folder + "/TideRising.asset");
            var tideFalling = Require<SkillAuthoring>(Folder + "/TideFalling.asset");
            var zhenyueGuard = Require<SkillAuthoring>(Folder + "/ZhenyueGuardDomain.asset");
            var vineGrowth = Require<SkillAuthoring>(Folder + "/VineGrowth.asset");
            var vinePropagation = Require<SkillAuthoring>(Folder + "/VinePropagation.asset");

            var treadingWind = Passive(
                "TreadingWind",
                "qinglan.passive.treading_wind",
                new[] { "passive.player", "build.movement", "build.sword" },
                LevelPercent("base.stat.move_speed", 0.04f, "treading_wind"));
            var clearMind = Passive(
                "ClearMind",
                "qinglan.passive.clear_mind",
                new[] { "passive.player", "build.talisman", "build.mark" },
                PairLevels(
                    "base.stat.cooldown", -0.03f,
                    "base.stat.attack_speed", 0.03f,
                    "clear_mind"));
            var artifactControl = Passive(
                "ArtifactControl",
                "qinglan.passive.artifact_control",
                new[] { "passive.player", "build.mechanism", "build.projectile" },
                new[]
                {
                    PassiveModifier(1, "base.stat.projectile_speed", ModifierOperation.AddPercent, 0.06f, 100, Group("artifact_control", 1, "speed")),
                    PassiveModifier(2, "base.stat.pierce", ModifierOperation.AddFlat, 1f, 100, Group("artifact_control", 2, "pierce")),
                    PassiveModifier(3, "base.stat.projectile_speed", ModifierOperation.AddPercent, 0.08f, 100, Group("artifact_control", 3, "speed")),
                    PassiveModifier(4, "base.stat.projectile_count", ModifierOperation.AddFlat, 1f, 100, Group("artifact_control", 4, "count")),
                    PassiveModifier(5, "base.stat.pierce", ModifierOperation.AddFlat, 1f, 100, Group("artifact_control", 5, "pierce"))
                });
            var domainExpansion = Passive(
                "DomainExpansion",
                "qinglan.passive.domain_expansion",
                new[] { "passive.player", "build.area", "build.control" },
                PairLevels(
                    "base.stat.range", 0.05f,
                    "base.stat.duration", 0.06f,
                    "domain_expansion"));
            var longBreath = Passive(
                "LongBreath",
                "qinglan.passive.long_breath",
                new[] { "passive.player", "build.defense", "build.shield" },
                new[]
                {
                    PassiveModifier(1, "base.stat.health", ModifierOperation.AddPercent, 0.05f, 100, Group("long_breath", 1, "health")),
                    PassiveModifier(2, "base.stat.armor", ModifierOperation.AddFlat, 1f, 100, Group("long_breath", 2, "armor")),
                    PassiveModifier(3, "base.stat.regeneration", ModifierOperation.AddFlat, 0.25f, 100, Group("long_breath", 3, "regeneration")),
                    PassiveModifier(4, "base.stat.health", ModifierOperation.AddPercent, 0.07f, 100, Group("long_breath", 4, "health")),
                    PassiveModifier(5, "base.stat.armor", ModifierOperation.AddFlat, 2f, 100, Group("long_breath", 5, "armor"))
                });
            var spiritGathering = Passive(
                "SpiritGathering",
                "qinglan.passive.spirit_gathering",
                new[] { "passive.player", "build.plant", "build.dot" },
                PairLevels(
                    "base.stat.duration", 0.05f,
                    "base.stat.pickup_range", 0.5f,
                    "spirit_gathering",
                    ModifierOperation.AddPercent,
                    ModifierOperation.AddFlat));

            var windTrail = Skill(
                "FlowingShadowWindTrail",
                "qinglan.skill.hidden.flowing_shadow_wind_trail",
                new[] { "skill.hidden", "weapon.sword", "mechanic.trail", "damage.secondary" },
                0f,
                Module("base.trigger.on_hit"),
                Module("base.condition.always"),
                Module("base.targeting.trigger_position", int0: 0),
                Module(
                    "base.delivery.area",
                    value0: 2.25f,
                    value1: 1.5f,
                    value2: 0.5f,
                    presentation: "placeholder.presentation.qinglan.skill.flowing_shadow_wind_trail"),
                new[] { Damage(5f, DamageType.Physical, DamageTags.DamageOverTime | DamageTags.Secondary) });
            var flowingShadow = Skill(
                "QinglanFlowingShadowSword",
                "qinglan.skill.evolved.qinglan_flowing_shadow_sword",
                new[] { "skill.player", "skill.evolved", "weapon.sword", "mechanic.trail", "mechanic.return" },
                1.35f,
                Module("base.trigger.timer"),
                Module("base.condition.always"),
                Module("base.targeting.self"),
                Module("base.delivery.instant"),
                new[] { Spawn(yufeng.ContentIdText), Spawn(windTrail.ContentIdText) });
            var taiyiArray = Skill(
                "TaiyiSpiritSealingArray",
                "qinglan.skill.evolved.taiyi_spirit_sealing_array",
                new[] { "skill.player", "skill.evolved", "weapon.talisman", "mechanic.mark", "delivery.area" },
                1.6f,
                Module("base.trigger.timer"),
                Module("base.condition.always"),
                Module("base.targeting.nearest", value0: 20f, int0: 1),
                Module(
                    "base.delivery.area",
                    value0: 4f,
                    value1: 1.6f,
                    value2: 0.5f,
                    presentation: "placeholder.presentation.qinglan.skill.taiyi_spirit_sealing_array"),
                new[]
                {
                    Damage(6f, DamageType.Lightning, DamageTags.DamageOverTime),
                    ApplyStatus("qinglan.status.marked", 1f),
                    Spawn("qinglan.skill.hidden.talisman_detonation")
                });
            var hundredCraft = Skill(
                "ChiluHundredCraftWheel",
                "qinglan.skill.evolved.chilu_hundred_craft_wheel",
                new[] { "skill.player", "skill.evolved", "weapon.mechanism", "mechanic.return", "mechanic.chain" },
                2f,
                Module("base.trigger.timer"),
                Module("base.condition.always"),
                Module("base.targeting.nearest", value0: 18f, int0: 2),
                Module(
                    "base.delivery.outbound_return",
                    value0: 14f,
                    value1: 18f,
                    value2: 0.5f,
                    value3: 7f,
                    int0: 3,
                    presentation: "placeholder.presentation.qinglan.skill.chilu_hundred_craft_wheel",
                    reference0: lihuoReturn.ContentIdText),
                new[] { Damage(16f, DamageType.Fire, DamageTags.Direct, 0.06f, true) });
            var mirrorTide = Skill(
                "MirrorSeaTideWheel",
                "qinglan.skill.evolved.mirror_sea_tide_wheel",
                new[] { "skill.player", "skill.evolved", "weapon.artifact", "mechanic.cycle", "skill.control" },
                1.5f,
                Module("base.trigger.timer"),
                Module("base.condition.always"),
                Module("base.targeting.self"),
                Module("base.delivery.instant"),
                new[] { Spawn(tideRising.ContentIdText, tideFalling.ContentIdText, true) });
            var mountainBoundary = Skill(
                "MountainBoundarySeal",
                "qinglan.skill.evolved.mountain_boundary_seal",
                new[] { "skill.player", "skill.evolved", "weapon.artifact", "skill.defense", "skill.defense.counter" },
                1.2f,
                Module("base.trigger.on_damage_taken"),
                Module("base.condition.always"),
                Module("base.targeting.circle", value0: 4.5f, int0: 16),
                Module("base.delivery.instant"),
                new[]
                {
                    Damage(14f, DamageType.Physical, DamageTags.Direct | DamageTags.Secondary),
                    Motion("base.effect.knockback", 3f),
                    Spawn(zhenyueGuard.ContentIdText)
                });
            var earthVein = Skill(
                "EarthVeinSpringBranch",
                "qinglan.skill.evolved.earth_vein_spring_branch",
                new[] { "skill.player", "skill.evolved", "weapon.plant", "mechanic.growth", "mechanic.propagation" },
                0.5f,
                Module("base.trigger.on_kill"),
                Module("base.condition.always"),
                Module("base.targeting.trigger_position", int0: 0),
                Module("base.delivery.instant", presentation: "placeholder.presentation.qinglan.skill.earth_vein_spring_branch"),
                new[]
                {
                    Spawn(vineGrowth.ContentIdText),
                    Spawn(vinePropagation.ContentIdText)
                });

            var skillOffers = new[]
            {
                Offer("OfferSkillYufengSword", "qinglan.offer.skill.yufeng_sword", yufeng, 1.2f, true),
                Offer("OfferSkillYellowTalisman", "qinglan.offer.skill.yellow_talisman", talisman, 1f, true),
                Offer("OfferSkillLihuoWheel", "qinglan.offer.skill.lihuo_wheel", lihuo, 1f, true),
                Offer("OfferSkillTideOrb", "qinglan.offer.skill.tide_orb", tide, 1f, true),
                Offer("OfferSkillZhenyueSeal", "qinglan.offer.skill.zhenyue_seal", zhenyue, 1f, true),
                Offer("OfferSkillSpiritVineSeed", "qinglan.offer.skill.spirit_vine_seed", vine, 1f, true)
            };
            var passiveOffers = new[]
            {
                Offer("OfferPassiveTreadingWind", "qinglan.offer.passive.treading_wind", treadingWind, 1.15f, true),
                Offer("OfferPassiveClearMind", "qinglan.offer.passive.clear_mind", clearMind, 1f, true),
                Offer("OfferPassiveArtifactControl", "qinglan.offer.passive.artifact_control", artifactControl, 1f, true),
                Offer("OfferPassiveDomainExpansion", "qinglan.offer.passive.domain_expansion", domainExpansion, 1f, true),
                Offer("OfferPassiveLongBreath", "qinglan.offer.passive.long_breath", longBreath, 1f, true),
                Offer("OfferPassiveSpiritGathering", "qinglan.offer.passive.spirit_gathering", spiritGathering, 1f, true)
            };

            var evolutions = new[]
            {
                Evolution("EvolutionQinglanFlowingShadowSword", "qinglan.evolution.qinglan_flowing_shadow_sword", yufeng, treadingWind, flowingShadow),
                Evolution("EvolutionTaiyiSpiritSealingArray", "qinglan.evolution.taiyi_spirit_sealing_array", talisman, clearMind, taiyiArray),
                Evolution("EvolutionChiluHundredCraftWheel", "qinglan.evolution.chilu_hundred_craft_wheel", lihuo, artifactControl, hundredCraft),
                Evolution("EvolutionMirrorSeaTideWheel", "qinglan.evolution.mirror_sea_tide_wheel", tide, domainExpansion, mirrorTide),
                Evolution("EvolutionMountainBoundarySeal", "qinglan.evolution.mountain_boundary_seal", zhenyue, longBreath, mountainBoundary),
                Evolution("EvolutionEarthVeinSpringBranch", "qinglan.evolution.earth_vein_spring_branch", vine, spiritGathering, earthVein)
            };
            var evolutionOffers = new UpgradeOfferAuthoring[evolutions.Length];
            for (var index = 0; index < evolutions.Length; index++)
            {
                var shortName = evolutions[index].ContentIdText.Substring("qinglan.evolution.".Length);
                evolutionOffers[index] = Offer(
                    "OfferEvolution" + Pascal(shortName),
                    "qinglan.offer.evolution." + shortName,
                    evolutions[index],
                    1f,
                    false);
            }

            var movingSwordPath = Synergy(
                "SynergyMovingSwordPath",
                "qinglan.synergy.moving_sword_path",
                new[] { Owns(yufeng), Owns(treadingWind) },
                new[]
                {
                    AddModifier("base.stat.projectile_speed", ModifierOperation.AddPercent, 0.12f, "moving_sword_path")
                });
            var talismanDetonation = Synergy(
                "SynergyTalismanDetonation",
                "qinglan.synergy.talisman_detonation",
                new[] { Owns(talisman), Owns(clearMind) },
                new[]
                {
                    AddEffect(talisman, ApplyStatus("qinglan.status.marked", 1f))
                });
            var livingGarden = Synergy(
                "SynergyLivingGarden",
                "qinglan.synergy.living_garden",
                new[] { Owns(vine), Owns(spiritGathering) },
                new[]
                {
                    AddModifier("base.stat.duration", ModifierOperation.AddPercent, 0.15f, "living_garden")
                });

            var additions = new List<ContentAuthoringBase>(40)
            {
                treadingWind, clearMind, artifactControl, domainExpansion, longBreath, spiritGathering,
                windTrail, flowingShadow, taiyiArray, hundredCraft, mirrorTide, mountainBoundary, earthVein,
                movingSwordPath, talismanDetonation, livingGarden
            };
            additions.AddRange(skillOffers);
            additions.AddRange(passiveOffers);
            additions.AddRange(evolutions);
            additions.AddRange(evolutionOffers);

            var definitions = new List<ContentAuthoringBase>(pack.Definitions.Count + additions.Count);
            for (var index = 0; index < pack.Definitions.Count; index++) definitions.Add(pack.Definitions[index]);
            for (var index = 0; index < additions.Count; index++)
                if (!definitions.Contains(additions[index])) definitions.Add(additions[index]);
            pack.Configure(
                "qinglan.pack.demo",
                "0.3.0",
                ContentPackTopology.QinglanDemoSchemaVersion,
                "0.1.0",
                string.Empty,
                Array.Empty<ContentPackDependencyAuthoring>(),
                "packs/qinglan.demo/catalog",
                "pack.qinglan.demo",
                false,
                definitions.ToArray());

            EnsureLocalization();
            for (var index = 0; index < additions.Count; index++) EditorUtility.SetDirty(additions[index]);
            EditorUtility.SetDirty(pack);
            AssetDatabase.SaveAssets();
            var bake = ContentBakeUtility.Bake(pack);
            if (!bake.IsSuccess) throw new UnityException(bake.Error.ToString());
            ContentBakeUtility.WriteCatalog(QinglanG12ContentSetup.PackPath, bake.Value);
            AssetDatabase.SaveAssets();
            Debug.Log("[Qinglan G1.4] Progression pack baked: entries=" +
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

        private static PassiveAuthoring Passive(
            string file,
            string id,
            string[] tags,
            PassiveLevelModifierAuthoringData[] modifiers)
        {
            var passive = LoadOrCreate<PassiveAuthoring>(Folder + "/" + file + ".asset");
            Identity(passive, id, tags);
            passive.Configure(5, modifiers);
            return passive;
        }

        private static EvolutionAuthoring Evolution(
            string file,
            string id,
            SkillAuthoring source,
            PassiveAuthoring passive,
            SkillAuthoring result)
        {
            var evolution = LoadOrCreate<EvolutionAuthoring>(Folder + "/" + file + ".asset");
            Identity(evolution, id, "build.evolution", "reward.manifestation");
            evolution.Configure(
                source,
                8,
                new[] { passive },
                Array.Empty<BuildConditionAuthoringData>(),
                result,
                EvolutionConsumePolicy.RetainRequiredPassives);
            return evolution;
        }

        private static UpgradeOfferAuthoring Offer(
            string file,
            string id,
            ContentAuthoringBase target,
            float weight,
            bool initiallyUnlocked)
        {
            var offer = LoadOrCreate<UpgradeOfferAuthoring>(Folder + "/" + file + ".asset");
            Identity(offer, id, "build.offer");
            offer.Configure(
                target,
                weight,
                initiallyUnlocked,
                Array.Empty<BuildConditionAuthoringData>(),
                Array.Empty<ContentAuthoringBase>());
            return offer;
        }

        private static SynergyAuthoring Synergy(
            string file,
            string id,
            BuildConditionAuthoringData[] conditions,
            SynergyOutputAuthoringData[] outputs)
        {
            var synergy = LoadOrCreate<SynergyAuthoring>(Folder + "/" + file + ".asset");
            Identity(synergy, id, "build.synergy");
            synergy.Configure(conditions, outputs);
            return synergy;
        }

        private static SynergyOutputAuthoringData AddModifier(
            string stat,
            ModifierOperation operation,
            float value,
            string suffix) =>
            new SynergyOutputAuthoringData
            {
                type = SynergyOutputType.AddModifier,
                modifier = Modifier(stat, operation, value, 200, "qinglan.stack.synergy." + suffix)
            };

        private static SynergyOutputAuthoringData AddEffect(
            ContentAuthoringBase source,
            SkillEffectAuthoringData effect) =>
            new SynergyOutputAuthoringData
            {
                type = SynergyOutputType.AddEffectOp,
                sourceContent = source,
                effect = effect
            };

        private static BuildConditionAuthoringData Owns(ContentAuthoringBase content) =>
            new BuildConditionAuthoringData
            {
                type = BuildConditionType.OwnsContent,
                content = content
            };

        private static PassiveLevelModifierAuthoringData[] LevelPercent(
            string stat,
            float value,
            string suffix)
        {
            var output = new PassiveLevelModifierAuthoringData[5];
            for (var level = 1; level <= 5; level++)
                output[level - 1] = PassiveModifier(
                    level,
                    stat,
                    ModifierOperation.AddPercent,
                    value,
                    100,
                    Group(suffix, level, "primary"));
            return output;
        }

        private static PassiveLevelModifierAuthoringData[] PairLevels(
            string firstStat,
            float firstValue,
            string secondStat,
            float secondValue,
            string suffix,
            ModifierOperation firstOperation = ModifierOperation.AddPercent,
            ModifierOperation secondOperation = ModifierOperation.AddPercent)
        {
            var output = new PassiveLevelModifierAuthoringData[10];
            for (var level = 1; level <= 5; level++)
            {
                output[(level - 1) * 2] = PassiveModifier(
                    level,
                    firstStat,
                    firstOperation,
                    firstValue,
                    100,
                    Group(suffix, level, "primary"));
                output[((level - 1) * 2) + 1] = PassiveModifier(
                    level,
                    secondStat,
                    secondOperation,
                    secondValue,
                    110,
                    Group(suffix, level, "secondary"));
            }
            return output;
        }

        private static PassiveLevelModifierAuthoringData PassiveModifier(
            int level,
            string stat,
            ModifierOperation operation,
            float value,
            int priority,
            string stackingGroup) =>
            new PassiveLevelModifierAuthoringData
            {
                level = level,
                modifier = Modifier(stat, operation, value, priority, stackingGroup)
            };

        private static BuildModifierAuthoringData Modifier(
            string stat,
            ModifierOperation operation,
            float value,
            int priority,
            string stackingGroup) =>
            new BuildModifierAuthoringData
            {
                statId = stat,
                operation = operation,
                value = value,
                priority = priority,
                stackingGroup = stackingGroup
            };

        private static string Group(string suffix, int level, string stat) =>
            "qinglan.stack.passive." + suffix + ".level_" + level + "." + stat;

        private static SkillAuthoring Skill(
            string file,
            string id,
            string[] tags,
            float cooldown,
            SkillModuleAuthoringData trigger,
            SkillModuleAuthoringData condition,
            SkillModuleAuthoringData targeting,
            SkillModuleAuthoringData delivery,
            SkillEffectAuthoringData[] effects)
        {
            var skill = LoadOrCreate<SkillAuthoring>(Folder + "/" + file + ".asset");
            Identity(skill, id, tags);
            skill.ConfigureRuntime(
                cooldown,
                0f,
                trigger,
                condition,
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
            float value3 = 0f,
            int int0 = 0,
            int int1 = 0,
            string presentation = "",
            string reference0 = "",
            string reference1 = "") =>
            new SkillModuleAuthoringData
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

        private static SkillEffectAuthoringData Damage(
            float amount,
            DamageType type,
            DamageTags tags,
            float criticalChance = 0f,
            bool canCritical = false) =>
            new SkillEffectAuthoringData
            {
                moduleId = "base.effect.damage",
                value0 = amount,
                value1 = criticalChance,
                int0 = (int)type,
                int1 = unchecked((int)(uint)tags),
                flags = canCritical ? EffectOpFlags.CanCritical : EffectOpFlags.None
            };

        private static SkillEffectAuthoringData ApplyStatus(string id, float strength) =>
            new SkillEffectAuthoringData
            {
                moduleId = "base.effect.apply_status",
                value0 = strength,
                referenceId0 = id
            };

        private static SkillEffectAuthoringData Spawn(string id, string alternate = "", bool alternating = false) =>
            new SkillEffectAuthoringData
            {
                moduleId = "base.effect.spawn_secondary_skill",
                referenceId0 = id,
                referenceId1 = alternate,
                int0 = alternating ? 1 : 0
            };

        private static SkillEffectAuthoringData Motion(string id, float magnitude) =>
            new SkillEffectAuthoringData { moduleId = id, value0 = magnitude };

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
            var value = AssetDatabase.LoadAssetAtPath<T>(path);
            if (value == null) throw new UnityException("Required G1.3 content is missing: " + path);
            return value;
        }

        private static string Pascal(string snake)
        {
            var result = string.Empty;
            var uppercase = true;
            for (var index = 0; index < snake.Length; index++)
            {
                var character = snake[index];
                if (character == '_')
                {
                    uppercase = true;
                    continue;
                }
                result += uppercase ? char.ToUpperInvariant(character) : character;
                uppercase = false;
            }
            return result;
        }

        private static void EnsureLocalization()
        {
            Localize("qinglan.passive.treading_wind", "Treading Wind", "Increases movement speed and supports the moving sword path.", "踏风步", "提高移动速度，并强化移动御剑构筑。");
            Localize("qinglan.passive.clear_mind", "Clear Mind", "Reduces cooldowns and improves attack speed.", "清心诀", "缩短冷却并提高攻击速度，使符印节奏更稳定。");
            Localize("qinglan.passive.artifact_control", "Artifact Control", "Improves projectile speed, pierce, and bounded projectile count.", "御器篇", "提高投射物速度、穿透与受控数量。");
            Localize("qinglan.passive.domain_expansion", "Domain Expansion", "Increases range and duration for area control.", "开域法", "提高范围与持续时间，强化领域控制。");
            Localize("qinglan.passive.long_breath", "Long Breath", "Improves health, armor, and regeneration in measured steps.", "长息功", "分级提高生命、护甲与恢复。");
            Localize("qinglan.passive.spirit_gathering", "Spirit Gathering", "Extends effects and expands pickup reach.", "采灵诀", "延长效果持续时间并扩大拾取范围。");
            Localize("qinglan.skill.hidden.flowing_shadow_wind_trail", "Flowing Shadow Wind Trail", "Leaves a short-lived bounded wind trail at the wielder's position.", "流影风痕", "在持剑者位置留下数量与寿命受限的风痕。");
            Localize("qinglan.skill.evolved.qinglan_flowing_shadow_sword", "Qinglan Flowing Shadow Sword", "Releases the returning sword while leaving bounded wind trails.", "青岚流影剑", "御使回返飞剑，并沿移动位置留下有界风痕。");
            Localize("qinglan.skill.evolved.taiyi_spirit_sealing_array", "Taiyi Spirit-Sealing Array", "Forms a sealing array that marks and atomically detonates targets.", "太一镇灵符阵", "展开符阵叠加符印，并原子引爆合法目标。");
            Localize("qinglan.skill.evolved.chilu_hundred_craft_wheel", "Chilu Hundred-Craft Wheel", "Launches additional returning wheels with bounded return bursts.", "赤炉百工轮", "增加往返飞轮数量，并在回程触发有限爆裂。");
            Localize("qinglan.skill.evolved.mirror_sea_tide_wheel", "Mirror-Sea Tide Wheel", "Alternates deterministic rising and falling tide control.", "镜海潮生轮", "确定性交替涨潮吸附与退潮击退爆发。");
            Localize("qinglan.skill.evolved.mountain_boundary_seal", "Mountain Boundary Seal", "Damage taken creates a cooldown-limited shield and countershock.", "山河镇界印", "受伤时按冷却生成护域并发动反震。");
            Localize("qinglan.skill.evolved.earth_vein_spring_branch", "Earth-Vein Spring Branch", "Kills grow connected fields that propagate only one bounded generation.", "地脉生春枝", "击杀生成相连藤丛，并以单代有界方式向邻区扩张。");
            Localize("qinglan.evolution.qinglan_flowing_shadow_sword", "Qinglan Flowing Shadow Sword Manifestation", "Transforms a mastered wind sword with Treading Wind.", "青岚流影剑显化", "将满级游风剑与踏风步显化为青岚流影剑。");
            Localize("qinglan.evolution.taiyi_spirit_sealing_array", "Taiyi Spirit-Sealing Array Manifestation", "Transforms a mastered talisman with Clear Mind.", "太一镇灵符阵显化", "将满级镇邪黄符与清心诀显化为太一镇灵符阵。");
            Localize("qinglan.evolution.chilu_hundred_craft_wheel", "Chilu Hundred-Craft Wheel Manifestation", "Transforms a mastered Lihuo wheel with Artifact Control.", "赤炉百工轮显化", "将满级离火飞轮与御器篇显化为赤炉百工轮。");
            Localize("qinglan.evolution.mirror_sea_tide_wheel", "Mirror-Sea Tide Wheel Manifestation", "Transforms a mastered tide orb with Domain Expansion.", "镜海潮生轮显化", "将满级听潮珠与开域法显化为镜海潮生轮。");
            Localize("qinglan.evolution.mountain_boundary_seal", "Mountain Boundary Seal Manifestation", "Transforms a mastered mountain seal with Long Breath.", "山河镇界印显化", "将满级震岳印与长息功显化为山河镇界印。");
            Localize("qinglan.evolution.earth_vein_spring_branch", "Earth-Vein Spring Branch Manifestation", "Transforms a mastered vine seed with Spirit Gathering.", "地脉生春枝显化", "将满级灵藤种与采灵诀显化为地脉生春枝。");
            Localize("qinglan.synergy.moving_sword_path", "Moving Sword Path", "Treading Wind and the wind sword improve projectile speed together.", "移动御剑", "踏风步与游风剑共同提高御剑飞行速度。");
            Localize("qinglan.synergy.talisman_detonation", "Talisman Detonation", "Clear Mind lets talisman hits add one additional legal mark.", "符阵爆发", "清心诀使黄符命中额外叠加一层合法符印。");
            Localize("qinglan.synergy.living_garden", "Living Garden", "Spirit gathering extends the lifetime of planted fields.", "草木铺场", "采灵诀延长草木领域的持续时间。");

            LocalizeOffer("skill.yufeng_sword", "Wandering Wind Sword", "游风剑");
            LocalizeOffer("skill.yellow_talisman", "Warding Yellow Talisman", "镇邪黄符");
            LocalizeOffer("skill.lihuo_wheel", "Lihuo Wheel", "离火飞轮");
            LocalizeOffer("skill.tide_orb", "Tide-Listening Orb", "听潮珠");
            LocalizeOffer("skill.zhenyue_seal", "Mountain-Shaking Seal", "震岳印");
            LocalizeOffer("skill.spirit_vine_seed", "Spirit Vine Seed", "灵藤种");
            LocalizeOffer("passive.treading_wind", "Treading Wind", "踏风步");
            LocalizeOffer("passive.clear_mind", "Clear Mind", "清心诀");
            LocalizeOffer("passive.artifact_control", "Artifact Control", "御器篇");
            LocalizeOffer("passive.domain_expansion", "Domain Expansion", "开域法");
            LocalizeOffer("passive.long_breath", "Long Breath", "长息功");
            LocalizeOffer("passive.spirit_gathering", "Spirit Gathering", "采灵诀");
            LocalizeOffer("evolution.qinglan_flowing_shadow_sword", "Qinglan Flowing Shadow Sword", "青岚流影剑");
            LocalizeOffer("evolution.taiyi_spirit_sealing_array", "Taiyi Spirit-Sealing Array", "太一镇灵符阵");
            LocalizeOffer("evolution.chilu_hundred_craft_wheel", "Chilu Hundred-Craft Wheel", "赤炉百工轮");
            LocalizeOffer("evolution.mirror_sea_tide_wheel", "Mirror-Sea Tide Wheel", "镜海潮生轮");
            LocalizeOffer("evolution.mountain_boundary_seal", "Mountain Boundary Seal", "山河镇界印");
            LocalizeOffer("evolution.earth_vein_spring_branch", "Earth-Vein Spring Branch", "地脉生春枝");
        }

        private static void LocalizeOffer(string suffix, string english, string chinese) =>
            Localize(
                "qinglan.offer." + suffix,
                english + " Offer",
                "Acquire or improve " + english + ".",
                chinese + "候选",
                "获得或提升" + chinese + "。");

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
