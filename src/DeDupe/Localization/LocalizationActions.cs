using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Documents;
using System;
using System.Collections.Generic;

namespace DeDupe.Localization
{
    public static class LocalizationActions
    {
        public record LocalizationActionArgs(DependencyObject DependencyObject, string Value);

        public record LocalizationAction(Type TargetType, Action<LocalizationActionArgs> Action);

        public static List<LocalizationAction> DefaultActions { get; } =
        [
            new LocalizationAction(typeof(Run), arguments =>
            {
                if (arguments.DependencyObject is Run target)
                {
                    target.Text = arguments.Value;
                }
            }),
            new LocalizationAction(typeof(Span), arguments =>
            {
                if (arguments.DependencyObject is Span target)
                {
                    target.Inlines.Clear();
                    target.Inlines.Add(new Run() { Text = arguments.Value });
                }
            }),
            new LocalizationAction(typeof(Bold), arguments =>
            {
                if (arguments.DependencyObject is Bold target)
                {
                    target.Inlines.Clear();
                    target.Inlines.Add(new Run() { Text = arguments.Value });
                }
            }),
            new LocalizationAction(typeof(Hyperlink), arguments =>
            {
                if (arguments.DependencyObject is Hyperlink target)
                {
                    target.Inlines.Clear();
                    target.Inlines.Add(new Run() { Text = arguments.Value });
                }
            }),
        ];
    }
}