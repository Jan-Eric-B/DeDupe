using DeDupe.Models.Ui;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace DeDupe.Controls
{
    public sealed partial class StepperControl : UserControl
    {
        public static readonly DependencyProperty StepsProperty = DependencyProperty.Register(nameof(Steps), typeof(ObservableCollection<StepItem>), typeof(StepperControl), new PropertyMetadata(null, OnStepsChanged));

        public ObservableCollection<StepItem> Steps
        {
            get => (ObservableCollection<StepItem>)GetValue(StepsProperty);
            set => SetValue(StepsProperty, value);
        }

        public event EventHandler<int>? StepClicked;

        public StepperControl()
        {
            InitializeComponent();
        }

        private static void OnStepsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is StepperControl control)
            {
                if (e.OldValue is ObservableCollection<StepItem> oldSteps)
                {
                    oldSteps.CollectionChanged -= control.Steps_CollectionChanged;
                    foreach (StepItem step in oldSteps)
                    {
                        step.PropertyChanged -= control.Step_PropertyChanged;
                    }
                }

                if (e.NewValue is ObservableCollection<StepItem> newSteps)
                {
                    newSteps.CollectionChanged += control.Steps_CollectionChanged;
                    foreach (StepItem step in newSteps)
                    {
                        step.PropertyChanged += control.Step_PropertyChanged;
                    }
                    control.UpdateStepper();
                }
            }
        }

        private void Steps_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (StepItem step in e.NewItems)
                {
                    step.PropertyChanged += Step_PropertyChanged;
                }
            }

            if (e.OldItems != null)
            {
                foreach (StepItem step in e.OldItems)
                {
                    step.PropertyChanged -= Step_PropertyChanged;
                }
            }

            UpdateStepper();
        }

        private void Step_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(StepItem.IsCompleted) || e.PropertyName == nameof(StepItem.IsCurrent) || e.PropertyName == nameof(StepItem.IsEnabled))
            {
                UpdateStepper();
            }
        }

        private void UpdateStepper()
        {
            StepsGrid.Children.Clear();

            if (Steps == null || Steps.Count == 0)
            {
                return;
            }

            for (int i = 0; i < Steps.Count; i++)
            {
                StepItem step = Steps[i];
                int columnIndex = i * 2; // Step + Connector

                // Step button
                Button stepButton = CreateStepButton(step);
                Grid.SetColumn(stepButton, columnIndex);
                StepsGrid.Children.Add(stepButton);

                // Connector
                if (i < Steps.Count - 1)
                {
                    Rectangle connector = CreateConnector(step);
                    Grid.SetColumn(connector, columnIndex + 1);
                    StepsGrid.Children.Add(connector);
                }
            }
        }

        private Button CreateStepButton(StepItem step)
        {
            Button button = new()
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Padding = new Thickness(12, 8, 12, 8),
                CornerRadius = new CornerRadius(8),
                BorderThickness = step.IsCurrent ? new Thickness(2) : new Thickness(1),
                Tag = step.StepNumber,
                IsEnabled = step.IsEnabled
            };

            if (step.IsCurrent)
            {
                button.Background = (SolidColorBrush)Application.Current.Resources["LayerFillColorAltBrush"];
                button.BorderBrush = (SolidColorBrush)Application.Current.Resources["AccentFillColorDefaultBrush"];
            }
            else if (step.IsCompleted || step.IsEnabled)
            {
                button.Background = (SolidColorBrush)Application.Current.Resources["LayerFillColorAltBrush"];
                button.BorderBrush = (SolidColorBrush)Application.Current.Resources["CardStrokeColorDefaultBrush"];
            }
            else
            {
                button.Background = new SolidColorBrush(Colors.Transparent);
                button.BorderBrush = new SolidColorBrush(Colors.Transparent);
            }

            button.Click += (s, e) => StepClicked?.Invoke(this, step.StepNumber);

            // Button content
            StackPanel stackPanel = new()
            {
                HorizontalAlignment = HorizontalAlignment.Center
            };

            // Number
            Border numberBorder = new()
            {
                Width = 28,
                Height = 28,
                Margin = new Thickness(0, 0, 0, 8),
                CornerRadius = new CornerRadius(16)
            };

            if (step.IsCompleted)
            {
                numberBorder.Background = (SolidColorBrush)Application.Current.Resources["AccentFillColorDefaultBrush"];
                FontIcon checkIcon = new()
                {
                    Glyph = "\uE73E",
                    FontSize = 16,
                    Foreground = new SolidColorBrush(Colors.White)
                };
                numberBorder.Child = checkIcon;
            }
            else if (step.IsCurrent)
            {
                numberBorder.Background = (SolidColorBrush)Application.Current.Resources["AccentFillColorDefaultBrush"];
                TextBlock numberText = new()
                {
                    Text = step.StepNumber.ToString(),
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Colors.White),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                numberBorder.Child = numberText;
            }
            else
            {
                numberBorder.Background = (SolidColorBrush)Application.Current.Resources["ControlFillColorDisabledBrush"];
                TextBlock numberText = new()
                {
                    Text = step.StepNumber.ToString(),
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Foreground = (SolidColorBrush)Application.Current.Resources["TextFillColorDisabledBrush"],
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                numberBorder.Child = numberText;
            }

            stackPanel.Children.Add(numberBorder);

            // Title
            TextBlock titleText = new()
            {
                Text = step.Title,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = step.IsEnabled ? (SolidColorBrush)Application.Current.Resources["TextFillColorPrimaryBrush"] : (SolidColorBrush)Application.Current.Resources["TextFillColorDisabledBrush"]
            };
            stackPanel.Children.Add(titleText);

            // Subtitle
            TextBlock subtitleText = new()
            {
                Text = step.Description,
                FontSize = 12,
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = step.IsEnabled ? (SolidColorBrush)Application.Current.Resources["TextFillColorSecondaryBrush"] : (SolidColorBrush)Application.Current.Resources["TextFillColorDisabledBrush"]
            };
            stackPanel.Children.Add(subtitleText);

            button.Content = stackPanel;
            return button;
        }

        private static Rectangle CreateConnector(StepItem step)
        {
            Rectangle connector = new()
            {
                Height = 2,
                Width = 40,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 28, 0, 0),
                Fill = step.IsCompleted ? (SolidColorBrush)Application.Current.Resources["AccentFillColorDefaultBrush"] : (SolidColorBrush)Application.Current.Resources["DividerStrokeColorDefaultBrush"]
            };

            return connector;
        }
    }
}