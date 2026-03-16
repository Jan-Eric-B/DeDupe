using System;

namespace DeDupe.Localization
{
    public record LocalizedEnumOption<T>(T Value, string DisplayName) where T : struct, Enum;
}