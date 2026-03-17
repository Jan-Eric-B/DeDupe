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
    public sealed partial class ImageGroupListItem : UserControl
    {
        private static readonly ILogger<ImageGroupListItem> _logger = App.Current.GetService<ILogger<ImageGroupListItem>>();

        public static readonly DependencyProperty GroupProperty = DependencyProperty.Register(nameof(Group), typeof(SimilarityGroup), typeof(ImageGroupListItem), new PropertyMetadata(null, OnGroupChanged));

        public SimilarityGroup? Group
        {
            get => (SimilarityGroup?)GetValue(GroupProperty);
            set => SetValue(GroupProperty, value);
        }

        public ImageGroupListItem()
        {
            InitializeComponent();
        }

        private static void OnGroupChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ImageGroupListItem control)
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
                e.Handled = true;
            }
        }

        private void UpdateStackedImages()
        {
            StackedThumbnails.Children.Clear();

            if (Group == null || Group.Count == 0)
            {
                PlaceholderIcon.Visibility = Visibility.Visible;
                return;
            }

            PlaceholderIcon.Visibility = Visibility.Collapsed;

            List<string> imagesToShow = [.. Group.GetImageThumbnailPaths().Take(4)];
            int imageCount = imagesToShow.Count;

            for (int i = imageCount - 1; i >= 0; i--)
            {
                CreateStackedImage(imagesToShow[i], i, imageCount);
            }
        }

        private void CreateStackedImage(string imagePath, int index, int totalCount)
        {
            Border imageBorder = new()
            {
                CornerRadius = new CornerRadius(3),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                Shadow = new ThemeShadow()
            };

            double offsetX = (totalCount - 1 - index) * 5;
            double offsetY = (totalCount - 1 - index) * 4;
            imageBorder.Margin = new Thickness(offsetX, offsetY, -offsetX, -offsetY);

            Image image = new()
            {
                Stretch = Stretch.UniformToFill,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            _ = LoadImageAsync(image, imagePath, 64);

            imageBorder.Child = image;
            StackedThumbnails.Children.Add(imageBorder);
        }

        private static async Task LoadImageAsync(Image imageControl, string imagePath, int decodeWidth)
        {
            try
            {
                StorageFile file = await StorageFile.GetFileFromPathAsync(imagePath);

                using IRandomAccessStreamWithContentType stream = await file.OpenReadAsync();

                BitmapImage bitmap = new()
                {
                    DecodePixelWidth = decodeWidth,
                    DecodePixelType = DecodePixelType.Logical
                };

                await bitmap.SetSourceAsync(stream);
                imageControl.Source = bitmap;
            }
            catch (Exception ex)
            {
                LogThumbnailLoadFailed(_logger, imagePath, ex);
            }
        }

        #region Logging

        [LoggerMessage(Level = LogLevel.Warning, Message = "List item thumbnail load failed for {FilePath}")]
        private static partial void LogThumbnailLoadFailed(ILogger logger, string filePath, Exception ex);

        #endregion Logging
    }
}