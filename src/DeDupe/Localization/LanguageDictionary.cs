using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace DeDupe.Localization
{
    public class LanguageDictionary(string language)
    {
        public record Entry(string Uid, string DependencyPropertyName, string Value, string ResourceName);

        private readonly Dictionary<string, List<Entry>> dictionary = [];

        public string Language { get; } = language;

        public void AddEntry(Entry entry)
        {
            if (dictionary.TryGetValue(entry.Uid, out List<Entry>? items))
            {
                int existingIndex = items.FindIndex(e => e.DependencyPropertyName == entry.DependencyPropertyName);

                if (existingIndex >= 0)
                {
                    items[existingIndex] = entry;
                }
                else
                {
                    items.Add(entry);
                }
            }
            else
            {
                dictionary[entry.Uid] = [entry];
            }
        }

        public IEnumerable<Entry> GetEntries()
        {
            return dictionary.Values.SelectMany(x => x);
        }

        public int GetEntryCount()
        {
            return dictionary.Values.Sum(x => x.Count);
        }

        public bool TryGetEntries(string uid, [MaybeNullWhen(false)] out List<Entry> entries)
        {
            return dictionary.TryGetValue(uid, out entries);
        }
    }
}