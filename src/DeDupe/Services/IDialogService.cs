using DeDupe.Localization;
using Microsoft.UI.Xaml;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DeDupe.Services
{
    /// <summary>
    /// Handles UI dialog and picker.
    /// </summary>
    public interface IDialogService
    {
        /// <summary>
        /// Sets XAML root for ContentDialog.
        /// </summary>
        void SetXamlRoot(XamlRoot xamlRoot);

        /// <summary>
        /// Shows confirmation dialog with OK and Cancel buttons.
        /// </summary>
        Task<bool> ShowConfirmationAsync(string title, string message, string primaryButtonText = "OK", string closeButtonText = "Cancel", bool destructive = false);

        /// <summary>
        /// Shows informational dialog with Close button.
        /// </summary>
        Task ShowInfoAsync(string title, string message, string closeButtonText = "OK");

        /// <summary>
        /// Shows file-operation result dialog. Only when failures.
        /// </summary>
        Task ShowOperationResultAsync(string operationName, int successCount, int failedCount, ILocalizer localizer);

        /// <summary>
        /// Opens system folder picker.
        /// </summary>
        Task<string?> PickFolderAsync(string commitButtonText = "Select Folder");

        /// <summary>
        /// Opens system file picker with provided type filters.
        /// </summary>
        Task<string?> PickFileAsync(IEnumerable<string> fileTypeFilters, string commitButtonText = "Select File");

        /// <summary>
        /// Opens system file picker with provided type filters.
        /// </summary>
        Task<IReadOnlyList<string>> PickFilesAsync(IEnumerable<string> fileTypeFilters, string commitButtonText = "Select Files");

        /// <summary>
        /// Opens folder in file explorer.
        /// </summary>
        Task OpenFolderInExplorerAsync(string folderPath, bool createIfMissing = true);

        /// <summary>
        /// Opens file explorer with the specified file selected.
        /// </summary>
        Task<bool> OpenFileInExplorerAsync(string filePath);
    }
}