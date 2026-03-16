using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace DeDupe.Localization
{
    public sealed partial class Localizer : ILocalizer
    {
        private readonly Lock sync = new();

        private readonly LocalizedElementTracker dependencyObjectTracker = new();

        private readonly Dictionary<string, LanguageDictionary> languageDictionaries = [];

        private readonly List<LocalizationActions.LocalizationAction> localizationActions = [];

        private readonly Dictionary<string, DependencyProperty?> resolvedAttachedPropertyCache = [];

        private static Localizer? _xamlInstance;

        public event EventHandler<LanguageChangedEventArgs>? LanguageChanged;

        internal Localizer(bool disableDefaultLocalizationActions)
        {
            if (!disableDefaultLocalizationActions)
            {
                localizationActions = LocalizationActions.DefaultActions;
            }

            Localization.DependencyObjectUidSet += Localization_DependencyObjectUidSet;
        }

        private static ILogger Logger { get; set; } = NullLogger.Instance;

        private LanguageDictionary CurrentDictionary { get; set; } = new("");

        internal static void SetXamlInstance(Localizer localizer) => _xamlInstance = localizer;

        public IEnumerable<string> GetAvailableLanguages()
        {
            return [.. languageDictionaries.Values.Select(x => x.Language)];
        }

        public string GetCurrentLanguage() => CurrentDictionary.Language;

        public bool SetLanguage(string language)
        {
            string previousLanguage = CurrentDictionary.Language;

            if (languageDictionaries.TryGetValue(language, out LanguageDictionary? dictionary) && dictionary is not null)
            {
                CurrentDictionary = dictionary;
            }
            else
            {
                LogLanguageNotFound(Logger, language);
                return false;
            }

            LocalizeDependencyObjects();
            OnLanguageChanged(previousLanguage, CurrentDictionary.Language);
            return true;
        }

        public string GetLocalizedString(string uid)
        {
            lock (sync)
            {
                if (CurrentDictionary.TryGetEntries(uid, out List<LanguageDictionary.Entry>? items) && items.LastOrDefault() is LanguageDictionary.Entry item)
                {
                    return item.Value;
                }

                return string.Empty;
            }
        }

        public IEnumerable<string> GetLocalizedStrings(string uid)
        {
            lock (sync)
            {
                if (CurrentDictionary.TryGetEntries(uid, out List<LanguageDictionary.Entry>? items))
                {
                    return [.. items.Select(x => x.Value)];
                }

                return [];
            }
        }

        public LanguageDictionary GetCurrentLanguageDictionary() => CurrentDictionary;

        public IEnumerable<LanguageDictionary> GetLanguageDictionaries() => this.languageDictionaries.Values;

        internal static void SetLogger(ILogger logger) => Logger = logger;

        internal void AddLanguageDictionary(LanguageDictionary languageDictionary)
        {
            if (languageDictionaries.TryGetValue(languageDictionary.Language, out LanguageDictionary? targetDictionary))
            {
                int previousItemsCount = targetDictionary.GetEntryCount();

                foreach (LanguageDictionary.Entry item in languageDictionary.GetEntries())
                {
                    targetDictionary.AddEntry(item);
                }

                LogMergedDictionaries(Logger, targetDictionary.Language, previousItemsCount, targetDictionary.GetEntryCount());
                return;
            }

            languageDictionaries.Add(languageDictionary.Language, languageDictionary);
            LogAddedNewDictionary(Logger, languageDictionary.Language, languageDictionary.GetEntryCount());
        }

        internal void AddLocalizationAction(LocalizationActions.LocalizationAction item)
        {
            localizationActions.Add(item);
        }

        internal void PreResolveAttachedProperties()
        {
            foreach (LanguageDictionary dictionary in languageDictionaries.Values)
            {
                foreach (LanguageDictionary.Entry entry in dictionary.GetEntries())
                {
                    string dpName = entry.DependencyPropertyName;

                    if (string.IsNullOrEmpty(dpName) ||
                        !dpName.Contains('.') ||
                        resolvedAttachedPropertyCache.ContainsKey(dpName))
                    {
                        continue;
                    }

                    DependencyProperty? resolved = ResolveAttachedProperty(dpName);
                    resolvedAttachedPropertyCache[dpName] = resolved;

                    if (resolved is null)
                    {
                        LogUnresolvedAttachedProperty(Logger, dpName, entry.Uid);
                    }
                    else
                    {
                        LogResolvedAttachedProperty(Logger, dpName, entry.Uid);
                    }
                }
            }

            LogAttachedPropertyCacheBuilt(Logger, resolvedAttachedPropertyCache.Count);
        }

        internal void RegisterDependencyObject(DependencyObject dependencyObject)
        {
            dependencyObjectTracker.Add(dependencyObject);
            LocalizeDependencyObject(dependencyObject);
        }

        private static void Localization_DependencyObjectUidSet(object? sender, DependencyObject dependencyObject)
        {
            _xamlInstance?.RegisterDependencyObject(dependencyObject);
        }

        private DependencyProperty? GetDependencyProperty(DependencyObject dependencyObject, string dependencyPropertyName)
        {
            Type type = dependencyObject.GetType();

            if (type.GetProperty(dependencyPropertyName, BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy) is PropertyInfo propertyInfo && propertyInfo.GetValue(null) is DependencyProperty property)
            {
                return property;
            }

            if (type.GetField(dependencyPropertyName, BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy) is FieldInfo fieldInfo && fieldInfo.GetValue(null) is DependencyProperty field)
            {
                return field;
            }

            if (resolvedAttachedPropertyCache.TryGetValue(dependencyPropertyName, out DependencyProperty? cached))
            {
                return cached;
            }

            return null;
        }

        private static DependencyProperty? ResolveAttachedProperty(string dependencyPropertyName)
        {
            if (dependencyPropertyName.Split('.') is not { Length: 2 } splitResult)
            {
                return null;
            }

            string attachedPropertyClassName = splitResult[0];
            string attachedPropertyName = splitResult[1];

            IEnumerable<Type> types = GetTypesFromName(attachedPropertyClassName);

            foreach (Type type in types)
            {
                if (type.GetProperty(attachedPropertyName, BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy) is PropertyInfo propertyInfo &&
                    propertyInfo.GetValue(null) is DependencyProperty dependencyProperty)
                {
                    return dependencyProperty;
                }

                if (type.GetField(attachedPropertyName, BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy) is FieldInfo fieldInfo &&
                    fieldInfo.GetValue(null) is DependencyProperty fieldDependencyProperty)
                {
                    return fieldDependencyProperty;
                }
            }

            return null;
        }

        private static readonly Dictionary<string, Type[]> resolvedTypeCache = [];

        private static Type[] GetTypesFromName(string name)
        {
            if (resolvedTypeCache.TryGetValue(name, out Type[]? cached))
            {
                return cached;
            }

            Type[] types = [.. AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(assembly =>
                {
                    try { return assembly.GetTypes(); }
                    catch (ReflectionTypeLoadException ex) { return ex.Types.OfType<Type>(); }
                })
                .Where(x => x.Name == name)];

            resolvedTypeCache[name] = types;
            return types;
        }

        private void LocalizeDependencyObjects()
        {
            foreach (DependencyObject dependencyObject in this.dependencyObjectTracker.GetDependencyObjects())
            {
                LocalizeDependencyObject(dependencyObject);
            }
        }

        private void LocalizeDependencyObject(DependencyObject dependencyObject)
        {
            if (Localization.GetUid(dependencyObject) is not string uidSource ||
                string.IsNullOrEmpty(uidSource))
            {
                LogDependencyObjectMissingUid(Logger, dependencyObject.GetType());
                return;
            }

            string uid = uidSource;
            string? uidDependencyPropertyName = null;

            if (uidSource.Split('.') is { Length: 2 } splitResult)
            {
                uid = splitResult[0];
                uidDependencyPropertyName = splitResult[1] + "Property";
            }

            if (!CurrentDictionary.TryGetEntries(uid, out List<LanguageDictionary.Entry>? items))
            {
                LogDependencyObjectMissingUidInDictionary(Logger, dependencyObject.GetType(), uid);
                return;
            }

            foreach (LanguageDictionary.Entry item in items)
            {
                LocalizeDependencyObject(dependencyObject, uidDependencyPropertyName ?? item.DependencyPropertyName, item.Value);
            }
        }

        private void LocalizeDependencyObject(DependencyObject dependencyObject, string dependencyPropertyName, string value)
        {
            if (GetDependencyProperty(
                dependencyObject,
                dependencyPropertyName) is DependencyProperty dependencyProperty)
            {
                LocalizeDependencyObjectWithProperty(dependencyObject, dependencyProperty, value);
                return;
            }

            LocalizeDependencyObjectWithoutProperty(dependencyObject, value);
        }

        private static void LocalizeDependencyObjectWithProperty(DependencyObject dependencyObject, DependencyProperty dependencyProperty, string value)
        {
            if (dependencyObject
                .GetValue(dependencyProperty)?
                .GetType() is Type propertyType &&
                propertyType.IsEnum &&
                Enum.TryParse(propertyType, value, out object? enumValue))
            {
                dependencyObject.SetValue(dependencyProperty, enumValue);
                return;
            }

            dependencyObject.SetValue(dependencyProperty, value);
        }

        private void LocalizeDependencyObjectWithoutProperty(DependencyObject dependencyObject, string value)
        {
            foreach (LocalizationActions.LocalizationAction item in this.localizationActions.Where(x => x.TargetType == dependencyObject.GetType()))
            {
                item.Action(new LocalizationActions.LocalizationActionArgs(dependencyObject, value));
            }
        }

        private void OnLanguageChanged(string previousLanguage, string currentLanguage)
        {
            LogLanguageChanged(Logger, previousLanguage, currentLanguage);
            LanguageChanged?.Invoke(this, new LanguageChangedEventArgs(previousLanguage, currentLanguage));
        }

        [LoggerMessage(Level = LogLevel.Warning, Message = "Language not found. [Language: {Language}]")]
        private static partial void LogLanguageNotFound(ILogger logger, string language);

        [LoggerMessage(Level = LogLevel.Information, Message = "Merged dictionaries. [Language: {Language} Items: {PreviousItemsCount} -> {CurrentItemsCount}]")]
        private static partial void LogMergedDictionaries(ILogger logger, string language, int previousItemsCount, int currentItemsCount);

        [LoggerMessage(Level = LogLevel.Information, Message = "Added new dictionary. [Language: {Language} Items: {ItemsCount}]")]
        private static partial void LogAddedNewDictionary(ILogger logger, string language, int itemsCount);

        [LoggerMessage(Level = LogLevel.Trace, Message = "Added DependencyObject. [Type: {Type} Total: {Count}]")]
        private static partial void LogDependencyObjectAdded(ILogger logger, Type type, int count);

        [LoggerMessage(Level = LogLevel.Trace, Message = "Removed DependencyObject. [Type: {Type} Total: {Count}]")]
        private static partial void LogDependencyObjectRemoved(ILogger logger, Type type, int count);

        [LoggerMessage(Level = LogLevel.Warning, Message = "DependencyObject does not have Uid. [Type: {Type}]")]
        private static partial void LogDependencyObjectMissingUid(ILogger logger, Type type);

        [LoggerMessage(Level = LogLevel.Warning, Message = "DependencyObject does not have Uid in the dictionary. [Type: {Type} Uid: {Uid}]")]
        private static partial void LogDependencyObjectMissingUidInDictionary(ILogger logger, Type type, string uid);

        [LoggerMessage(Level = LogLevel.Information, Message = "Changed language. [{PreviousLanguage} -> {CurrentLanguage}]")]
        private static partial void LogLanguageChanged(ILogger logger, string previousLanguage, string currentLanguage);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Could not resolve attached DependencyProperty during build. [Property: {PropertyName} Uid: {Uid}]")]
        private static partial void LogUnresolvedAttachedProperty(ILogger logger, string propertyName, string uid);

        [LoggerMessage(Level = LogLevel.Debug, Message = "Resolved attached DependencyProperty during build. [Property: {PropertyName} Uid: {Uid}]")]
        private static partial void LogResolvedAttachedProperty(ILogger logger, string propertyName, string uid);

        [LoggerMessage(Level = LogLevel.Information, Message = "Attached property cache built. [CachedProperties: {Count}]")]
        private static partial void LogAttachedPropertyCacheBuilt(ILogger logger, int count);
    }
}