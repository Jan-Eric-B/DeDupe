using DeDupe.Models.Analysis;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.System;

namespace DeDupe.Controls
{
    public sealed partial class ImageDetailCard : UserControl
    {
        #region Dependency Properties

        public static readonly DependencyProperty SelectableImageProperty = DependencyProperty.Register(nameof(SelectableImage), typeof(SelectableImage), typeof(ImageDetailCard), new PropertyMetadata(null, OnSelectableImageChanged));

        public static readonly DependencyProperty IsSelectedProperty = DependencyProperty.Register(nameof(IsSelected), typeof(bool), typeof(ImageDetailCard), new PropertyMetadata(false, OnIsSelectedChanged));

        #endregion Dependency Properties

        #region Properties

        public SelectableImage SelectableImage
        {
            get => (SelectableImage)GetValue(SelectableImageProperty);
            set => SetValue(SelectableImageProperty, value);
        }

        public bool IsSelected
        {
            get => (bool)GetValue(IsSelectedProperty);
            set => SetValue(IsSelectedProperty, value);
        }

        #endregion Properties

        #region Events

        public event EventHandler<bool>? SelectionChanged;

        #endregion Events

        public ImageDetailCard()
        {
            InitializeComponent();
        }

        #region Property Changed Handlers

        private static void OnSelectableImageChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ImageDetailCard control)
            {
                control.UpdateDisplay();

                // Unsubscribe from old
                if (e.OldValue is SelectableImage oldImage)
                {
                    oldImage.SelectionChanged -= control.OnImageSelectionChanged;
                }

                // Subscribe to new
                if (e.NewValue is SelectableImage newImage)
                {
                    newImage.SelectionChanged += control.OnImageSelectionChanged;
                    control.IsSelected = newImage.IsSelected;
                }
            }
        }

        private static void OnIsSelectedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ImageDetailCard control)
            {
                control.UpdateSelectionVisualState();
                control.UpdateCheckboxState();
            }
        }

        private void OnImageSelectionChanged(object? sender, EventArgs e)
        {
            if (SelectableImage != null)
            {
                IsSelected = SelectableImage.IsSelected;
            }
        }

        #endregion Property Changed Handlers

        #region Display Updates

        private void UpdateDisplay()
        {
            if (SelectableImage == null)
            {
                ClearDisplay();
                return;
            }

            // Update text fields
            FileNameTextBlock.Text = SelectableImage.FileName;
            ResolutionTextBlock.Text = SelectableImage.Resolution;
            FileSizeTextBlock.Text = SelectableImage.FormattedFileSize;
            FormatTextBlock.Text = SelectableImage.Extension;

            // Set tooltip to show full path
            ToolTipService.SetToolTip(FileNameTextBlock, SelectableImage.FilePath);

            // Update checkbox
            SelectionCheckBox.IsChecked = SelectableImage.IsSelected;

            // Load image preview
            _ = LoadImagePreviewAsync(SelectableImage.FilePath);
        }

        private void ClearDisplay()
        {
            FileNameTextBlock.Text = string.Empty;
            ResolutionTextBlock.Text = string.Empty;
            FileSizeTextBlock.Text = string.Empty;
            FormatTextBlock.Text = string.Empty;
            PreviewImage.Source = null;
            PlaceholderIcon.Visibility = Visibility.Visible;
        }

        private async Task LoadImagePreviewAsync(string imagePath)
        {
            try
            {
                StorageFile file = await StorageFile.GetFileFromPathAsync(imagePath);

                using var stream = await file.OpenReadAsync();

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

        private void UpdateSelectionVisualState()
        {
            string stateName = IsSelected ? "Selected" : "Unselected";
            VisualStateManager.GoToState(this, stateName, true);
        }

        private void UpdateCheckboxState()
        {
            if (SelectionCheckBox.IsChecked != IsSelected)
            {
                SelectionCheckBox.IsChecked = IsSelected;
            }
        }

        #endregion Display Updates

        #region Event Handlers

        private void SelectionCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (SelectableImage != null && SelectionCheckBox.IsChecked.HasValue)
            {
                bool newValue = SelectionCheckBox.IsChecked.Value;

                // Update if different
                if (SelectableImage.IsSelected != newValue)
                {
                    SelectableImage.IsSelected = newValue;
                }

                IsSelected = newValue;
                SelectionChanged?.Invoke(this, newValue);
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
    }
}