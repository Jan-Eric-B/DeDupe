using DeDupe.Helper;
using Microsoft.UI.Xaml.Data;
using System;

namespace DeDupe.Converters
{
    public partial class EnumDescriptionConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is Enum enumValue)
            {
                return enumValue.GetDescription();
            }
            return value?.ToString() ?? string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}