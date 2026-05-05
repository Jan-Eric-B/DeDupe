using CommunityToolkit.WinUI.Controls;
using DeDupe.Controls;
using DeDupe.Enums;
using DeDupe.Localization;
using DeDupe.Models.Analysis;
using DeDupe.Models.Results;
using DeDupe.Services;
using DeDupe.ViewModels;
using DeDupe.ViewModels.Pages;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Search;
using Windows.System;

namespace DeDupe.Views.Pages
{
    public sealed partial class ManagementPage : Page
    {
        private readonly ILogger<ManagementPage> _logger = App.Current.GetService<ILogger<ManagementPage>>();
        private readonly IDialogService _dialogService = App.Current.GetService<IDialogService>();
        private readonly ILocalizer _localizer = App.Current.GetService<ILocalizer>();

        private ManagementViewModel ViewModel { get; }

        public ManagementPage()
        {
            InitializeComponent();
            ViewModel = App.Current.GetService<ManagementViewModel>();
            DataContext = ViewModel;

            ViewModel.PropertyChanged += ViewModel_PropertyChanged;
            SplitViewGrid.SizeChanged += SplitViewGrid_SizeChanged;

            Loaded += ManagementPage_Loaded;
        }

        private string L(string key) => _localizer.GetLocalizedString(key) ?? key;

        private string L(string key, params object[] args) => string.Format(_localizer.GetLocalizedString(key) ?? key, args);

        private async void ManagementPage_Loaded(object sender, RoutedEventArgs e)
        {
            _dialogService.SetXamlRoot(XamlRoot);
            await ViewModel.TryAutoAnalyzeAsync();
        }

        private void BackToConfiguration_Click(object sender, RoutedEventArgs e)
        {
            MainWindowViewModel? mainViewModel = App.Current.GetService<MainWindowViewModel>();
            mainViewModel?.BackToConfigurationCommand.Execute(null);
        }

        #region Group Selection & Panel Management

        private double _rightPanelWidthRatio = 0.4;

        private bool _isPanelOpen = false;

        private SimilarityGroup? _subscribedGroup;

        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ManagementViewModel.SelectedGroup))
            {
                HandleGroupSelectionChanged();
            }
        }

        private void HandleGroupSelectionChanged()
        {
            ExitEditMode(false);

            UnsubscribeFromGroup();

            if (ViewModel.SelectedGroup != null)
            {
                SubscribeToGroup(ViewModel.SelectedGroup);

                SetupPanelForGroup();

                UpdateSelection();

                ShowGroupContentPanelStoryboard.Begin();
            }
            else
            {
                SavePanelWidthRatio();

                HideRightPanel();

                HideGroupContentPanelStoryboard.Begin();
            }
        }

        private void SubscribeToGroup(SimilarityGroup group)
        {
            _subscribedGroup = group;
            group.PropertyChanged += OnGroupPropertyChanged;
        }

        private void UnsubscribeFromGroup()
        {
            _subscribedGroup?.PropertyChanged -= OnGroupPropertyChanged;
            _subscribedGroup = null;
        }

        private void CloseGroupContentPanel_Click(object sender, RoutedEventArgs e)
        {
            ExitEditMode(false);
            ViewModel.SelectedGroup = null;
        }

        private void UpdateRightPanelWidth()
        {
            if (!_isPanelOpen || SplitViewGrid.ActualWidth <= 0)
            {
                return;
            }

            double totalWidth = SplitViewGrid.ActualWidth;
            double rightPanelWidth = totalWidth * _rightPanelWidthRatio;

            double minWidth = 300;
            double maxWidth = totalWidth * 0.7;
            rightPanelWidth = Math.Clamp(rightPanelWidth, minWidth, maxWidth);

            SplitViewGrid.ColumnDefinitions[2].Width = new GridLength(rightPanelWidth, GridUnitType.Pixel);
        }

        private void SavePanelWidthRatio()
        {
            if (_isPanelOpen && SplitViewGrid.ActualWidth > 0)
            {
                double currentRightWidth = SplitViewGrid.ColumnDefinitions[2].ActualWidth;
                if (currentRightWidth > 0)
                {
                    _rightPanelWidthRatio = currentRightWidth / SplitViewGrid.ActualWidth;
                    _rightPanelWidthRatio = Math.Clamp(_rightPanelWidthRatio, 0.2, 0.7);
                }
            }
        }

        private void SetupPanelForGroup()
        {
            double totalWidth = SplitViewGrid.ActualWidth;

            if (totalWidth <= 0)
            {
                totalWidth = 1000;
            }

            double rightPanelWidth = totalWidth * _rightPanelWidthRatio;

            double minWidth = 300;
            double maxWidth = totalWidth * 0.7;

            if (maxWidth < minWidth)
            {
                maxWidth = minWidth;
            }

            rightPanelWidth = Math.Clamp(rightPanelWidth, minWidth, maxWidth);

            if (!_isPanelOpen)
            {
                SplitViewGrid.ColumnDefinitions[1].Width = new GridLength(0, GridUnitType.Auto);
                SplitViewGrid.ColumnDefinitions[2].Width = new GridLength(rightPanelWidth, GridUnitType.Pixel);
                SplitViewGrid.ColumnDefinitions[2].MinWidth = minWidth;

                _isPanelOpen = true;
            }
        }

        private void HideRightPanel()
        {
            SplitViewGrid.ColumnDefinitions[2].MinWidth = 0;
            SplitViewGrid.ColumnDefinitions[1].Width = new GridLength(0);
            SplitViewGrid.ColumnDefinitions[2].Width = new GridLength(0);

            _isPanelOpen = false;
        }

        private void SplitViewGrid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_isPanelOpen && e.NewSize.Width > 0)
            {
                UpdateRightPanelWidth();
            }
        }

        #endregion Group Selection & Panel Management

        #region View

        private void ViewSegmented_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ViewModel == null) return;

            if (sender is Segmented segmented && segmented.SelectedItem is SegmentedItem item)
            {
                bool isList = item.Tag?.ToString() == "List";
                ViewModel.IsListView = isList;
                LogViewSwitched(isList ? "List" : "Grid");
            }
        }

        #endregion View

        #region Sorting

        private void SortBy_Similarity(object sender, RoutedEventArgs e)
        {
            ViewModel.SortGroups(GroupSortingOption.Similarity);
        }

        private void SortBy_ImageCount(object sender, RoutedEventArgs e)
        {
            ViewModel.SortGroups(GroupSortingOption.ImageCount);
        }

        private void SortBy_Name(object sender, RoutedEventArgs e)
        {
            ViewModel.SortGroups(GroupSortingOption.Name);
        }

        #endregion Sorting

        #region Filtering

        private void FilterBy_All(object sender, RoutedEventArgs e)
        {
            ViewModel.FilterGroups(GroupFilterOption.All);
        }

        private void FilterBy_ExactMatchesOnly(object sender, RoutedEventArgs e)
        {
            ViewModel.FilterGroups(GroupFilterOption.ExactMatchesOnly);
        }

        private void FilterBy_SimilarOnly(object sender, RoutedEventArgs e)
        {
            ViewModel.FilterGroups(GroupFilterOption.SimilarOnly);
        }

        #endregion Filtering

        #region Selection

        private void OnGroupPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(SimilarityGroup.IsSelected) or nameof(SimilarityGroup.SelectedCount) or nameof(SimilarityGroup.AllSelected) or nameof(SimilarityGroup.NoneSelected))
            {
                DispatcherQueue.TryEnqueue(UpdateSelection);
            }
        }

        private void UpdateSelection()
        {
            if (ViewModel.SelectedGroup == null)
            {
                return;
            }

            SimilarityGroup group = ViewModel.SelectedGroup;

            SelectAllCheckBox.IsChecked = group.IsSelected;

            SelectionCountText.Text = L("ManagementPage_GroupSelectionCount", group.SelectedCount, group.Count);
        }

        private void SelectAllGroupCheckBox_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.SelectedGroup == null)
            {
                return;
            }

            if (ViewModel.SelectedGroup.AllSelected)
            {
                ViewModel.SelectedGroup.DeselectAll();
            }
            else
            {
                ViewModel.SelectedGroup.SelectAll();
            }
        }

        private void SelectAllCheckBox_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.ApplyStrategyToAllGroupsCommand.Execute(SelectionStrategy.KeepNone);
        }

        #endregion Selection

        #region Auto Selection - Current Group

        private void ApplyStrategy_KeepHighestResolution(object sender, RoutedEventArgs e)
        {
            ViewModel.ApplyStrategyToCurrentGroupCommand.Execute(SelectionStrategy.KeepHighestResolution);
        }

        private void ApplyStrategy_KeepLowestResolution(object sender, RoutedEventArgs e)
        {
            ViewModel.ApplyStrategyToCurrentGroupCommand.Execute(SelectionStrategy.KeepLowestResolution);
        }

        private void ApplyStrategy_KeepNewest(object sender, RoutedEventArgs e)
        {
            ViewModel.ApplyStrategyToCurrentGroupCommand.Execute(SelectionStrategy.KeepNewest);
        }

        private void ApplyStrategy_KeepOldest(object sender, RoutedEventArgs e)
        {
            ViewModel.ApplyStrategyToCurrentGroupCommand.Execute(SelectionStrategy.KeepOldest);
        }

        private void ApplyStrategy_KeepLargestFileSize(object sender, RoutedEventArgs e)
        {
            ViewModel.ApplyStrategyToCurrentGroupCommand.Execute(SelectionStrategy.KeepLargestFileSize);
        }

        private void ApplyStrategy_KeepSmallestFileSize(object sender, RoutedEventArgs e)
        {
            ViewModel.ApplyStrategyToCurrentGroupCommand.Execute(SelectionStrategy.KeepSmallestFileSize);
        }

        private void ClearGroupSelection_Click(object _, RoutedEventArgs __)
        {
            ViewModel.ClearCurrentGroupSelectionCommand.Execute(null);
        }

        #endregion Auto Selection - Current Group

        #region Auto Selection - All Groups

        private void ApplyStrategyToAll_KeepHighestResolution(object sender, RoutedEventArgs e)
        {
            ViewModel.ApplyStrategyToAllGroupsCommand.Execute(SelectionStrategy.KeepHighestResolution);
        }

        private void ApplyStrategyToAll_KeepLowestResolution(object sender, RoutedEventArgs e)
        {
            ViewModel.ApplyStrategyToAllGroupsCommand.Execute(SelectionStrategy.KeepLowestResolution);
        }

        private void ApplyStrategyToAll_KeepNewest(object sender, RoutedEventArgs e)
        {
            ViewModel.ApplyStrategyToAllGroupsCommand.Execute(SelectionStrategy.KeepNewest);
        }

        private void ApplyStrategyToAll_KeepOldest(object sender, RoutedEventArgs e)
        {
            ViewModel.ApplyStrategyToAllGroupsCommand.Execute(SelectionStrategy.KeepOldest);
        }

        private void ApplyStrategyToAll_KeepLargestFileSize(object sender, RoutedEventArgs e)
        {
            ViewModel.ApplyStrategyToAllGroupsCommand.Execute(SelectionStrategy.KeepLargestFileSize);
        }

        private void ApplyStrategyToAll_KeepSmallestFileSize(object sender, RoutedEventArgs e)
        {
            ViewModel.ApplyStrategyToAllGroupsCommand.Execute(SelectionStrategy.KeepSmallestFileSize);
        }

        private void ClearAllSelections_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.ClearAllSelectionsCommand.Execute(null);
        }

        private void SelectAllGroups_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.ApplyStrategyToAllGroupsCommand.Execute(SelectionStrategy.KeepNone);
        }

        #endregion Auto Selection - All Groups

        #region File Operations

        private async void MoveToSingleFolder_Click(object sender, RoutedEventArgs e)
        {
            int count = ViewModel.TotalSelectedCount;
            if (count == 0 || !ViewModel.CanMoveOrCopy)
            {
                return;
            }

            string? folderPath = await _dialogService.PickFolderAsync(L("ManagementPage_Dialog_SelectDestination"));
            if (string.IsNullOrEmpty(folderPath))
            {
                return;
            }

            bool confirmed = await _dialogService.ShowConfirmationAsync(
                L("ManagementPage_Dialog_ConfirmMove"),
                L("ManagementPage_Dialog_MoveMessage", count, folderPath),
                L("ManagementPage_Dialog_MoveButton"));
            if (!confirmed)
            {
                return;
            }

            LogFileOperationStarting("Move", count, folderPath);

            FileOperationResult result = await ViewModel.MoveToSingleFolderAsync(folderPath);

            await _dialogService.ShowOperationResultAsync(L("ManagementPage_Dialog_MoveButton"), result.SuccessCount, result.FailedCount, _localizer);
        }

        private async void MoveToGroupFolders_Click(object sender, RoutedEventArgs e)
        {
            int count = ViewModel.TotalSelectedCount;
            int groupCount = ViewModel.GroupsWithSelectionsCount;
            if (count == 0 || !ViewModel.CanMoveOrCopy)
            {
                return;
            }

            string? folderPath = await _dialogService.PickFolderAsync(L("ManagementPage_Dialog_SelectRootFolder"));
            if (string.IsNullOrEmpty(folderPath))
            {
                return;
            }

            string groupInfo = L("ManagementPage_Dialog_GroupFolderInfo", groupCount);
            bool confirmed = await _dialogService.ShowConfirmationAsync(
                L("ManagementPage_Dialog_ConfirmMove"),
                L("ManagementPage_Dialog_MoveMessage", count, folderPath) + groupInfo,
                L("ManagementPage_Dialog_MoveButton"));
            if (!confirmed)
            {
                return;
            }

            LogFileOperationStarting("Move to group folders", count, folderPath);

            FileOperationResult result = await ViewModel.MoveToGroupFoldersAsync(folderPath);

            await _dialogService.ShowOperationResultAsync(L("ManagementPage_Dialog_MoveButton"), result.SuccessCount, result.FailedCount, _localizer);
        }

        private async void CopyToSingleFolder_Click(object sender, RoutedEventArgs e)
        {
            int count = ViewModel.TotalSelectedCount;
            if (count == 0 || !ViewModel.CanMoveOrCopy)
            {
                return;
            }

            string? folderPath = await _dialogService.PickFolderAsync(L("ManagementPage_Dialog_SelectDestination"));
            if (string.IsNullOrEmpty(folderPath))
            {
                return;
            }

            bool confirmed = await _dialogService.ShowConfirmationAsync(
                L("ManagementPage_Dialog_ConfirmCopy"),
                L("ManagementPage_Dialog_CopyMessage", count, folderPath),
                L("ManagementPage_Dialog_CopyButton"));
            if (!confirmed)
            {
                return;
            }

            LogFileOperationStarting("Copy", count, folderPath);

            FileOperationResult result = await ViewModel.CopyToSingleFolderAsync(folderPath);

            await _dialogService.ShowOperationResultAsync(L("ManagementPage_Dialog_CopyButton"), result.SuccessCount, result.FailedCount, _localizer);
        }

        private async void CopyToGroupFolders_Click(object sender, RoutedEventArgs e)
        {
            int count = ViewModel.TotalSelectedCount;
            int groupCount = ViewModel.GroupsWithSelectionsCount;
            if (count == 0 || !ViewModel.CanMoveOrCopy)
            {
                return;
            }

            string? folderPath = await _dialogService.PickFolderAsync(L("ManagementPage_Dialog_SelectRootFolder"));
            if (string.IsNullOrEmpty(folderPath))
            {
                return;
            }

            string groupInfo = L("ManagementPage_Dialog_GroupFolderInfo", groupCount);
            bool confirmed = await _dialogService.ShowConfirmationAsync(
                L("ManagementPage_Dialog_ConfirmCopy"),
                L("ManagementPage_Dialog_CopyMessage", count, folderPath) + groupInfo,
                L("ManagementPage_Dialog_CopyButton"));
            if (!confirmed)
            {
                return;
            }

            LogFileOperationStarting("Copy to group folders", count, folderPath);

            FileOperationResult result = await ViewModel.CopyToGroupFoldersAsync(folderPath);

            await _dialogService.ShowOperationResultAsync(L("ManagementPage_Dialog_CopyButton"), result.SuccessCount, result.FailedCount, _localizer);
        }

        private async void ExtractGroups_Click(object sender, RoutedEventArgs e)
        {
            int groupCount = ViewModel.GroupsWithAnySelectionCount;
            int totalFiles = ViewModel.TotalFilesInSelectedGroups;
            if (groupCount == 0 || !ViewModel.CanMoveOrCopy)
            {
                return;
            }

            string? folderPath = await _dialogService.PickFolderAsync(L("ManagementPage_Dialog_SelectRootFolder"));
            if (string.IsNullOrEmpty(folderPath))
            {
                return;
            }

            bool confirmed = await _dialogService.ShowConfirmationAsync(
                L("ManagementPage_Dialog_ConfirmExtract"),
                L("ManagementPage_Dialog_ExtractMessage", totalFiles, groupCount, folderPath),
                L("ManagementPage_Dialog_ExtractButton"));
            if (!confirmed)
            {
                return;
            }

            LogFileOperationStarting("Extract groups", totalFiles, folderPath);

            FileOperationResult result = await ViewModel.ExtractGroupsAsync(folderPath);

            await _dialogService.ShowOperationResultAsync(L("ManagementPage_Dialog_ExtractButton"), result.SuccessCount, result.FailedCount, _localizer);
        }

        private void DeleteSplitButton_Click(SplitButton sender, SplitButtonClickEventArgs args)
        {
            DeleteSelectedFiles_Click(sender, new RoutedEventArgs());
        }

        private void DeleteToRecycleBin_Click(object sender, RoutedEventArgs e)
        {
            DeleteSelectedFiles_Click(sender, e);
        }

        private async void DeletePermanently_Click(object sender, RoutedEventArgs e)
        {
            int count = ViewModel.TotalSelectedCount;
            if (count == 0)
            {
                return;
            }

            bool confirmed = await _dialogService.ShowConfirmationAsync(
                L("ManagementPage_Dialog_PermanentDeletion"),
                L("ManagementPage_Dialog_PermanentDeleteMessage", count),
                L("ManagementPage_Dialog_DeletePermanentlyButton"),
                L("ManagementPage_Dialog_CancelButton"),
                destructive: true);

            if (confirmed)
            {
                LogFileOperationStarting("Permanent delete", count, "N/A");
                await ViewModel.DeletePermanentlyCommand.ExecuteAsync(null);
            }
        }

        private async void DeleteSelectedFiles_Click(object _, RoutedEventArgs __)
        {
            int count = ViewModel.TotalSelectedCount;
            if (count == 0)
            {
                return;
            }

            bool confirmed = await _dialogService.ShowConfirmationAsync(
                L("ManagementPage_Dialog_ConfirmDeletion"),
                L("ManagementPage_Dialog_RecycleBinMessage", count),
                L("ManagementPage_Dialog_DeleteButton"),
                L("ManagementPage_Dialog_CancelButton"),
                destructive: true);

            if (confirmed)
            {
                LogFileOperationStarting("Recycle bin delete", count, "N/A");
                await ViewModel.DeleteSelectedFilesCommand.ExecuteAsync(null);
            }
        }

        #endregion File Operations

        #region Group Renaming

        private bool _isEditingGroupName;

        private bool _isConfirmingRename;

        private void GroupNameTextBlock_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            EnterEditMode();
        }

        private void EditGroupNameButton_Click(object sender, RoutedEventArgs e)
        {
            EnterEditMode();
        }

        private void ConfirmRenameButton_Click(object sender, RoutedEventArgs e)
        {
            ExitEditMode(true);
        }

        private void CancelRenameButton_Click(object sender, RoutedEventArgs e)
        {
            ExitEditMode(false);
        }

        private void GroupNameTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                ExitEditMode(true);
                e.Handled = true;
            }
            else if (e.Key == Windows.System.VirtualKey.Escape)
            {
                ExitEditMode(false);
                e.Handled = true;
            }
        }

        private void GroupNameTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (!_isEditingGroupName || _isConfirmingRename)
            {
                return;
            }

            DispatcherQueue.TryEnqueue(() =>
            {
                if (_isEditingGroupName && !_isConfirmingRename)
                {
                    ExitEditMode(true);
                }
            });
        }

        private void EnterEditMode()
        {
            if (_isEditingGroupName || ViewModel.SelectedGroup == null)
            {
                return;
            }

            _isEditingGroupName = true;

            GroupNameTextBox.Text = ViewModel.SelectedGroup.Name;

            GroupNameDisplayPanel.Visibility = Visibility.Collapsed;
            GroupNameEditPanel.Visibility = Visibility.Visible;

            GroupNameTextBox.Focus(FocusState.Programmatic);
            GroupNameTextBox.SelectAll();
        }

        private void ExitEditMode(bool save)
        {
            if (!_isEditingGroupName)
            {
                return;
            }

            _isConfirmingRename = true;

            if (save && ViewModel.SelectedGroup != null)
            {
                string newName = GroupNameTextBox.Text?.Trim() ?? string.Empty;

                if (!string.IsNullOrWhiteSpace(newName) && newName != ViewModel.SelectedGroup.Name)
                {
                    if (FolderNameValidationService.Validate(newName))
                    {
                        ViewModel.SelectedGroup.Name = newName;
                    }
                    else
                    {
                        string? sanitized = FolderNameValidationService.Sanitize(newName);
                        LogGroupNameSanitized(newName, sanitized ?? "(null)");
                        ViewModel.SelectedGroup.Name = sanitized ?? ViewModel.SelectedGroup.Name;
                    }
                }
            }

            GroupNameEditPanel.Visibility = Visibility.Collapsed;
            GroupNameDisplayPanel.Visibility = Visibility.Visible;

            _isEditingGroupName = false;
            _isConfirmingRename = false;
        }

        #endregion Group Renaming

        #region Batch Image Viewer

        [LibraryImport("kernel32.dll", EntryPoint = "CreateHardLinkW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool CreateHardLink(string lpFileName, string lpExistingFileName, IntPtr lpSecurityAttributes);

        private void ImageCardsRepeater_ElementPrepared(ItemsRepeater sender, ItemsRepeaterElementPreparedEventArgs args)
        {
            if (args.Element is ImageDetailCard card)
            {
                card.ImageOpenRequested += OnImageOpenRequested;
            }
        }

        private void ImageCardsRepeater_ElementClearing(ItemsRepeater sender, ItemsRepeaterElementClearingEventArgs args)
        {
            if (args.Element is ImageDetailCard card)
            {
                card.ImageOpenRequested -= OnImageOpenRequested;
            }
        }

        private async void OnImageOpenRequested(object? sender, SelectableItem targetItem)
        {
            SimilarityGroup? group = ViewModel.SelectedGroup;
            if (group == null)
            {
                return;
            }

            try
            {
                List<string> groupFilePaths = group.SelectableItems.Select(i => i.FilePath).ToList();

                if (groupFilePaths.Count <= 1)
                {
                    // Single image - just open directly
                    StorageFile file = await StorageFile.GetFileFromPathAsync(targetItem.FilePath);
                    await Launcher.LaunchFileAsync(file);
                    return;
                }

                string tempFolder = PrepareBatchFolder(group.Id, groupFilePaths, targetItem.FilePath, out string targetTempPath);

                StorageFolder folder = await StorageFolder.GetFolderFromPathAsync(tempFolder);
                StorageFile targetFile = await StorageFile.GetFileFromPathAsync(targetTempPath);

                QueryOptions queryOptions = new(CommonFileQuery.DefaultQuery, [".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tiff", ".tif", ".webp", ".heic", ".heif", ".avif", ".jxl"])
                {
                    SortOrder = { new SortEntry { PropertyName = "System.FileName", AscendingOrder = true } }
                };

                StorageFileQueryResult queryResult = folder.CreateFileQueryWithOptions(queryOptions);

                LauncherOptions options = new()
                {
                    NeighboringFilesQuery = queryResult
                };

                await Launcher.LaunchFileAsync(targetFile, options);
            }
            catch (Exception ex)
            {
                LogBatchImageOpenFailed(targetItem.FilePath, ex);

                // Fallback: open single file directly
                try
                {
                    StorageFile file = await StorageFile.GetFileFromPathAsync(targetItem.FilePath);
                    await Launcher.LaunchFileAsync(file);
                }
                catch (Exception fallbackEx)
                {
                    LogBatchImageOpenFailed(targetItem.FilePath, fallbackEx);
                }
            }
        }

        private string PrepareBatchFolder(int groupId, List<string> filePaths, string targetFilePath, out string targetTempPath)
        {
            string baseTempDir = Path.Combine(Path.GetTempPath(), "DeDupe", "BatchView");
            string groupFolder = Path.Combine(baseTempDir, $"Group_{groupId}");

            // Clean up existing folder for this group to avoid stale files
            if (Directory.Exists(groupFolder))
            {
                try { Directory.Delete(groupFolder, true); } catch { /* best effort */ }
            }

            Directory.CreateDirectory(groupFolder);

            targetTempPath = string.Empty;
            int index = 0;

            foreach (string filePath in filePaths)
            {
                string extension = Path.GetExtension(filePath);
                string originalName = Path.GetFileNameWithoutExtension(filePath);
                // Prefix with index to control navigation order
                string linkedName = $"{index:D3}_{originalName}{extension}";
                string linkedPath = Path.Combine(groupFolder, linkedName);

                bool linked = CreateHardLink(linkedPath, filePath, IntPtr.Zero);

                if (!linked)
                {
                    // Fallback to file copy if hard link fails (cross-volume, network, etc.)
                    try
                    {
                        File.Copy(filePath, linkedPath, overwrite: true);
                        linked = true;
                    }
                    catch
                    {
                        // Skip files that can't be linked or copied
                    }
                }

                if (linked && string.Equals(filePath, targetFilePath, StringComparison.OrdinalIgnoreCase))
                {
                    targetTempPath = linkedPath;
                }

                index++;
            }

            // If target wasn't matched (shouldn't happen), pick the first file
            if (string.IsNullOrEmpty(targetTempPath))
            {
                string[] files = Directory.GetFiles(groupFolder);
                targetTempPath = files.Length > 0 ? files[0] : targetFilePath;
            }

            return groupFolder;
        }

        #endregion Batch Image Viewer

        #region Logging

        [LoggerMessage(Level = LogLevel.Information, Message = "File operation starting: {Operation} for {FileCount} files to {DestinationPath}")]
        private partial void LogFileOperationStarting(string operation, int fileCount, string destinationPath);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Group name sanitized from {OriginalName} to {SanitizedName}")]
        private partial void LogGroupNameSanitized(string originalName, string sanitizedName);

        [LoggerMessage(Level = LogLevel.Debug, Message = "View mode switched to {ViewMode}")]
        private partial void LogViewSwitched(string viewMode);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Batch image open failed for {FilePath}")]
        private partial void LogBatchImageOpenFailed(string filePath, Exception ex);

        #endregion Logging
    }
}