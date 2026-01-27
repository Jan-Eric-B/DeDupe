using Microsoft.UI.Xaml.Data;
using System;

namespace DeDupe.Converters
{
    /// <summary>
    /// Converts a double value (0-100) to a percentage string like "16.8%"
    /// </summary>
    public partial class PercentageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is double percentage)
            {
                return $"{percentage:F1}%";
            }
            return "0%";
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}