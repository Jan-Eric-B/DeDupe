using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Streams;

namespace DeDupe.Controls
{
    public sealed partial class StackedImagesControl : UserControl
    {
        public static readonly DependencyProperty ImagePathsProperty =
            DependencyProperty.Register(
                nameof(ImagePaths),
                typeof(List<string>),
                typeof(StackedImagesControl),
                new PropertyMetadata(null, OnImagePathsChanged));

        public List<string> ImagePaths
        {
            get => (List<string>)GetValue(ImagePathsProperty);
            set => SetValue(ImagePathsProperty, value);
        }

        public StackedImagesControl()
        {
            InitializeComponent();
        }

        private static void OnImagePathsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is StackedImagesControl control)
            {
                control.UpdateStackedImages();
            }
        }

        private void UpdateStackedImages()
        {
            RootGrid.Children.Clear();

            if (ImagePaths == null || ImagePaths.Count == 0)
            {
                // No images
                ShowPlaceholder();
                return;
            }

            // 4 images for display
            List<string> imagesToShow = [.. ImagePaths.Take(4)];
            int imageCount = imagesToShow.Count;

            // Calculate stacking offsets
            for (int i = imageCount - 1; i >= 0; i--)
            {
                CreateStackedImage(imagesToShow[i], i, imageCount);
            }
        }

        private void CreateStackedImage(string imagePath, int index, int totalCount)
        {
            // Add shadow and offset
            Border imageBorder = new()
            {
                CornerRadius = new CornerRadius(4),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                Shadow = new ThemeShadow()
            };

            // Calculate offset based on position in stack
            double offsetX = (totalCount - 1 - index) * 16;
            double offsetY = (totalCount - 1 - index) * 12;

            // Margin for stacked effect
            imageBorder.Margin = new Thickness(offsetX, offsetY, -offsetX, -offsetY);

            Image image = new()
            {
                Stretch = Stretch.UniformToFill,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            // Load image thumbnail
            _ = LoadImageAsync(image, imagePath);

            imageBorder.Child = image;

            // Add to grid (back to front)
            RootGrid.Children.Add(imageBorder);
        }

        private static async Task LoadImageAsync(Image imageControl, string imagePath)
        {
            try
            {
                // Load image
                StorageFile file = await StorageFile.GetFileFromPathAsync(imagePath);

                using IRandomAccessStreamWithContentType stream = await file.OpenReadAsync();

                // Create BitmapImage thumbnail
                BitmapImage bitmap = new()
                {
                    DecodePixelWidth = 280, // Matching card width
                    DecodePixelType = DecodePixelType.Logical
                };

                await bitmap.SetSourceAsync(stream);
                imageControl.Source = bitmap;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load image: {imagePath}, Error: {ex.Message}");
            }
        }

        private void ShowPlaceholder()
        {
            Border placeholderBorder = new()
            {
                Background = (SolidColorBrush)Application.Current.Resources["LayerFillColorDefaultBrush"],
                CornerRadius = new CornerRadius(4),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };

            FontIcon placeholderIcon = new()
            {
                Glyph = "\uEB9F", // Image icon
                FontSize = 64,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = (SolidColorBrush)Application.Current.Resources["TextFillColorTertiaryBrush"]
            };

            placeholderBorder.Child = placeholderIcon;
            RootGrid.Children.Add(placeholderBorder);
        }
    }
}