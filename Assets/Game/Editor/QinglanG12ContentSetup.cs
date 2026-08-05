using System;
using Game.Content.Authoring;
using Game.Content.Runtime;
using Game.Core;
using Game.Simulation;
using UnityEditor;
using UnityEngine;
using NumericsVector2 = System.Numerics.Vector2;

namespace Game.Editor
{
    /// <summary>Creates the checked-in G1.2 Qinglan character and combat Placeholder slice.</summary>
    public static class QinglanG12ContentSetup
    {
        public const string Folder = "Assets/GameAssets/Placeholder/QinglanDemo";
        public const string PackPath = Folder + "/QinglanDemoContentPack.asset";
        public const string BakedCatalogPath = Folder + "/QinglanDemoContentPack.baked.json";

        [MenuItem("Tools/Free World/Qinglan/G1.2 Configure Character Combat Slice")]
        public static void Configure()
        {
            EnsureFolder("Assets/GameAssets/Placeholder", "QinglanDemo");

            var breeze = Trait(
                "RidingWindBreeze",
                "qinglan.trait.lu_qingye.riding_wind.breeze",
                new[] { "mechanic.output.affinity_only", "mechanic.riding_wind.tier_1" },
                Modifier("base.stat.projectile_speed", ModifierOperation.AddPercent, 0.10f));
            var swift = Trait(
                "RidingWindSwift",
                "qinglan.trait.lu_qingye.riding_wind.swift",
                new[] { "mechanic.output.innate_only", "mechanic.riding_wind.tier_2" },
                Modifier("base.stat.move_speed", ModifierOperation.AddPercent, 0.05f),
                Modifier("base.stat.cooldown", ModifierOperation.Multiply, 0.92f));
            var riding = Trait(
                "RidingWind",
                "qinglan.trait.lu_qingye.riding_wind",
                new[] { "mechanic.output.return_secondary", "mechanic.riding_wind.tier_3" });

            var mechanic = LoadOrCreate<QinglanDefinitionAuthoring>(Folder + "/RidingWindMechanic.asset");
            Identity(
                mechanic,
                "qinglan.mechanic.lu_qingye.riding_wind",
                "character.mechanic",
                "mechanic.riding_wind");
            mechanic.ConfigureRuntime(
                RuntimeContentKinds.CharacterMechanic,
                new QinglanRuntimeDefinitionDto
                {
                    presentationProfileId = "qinglan.presentation.character.lu_qingye.riding_wind",
                    resourceId = "qinglan.resource.riding_wind",
                    value0 = 1f,
                    value1 = 8f,
                    mechanicTiers = new[]
                    {
                        new QinglanMechanicTierDto { threshold = 6f, outputId = breeze.ContentIdText },
                        new QinglanMechanicTierDto { threshold = 16f, outputId = swift.ContentIdText },
                        new QinglanMechanicTierDto { threshold = 30f, outputId = riding.ContentIdText }
                    }
                });

            var character = LoadOrCreate<CharacterAuthoring>(Folder + "/LuQingye.asset");
            Identity(character, "qinglan.character.lu_qingye", "character.player", "character.lu_qingye");
            character.Configure(120f, 6f, Array.Empty<SkillAuthoring>());
            character.ConfigureMechanics(new[] { mechanic });

            var burning = Status(
                "Burning",
                "qinglan.status.burning",
                new[] { "status.damage_over_time", "status.burning" },
                StatusStackingPolicy.AddStacks,
                4f,
                5,
                1f,
                new[] { "status.dispel.elemental" },
                Array.Empty<string>(),
                default,
                new RuntimeStatusPeriodicDamage(
                    DamageType.Fire,
                    DamageTags.DamageOverTime | DamageTags.Status,
                    2.5f,
                    false,
                    0f,
                    NumericsVector2.Zero));
            var poisoned = Status(
                "Poisoned",
                "qinglan.status.poisoned",
                new[] { "status.damage_over_time", "status.poisoned" },
                StatusStackingPolicy.IndependentInstances,
                6f,
                4,
                1.5f,
                new[] { "status.dispel.poison" },
                Array.Empty<string>(),
                default,
                new RuntimeStatusPeriodicDamage(
                    DamageType.Poison,
                    DamageTags.DamageOverTime | DamageTags.Status,
                    1.75f,
                    false,
                    0f,
                    NumericsVector2.Zero));
            var slowed = Status(
                "Slowed",
                "qinglan.status.slowed",
                new[] { "status.control", "status.control.slow" },
                StatusStackingPolicy.ReplaceIfStronger,
                2.5f,
                1,
                0f,
                new[] { "status.dispel.control" },
                new[] { "status.immunity.slow" },
                StatusModifier("base.stat.move_speed", ModifierOperation.Multiply, 0.70f, "base.status.control.movement"));
            var rooted = Status(
                "Rooted",
                "qinglan.status.rooted",
                new[] { "status.control", "status.control.root" },
                StatusStackingPolicy.RefreshDuration,
                1f,
                1,
                0f,
                new[] { "status.dispel.control" },
                new[] { "status.immunity.root" },
                StatusModifier("base.stat.move_speed", ModifierOperation.Override, 0f, "base.status.control.movement"));
            var armorBroken = Status(
                "ArmorBroken",
                "qinglan.status.armor_broken",
                new[] { "status.debuff", "status.debuff.armor" },
                StatusStackingPolicy.ReplaceIfStronger,
                5f,
                1,
                0f,
                new[] { "status.dispel.debuff" },
                Array.Empty<string>(),
                StatusModifier("base.stat.armor", ModifierOperation.Multiply, 0.70f, "base.status.debuff.armor"));
            var marked = Status(
                "Marked",
                "qinglan.status.marked",
                new[] { "status.debuff", "status.mark" },
                StatusStackingPolicy.AddStacks,
                6f,
                6,
                0f,
                new[] { "status.dispel.mark" },
                Array.Empty<string>(),
                default);
            var damageImmunity = Status(
                "DamageImmunity",
                "qinglan.status.damage_immunity",
                new[] { "status.beneficial", "base.damage_policy.immune.all" },
                StatusStackingPolicy.RefreshDuration,
                1.5f,
                1,
                0f,
                new[] { "status.dispel.beneficial" },
                Array.Empty<string>(),
                default);

            var definitions = new ContentAuthoringBase[]
            {
                character,
                mechanic,
                breeze,
                swift,
                riding,
                burning,
                poisoned,
                slowed,
                rooted,
                armorBroken,
                marked,
                damageImmunity
            };
            var pack = LoadOrCreate<ContentPackAuthoring>(PackPath);
            pack.Configure(
                "qinglan.pack.demo",
                "0.1.0",
                ContentPackTopology.QinglanDemoSchemaVersion,
                "0.1.0",
                string.Empty,
                Array.Empty<ContentPackDependencyAuthoring>(),
                "packs/qinglan.demo/catalog",
                "pack.qinglan.demo",
                false,
                definitions);

            EnsureLocalization();

            for (var index = 0; index < definitions.Length; index++) EditorUtility.SetDirty(definitions[index]);
            EditorUtility.SetDirty(pack);
            AssetDatabase.SaveAssets();
            var bake = ContentBakeUtility.Bake(pack);
            if (!bake.IsSuccess) throw new UnityException(bake.Error.ToString());
            ContentBakeUtility.WriteCatalog(PackPath, bake.Value);
            AssetDatabase.SaveAssets();
            Debug.Log("[Qinglan G1.2] Character/combat pack baked: entries=" +
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

        private static TraitAuthoring Trait(
            string fileName,
            string id,
            string[] tags,
            params BuildModifierAuthoringData[] modifiers)
        {
            var trait = LoadOrCreate<TraitAuthoring>(Folder + "/" + fileName + ".asset");
            Identity(trait, id, tags);
            trait.Configure(modifiers ?? Array.Empty<BuildModifierAuthoringData>());
            return trait;
        }

        private static StatusEffectAuthoring Status(
            string fileName,
            string id,
            string[] tags,
            StatusStackingPolicy policy,
            float duration,
            int maxStacks,
            float interval,
            string[] dispelTags,
            string[] immunityTags,
            RuntimeStatusModifier modifier,
            RuntimeStatusPeriodicDamage periodic = default)
        {
            var status = LoadOrCreate<StatusEffectAuthoring>(Folder + "/" + fileName + ".asset");
            Identity(status, id, tags);
            status.Configure(policy, duration, maxStacks, interval, dispelTags, immunityTags);
            status.ConfigureBehavior(modifier, periodic, 0f);
            return status;
        }

        private static RuntimeStatusModifier StatusModifier(
            string stat,
            ModifierOperation operation,
            float value,
            string stackingGroup) =>
            new RuntimeStatusModifier(
                StatId.Create(stat).Value,
                operation,
                value,
                0,
                ContentId.Create(stackingGroup).Value);

        private static void EnsureLocalization()
        {
            Localize(
                "qinglan.character.lu_qingye",
                "Lu Qingye",
                "A wandering swordsman who builds Riding Wind through real movement.",
                "陆青野",
                "以真实位移积蓄乘风之势的游历剑客。");
            Localize(
                "qinglan.mechanic.lu_qingye.riding_wind",
                "Riding Wind",
                "Real movement raises wind tiers; taking actual damage lowers one tier.",
                "乘风",
                "真实位移提升风势档位，受到实际伤害时降低一档。");
            Localize(
                "qinglan.trait.lu_qingye.riding_wind.breeze",
                "Breeze",
                "Riding Wind tier one: affinity deliveries travel faster.",
                "微风",
                "乘风一档：亲和投射物飞行更快。");
            Localize(
                "qinglan.trait.lu_qingye.riding_wind.swift",
                "Swift Wind",
                "Riding Wind tier two: move faster and shorten innate cooldowns.",
                "疾风",
                "乘风二档：提升移速并缩短本命器冷却。");
            Localize(
                "qinglan.trait.lu_qingye.riding_wind",
                "Full Wind",
                "Riding Wind tier three: returning innate weapons emit a lesser wind blade.",
                "乘风",
                "乘风三档：本命器回返后追加一道弱风刃。");
            Localize("qinglan.status.burning", "Burning", "Takes periodic fire damage.", "灼烧", "持续受到火焰伤害。");
            Localize("qinglan.status.poisoned", "Poisoned", "Independent poison instances deal periodic damage.", "中毒", "独立毒素实例持续造成伤害。");
            Localize("qinglan.status.slowed", "Slowed", "Movement speed is reduced.", "减速", "移动速度降低。");
            Localize("qinglan.status.rooted", "Rooted", "Movement speed is temporarily set to zero.", "定身", "移动速度暂时降为零。");
            Localize("qinglan.status.armor_broken", "Armor Broken", "Armor effectiveness is reduced.", "破甲", "护甲效果降低。");
            Localize("qinglan.status.marked", "Marked", "Receives stack-based mark effects.", "标记", "承受可叠加的标记效果。");
            Localize("qinglan.status.damage_immunity", "Damage Immunity", "Temporarily ignores all damage channels.", "伤害免疫", "暂时免疫全部伤害通道。");
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

        private static BuildModifierAuthoringData Modifier(
            string stat,
            ModifierOperation operation,
            float value) =>
            new BuildModifierAuthoringData
            {
                statId = stat,
                operation = operation,
                value = value
            };

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

        private static void EnsureFolder(string parent, string name)
        {
            var path = parent + "/" + name;
            if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, name);
        }
    }
}
