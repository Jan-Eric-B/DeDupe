using System;
using System.Collections.Generic;

namespace DeDupe.Localization
{
    public class NullLocalizer : ILocalizer
    {
        private NullLocalizer()
        {
        }

        public static ILocalizer Instance { get; } = new NullLocalizer();

        public event EventHandler<LanguageChangedEventArgs>? LanguageChanged;

        public IEnumerable<string> GetAvailableLanguages() => [];

        public string GetCurrentLanguage() => string.Empty;

        public bool SetLanguage(string language) => false;

        public string GetLocalizedString(string uid) => uid;

        public IEnumerable<string> GetLocalizedStrings(string uid) => [uid];

        public LanguageDictionary GetCurrentLanguageDictionary() => new("");

        public IEnumerable<LanguageDictionary> GetLanguageDictionaries() => [];
    }
}