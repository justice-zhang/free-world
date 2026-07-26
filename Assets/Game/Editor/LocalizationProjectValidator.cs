using System;
using UnityEditor.Localization;
using UnityEngine.Localization.Pseudo;
using UnityEngine.Localization.Tables;

namespace Game.Editor
{
    /// <summary>Validates the checked-in M8 locales and every required localization key.</summary>
    internal static class LocalizationProjectValidator
    {
        internal static void AppendCurrentProject(ValidationReport report)
        {
            if (report == null) throw new ArgumentNullException(nameof(report));
            var settings = LocalizationEditorSettings.ActiveLocalizationSettings;
            if (settings == null)
            {
                report.Add("M8-LOCALIZATION-SETTINGS", "No active Localization Settings asset is configured.");
                return;
            }

            var englishLocale = FindLocale("en");
            var chineseLocale = FindLocale("zh-Hans");
            var pseudoFound = LocalizationEditorSettings.GetPseudoLocales().Count > 0;
            if (englishLocale == null) report.Add("M8-LOCALE-EN", "English locale 'en' is missing.");
            if (chineseLocale == null) report.Add("M8-LOCALE-ZH-HANS", "Simplified Chinese locale 'zh-Hans' is missing.");
            if (!pseudoFound) report.Add("M8-LOCALE-PSEUDO", "A pseudo locale is missing.");

            var collection = LocalizationEditorSettings.GetStringTableCollection("UI");
            if (collection == null)
            {
                report.Add("M8-LOCALIZATION-TABLE", "The UI string table collection is missing.");
                return;
            }
            var english = englishLocale == null ? null : collection.GetTable(englishLocale.Identifier) as StringTable;
            var chinese = chineseLocale == null ? null : collection.GetTable(chineseLocale.Identifier) as StringTable;
            if (english == null) report.Add("M8-LOCALIZATION-TABLE-EN", "The English UI string table is missing.");
            if (chinese == null) report.Add("M8-LOCALIZATION-TABLE-ZH-HANS", "The Simplified Chinese UI string table is missing.");
            if (english == null || chinese == null) return;

            var keys = M8ProjectSetup.CollectRequiredKeys();
            for (var index = 0; index < keys.Length; index++)
            {
                ValidateEntry(english, keys[index], "en", report);
                ValidateEntry(chinese, keys[index], "zh-Hans", report);
            }
        }

        private static UnityEngine.Localization.Locale FindLocale(string code)
        {
            var locales = LocalizationEditorSettings.GetLocales();
            for (var index = 0; index < locales.Count; index++)
            {
                var locale = locales[index];
                if (!(locale is PseudoLocale) && string.Equals(locale.Identifier.Code, code, StringComparison.OrdinalIgnoreCase))
                    return locale;
            }
            return null;
        }

        private static void ValidateEntry(StringTable table, string key, string locale, ValidationReport report)
        {
            var entry = table.GetEntry(key);
            if (entry == null || string.IsNullOrWhiteSpace(entry.Value))
                report.Add("M8-LOCALIZATION-KEY", locale + " is missing non-empty UI entry '" + key + "'.");
        }
    }
}
