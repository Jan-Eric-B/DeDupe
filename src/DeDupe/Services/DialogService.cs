using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System;
using WinRT.Interop;

namespace DeDupe.Services
{
    /// <inheritdoc/>
    public sealed partial class DialogService(ILogger<DialogService> logger) : IDialogService
    {
        private readonly ILogger<DialogService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        private XamlRoot? _xamlRoot;

        /// <inheritdoc />
        public void SetXamlRoot(XamlRoot xamlRoot)
        {
            _xamlRoot = xamlRoot ?? throw new ArgumentNullException(nameof(xamlRoot));
        }

        /// <inheritdoc />
        public async Task<bool> ShowConfirmationAsync(string title, string message, string primaryButtonText = "OK", string closeButtonText = "Cancel", bool destructive = false)
        {
            EnsureXamlRoot();

            ContentDialog dialog = new()
            {
                Title = title,
                Content = message,
                PrimaryButtonText = primaryButtonText,
                CloseButtonText = closeButtonText,
                DefaultButton = destructive ? ContentDialogButton.Close : ContentDialogButton.Primary,
                XamlRoot = _xamlRoot
            };

            ContentDialogResult result = await dialog.ShowAsync();

            LogDialogResult(title, result.ToString());

            return result == ContentDialogResult.Primary;
        }

        /// <inheritdoc />
        public async Task ShowInfoAsync(string title, string message, string closeButtonText = "OK")
        {
            EnsureXamlRoot();

            ContentDialog dialog = new()
            {
                Title = title,
                Content = message,
                CloseButtonText = closeButtonText,
                XamlRoot = _xamlRoot
            };

            await dialog.ShowAsync();
        }

        /// <inheritdoc />
        public async Task ShowOperationResultAsync(string operationName, int successCount, int failedCount)
        {
            if (failedCount == 0)
            {
                // Silent on full success — ViewModel updates status bar instead.
                return;
            }

            string message = $"Completed with some issues:\n\n" +
                             $"\u2713 {successCount} file{(successCount == 1 ? "" : "s")} {operationName.ToLower()}d successfully\n" +
                             $"\u2717 {failedCount} file{(failedCount == 1 ? "" : "s")} failed";

            await ShowInfoAsync($"{operationName} Completed", message);
        }

        /// <inheritdoc />
        public async Task<string?> PickFolderAsync(string commitButtonText = "Select Folder")
        {
            FolderPicker folderPicker = new()
            {
                ViewMode = PickerViewMode.List,
                SuggestedStartLocation = PickerLocationId.PicturesLibrary,
                CommitButtonText = commitButtonText
            };

            folderPicker.FileTypeFilter.Add("*");

            nint windowHandle = WindowNative.GetWindowHandle(App.Window);
            InitializeWithWindow.Initialize(folderPicker, windowHandle);

            StorageFolder? folder = await folderPicker.PickSingleFolderAsync();

            return folder?.Path;
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<string>> PickFilesAsync(IEnumerable<string> fileTypeFilters, string commitButtonText = "Select Files")
        {
            FileOpenPicker fileOpenPicker = new()
            {
                ViewMode = PickerViewMode.Thumbnail,
                SuggestedStartLocation = PickerLocationId.PicturesLibrary,
                CommitButtonText = commitButtonText
            };

            foreach (string ext in fileTypeFilters)
            {
                fileOpenPicker.FileTypeFilter.Add(ext);
            }

            nint windowHandle = WindowNative.GetWindowHandle(App.Window);
            InitializeWithWindow.Initialize(fileOpenPicker, windowHandle);

            IReadOnlyList<StorageFile> files = await fileOpenPicker.PickMultipleFilesAsync();

            if (files is null || files.Count == 0)
            {
                return [];
            }

            return [.. files.Select(f => f.Path)];
        }

        /// <inheritdoc />
        public async Task OpenFolderInExplorerAsync(string folderPath, bool createIfMissing = true)
        {
            if (string.IsNullOrEmpty(folderPath))
            {
                return;
            }

            if (createIfMissing && !Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
                LogFolderCreatedForExplorer(folderPath);
            }

            await Launcher.LaunchFolderPathAsync(folderPath);
        }

        private void EnsureXamlRoot()
        {
            if (_xamlRoot is null)
            {
                throw new InvalidOperationException($"{nameof(DialogService)}.{nameof(SetXamlRoot)} must be called before showing dialogs. Call it in the page's Loaded event handler.");
            }
        }

        #region Logging

        [LoggerMessage(Level = LogLevel.Debug, Message = "Dialog '{Title}' result: {Result}")]
        private partial void LogDialogResult(string title, string result);

        [LoggerMessage(Level = LogLevel.Debug, Message = "Folder created for explorer launch: {FolderPath}")]
        private partial void LogFolderCreatedForExplorer(string folderPath);

        #endregion Logging
    }
}