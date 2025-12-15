using Microsoft.UI.Xaml.Data;
using System;

namespace DeDupe.Converters
{
    /// <summary>
    /// Converts similarity value (0.0-1.0) to percentage
    /// </summary>
    public partial class SimilarityToPercentageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is double similarity)
            {
                return $"{similarity * 100:F1}";
            }
            return "0.0";
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}