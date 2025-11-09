using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using System;

namespace DeDupe.Converters
{
    /// <summary>
    /// Converter to negate boolean values for visibility binding
    /// </summary>
    public partial class BoolNegationConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is bool boolValue)
            {
                return boolValue ? Visibility.Collapsed : Visibility.Visible;
            }
            return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}