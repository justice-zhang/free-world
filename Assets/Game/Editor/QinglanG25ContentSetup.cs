using System;
using System.Collections.Generic;
using Game.Content.Authoring;
using Game.Content.Runtime;
using Game.Core;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>Creates the complete G2.5 Meta, hub, story, and collectible catalog.</summary>
    public static class QinglanG25ContentSetup
    {
        private const string Folder = QinglanG12ContentSetup.Folder;
        private const string StoryOne = "qinglan.story.lu_qingye.hearing_sword";
        private const string StoryTwo = "qinglan.story.lu_qingye.old_sword_and_gourd";
        private const string StoryThree = "qinglan.story.lu_qingye.refusing_inheritance";
        private static readonly string[] TerminalIds =
        {
            "qinglan.meta.lu_qingye.innate.04",
            "qinglan.meta.lu_qingye.movement.04",
            "qinglan.meta.lu_qingye.mind.04"
        };
        private static readonly string[] DiagnosticKeys =
        {
            "meta.error.duplicate_loadout",
            "meta.error.insufficient_spirit_sand",
            "meta.error.invalid_branch_node",
            "meta.error.invalid_insert",
            "meta.error.invalid_terminal",
            "meta.error.loadout_capacity",
            "meta.error.loadout_locked",
            "meta.error.loadout_missing",
            "meta.error.missing_loadout_content",
            "meta.error.multiple_terminals",
            "meta.error.mutually_exclusive",
            "meta.error.prerequisite_locked",
            "meta.error.purchase_missing",
            "save.error.invalid_run_result",
            "save.error.recovery_rejection_state",
            "save.error.result_collectible_invalid",
            "save.error.result_currency_duplicate",
            "save.error.result_currency_invalid",
            "save.error.result_story_invalid",
            "save.error.result_unique_invalid",
            "save.error.result_unlock_missing",
            "save.error.run_identity_missing",
            "save.prompt.recovery_rejected",
            "save.status.result_saved"
        };
        private static readonly string[] DiagnosticEnglish =
        {
            "The loadout contains a duplicate selection.",
            "There is not enough Spirit Sand.",
            "The selected branch node is unavailable.",
            "The selected insert is unavailable.",
            "The selected terminal node is unavailable.",
            "The loadout exceeds its slot capacity.",
            "The selected item has not been unlocked.",
            "No loadout was supplied.",
            "Saved loadout content is missing.",
            "Only one terminal node may be equipped.",
            "The selected nodes are mutually exclusive.",
            "A prerequisite has not been unlocked.",
            "The requested Meta item is unavailable.",
            "The run result is invalid and was not saved.",
            "Recovery rejection is not valid in the current state.",
            "A collectible in the result is unavailable.",
            "The result contains duplicate currency entries.",
            "The result contains an invalid currency entry.",
            "A story in the result is unavailable.",
            "A unique reward in the result is invalid.",
            "Unlocked result content is unavailable.",
            "The run character or map is unavailable.",
            "The previous run cannot be continued. Start a new run to clear its recovery marker.",
            "Progress saved."
        };
        private static readonly string[] DiagnosticChinese =
        {
            "装配中存在重复选择。",
            "灵砂不足。",
            "所选支脉节点不可用。",
            "所选嵌片不可用。",
            "所选终端节点不可用。",
            "装配数量超过槽位容量。",
            "所选内容尚未解锁。",
            "未提供有效装配。",
            "存档中的装配内容缺失。",
            "最多只能装配一个终端节点。",
            "所选节点彼此互斥。",
            "前置内容尚未解锁。",
            "请求的局外成长内容不可用。",
            "本局结果无效，未执行保存。",
            "当前状态不能执行恢复拒绝。",
            "结算中的藏品内容不可用。",
            "结算中存在重复货币条目。",
            "结算中存在无效货币条目。",
            "结算中的故事内容不可用。",
            "结算中的唯一奖励无效。",
            "结算中的解锁内容不可用。",
            "本局角色或地图内容不可用。",
            "上一局无法继续。开始新局后将清除恢复标记。",
            "进度已保存。"
        };

        [MenuItem("Tools/Free World/Qinglan/G2.5 Configure Meta and Save Content")]
        public static void Configure()
        {
            var pack = Require<ContentPackAuthoring>(QinglanG12ContentSetup.PackPath);
            var additions = new List<ContentAuthoringBase>(43);
            var nodes = new List<QinglanDefinitionAuthoring>(12);
            var outputs = new List<TraitAuthoring>(15);

            AddBranch(
                "Innate",
                "innate",
                new[] { "Innate Affinity", "Riding-Wind Threshold", "Innate Preview", "Final Form Qualification" },
                new[] { "本命亲和", "乘风微调", "本命预览", "终式资格" },
                new[] { "meta.rule.offer_affinity", "meta.rule.mechanic_tolerance", "meta.rule.build_preview", "meta.rule.evolution_qualification" },
                new[] { 0f, -0.02f, 0f, 0.05f },
                new[] { "", "base.stat.cooldown", "", "base.stat.projectile_speed" },
                nodes,
                outputs);
            AddBranch(
                "Movement",
                "movement",
                new[] { "Footwork Tolerance", "Recovery Window", "Route Tolerance", "Wind Route Terminal" },
                new[] { "身法余裕", "回息窗口", "寻路余裕", "风路终端" },
                new[] { "meta.rule.movement_tolerance", "meta.rule.recovery_window", "meta.rule.route_tolerance", "meta.rule.route_terminal" },
                new[] { 0.03f, 0.10f, 0.25f, 0.03f },
                new[] { "base.stat.move_speed", "base.stat.regeneration", "base.stat.pickup_range", "base.stat.move_speed" },
                nodes,
                outputs);
            AddBranch(
                "Mind",
                "mind",
                new[] { "Candidate Insight", "Choice Reserve", "Journey Record", "Risk Choice Terminal" },
                new[] { "候选洞察", "取舍余量", "行迹留存", "险择终端" },
                new[] { "meta.rule.candidate_information", "meta.rule.choice_capacity", "meta.rule.landmark_information", "meta.rule.risk_choice" },
                new[] { 0f, 0f, 0f, 0f },
                new[] { "", "", "", "" },
                nodes,
                outputs);

            for (var index = 0; index < nodes.Count; index++) additions.Add(nodes[index]);
            for (var index = 0; index < outputs.Count; index++) additions.Add(outputs[index]);

            var windInsertOutput = MetaTrait(
                "MetaInsertOutputWindPattern",
                "qinglan.trait.meta.insert.qinglan_wind_pattern",
                "Wind-Pattern Affinity",
                "青岚风纹亲和",
                "meta.rule.wind_affinity",
                "base.stat.move_speed",
                0.02f,
                ModifierOperation.AddPercent,
                560);
            var herbInsertOutput = MetaTrait(
                "MetaInsertOutputHerbClasp",
                "qinglan.trait.meta.insert.herb_garden_spring_clasp",
                "Spring-Clasp Recovery",
                "生春扣回息",
                "meta.rule.recovery_efficiency",
                "base.stat.regeneration",
                0.15f,
                ModifierOperation.AddFlat,
                561);
            var needleInsertOutput = MetaTrait(
                "MetaInsertOutputVeinNeedle",
                "qinglan.trait.meta.insert.old_court_vein_needle",
                "Vein-Needle Search",
                "寻脉针探查",
                "meta.rule.landmark_hint",
                "base.stat.pickup_range",
                0.35f,
                ModifierOperation.AddFlat,
                562);
            additions.Add(windInsertOutput);
            additions.Add(herbInsertOutput);
            additions.Add(needleInsertOutput);
            additions.Add(MetaInsert(
                "MetaInsertWindPattern",
                "qinglan.insert.qinglan_wind_pattern",
                "Qinglan Wind-Pattern Shard",
                "青岚风纹片",
                30,
                "meta.slot.offense",
                windInsertOutput.ContentIdText));
            additions.Add(MetaInsert(
                "MetaInsertHerbClasp",
                "qinglan.insert.herb_garden_spring_clasp",
                "Herb-Garden Spring Clasp",
                "药圃生春扣",
                30,
                "meta.slot.defense",
                herbInsertOutput.ContentIdText));
            additions.Add(MetaInsert(
                "MetaInsertVeinNeedle",
                "qinglan.insert.old_court_vein_needle",
                "Old-Court Vein Needle",
                "旧庭寻脉针",
                30,
                "meta.slot.exploration",
                needleInsertOutput.ContentIdText));

            var storyOne = Story(
                "StoryHearingSword",
                StoryOne,
                "Hearing the Sword Below the Mountain",
                "山脚听剑",
                "qinglan.landmark.wind_vein_stele",
                "qinglan.story.sequence.lu_qingye.01",
                false);
            var storyTwo = Story(
                "StoryOldSwordAndGourd",
                StoryTwo,
                "Old Sword and Wine Gourd",
                "旧剑与酒葫",
                StoryOne,
                "qinglan.story.sequence.lu_qingye.02",
                false);
            var storyThree = Story(
                "StoryRefusingInheritance",
                StoryThree,
                "Refusing the Inheritance",
                "不认传承",
                "qinglan.progress.region_mark.qinglan",
                "qinglan.story.sequence.lu_qingye.03",
                true);
            additions.Add(storyOne);
            additions.Add(storyTwo);
            additions.Add(storyThree);

            var collectibles = new[]
            {
                Collectible("CollectibleOldCourt01", "qinglan.collectible.old_court.01", "Broken Balance Inscription", "断衡残铭", "qinglan.topic.old_court.balance_court", "qinglan.landmark.wind_vein_stele", true),
                Collectible("CollectibleOldCourt02", "qinglan.collectible.old_court.02", "Old-Court Sword-Trial Record", "旧庭试剑录", "qinglan.topic.old_court.balance_court", "qinglan.landmark.sealed_sword_cache", false),
                Collectible("CollectibleOldCourt03", "qinglan.collectible.old_court.03", "Tingyun Sword Note", "停云剑札", "qinglan.topic.old_court.shen_tingyun", "qinglan.landmark.herb_garden_variant", false),
                Collectible("CollectibleOldCourt04", "qinglan.collectible.old_court.04", "Unsent Mountain Letter", "未寄山信", "qinglan.topic.old_court.shen_tingyun", "qinglan.landmark.broken_wall_sword_mark", false),
                Collectible("CollectibleOldCourt05", "qinglan.collectible.old_court.05", "Herb-Garden Wooden Tag", "药圃木牌", "qinglan.topic.old_court.daily_life", "qinglan.landmark.guest_pavilion_letter", false),
                Collectible("CollectibleOldCourt06", "qinglan.collectible.old_court.06", "Guest-Pavilion Wine Token", "迎客亭酒筹", "qinglan.topic.old_court.daily_life", StoryOne, false)
            };
            for (var index = 0; index < collectibles.Length; index++) additions.Add(collectibles[index]);

            additions.Add(Facility(
                "FacilityVeinInquiry",
                "qinglan.facility.vein_inquiry_platform",
                "Vein Inquiry Platform",
                "问脉台",
                "qinglan.meta.lu_qingye.innate.01",
                "qinglan.page.hub.vein_inquiry"));
            additions.Add(Facility(
                "FacilityScrollPavilion",
                "qinglan.facility.scroll_pavilion",
                "Scroll Pavilion",
                "藏卷楼",
                StoryOne,
                "qinglan.page.hub.scroll_pavilion"));
            additions.Add(Facility(
                "FacilityHundredArtifact",
                "qinglan.facility.hundred_artifact_pavilion",
                "Hundred-Artifact Pavilion",
                "百器阁",
                "qinglan.meta.lu_qingye.innate.02",
                "qinglan.page.hub.hundred_artifact"));
            additions.Add(Facility(
                "FacilityMyriadPhenomena",
                "qinglan.facility.myriad_phenomena_pavilion",
                "Myriad-Phenomena Pavilion",
                "万象阁",
                "qinglan.collectible.old_court.01",
                "qinglan.page.hub.myriad_phenomena"));

            var trialStele = Require<QinglanDefinitionAuthoring>(Folder + "/LandmarkTrialStele.asset");
            var guestLetter = Require<QinglanDefinitionAuthoring>(Folder + "/LandmarkGuestLetter.asset");
            trialStele.RuntimeData.referenceId2 = StoryOne;
            guestLetter.RuntimeData.referenceId2 = StoryTwo;

            var definitions = new List<ContentAuthoringBase>(pack.Definitions.Count + additions.Count);
            for (var index = 0; index < pack.Definitions.Count; index++) AddUnique(definitions, pack.Definitions[index]);
            for (var index = 0; index < additions.Count; index++) AddUnique(definitions, additions[index]);
            pack.Configure(
                "qinglan.pack.demo",
                "0.9.0",
                ContentPackTopology.QinglanDemoSchemaVersion,
                "0.1.0",
                string.Empty,
                Array.Empty<ContentPackDependencyAuthoring>(),
                "packs/qinglan.demo/catalog",
                "pack.qinglan.demo",
                false,
                definitions.ToArray());
            EnsureDiagnosticLocalization();

            for (var index = 0; index < additions.Count; index++) EditorUtility.SetDirty(additions[index]);
            EditorUtility.SetDirty(trialStele);
            EditorUtility.SetDirty(guestLetter);
            EditorUtility.SetDirty(pack);
            AssetDatabase.SaveAssets();
            var baked = ContentBakeUtility.Bake(pack);
            if (!baked.IsSuccess) throw new UnityException(baked.Error.ToString());
            if (baked.Value.Definitions.Count != 193)
                throw new UnityException("G2.5 pack must contain exactly 193 definitions.");
            ContentBakeUtility.WriteCatalog(QinglanG12ContentSetup.PackPath, baked.Value);
            QinglanG17PackSetup.Configure();
            AssetDatabase.SaveAssets();
            Debug.Log("[Qinglan G2.5] PASS: entries=" + baked.Value.Definitions.Count +
                      ", hash=" + baked.Value.ContentHash + ".");
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

        private static void EnsureDiagnosticLocalization()
        {
            if (DiagnosticKeys.Length != DiagnosticEnglish.Length ||
                DiagnosticKeys.Length != DiagnosticChinese.Length ||
                (DiagnosticKeys.Length & 1) != 0)
                throw new InvalidOperationException("G2.5 diagnostic localization entries must be paired.");
            for (var index = 0; index < DiagnosticKeys.Length; index += 2)
            {
                M9LocalizationUtility.EnsureContentEntries(
                    DiagnosticKeys[index],
                    DiagnosticKeys[index + 1],
                    DiagnosticEnglish[index],
                    DiagnosticEnglish[index + 1],
                    DiagnosticChinese[index],
                    DiagnosticChinese[index + 1]);
            }
        }

        private static void AddBranch(
            string filePrefix,
            string branch,
            string[] englishNames,
            string[] chineseNames,
            string[] ruleTags,
            float[] values,
            string[] stats,
            List<QinglanDefinitionAuthoring> nodes,
            List<TraitAuthoring> outputs)
        {
            for (var index = 0; index < 4; index++)
            {
                var number = (index + 1).ToString("00");
                var id = "qinglan.meta.lu_qingye." + branch + "." + number;
                var output = MetaTrait(
                    "MetaOutput" + filePrefix + number,
                    "qinglan.trait.meta.lu_qingye." + branch + "." + number,
                    englishNames[index] + " Output",
                    chineseNames[index] + "输出",
                    ruleTags[index],
                    stats[index],
                    values[index],
                    stats[index] == "base.stat.cooldown" ? ModifierOperation.AddPercent : ModifierOperation.AddPercent,
                    500 + nodes.Count);
                outputs.Add(output);
                var prerequisites = index == 0
                    ? Array.Empty<string>()
                    : new[] { "qinglan.meta.lu_qingye." + branch + "." + index.ToString("00") };
                var mutex = index == 3 ? OtherTerminals(id) : Array.Empty<string>();
                var tags = new List<string>
                {
                    "meta.node",
                    "meta.branch." + branch
                };
                if (index == 0) tags.Add("meta.initial");
                if (branch == "innate" && index == 1) tags.Add("meta.condition.any_upgrade");
                if (index == 3) tags.Add("meta.terminal");
                nodes.Add(MetaNode(
                    "MetaNode" + filePrefix + number,
                    id,
                    englishNames[index],
                    chineseNames[index],
                    index == 3 ? MetaNodeKind.Terminal : MetaNodeKind.Branch,
                    index == 0 ? 0 : index == 1 ? 20 : index == 2 ? 35 : 60,
                    prerequisites,
                    mutex,
                    output.ContentIdText,
                    tags.ToArray()));
            }
        }

        private static QinglanDefinitionAuthoring MetaNode(
            string file,
            string id,
            string english,
            string chinese,
            MetaNodeKind kind,
            int cost,
            string[] prerequisites,
            string[] mutex,
            string output,
            string[] tags)
        {
            var node = Definition(file, id, english, chinese, RuntimeContentKinds.MetaNode, tags);
            node.ConfigureRuntime(
                RuntimeContentKinds.MetaNode,
                new QinglanRuntimeDefinitionDto
                {
                    enum0 = (int)kind,
                    int0 = cost,
                    references0 = prerequisites,
                    references1 = new[] { output },
                    references2 = mutex,
                    presentationProfileId = "placeholder.presentation." + id
                });
            return node;
        }

        private static QinglanDefinitionAuthoring MetaInsert(
            string file,
            string id,
            string english,
            string chinese,
            int cost,
            string slotTag,
            string output)
        {
            var insert = Definition(file, id, english, chinese, RuntimeContentKinds.MetaInsert, "meta.insert");
            insert.ConfigureRuntime(
                RuntimeContentKinds.MetaInsert,
                new QinglanRuntimeDefinitionDto
                {
                    int0 = cost,
                    tags0 = new[] { slotTag },
                    references1 = new[] { output },
                    presentationProfileId = "placeholder.presentation." + id
                });
            return insert;
        }

        private static QinglanDefinitionAuthoring Facility(
            string file,
            string id,
            string english,
            string chinese,
            string condition,
            string page)
        {
            var facility = Definition(file, id, english, chinese, RuntimeContentKinds.MetaFacility, "meta.facility");
            facility.ConfigureRuntime(
                RuntimeContentKinds.MetaFacility,
                new QinglanRuntimeDefinitionDto
                {
                    referenceId0 = condition,
                    referenceId1 = page,
                    presentationProfileId = "placeholder.presentation." + id
                });
            return facility;
        }

        private static QinglanDefinitionAuthoring Story(
            string file,
            string id,
            string english,
            string chinese,
            string condition,
            string uniqueKey,
            bool victoryOnly)
        {
            var story = Definition(
                file,
                id,
                english,
                chinese,
                RuntimeContentKinds.Story,
                victoryOnly ? new[] { "story.profile", "story.victory_only" } : new[] { "story.profile" });
            var sequence = new[] { "story." + id + ".01", "story." + id + ".02" };
            story.ConfigureRuntime(
                RuntimeContentKinds.Story,
                new QinglanRuntimeDefinitionDto
                {
                    referenceId0 = condition,
                    text0 = uniqueKey,
                    localizedSequenceKeys = sequence,
                    presentationProfileId = "placeholder.presentation." + id
                });
            M9LocalizationUtility.EnsureContentEntries(sequence[0], sequence[1],
                english + " I", english + " II", chinese + "·一", chinese + "·二");
            return story;
        }

        private static QinglanDefinitionAuthoring Collectible(
            string file,
            string id,
            string english,
            string chinese,
            string topic,
            string acquireRule,
            bool anyCollectibleCondition)
        {
            var tags = anyCollectibleCondition
                ? new[] { "collectible.profile", "meta.condition.any_collectible" }
                : new[] { "collectible.profile" };
            var collectible = Definition(file, id, english, chinese, RuntimeContentKinds.Collectible, tags);
            var bodyKey = "collectible." + id + ".body";
            collectible.ConfigureRuntime(
                RuntimeContentKinds.Collectible,
                new QinglanRuntimeDefinitionDto
                {
                    referenceId0 = topic,
                    referenceId1 = acquireRule,
                    referenceId2 = "qinglan.reward.fallback.spirit_sand",
                    text0 = bodyKey,
                    presentationProfileId = "placeholder.presentation." + id
                });
            M9LocalizationUtility.EnsureContentEntries(
                bodyKey,
                bodyKey + ".description",
                english + " record.",
                "A recoverable old-court archive entry.",
                chinese + "记录。",
                "可追踪获得的旧庭档案条目。");
            return collectible;
        }

        private static TraitAuthoring MetaTrait(
            string file,
            string id,
            string english,
            string chinese,
            string ruleTag,
            string stat,
            float value,
            ModifierOperation operation,
            int priority)
        {
            var trait = LoadOrCreate<TraitAuthoring>(Folder + "/" + file + ".asset");
            Identity(trait, id, "trait.meta", ruleTag);
            trait.Configure(string.IsNullOrEmpty(stat)
                ? Array.Empty<BuildModifierAuthoringData>()
                : new[]
                {
                    new BuildModifierAuthoringData
                    {
                        statId = stat,
                        operation = operation,
                        value = value,
                        priority = priority,
                        stackingGroup = "qinglan.stack." + id
                    }
                });
            Localize(id, english, "A bounded data-driven Meta output.", chinese, "有界、数据驱动的局外装配输出。");
            return trait;
        }

        private static QinglanDefinitionAuthoring Definition(
            string file,
            string id,
            string english,
            string chinese,
            string kind,
            params string[] tags)
        {
            var definition = LoadOrCreate<QinglanDefinitionAuthoring>(Folder + "/" + file + ".asset");
            Identity(definition, id, tags);
            Localize(id, english, "Qinglan Demo " + kind + " content.", chinese, "《剑起青岚》Demo 局外内容。");
            return definition;
        }

        private static string[] OtherTerminals(string current)
        {
            var result = new string[TerminalIds.Length - 1];
            var write = 0;
            for (var index = 0; index < TerminalIds.Length; index++)
                if (!string.Equals(TerminalIds[index], current, StringComparison.Ordinal))
                    result[write++] = TerminalIds[index];
            return result;
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
            var merged = new string[(tags?.Length ?? 0) + 1];
            merged[0] = "content.placeholder";
            if (tags != null) Array.Copy(tags, 0, merged, 1, tags.Length);
            content.ConfigureIdentity(
                id,
                "content." + id + ".name",
                "content." + id + ".description",
                merged);
        }

        private static void AddUnique(List<ContentAuthoringBase> definitions, ContentAuthoringBase value)
        {
            if (value != null && !definitions.Contains(value)) definitions.Add(value);
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
