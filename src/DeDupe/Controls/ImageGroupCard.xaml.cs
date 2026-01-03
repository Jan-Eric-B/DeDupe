using DeDupe.Models.Analysis;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Streams;

namespace DeDupe.Controls
{
    public sealed partial class ImageGroupCard : UserControl
    {
        #region Properties

        public static readonly DependencyProperty GroupProperty = DependencyProperty.Register(nameof(Group), typeof(SimilarityGroup), typeof(ImageGroupCard), new PropertyMetadata(null, OnGroupChanged));

        public SimilarityGroup? Group
        {
            get => (SimilarityGroup?)GetValue(GroupProperty);
            set => SetValue(GroupProperty, value);
        }

        #endregion Properties

        #region Constructor

        public ImageGroupCard()
        {
            InitializeComponent();
        }

        #endregion Constructor

        private static void OnGroupChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ImageGroupCard control)
            {
                control.UpdateStackedImages();
            }
        }

        private void UpdateStackedImages()
        {
            StackedThumbnails.Children.Clear();

            if (Group == null || Group.Count == 0)
            {
                ShowPlaceholder();
                return;
            }

            // 4 images for display
            List<string> imagesToShow = [.. Group.GetImageThumbnailPaths().Take(4)];
            int imageCount = imagesToShow.Count;

            // Calculate stacking offsets
            for (int i = imageCount - 1; i >= 0; i--)
            {
                CreateStackedImage(imagesToShow[i], i, imageCount);
            }
        }

        private void CreateStackedImage(string imagePath, int index, int totalCount)
        {
            Border imageBorder = new()
            {
                CornerRadius = new CornerRadius(4),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                Shadow = new ThemeShadow()
            };

            // Offset based on position in stack
            double offsetX = (totalCount - 1 - index) * 16;
            double offsetY = (totalCount - 1 - index) * 12;
            imageBorder.Margin = new Thickness(offsetX, offsetY, -offsetX, -offsetY);

            Image image = new()
            {
                Stretch = Stretch.UniformToFill,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            _ = LoadImageAsync(image, imagePath);

            imageBorder.Child = image;

            // Add to grid (back to front)
            StackedThumbnails.Children.Add(imageBorder);
        }

        private static async Task LoadImageAsync(Image imageControl, string imagePath)
        {
            try
            {
                StorageFile file = await StorageFile.GetFileFromPathAsync(imagePath);

                using IRandomAccessStreamWithContentType stream = await file.OpenReadAsync();

                // Create BitmapImage thumbnail
                BitmapImage bitmap = new()
                {
                    DecodePixelWidth = 280, // Card width
                    DecodePixelType = DecodePixelType.Logical
                };

                await bitmap.SetSourceAsync(stream);
                imageControl.Source = bitmap;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to load image: {imagePath}, Error: {ex.Message}");
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
                Glyph = "\uEB9F",
                FontSize = 64,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = (SolidColorBrush)Application.Current.Resources["TextFillColorTertiaryBrush"]
            };

            placeholderBorder.Child = placeholderIcon;
            StackedThumbnails.Children.Add(placeholderBorder);
        }
    }
}