using DeDupe.Models.Analysis;
using DeDupe.Models.Media;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.Globalization.DateTimeFormatting;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.System;

namespace DeDupe.Controls
{
    public sealed partial class ImageDetailCard : UserControl
    {
        #region Properties

        public static readonly DependencyProperty SelectableItemProperty = DependencyProperty.Register(nameof(SelectableItem), typeof(SelectableItem), typeof(ImageDetailCard), new PropertyMetadata(null, OnSelectableItemChanged));

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
                return;

            // Unsubscribe from old item
            if (e.OldValue is SelectableItem oldItem)
            {
                oldItem.PropertyChanged -= control.OnSelectableItemPropertyChanged;
            }

            // Subscribe to new item
            if (e.NewValue is SelectableItem newItem)
            {
                newItem.PropertyChanged += control.OnSelectableItemPropertyChanged;
                control.UpdateDisplay(newItem);
                control.UpdateSelection(newItem.IsSelected);
            }
            else
            {
                control.ClearDisplay();
            }
        }

        private void OnSelectableItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is SelectableItem item && e.PropertyName == nameof(SelectableItem.IsSelected))
            {
                UpdateSelection(item.IsSelected);
            }
        }

        #endregion Property Changed Handlers

        #region Display Updates

        private void UpdateDisplay(SelectableItem item)
        {
            MediaMetadata metadata = item.Metadata;

            FileNameTextBlock.Text = metadata.FileName;
            FormatTextBlock.Text = metadata.ExtensionDisplay;
            FileSizeTextBlock.Text = metadata.FormattedFileSize;
            CreatedDateTextBlock.Text = FormatDate(metadata.CreatedDate);
            ToolTipService.SetToolTip(FileNameTextBlock, metadata.FilePath);

            UpdateResolutionDisplay(metadata);

            _ = LoadThumbnailAsync(metadata.FilePath);
            _ = EnsureDimensionsAndUpdateAsync(item);
        }

        private void UpdateResolutionDisplay(MediaMetadata metadata)
        {
            if (metadata.Width > 0 && metadata.Height > 0)
            {
                ResolutionTextBlock.Text = metadata.Resolution;
            }
            else
            {
                ResolutionTextBlock.Text = "...";
            }
        }

        private async Task EnsureDimensionsAndUpdateAsync(SelectableItem item)
        {
            try
            {
                SourceMedia source = item.Item.Source;

                if (!source.AreDimensionsLoaded)
                {
                    await source.EnsureDimensionsLoadedAsync();
                }

                DispatcherQueue.TryEnqueue(() =>
                {
                    if (SelectableItem == item)
                    {
                        ResolutionTextBlock.Text = source.Metadata.Resolution;
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to load dimensions: {ex.Message}");

                DispatcherQueue.TryEnqueue(() =>
                {
                    if (SelectableItem == item && ResolutionTextBlock.Text == "...")
                    {
                        ResolutionTextBlock.Text = "—";
                    }
                });
            }
        }

        private static string FormatDate(DateTime date)
        {
            if (date == default || date == DateTime.MinValue)
            {
                return "";
            }

            // Globalized formatting
            DateTimeFormatter formatter = new("shortdate");
            return formatter.Format(date);
        }

        private void ClearDisplay()
        {
            FileNameTextBlock.Text = string.Empty;
            ResolutionTextBlock.Text = string.Empty;
            FileSizeTextBlock.Text = string.Empty;
            FormatTextBlock.Text = string.Empty;
            CreatedDateTextBlock.Text = string.Empty;
            ImageThumbnail.Source = null;
            PlaceholderIcon.Visibility = Visibility.Visible;
            SelectionCheckBox.IsChecked = false;
        }

        private async Task LoadThumbnailAsync(string imagePath)
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
                ImageThumbnail.Source = bitmap;
                PlaceholderIcon.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to load thumbnail: {imagePath}, Error: {ex.Message}");
                PlaceholderIcon.Visibility = Visibility.Visible;
            }
        }

        private void UpdateSelection(bool isSelected)
        {
            if (SelectionCheckBox.IsChecked != isSelected)
            {
                SelectionCheckBox.IsChecked = isSelected;
            }

            VisualStateManager.GoToState(this, isSelected ? "Selected" : "Unselected", true);
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

        private async void OpenItem_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
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
                Debug.WriteLine($"Failed to open file: {ex.Message}");
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

            // Unsubscribe from event
            if (SelectableItem != null)
            {
                SelectableItem.PropertyChanged -= OnSelectableItemPropertyChanged;
            }
        }

        #endregion Lifecycle
    }
}