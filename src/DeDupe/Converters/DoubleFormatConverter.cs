using Microsoft.UI.Xaml.Data;
using System;

namespace DeDupe.Converters
{
    public partial class DoubleFormatConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is double doubleValue)
            {
                int decimals = parameter != null ? int.Parse(parameter.ToString()!) : 2;
                return doubleValue.ToString($"F{decimals}");
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}