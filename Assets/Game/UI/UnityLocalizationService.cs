using System;
using UnityEngine.Localization;
using UnityEngine.Localization.Pseudo;
using UnityEngine.Localization.Settings;

namespace Game.UI
{
    /// <summary>Localization boundary used by Presenter and View code.</summary>
    public interface ILocalizationService
    {
        /// <summary>Gets the selected locale code, using pseudo for a PseudoLocale.</summary>
        string SelectedLocaleCode { get; }
        /// <summary>Resolves one stable key through the UI table.</summary>
        string Resolve(string localizationKey);
        /// <summary>Selects a locale by code.</summary>
        bool SelectLocale(string localeCode);
        /// <summary>Selects the next configured locale.</summary>
        bool SelectNextLocale();
    }

    /// <summary>Unity Localization adapter for the checked-in M8 UI string table.</summary>
    public sealed class UnityLocalizationService : ILocalizationService
    {
        /// <summary>Name of the checked-in runtime string table collection.</summary>
        public const string TableName = "UI";
        private static readonly string[] LocaleOrder = { "en", "zh-Hans", "pseudo" };

        public UnityLocalizationService()
        {
            LocalizationSettings.InitializeSynchronously = true;
            LocalizationSettings.InitializationOperation.WaitForCompletion();
        }

        /// <inheritdoc />
        public string SelectedLocaleCode
        {
            get
            {
                var locale = LocalizationSettings.SelectedLocale;
                if (locale is PseudoLocale) return "pseudo";
                return locale == null ? string.Empty : locale.Identifier.Code;
            }
        }

        /// <inheritdoc />
        public string Resolve(string localizationKey)
        {
            if (string.IsNullOrEmpty(localizationKey)) return string.Empty;
            var locale = LocalizationSettings.SelectedLocale;
            if (locale == null) return localizationKey;
            var pseudo = locale as PseudoLocale;
            var lookupLocale = pseudo == null ? locale : FindSourceLocale();
            if (lookupLocale == null) return localizationKey;
            var result = LocalizationSettings.StringDatabase.GetTableEntry(TableName, localizationKey, lookupLocale);
            if (result.Entry == null) return localizationKey;
            var value = result.Entry.GetLocalizedString(null, null, pseudo);
            return string.IsNullOrEmpty(value) ? localizationKey : value;
        }

        /// <inheritdoc />
        public bool SelectLocale(string localeCode)
        {
            if (string.IsNullOrWhiteSpace(localeCode)) return false;
            var locales = LocalizationSettings.AvailableLocales.Locales;
            for (var index = 0; index < locales.Count; index++)
            {
                var locale = locales[index];
                var matches = string.Equals(localeCode, "pseudo", StringComparison.OrdinalIgnoreCase)
                    ? locale is PseudoLocale
                    : !(locale is PseudoLocale) && string.Equals(locale.Identifier.Code, localeCode, StringComparison.OrdinalIgnoreCase);
                if (!matches) continue;
                LocalizationSettings.SelectedLocale = locale;
                return true;
            }
            return false;
        }

        /// <inheritdoc />
        public bool SelectNextLocale()
        {
            var current = SelectedLocaleCode;
            var index = 0;
            for (; index < LocaleOrder.Length; index++)
                if (string.Equals(LocaleOrder[index], current, StringComparison.OrdinalIgnoreCase)) break;
            return SelectLocale(LocaleOrder[(index + 1) % LocaleOrder.Length]);
        }

        private static Locale FindSourceLocale()
        {
            var projectLocale = LocalizationSettings.ProjectLocale;
            if (projectLocale != null && !(projectLocale is PseudoLocale)) return projectLocale;
            var locales = LocalizationSettings.AvailableLocales.Locales;
            for (var index = 0; index < locales.Count; index++)
            {
                var locale = locales[index];
                if (!(locale is PseudoLocale) && string.Equals(locale.Identifier.Code, "en", StringComparison.OrdinalIgnoreCase))
                    return locale;
            }
            return null;
        }
    }
}
