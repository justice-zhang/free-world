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
            return entries;
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
