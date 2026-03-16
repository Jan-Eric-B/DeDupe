using System;

namespace DeDupe.Localization
{
    public sealed class LanguageChangedEventArgs(string previousLanguage, string newLanguage) : EventArgs
    {
        public string PreviousLanguage { get; } = previousLanguage;

        public string NewLanguage { get; } = newLanguage;
    }
}