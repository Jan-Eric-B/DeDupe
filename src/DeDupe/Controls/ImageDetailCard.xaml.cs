using DeDupe.Models.Analysis;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.System;

namespace DeDupe.Controls
{
    public sealed partial class ImageDetailCard : UserControl
    {
        #region Dependency Properties

        public static readonly DependencyProperty SelectableImageProperty = DependencyProperty.Register(nameof(SelectableImage), typeof(SelectableImage), typeof(ImageDetailCard), new PropertyMetadata(null, OnSelectableImageChanged));

        #endregion Dependency Properties

        #region Properties

        public SelectableImage? SelectableImage
        {
            get => (SelectableImage?)GetValue(SelectableImageProperty);
            set => SetValue(SelectableImageProperty, value);
        }

        #endregion Properties

        #region Constructor

        public ImageDetailCard()
        {
            InitializeComponent();
        }

        #endregion Constructor

        #region Property Changed Handlers

        private static void OnSelectableImageChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not ImageDetailCard control)
            {
                return;
            }

            // Unsubscribe from old SelectableImage
            if (e.OldValue is SelectableImage oldImage)
            {
                oldImage.PropertyChanged -= control.OnSelectableImagePropertyChanged;
            }

            // Subscribe to new SelectableImage
            if (e.NewValue is SelectableImage newImage)
            {
                newImage.PropertyChanged += control.OnSelectableImagePropertyChanged;
                control.UpdateDisplay(newImage);
                control.UpdateCheckboxToModel(newImage.IsSelected);
                control.UpdateSelectionVisualState(newImage.IsSelected);
            }
            else
            {
                control.ClearDisplay();
            }
        }

        private void OnSelectableImagePropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is not SelectableImage image)
            {
                return;
            }

            // Update UI
            if (e.PropertyName == nameof(SelectableImage.IsSelected))
            {
                UpdateCheckboxToModel(image.IsSelected);
                UpdateSelectionVisualState(image.IsSelected);
            }
        }

        #endregion Property Changed Handlers

        #region Display Updates

        private void UpdateDisplay(SelectableImage image)
        {
            // Update text fields
            FileNameTextBlock.Text = image.FileName;
            ResolutionTextBlock.Text = image.Resolution;
            FileSizeTextBlock.Text = image.FormattedFileSize;
            FormatTextBlock.Text = image.Extension;

            // Set tooltip to show full path
            ToolTipService.SetToolTip(FileNameTextBlock, image.FilePath);

            // Load image preview
            _ = LoadImagePreviewAsync(image.FilePath);
        }

        private void ClearDisplay()
        {
            FileNameTextBlock.Text = string.Empty;
            ResolutionTextBlock.Text = string.Empty;
            FileSizeTextBlock.Text = string.Empty;
            FormatTextBlock.Text = string.Empty;
            PreviewImage.Source = null;
            PlaceholderIcon.Visibility = Visibility.Visible;
            SelectionCheckBox.IsChecked = false;
        }

        private async Task LoadImagePreviewAsync(string imagePath)
        {
            try
            {
                StorageFile file = await StorageFile.GetFileFromPathAsync(imagePath);

                using IRandomAccessStreamWithContentType stream = await file.OpenReadAsync();

                BitmapImage bitmap = new()
                {
                    DecodePixelWidth = 320,
                    DecodePixelType = DecodePixelType.Logical
                };

                await bitmap.SetSourceAsync(stream);
                PreviewImage.Source = bitmap;
                PlaceholderIcon.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to load image preview: {imagePath}, Error: {ex.Message}");
                PlaceholderIcon.Visibility = Visibility.Visible;
            }
        }

        /// <summary>
        /// Update checkbox UI to match models IsSelected state.
        /// </summary>
        private void UpdateCheckboxToModel(bool isSelected)
        {
            if (SelectionCheckBox.IsChecked != isSelected)
            {
                // Temporarily unhook event to prevent feedback loop
                SelectionCheckBox.Checked -= SelectionCheckBox_CheckedChanged;
                SelectionCheckBox.Unchecked -= SelectionCheckBox_CheckedChanged;

                SelectionCheckBox.IsChecked = isSelected;

                // Re-hook events
                SelectionCheckBox.Checked += SelectionCheckBox_CheckedChanged;
                SelectionCheckBox.Unchecked += SelectionCheckBox_CheckedChanged;
            }
        }

        private void UpdateSelectionVisualState(bool isSelected)
        {
            string stateName = isSelected ? "Selected" : "Unselected";
            VisualStateManager.GoToState(this, stateName, true);
        }

        #endregion Display Updates

        #region Event Handlers

        private void SelectionCheckBox_CheckedChanged(object sender, RoutedEventArgs e)
        {
            if (SelectableImage != null && SelectionCheckBox.IsChecked.HasValue)
            {
                SelectableImage.IsSelected = SelectionCheckBox.IsChecked.Value;
            }
        }

        private async void ImagePreview_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            if (SelectableImage == null)
            {
                return;
            }

            try
            {
                StorageFile file = await StorageFile.GetFileFromPathAsync(SelectableImage.FilePath);
                await Launcher.LaunchFileAsync(file);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to preview file: {ex.Message}");
            }
        }

        private async void FileName_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            if (SelectableImage == null)
            {
                return;
            }

            try
            {
                string? folderPath = SelectableImage.DirectoryPath;
                if (!string.IsNullOrEmpty(folderPath))
                {
                    StorageFolder folder = await StorageFolder.GetFolderFromPathAsync(folderPath);
                    await Launcher.LaunchFolderAsync(folder);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to open folder: {ex.Message}");
            }
        }

        #endregion Event Handlers

        #region Lifecycle

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            SelectionCheckBox.Checked += SelectionCheckBox_CheckedChanged;
            SelectionCheckBox.Unchecked += SelectionCheckBox_CheckedChanged;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            SelectionCheckBox.Checked -= SelectionCheckBox_CheckedChanged;
            SelectionCheckBox.Unchecked -= SelectionCheckBox_CheckedChanged;

            // Unsubscribe from events
            if (SelectableImage != null)
            {
                SelectableImage.PropertyChanged -= OnSelectableImagePropertyChanged;
            }
        }

        #endregion Lifecycle
    }
}