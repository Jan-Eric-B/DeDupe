using DeDupe.ViewModels.Pages;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;

namespace DeDupe.Views.Pages
{
    public sealed partial class PreProcessingPage : Page
    {
        public PreProcessingViewModel ViewModel { get; }

        public PreProcessingPage()
        {
            this.InitializeComponent();
            ViewModel = App.Current.GetService<PreProcessingViewModel>();
            this.DataContext = ViewModel;
        }

        private async void PaddingColorButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ColorPicker? colorPicker = new()
                {
                    Color = ViewModel.PaddingColor.Color,
                    ColorSpectrumShape = ColorSpectrumShape.Ring,
                    IsColorSliderVisible = true,
                    IsColorChannelTextInputVisible = true,
                    IsHexInputVisible = true,
                    IsAlphaEnabled = false
                };

                ContentDialog? dialog = new()
                {
                    Title = "Select Padding Color",
                    Content = colorPicker,
                    PrimaryButtonText = "OK",
                    SecondaryButtonText = "Cancel",
                    DefaultButton = ContentDialogButton.Primary,
                    XamlRoot = this.XamlRoot
                };

                ContentDialogResult result = await dialog.ShowAsync();

                if (result == ContentDialogResult.Primary)
                {
                    ViewModel.PaddingColor = new SolidColorBrush(colorPicker.Color);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error showing color picker: {ex.Message}");
            }
        }
    }
}