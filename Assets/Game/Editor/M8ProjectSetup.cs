using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Metadata;
using UnityEngine.Localization.Pseudo;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;

namespace Game.Editor
{
    /// <summary>Creates deterministic M8 Localization settings, locales, and UI tables.</summary>
    public static class M8ProjectSetup
    {
        private const string Root = "Assets/GameAssets/Localization";
        private const string SettingsPath = Root + "/M8LocalizationSettings.asset";
        private const string EnglishPath = Root + "/Locale_en.asset";
        private const string ChinesePath = Root + "/Locale_zh-Hans.asset";
        private const string PseudoPath = Root + "/Locale_qps-ploc.asset";
        private static readonly Regex LocalizationKeyPattern = new Regex(
            "\\\"localized(?:Name|Description)Key\\\"\\s*:\\s*\\\"([^\\\"]+)\\\"",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        [MenuItem("Tools/AzureSword/Setup M8 Save Localization Platform")]
        public static void Run()
        {
            EnsureFolder("Assets/GameAssets", "Localization");
            var settings = LoadOrCreateSettings();
            LocalizationEditorSettings.ActiveLocalizationSettings = settings;
            var english = LoadOrCreateLocale(EnglishPath, "en", "English");
            var chinese = LoadOrCreateLocale(ChinesePath, "zh-Hans", "简体中文");
            var pseudo = LoadOrCreatePseudo(PseudoPath, english);
            AddLocaleIfMissing(english);
            AddLocaleIfMissing(chinese);
            AddLocaleIfMissing(pseudo);

            LocalizationSettings.StartupLocaleSelectors.Clear();
            LocalizationSettings.StartupLocaleSelectors.Add(new SpecificLocaleSelector { LocaleId = english.Identifier });
            LocalizationSettings.ProjectLocale = english;
            LocalizationSettings.InitializeSynchronously = true;
            EditorUtility.SetDirty(settings);

            var locales = new List<Locale> { english, chinese };
            var collection = LocalizationEditorSettings.GetStringTableCollection("UI") ??
                             LocalizationEditorSettings.CreateStringTableCollection("UI", Root, locales);
            var englishTable = collection.GetTable(english.Identifier) as StringTable;
            var chineseTable = collection.GetTable(chinese.Identifier) as StringTable;
            if (englishTable == null) englishTable = collection.AddNewTable(english.Identifier) as StringTable;
            if (chineseTable == null) chineseTable = collection.AddNewTable(chinese.Identifier) as StringTable;

            var entries = CreateUiEntries();
            AddContentKeys(entries);
            foreach (var pair in entries)
            {
                SetEntry(englishTable, pair.Key, pair.Value.English);
                SetEntry(chineseTable, pair.Key, pair.Value.Chinese);
            }
            LocalizationEditorSettings.SetPreloadTableFlag(englishTable, true);
            LocalizationEditorSettings.SetPreloadTableFlag(chineseTable, true);
            EditorUtility.SetDirty(englishTable);
            EditorUtility.SetDirty(chineseTable);
            EditorUtility.SetDirty(collection.SharedData);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[M8 Setup] Unity Localization configured: en, zh-Hans, pseudo; keys=" + entries.Count + ".");
        }

        internal static string[] CollectRequiredKeys()
        {
            var entries = CreateUiEntries();
            AddContentKeys(entries);
            var keys = new string[entries.Count];
            entries.Keys.CopyTo(keys, 0);
            Array.Sort(keys, StringComparer.Ordinal);
            return keys;
        }

        public static void RunFromCommandLine()
        {
            Run();
            EditorApplication.Exit(0);
        }

        private static LocalizationSettings LoadOrCreateSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<LocalizationSettings>(SettingsPath);
            if (settings != null) return settings;
            settings = ScriptableObject.CreateInstance<LocalizationSettings>();
            settings.name = "M8 Localization Settings";
            AssetDatabase.CreateAsset(settings, SettingsPath);
            return settings;
        }

        private static Locale LoadOrCreateLocale(string path, string code, string localeName)
        {
            var locale = AssetDatabase.LoadAssetAtPath<Locale>(path);
            if (locale != null) return locale;
            locale = Locale.CreateLocale(new LocaleIdentifier(code));
            locale.name = "Locale_" + code;
            locale.LocaleName = localeName;
            AssetDatabase.CreateAsset(locale, path);
            return locale;
        }

        private static PseudoLocale LoadOrCreatePseudo(string path, Locale english)
        {
            var locale = AssetDatabase.LoadAssetAtPath<PseudoLocale>(path);
            if (locale != null) return locale;
            locale = PseudoLocale.CreatePseudoLocale();
            locale.Identifier = new LocaleIdentifier("qps-ploc");
            locale.name = "Locale_qps-ploc";
            locale.LocaleName = "Pseudo (Expanded)";
            locale.Metadata.AddMetadata(new FallbackLocale(english));
            AssetDatabase.CreateAsset(locale, path);
            return locale;
        }

        private static void AddLocaleIfMissing(Locale locale)
        {
            var all = LocalizationSettings.AvailableLocales.Locales;
            for (var index = 0; index < all.Count; index++)
                if (all[index] == locale) return;
            LocalizationEditorSettings.AddLocale(locale);
        }

        private static void SetEntry(StringTable table, string key, string value)
        {
            var entry = table.GetEntry(key) ?? table.AddEntry(key, value);
            entry.Value = value;
        }

        private static Dictionary<string, Translation> CreateUiEntries()
        {
            var entries = new Dictionary<string, Translation>(StringComparer.Ordinal);
            Add(entries, "ui.bootstrap.title", "Starting", "正在启动");
            Add(entries, "ui.main_menu.title", "Free World Framework", "自由世界框架");
            Add(entries, "ui.main_menu.start", "Start Test Run", "开始测试局");
            Add(entries, "ui.main_menu.settings", "Settings", "设置");
            Add(entries, "ui.character_select.title", "Select Character", "选择角色");
            Add(entries, "ui.map_select.title", "Select Map", "选择地图");
            Add(entries, "ui.loading.title", "Loading", "加载中");
            Add(entries, "ui.run_hud.title", "Test Run", "测试局");
            Add(entries, "ui.pause.title", "Paused", "已暂停");
            Add(entries, "ui.pause.resume", "Resume", "继续");
            Add(entries, "ui.pause.settings", "Settings", "设置");
            Add(entries, "ui.pause.end_run", "End Run", "结束本局");
            Add(entries, "ui.level_up.title", "Choose Upgrade", "选择升级");
            Add(entries, "ui.level_up.skip", "Skip", "跳过");
            Add(entries, "ui.result.title", "Run Result", "本局结算");
            Add(entries, "ui.result.main_menu", "Main Menu", "返回主菜单");
            Add(entries, "ui.result.reason.completed", "Completed", "已完成");
            Add(entries, "ui.result.reason.defeated", "Defeated", "已失败");
            Add(entries, "ui.result.reason.abandoned", "Abandoned", "已放弃");
            Add(entries, "ui.settings.title", "Settings", "设置");
            Add(entries, "ui.settings.rebind", "Rebind Controls", "重映射按键");
            Add(entries, "ui.settings.deadzone", "Stick Deadzone", "摇杆死区");
            Add(entries, "ui.settings.vibration", "Vibration", "震动");
            Add(entries, "ui.settings.screen_shake", "Screen Shake", "屏幕震动");
            Add(entries, "ui.settings.flash_intensity", "Flash Intensity", "闪光强度");
            Add(entries, "ui.settings.damage_numbers", "Damage Numbers", "伤害数字");
            Add(entries, "ui.settings.auto_aim", "Auto Aim", "自动瞄准");
            Add(entries, "ui.settings.language", "Language", "语言");
            Add(entries, "ui.content_error.title", "Content Error", "内容错误");
            Add(entries, "ui.content_error.main_menu", "Main Menu", "返回主菜单");
            Add(entries, "save.error.not_found", "No previous save was found.", "未找到已有存档。");
            Add(entries, "save.error.cancelled", "Saving was cancelled.", "保存已取消。");
            Add(entries, "save.error.io", "The save file could not be accessed.", "无法访问存档文件。");
            Add(entries, "save.error.invalid_format", "The save format is invalid.", "存档格式无效。");
            Add(entries, "save.error.checksum", "The save checksum is invalid.", "存档校验失败。");
            Add(entries, "save.error.schema_newer", "This save requires a newer game version.", "该存档需要更新版本的游戏。");
            Add(entries, "save.error.migration_missing", "This save cannot be migrated.", "该存档无法迁移。");
            Add(entries, "save.error.recovery_missing_content", "This run cannot resume because content is missing.", "局内恢复所需内容缺失，无法继续。");
            Add(entries, "save.warning.recovered_backup", "The previous save backup was restored.", "已恢复上一份存档备份。");
            Add(entries, "save.warning.missing_unlock", "An unavailable unlock was retained for diagnostics.", "已保留缺失解锁项用于诊断。");
            Add(entries, "platform.unavailable", "Platform services are unavailable; local play remains enabled.", "平台服务不可用；仍可进行本地游戏。");
            Add(entries, "platform.cancelled", "The platform operation was cancelled.", "平台操作已取消。");
            Add(entries, "platform.failed", "The platform operation failed; local play remains enabled.", "平台操作失败；仍可进行本地游戏。");
            AddQinglanDemoUiEntries(entries);
            return entries;
        }

        private static void AddQinglanDemoUiEntries(Dictionary<string, Translation> entries)
        {
            Add(entries, "ui.common.back", "Back", "返回");
            Add(entries, "ui.common.close", "Close", "关闭");
            Add(entries, "ui.common.on", "On", "开启");
            Add(entries, "ui.common.off", "Off", "关闭");
            Add(entries, "ui.qinglan.title.name", "Sword Rises in Qinglan", "剑起青岚");
            Add(entries, "ui.qinglan.title.subtitle", "The old court waits for the wind to return.", "旧庭待风归，青岚自此起。");
            Add(entries, "ui.qinglan.title.start", "Enter the Old Court", "进入旧庭");
            Add(entries, "ui.qinglan.title.start.description", "Select Lu Qingye and prepare this run.", "选择陆青野并整备本次出行。");
            Add(entries, "ui.qinglan.profile.ready", "Local profile ready", "本地档案已就绪");
            Add(entries, "ui.qinglan.character_select.title", "Choose a Sword Bearer", "选择御剑者");
            Add(entries, "ui.qinglan.character_select.subtitle", "The Demo currently follows Lu Qingye.", "Demo 当前跟随陆青野的旧庭之行。");
            Add(entries, "ui.qinglan.map_select.title", "Choose a Realm", "选择境域");
            Add(entries, "ui.qinglan.map_select.subtitle", "Objectives in the Old Court reshape the final oath.", "旧庭目标会改变最终古誓。");
            Add(entries, "ui.qinglan.loadout.title", "Set the Mountain-Vein Loadout", "整备山河脉装配");
            Add(entries, "ui.qinglan.loadout.subtitle", "Only the saved, validated loadout enters the run.", "仅已保存并通过校验的装配进入本局。");
            Add(entries, "ui.qinglan.loadout.depart", "Ride the Wind", "乘风出发");
            Add(entries, "ui.qinglan.loadout.depart.description", "Load the Old Court and create one owned run.", "加载旧庭并创建唯一局内生命周期。");
            Add(entries, "ui.qinglan.loading.title", "Opening the Old Court", "正在开启旧庭");
            Add(entries, "ui.qinglan.loading.subtitle", "Validating content and assembling the run.", "正在校验内容并装配本局。");
            Add(entries, "ui.qinglan.ending.title", "The Wind Settles", "风息归卷");
            Add(entries, "ui.qinglan.ending.subtitle", "Freezing the immutable run result.", "正在冻结不可变结算结果。");
            Add(entries, "ui.qinglan.run_hud.title", "Old Court", "旧庭");
            Add(entries, "ui.qinglan.map_overlay.title", "Old Court Map", "旧庭舆图");
            Add(entries, "ui.qinglan.map_overlay.hint", "Shape and progress remain readable without color.", "目标以形状与进度双重标识，不依赖颜色。");
            Add(entries, "ui.qinglan.hud.vitals", "VITALS", "生息");
            Add(entries, "ui.qinglan.hud.run", "RUN", "行程");
            Add(entries, "ui.qinglan.hud.windride", "Windride", "乘风");
            Add(entries, "ui.qinglan.hud.build", "BUILD", "构筑");
            Add(entries, "ui.qinglan.hud.map", "OBJECTIVES", "目标");
            Add(entries, "ui.qinglan.level_up.title", "Choose a New Sword Path", "选择新的剑路");
            Add(entries, "ui.qinglan.level_up.subtitle", "Cards describe behavior, level change, and build relation.", "卡牌说明行为、等级变化与当前构筑关系。");
            Add(entries, "ui.qinglan.level_up.reroll", "Reroll", "重掷");
            Add(entries, "ui.qinglan.level_up.reroll.description", "Replace this frozen ordinary offer set.", "替换本次已冻结的普通候选。");
            Add(entries, "ui.qinglan.level_up.skip", "Skip", "跳过");
            Add(entries, "ui.qinglan.level_up.skip.description", "Consume this level-up without a selection.", "消耗本次升级并不作选择。");
            Add(entries, "ui.qinglan.reward.title", "Choose a Manifestation or Relic", "选择显化或奇物");
            Add(entries, "ui.qinglan.reward.subtitle", "Eligibility and conflicts were resolved before this page opened.", "资格与冲突已在页面打开前由玩法真值层确定。");
            Add(entries, "ui.qinglan.reward.fallback", "No valid card remains; the deterministic fallback was granted.", "没有合格候选，已发放确定性保底。");
            Add(entries, "ui.qinglan.pause.title", "Wind Paused", "风行暂歇");
            Add(entries, "ui.qinglan.pause.subtitle", "Simulation is paused; interface input remains active.", "模拟已暂停，界面输入仍可使用。");
            Add(entries, "ui.qinglan.pause.resume", "Return to the Wind", "重归风行");
            Add(entries, "ui.qinglan.pause.abandon", "Leave This Run", "离开本局");
            Add(entries, "ui.qinglan.pause.abandon.description", "Keep only rewards permitted for an abandoned run.", "仅保留规则允许在放弃时结算的奖励。");
            Add(entries, "ui.qinglan.settings.title", "Settings and Accessibility", "设置与可访问性");
            Add(entries, "ui.qinglan.settings.description", "All changes use one saved Settings owner.", "所有改动由同一个 Settings 存档所有者保存。");
            Add(entries, "ui.qinglan.settings.font_scale", "Font Size", "字体大小");
            Add(entries, "ui.qinglan.settings.color_vision", "Color Distinction", "色觉区分");
            Add(entries, "ui.qinglan.settings.master_volume", "Master Volume", "主音量");
            Add(entries, "ui.qinglan.settings.music_volume", "Music Volume", "音乐音量");
            Add(entries, "ui.qinglan.settings.ambience_volume", "Ambience Volume", "环境音量");
            Add(entries, "ui.qinglan.settings.effects_volume", "Effects Volume", "音效音量");
            Add(entries, "ui.qinglan.settings.subtitles", "Subtitles", "字幕");
            Add(entries, "ui.qinglan.loadout.confirm.title", "Apply Loadout?", "确认应用配置？");
            Add(entries, "ui.qinglan.loadout.confirm.description", "This replaces the active branch and insert selection after validation.", "验证通过后将替换当前行脉与嵌片配置。");
            Add(entries, "ui.qinglan.loadout.confirm.apply", "Confirm Apply", "确认应用");
            Add(entries, "ui.qinglan.card.tag.skill", "Weapon", "武器");
            Add(entries, "ui.qinglan.card.tag.passive", "Mind Art", "心诀");
            Add(entries, "ui.qinglan.card.tag.evolution", "Evolution", "显化");
            Add(entries, "ui.qinglan.card.tag.relic", "Relic", "奇物");
            Add(entries, "ui.qinglan.card.tag.reward", "Reward", "奖励");
            Add(entries, "ui.qinglan.card.relation.new", "New build branch", "新增构筑分支");
            Add(entries, "ui.qinglan.card.relation.upgrade", "Upgrades current build", "强化当前构筑");
            Add(entries, "ui.qinglan.card.relation.reward", "Run reward", "本局奖励");
            Add(entries, "ui.qinglan.card.evolution.ready", "Evolution requirements met", "显化条件已满足");
            Add(entries, "ui.qinglan.rebind.invalid", "The requested binding is invalid.", "请求的按键绑定无效。");
            Add(entries, "ui.qinglan.rebind.conflict", "That control is already bound; choose another.", "该控制已被占用，请选择其他按键。");
            Add(entries, "ui.qinglan.rebind.applied", "Binding saved.", "按键绑定已保存。");
            Add(entries, "ui.qinglan.result.victory", "The Old Oath Is Quiet", "古誓暂息");
            Add(entries, "ui.qinglan.result.defeat", "The Wind Has Fallen", "风行已折");
            Add(entries, "ui.qinglan.result.abandoned", "Returned Before the Oath", "未竟古誓而返");
            Add(entries, "ui.qinglan.result.recovery_rejected", "Incomplete Record Rejected", "未完成记录已拒绝");
            Add(entries, "ui.qinglan.result.subtitle", "The frozen result is committed before this page can close.", "冻结结果完成持久化后，本页才可离开。");
            Add(entries, "ui.qinglan.result.saving", "Saving…", "正在保存……");
            Add(entries, "ui.qinglan.result.saved", "Saved", "已保存");
            Add(entries, "ui.qinglan.result.not_saved", "Not saved", "未保存");
            Add(entries, "ui.qinglan.result.retry_save", "Retry Save", "重试保存");
            Add(entries, "ui.qinglan.result.continue_hub", "Return to the Mountain Gate", "返回山门");
            Add(entries, "ui.qinglan.result.continue_hub.description", "Available only after Profile save and Recovery cleanup.", "仅在 Profile 保存并清理恢复标记后可用。");
            Add(entries, "ui.qinglan.hub.title", "Qinglan Mountain Gate", "青岚山门");
            Add(entries, "ui.qinglan.hub.subtitle", "Four facilities consume the same permanent profile.", "四处设施共同读取唯一永久档案。");
            Add(entries, "ui.qinglan.hub.spirit_sand", "Spirit Sand", "灵砂");
            Add(entries, "ui.qinglan.hub.locked", "Locked", "尚未开放");
            Add(entries, "ui.qinglan.hub.start_again", "Set Out Again", "再次出发");
            Add(entries, "ui.qinglan.hub.return_title", "Return to Title", "返回标题");
            Add(entries, "ui.qinglan.meta.owned", "Owned", "已持有");
            Add(entries, "ui.qinglan.meta.equipped", "Equipped", "已装配");
            Add(entries, "ui.qinglan.meta.saved", "Loadout saved", "装配已保存");
            Add(entries, "ui.qinglan.meta.apply_loadout", "Confirm Free Reset", "确认免费重置");
            Add(entries, "ui.qinglan.meta.apply_loadout.description", "The Profile owner validates capacity, prerequisites, and conflicts.", "由 Profile 所有者校验容量、前置与互斥。");
            Add(entries, "ui.qinglan.story.locked", "This memory has not awakened.", "这段记忆尚未苏醒。");
            Add(entries, "ui.qinglan.story.page", "Story page", "故事页");
            Add(entries, "ui.qinglan.collection.hint", "Explore landmarks and stories to reveal this record.", "探索地标与故事以揭示这份藏录。");
            Add(entries, "ui.qinglan.collection.collected", "Collected", "已收录");
            Add(entries, "ui.qinglan.collection.unknown", "Unknown", "未知");
            Add(entries, "ui.qinglan.content_error.code", "Stable error code", "稳定错误码");
            Add(entries, "ui.qinglan.content.unknown", "Unavailable content", "不可用内容");
            Add(entries, "ui.qinglan.accessibility.danger_legend", "▲ DANGER  ·  ≫ DIRECTION  ·  !!! IMPACT\nWarnings retain shape, direction, boundary, and sound when color, flash, vibration, or damage numbers are reduced.", "▲ 高危  ·  ≫ 方向  ·  !!! 冲击\n降低色彩、闪光、震动或关闭伤害数字后，预警仍保留形状、方向、边界与声音。");
            Add(entries, "save.error.write_failed", "The file could not be saved.", "文件未能保存。");
        }

        private static void AddContentKeys(Dictionary<string, Translation> entries)
        {
            var root = Path.Combine(UnityEngine.Application.dataPath, "GameAssets", "Placeholder");
            if (!Directory.Exists(root)) return;
            var files = Directory.GetFiles(root, "*.baked.json", SearchOption.AllDirectories);
            for (var fileIndex = 0; fileIndex < files.Length; fileIndex++)
            {
                var matches = LocalizationKeyPattern.Matches(File.ReadAllText(files[fileIndex]));
                for (var matchIndex = 0; matchIndex < matches.Count; matchIndex++)
                {
                    var key = matches[matchIndex].Groups[1].Value;
                    if (entries.ContainsKey(key)) continue;
                    var label = HumanizeContentKey(key);
                    Add(entries, key, "[Placeholder] " + label, "[占位] " + label);
                }
            }
        }

        private static string HumanizeContentKey(string key)
        {
            var parts = key.Split('.');
            var subject = parts.Length >= 2 ? parts[parts.Length - 2] : key;
            subject = subject.Replace('_', ' ');
            return key.EndsWith(".description", StringComparison.Ordinal) ? subject + " description" : subject;
        }

        private static void Add(Dictionary<string, Translation> entries, string key, string english, string chinese) =>
            entries[key] = new Translation(english, chinese);

        private static void EnsureFolder(string parent, string name)
        {
            var path = parent + "/" + name;
            if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, name);
        }

        private readonly struct Translation
        {
            public Translation(string english, string chinese) { English = english; Chinese = chinese; }
            public string English { get; }
            public string Chinese { get; }
        }
    }
}
