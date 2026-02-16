using DeDupe.Models.Analysis;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.System;
using Windows.UI.Core;

namespace DeDupe.Controls
{
    public sealed partial class ImageGroupCard : UserControl
    {
        private static readonly ILogger<ImageGroupCard> _logger = App.Current.GetService<ILogger<ImageGroupCard>>();

        public static readonly DependencyProperty GroupProperty = DependencyProperty.Register(nameof(Group), typeof(SimilarityGroup), typeof(ImageGroupCard), new PropertyMetadata(null, OnGroupChanged));

        public SimilarityGroup? Group
        {
            get => (SimilarityGroup?)GetValue(GroupProperty);
            set => SetValue(GroupProperty, value);
        }

        public ImageGroupCard()
        {
            InitializeComponent();
        }

        private static void OnGroupChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ImageGroupCard control)
            {
                control.UpdateStackedImages();
            }
        }

        private void OnCardPointerPressed(object sender, PointerRoutedEventArgs e)
        {
            CoreVirtualKeyStates ctrlState = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control);
            bool isCtrlPressed = (ctrlState & CoreVirtualKeyStates.Down) != 0;

            if (isCtrlPressed && Group != null)
            {
                Group.ToggleSelection();
                e.Handled = true; // Prevent other click behaviors
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
                LogGroupThumbnailLoadFailed(_logger, imagePath, ex);
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

        #region Logging

        [LoggerMessage(Level = LogLevel.Warning, Message = "Group card thumbnail load skipped for {FilePath}")]
        private static partial void LogGroupThumbnailLoadFailed(ILogger logger, string filePath, Exception ex);

        #endregion Logging
    }
}