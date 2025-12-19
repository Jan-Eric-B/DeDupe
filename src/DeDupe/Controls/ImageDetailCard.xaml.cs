using DeDupe.Models;
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

        public static readonly DependencyProperty SelectableItemProperty = DependencyProperty.Register(nameof(Models.Analysis.SelectableItem), typeof(SelectableItem), typeof(ImageDetailCard), new PropertyMetadata(null, OnSelectableItemChanged));

        #endregion Dependency Properties

        #region Properties

        public SelectableItem? SelectableItem
        {
            get => (SelectableItem?)GetValue(SelectableItemProperty);
            set => SetValue(SelectableItemProperty, value);
        }

        #endregion Properties

        #region Constructor

        public ImageDetailCard()
        {
            InitializeComponent();
        }

        #endregion Constructor

        #region Property Changed Handlers

        private static void OnSelectableItemChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not ImageDetailCard control)
            {
                return;
            }

            // Unsubscribe from old SelectableItem
            if (e.OldValue is SelectableItem oldImage)
            {
                oldImage.PropertyChanged -= control.OnSelectableItemPropertyChanged;
            }

            // Subscribe to new SelectableItem
            if (e.NewValue is SelectableItem newImage)
            {
                newImage.PropertyChanged += control.OnSelectableItemPropertyChanged;
                control.UpdateDisplay(newImage);
                control.UpdateCheckboxToModel(newImage.IsSelected);
                control.UpdateSelectionVisualState(newImage.IsSelected);
            }
            else
            {
                control.ClearDisplay();
            }
        }

        private void OnSelectableItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is not SelectableItem image)
            {
                return;
            }

            // Update UI
            if (e.PropertyName == nameof(SelectableItem.IsSelected))
            {
                UpdateCheckboxToModel(image.IsSelected);
                UpdateSelectionVisualState(image.IsSelected);
            }
        }

        #endregion Property Changed Handlers

        #region Display Updates

        private void UpdateDisplay(SelectableItem image)
        {
            // Get metadata
            MediaMetadata metadata = image.Metadata;

            // Update text fields
            FileNameTextBlock.Text = metadata.FileName;
            ResolutionTextBlock.Text = metadata.Resolution;
            FileSizeTextBlock.Text = metadata.FormattedFileSize;
            FormatTextBlock.Text = metadata.ExtensionDisplay;

            // Set tooltip to show full path
            ToolTipService.SetToolTip(FileNameTextBlock, metadata.FilePath);

            // Load image preview
            _ = LoadImagePreviewAsync(metadata.FilePath);
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
            if (SelectableItem != null && SelectionCheckBox.IsChecked.HasValue)
            {
                SelectableItem.IsSelected = SelectionCheckBox.IsChecked.Value;
            }
        }

        private async void ImagePreview_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            if (SelectableItem == null)
            {
                return;
            }

            try
            {
                StorageFile file = await StorageFile.GetFileFromPathAsync(SelectableItem.FilePath);
                await Launcher.LaunchFileAsync(file);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to preview file: {ex.Message}");
            }
        }

        private async void FileName_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            if (SelectableItem == null)
            {
                return;
            }

            try
            {
                string? folderPath = SelectableItem.Metadata.DirectoryPath;
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
            if (SelectableItem != null)
            {
                SelectableItem.PropertyChanged -= OnSelectableItemPropertyChanged;
            }
        }

        #endregion Lifecycle
    }
}