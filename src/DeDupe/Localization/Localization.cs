using Microsoft.UI.Xaml;
using System;

namespace DeDupe.Localization
{
    public static class Localization
    {
        public static readonly DependencyProperty UidProperty = DependencyProperty.RegisterAttached("Uid", typeof(string), typeof(Localization), new PropertyMetadata(default));

        internal static event EventHandler<DependencyObject>? DependencyObjectUidSet;

        public static string GetUid(DependencyObject dependencyObject)
        {
            return (string)dependencyObject.GetValue(UidProperty);
        }

        public static void SetUid(DependencyObject dependencyObject, string uid)
        {
            dependencyObject.SetValue(UidProperty, uid);
            DependencyObjectUidSet?.Invoke(null, dependencyObject);
        }
    }
}