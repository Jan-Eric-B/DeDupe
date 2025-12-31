using Microsoft.UI.Xaml.Data;
using System;

namespace DeDupe.Converters
{
    public partial class SimilarityToPercentageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is double similarity)
            {
                // Format as percentage with 1 decimal place
                return $"{similarity * 100:F1}%";
            }
            return "0.0%";
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}