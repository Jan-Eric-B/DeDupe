using System;
using System.ComponentModel;
using System.Reflection;

namespace DeDupe.Helper
{
    public static class EnumExtensions
    {
        public static string GetDescription(this Enum value)
        {
            FieldInfo? field = value.GetType().GetField(value.ToString());
            DescriptionAttribute? attribute = field?.GetCustomAttribute<DescriptionAttribute>();

            return attribute?.Description ?? value.ToString();
        }

        public static string GetEnumDescription(Enum value) => value.GetDescription();
    }
}