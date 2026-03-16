using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;

namespace DeDupe.Localization
{
    public class LocalizerBuilder
    {
        private sealed record StringResourceItem(string Name, string Value, string Comment);
        private sealed record StringResourceItems(string Language, IEnumerable<StringResourceItem> Items);

        private readonly List<(string Path, bool IgnoreExceptions)> stringResourcesFolders = [];

        private readonly List<LanguageDictionary> languageDictionaries = [];
        private readonly List<LocalizationActions.LocalizationAction> localizationActions = [];

        private string defaultLanguage = "en-US";
        private bool disableDefaultLocalizationActions;
        private string defaultStringResourcesFileName = "Resources.resw";
        private string stringResourcesFileXPath = "//root/data";
        private ILogger? logger;

        public LocalizerBuilder SetDefaultLanguage(string language)
        {
            defaultLanguage = language;
            return this;
        }

        public LocalizerBuilder SetDisableDefaultLocalizationActions(bool disable)
        {
            disableDefaultLocalizationActions = disable;
            return this;
        }

        public LocalizerBuilder SetDefaultStringResourcesFileName(string fileName)
        {
            defaultStringResourcesFileName = fileName;
            return this;
        }

        public LocalizerBuilder SetStringResourcesFileXPath(string xPath)
        {
            stringResourcesFileXPath = xPath;
            return this;
        }

        public LocalizerBuilder SetLogger(ILogger<Localizer> logger)
        {
            this.logger = logger;
            return this;
        }

        public LocalizerBuilder AddStringResourcesFolderForLanguageDictionaries(
            string stringResourcesFolderPath, bool ignoreExceptions = false)
        {
            stringResourcesFolders.Add((stringResourcesFolderPath, ignoreExceptions));
            return this;
        }

        public LocalizerBuilder AddLanguageDictionary(LanguageDictionary dictionary)
        {
            languageDictionaries.Add(dictionary);
            return this;
        }

        public LocalizerBuilder AddLocalizationAction(LocalizationActions.LocalizationAction item)
        {
            localizationActions.Add(item);
            return this;
        }

        public ILocalizer Build()
        {
            Localizer localizer = new(disableDefaultLocalizationActions);

            if (logger is not null)
            {
                Localizer.SetLogger(logger);
            }

            foreach ((string path, bool ignoreExceptions) in this.stringResourcesFolders)
            {
                foreach (string languageFolderPath in Directory.GetDirectories(path))
                {
                    try
                    {
                        LoadDictionariesFromLanguageFolder(languageFolderPath);
                    }
                    catch (Exception ex) when (ignoreExceptions)
                    {
                        logger?.LogDebug(ex, "Skipped language folder '{Path}' due to exception.", languageFolderPath);
                    }
                }
            }

            foreach (LanguageDictionary dictionary in languageDictionaries)
            {
                localizer.AddLanguageDictionary(dictionary);
            }

            foreach (LocalizationActions.LocalizationAction item in localizationActions)
            {
                localizer.AddLocalizationAction(item);
            }

            localizer.PreResolveAttachedProperties();

            if (!localizer.SetLanguage(defaultLanguage))
            {
                logger?.LogWarning("Default language '{Language}' not found during build.", defaultLanguage);
            }

            Localizer.SetXamlInstance(localizer);
            return localizer;
        }

        private void LoadDictionariesFromLanguageFolder(string languageFolderPath)
        {
            foreach (string filePath in Directory.GetFiles(languageFolderPath, "*.resw"))
            {
                string fileName = Path.GetFileName(filePath);
                string sourceName = fileName == defaultStringResourcesFileName
                    ? string.Empty
                    : Path.GetFileNameWithoutExtension(fileName);

                if (CreateLanguageDictionaryFromStringResourcesFile(sourceName, filePath, stringResourcesFileXPath) is LanguageDictionary dictionary)
                {
                    languageDictionaries.Add(dictionary);
                }
            }
        }

        private static LanguageDictionary? CreateLanguageDictionaryFromStringResourcesFile(string sourceName, string filePath, string fileXPath)
        {
            if (CreateStringResourceItemsFromResourcesFile(
                sourceName,
                filePath,
                fileXPath) is StringResourceItems stringResourceItems)
            {
                return CreateLanguageDictionaryFromStringResourceItems(stringResourceItems);
            }

            return null;
        }

        private static LanguageDictionary CreateLanguageDictionaryFromStringResourceItems(StringResourceItems stringResourceItems)
        {
            LanguageDictionary dictionary = new(stringResourceItems.Language);

            foreach (StringResourceItem stringResourceItem in stringResourceItems.Items)
            {
                LanguageDictionary.Entry item = CreateLanguageDictionaryItem(stringResourceItem);
                dictionary.AddEntry(item);
            }

            return dictionary;
        }

        private static LanguageDictionary.Entry CreateLanguageDictionaryItem(StringResourceItem stringResourceItem) => CreateLanguageDictionaryItem(stringResourceItem.Name, stringResourceItem.Value);

        internal static LanguageDictionary.Entry CreateLanguageDictionaryItem(string name, string value)
        {
            (string Uid, string DependencyPropertyName) = name.IndexOf('.') is int firstSeparatorIndex && firstSeparatorIndex > 1
                ? (name[..firstSeparatorIndex], string.Concat(name.AsSpan(firstSeparatorIndex + 1), "Property"))
                : (name, string.Empty);
            return new LanguageDictionary.Entry(
                Uid,
                DependencyPropertyName,
                value,
                name);
        }

        private static StringResourceItems? CreateStringResourceItemsFromResourcesFile(string sourceName, string filePath, string xPath = "//root/data")
        {
            FileInfo fileInfo = new(filePath);
            if (fileInfo.Directory?.Name is string language)
            {
                XmlDocument document = new();
                document.Load(fileInfo.FullName);

                if (document.SelectNodes(xPath) is XmlNodeList nodeList)
                {
                    List<StringResourceItem> items = [];
                    IEnumerable<StringResourceItem> stringResourceItems = CreateStringResourceItems(sourceName, nodeList);
                    items.AddRange(stringResourceItems);
                    return new StringResourceItems(language, items);
                }
            }

            return null;
        }

        private static IEnumerable<StringResourceItem> CreateStringResourceItems(string sourceName, XmlNodeList nodeList)
        {
            foreach (XmlNode node in nodeList)
            {
                if (CreateStringResourceItem(sourceName, node) is StringResourceItem item)
                {
                    yield return item;
                }
            }
        }

        private static StringResourceItem? CreateStringResourceItem(string sourceName, XmlNode node)
        {
            string prefix = !string.IsNullOrEmpty(sourceName) ? $"/{sourceName}/"
                : string.Empty;

            return new StringResourceItem(
                Name: $"{prefix}{node.Attributes?["name"]?.Value ?? string.Empty}",
                Value: node["value"]?.InnerText ?? string.Empty,
                Comment: string.Empty);
        }
    }
}