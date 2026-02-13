using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using System;

namespace DeDupe.Converters
{
    public sealed class UrlHostToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is not string url || parameter is not string host)
            {
                return Visibility.Collapsed;
            }

            return url.Contains(host, StringComparison.OrdinalIgnoreCase) ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
            => throw new NotImplementedException();
    }
}