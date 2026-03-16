using DeDupe.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DeDupe.Localization
{
    public static class LocalizerExtensions
    {
        public static IReadOnlyList<LocalizedEnumOption<T>> BuildLocalizedOptions<T>(this ILocalizer localizer) where T : struct, Enum
        {
            string prefix = typeof(T).Name;

            return [.. Enum.GetValues<T>()
                .Select(value =>
                {
                    // Convention: {EnumTypeName}_{MemberName}
                    string key = $"{prefix}_{value}";
                    string display = localizer.GetLocalizedString(key);

                    return new LocalizedEnumOption<T>(
                        value,
                        string.IsNullOrEmpty(display) ? value.GetDescription() : display);
                })];
        }
    }
}