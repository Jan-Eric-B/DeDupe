using System;
using System.Collections.Generic;

namespace DeDupe.Localization
{
    public interface ILocalizer
    {
        event EventHandler<LanguageChangedEventArgs>? LanguageChanged;

        IEnumerable<string> GetAvailableLanguages();

        string GetCurrentLanguage();

        bool SetLanguage(string language);

        string GetLocalizedString(string uid);

        IEnumerable<string> GetLocalizedStrings(string uid);

        LanguageDictionary GetCurrentLanguageDictionary();

        IEnumerable<LanguageDictionary> GetLanguageDictionaries();
    }
}