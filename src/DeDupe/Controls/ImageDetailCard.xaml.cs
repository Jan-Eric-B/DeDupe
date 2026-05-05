using DeDupe.Models.Analysis;
using DeDupe.Models.Media;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Windows.Globalization.DateTimeFormatting;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.System;
using Windows.UI.Core;

namespace DeDupe.Controls
{
    public sealed partial class ImageDetailCard : UserControl
    {
        private readonly ILogger<ImageDetailCard> _logger = App.Current.GetService<ILogger<ImageDetailCard>>();

        /// <summary>
        /// Shared semaphore to limit concurrent detail image loads.
        /// Detail cards use 320px decode width (larger), so we allow fewer concurrent loads.
        /// </summary>
        private static readonly SemaphoreSlim s_loadThrottle = new(6, 6);

        private CancellationTokenSource? _loadCts;

        public ImageDetailCard()
        {
            InitializeComponent();
        }

        #region Loading

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            SelectionCheckBox.Checked += SelectionCheckBox_CheckedChanged;
            SelectionCheckBox.Unchecked += SelectionCheckBox_CheckedChanged;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            CancelPendingLoads();

            SelectionCheckBox.Checked -= SelectionCheckBox_CheckedChanged;
            SelectionCheckBox.Unchecked -= SelectionCheckBox_CheckedChanged;

            SelectableItem?.PropertyChanged -= OnSelectableItemPropertyChanged;
        }

        private void CancelPendingLoads()
        {
            _loadCts?.Cancel();
            _loadCts?.Dispose();
            _loadCts = null;
        }

        private async Task LoadThumbnailAsync(string imagePath, CancellationToken cancellationToken)
        {
            try
            {
                await s_loadThrottle.WaitAsync(cancellationToken);
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    StorageFile file = await StorageFile.GetFileFromPathAsync(imagePath);

                    cancellationToken.ThrowIfCancellationRequested();

                    using IRandomAccessStreamWithContentType stream = await file.OpenReadAsync();

                    cancellationToken.ThrowIfCancellationRequested();

                    BitmapImage bitmap = new()
                    {
                        DecodePixelWidth = 320,
                        DecodePixelType = DecodePixelType.Logical
                    };

                    await bitmap.SetSourceAsync(stream);

                    cancellationToken.ThrowIfCancellationRequested();

                    ImageThumbnail.Source = bitmap;
                    PlaceholderIcon.Visibility = Visibility.Collapsed;
                }
                finally
                {
                    s_loadThrottle.Release();
                }
            }
            catch (OperationCanceledException)
            {
                // Card was recycled - discard silently
            }
            catch (Exception ex)
            {
                LogThumbnailLoadFailed(imagePath, ex);
                PlaceholderIcon.Visibility = Visibility.Visible;
            }
        }

        #endregion Loading

        private void UpdateDisplay(SelectableItem item)
        {
            MediaMetadata metadata = item.Metadata;

            FileNameTextBlock.Text = metadata.FileName;
            FormatTextBlock.Text = metadata.ExtensionDisplay;
            FileSizeTextBlock.Text = metadata.FormattedFileSize;
            CreatedDateTextBlock.Text = FormatDate(metadata.CreatedDate);
            ToolTipService.SetToolTip(FileNameTextBlock, metadata.FilePath);

            UpdateResolutionDisplay(metadata);

            // Cancel any in-flight loads from previous item
            CancelPendingLoads();
            _loadCts = new CancellationTokenSource();
            CancellationToken ct = _loadCts.Token;

            // Clear old bitmap immediately to free memory
            ImageThumbnail.Source = null;
            PlaceholderIcon.Visibility = Visibility.Visible;

            _ = LoadThumbnailAsync(metadata.FilePath, ct);
            _ = EnsureDimensionsAndUpdateAsync(item, ct);
        }

        private void ClearDisplay()
        {
            CancelPendingLoads();

            FileNameTextBlock.Text = string.Empty;
            ResolutionTextBlock.Text = string.Empty;
            FileSizeTextBlock.Text = string.Empty;
            FormatTextBlock.Text = string.Empty;
            CreatedDateTextBlock.Text = string.Empty;
            ImageThumbnail.Source = null;
            PlaceholderIcon.Visibility = Visibility.Visible;
            SelectionCheckBox.IsChecked = false;
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

        private async Task EnsureDimensionsAndUpdateAsync(SelectableItem item, CancellationToken cancellationToken)
        {
            try
            {
                SourceMedia source = item.Item.Source;

                if (!source.AreDimensionsLoaded)
                {
                    await source.EnsureDimensionsLoadedAsync();
                }

                cancellationToken.ThrowIfCancellationRequested();

                DispatcherQueue.TryEnqueue(() =>
                {
                    if (SelectableItem == item)
                    {
                        ResolutionTextBlock.Text = source.Metadata.Resolution;
                    }
                });
            }
            catch (OperationCanceledException)
            {
                // Card was recycled - discard silently
            }
            catch (Exception ex)
            {
                LogDimensionLoadFailed(item.FilePath, ex);

                if (cancellationToken.IsCancellationRequested)
                    return;

                DispatcherQueue.TryEnqueue(() =>
                {
                    if (SelectableItem == item && ResolutionTextBlock.Text == "...")
                    {
                        ResolutionTextBlock.Text = "\u2014";
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
                LogFileLaunchFailed(SelectableItem.FilePath, ex);
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
                LogFolderLaunchFailed(SelectableItem.Metadata.DirectoryPath ?? "unknown", ex);
            }
        }

        #region Selection

        public static readonly DependencyProperty SelectableItemProperty = DependencyProperty.Register(nameof(SelectableItem), typeof(SelectableItem), typeof(ImageDetailCard), new PropertyMetadata(null, OnSelectableItemChanged));

        public SelectableItem? SelectableItem
        {
            get => (SelectableItem?)GetValue(SelectableItemProperty);
            set => SetValue(SelectableItemProperty, value);
        }

        private static void OnSelectableItemChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not ImageDetailCard control)
                return;

            if (e.OldValue is SelectableItem oldItem)
            {
                oldItem.PropertyChanged -= control.OnSelectableItemPropertyChanged;
            }

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

        private void UpdateSelection(bool isSelected)
        {
            if (SelectionCheckBox.IsChecked != isSelected)
            {
                SelectionCheckBox.IsChecked = isSelected;
            }

            VisualStateManager.GoToState(this, isSelected ? "Selected" : "Unselected", true);
        }

        private void SelectionCheckBox_CheckedChanged(object sender, RoutedEventArgs e)
        {
            if (SelectableItem != null && SelectionCheckBox.IsChecked.HasValue)
            {
                SelectableItem.IsSelected = SelectionCheckBox.IsChecked.Value;
            }
        }

        private void OnCardPointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (SelectableItem == null)
            {
                return;
            }

            CoreVirtualKeyStates ctrlState = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control);
            bool isCtrlPressed = (ctrlState & CoreVirtualKeyStates.Down) != 0;

            if (isCtrlPressed)
            {
                SelectableItem.IsSelected = !SelectableItem.IsSelected;
                e.Handled = true;
            }
        }

        #endregion Selection

        #region Logging

        [LoggerMessage(Level = LogLevel.Warning, Message = "Thumbnail load skipped for {FilePath}")]
        private partial void LogThumbnailLoadFailed(string filePath, Exception ex);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Dimension load skipped for {FilePath}")]
        private partial void LogDimensionLoadFailed(string filePath, Exception ex);

        [LoggerMessage(Level = LogLevel.Warning, Message = "File launch failed for {FilePath}")]
        private partial void LogFileLaunchFailed(string filePath, Exception ex);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Folder launch failed for {FolderPath}")]
        private partial void LogFolderLaunchFailed(string folderPath, Exception ex);

        #endregion Logging
    }
}
