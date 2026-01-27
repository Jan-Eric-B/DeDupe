using DeDupe.Enums;
using DeDupe.Models.Analysis;
using DeDupe.Models.Results;
using DeDupe.Services;
using DeDupe.ViewModels;
using DeDupe.ViewModels.Pages;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;
using System.ComponentModel;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace DeDupe.Views.Pages
{
    public sealed partial class ManagementPage : Page
    {
        #region Fields

        private bool _isEditingGroupName;
        private bool _isConfirmingRename;
        private double _rightPanelWidthRatio = 0.4;
        private bool _isPanelOpen = false;

        private SimilarityGroup? _subscribedCluster;

        #endregion Fields

        #region Properties

        public ManagementViewModel ViewModel { get; }

        #endregion Properties

        #region Constructor

        public ManagementPage()
        {
            InitializeComponent();
            ViewModel = App.Current.GetService<ManagementViewModel>();
            DataContext = ViewModel;

            ViewModel.PropertyChanged += ViewModel_PropertyChanged;
            SplitViewGrid.SizeChanged += SplitViewGrid_SizeChanged;
        }

        #endregion Constructor

        #region ViewModel Event Handlers

        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ManagementViewModel.SelectedCluster))
            {
                HandleClusterSelectionChanged();
            }
        }

        #endregion ViewModel Event Handlers

        #region Panel Management

        private void SplitViewGrid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_isPanelOpen && e.NewSize.Width > 0)
            {
                UpdateRightPanelWidth();
            }
        }

        private void HandleClusterSelectionChanged()
        {
            ExitEditMode(false);

            // Unsubscribe from cluster
            UnsubscribeFromCluster();

            if (ViewModel.SelectedCluster != null)
            {
                // Subscribe to cluster events
                SubscribeToCluster(ViewModel.SelectedCluster);

                SetupPanelForCluster();

                UpdateSelection();

                ShowGroupContentPanelStoryboard.Begin();
            }
            else
            {
                // Save width ratio before closing
                SavePanelWidthRatio();

                HideRightPanel();

                HideGroupContentPanelStoryboard.Begin();
            }
        }

        private void SubscribeToCluster(SimilarityGroup cluster)
        {
            _subscribedCluster = cluster;
            cluster.PropertyChanged += OnClusterPropertyChanged;
        }

        private void UnsubscribeFromCluster()
        {
            if (_subscribedCluster != null)
            {
                _subscribedCluster.PropertyChanged -= OnClusterPropertyChanged;
                _subscribedCluster = null;
            }
        }

        private void OnClusterPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // update UI when selection change
            if (e.PropertyName is nameof(SimilarityGroup.IsSelected) or nameof(SimilarityGroup.SelectedCount) or nameof(SimilarityGroup.AllSelected) or nameof(SimilarityGroup.NoneSelected))
            {
                DispatcherQueue.TryEnqueue(UpdateSelection);
            }
        }

        private void SetupPanelForCluster()
        {
            double totalWidth = SplitViewGrid.ActualWidth;

            if (totalWidth <= 0)
            {
                totalWidth = 1000; // Fallback
            }

            double rightPanelWidth = totalWidth * _rightPanelWidthRatio;

            // Constraints
            double minWidth = 300;
            double maxWidth = totalWidth * 0.7;

            if (maxWidth < minWidth)
            {
                maxWidth = minWidth;
            }

            rightPanelWidth = System.Math.Clamp(rightPanelWidth, minWidth, maxWidth);

            if (!_isPanelOpen)
            {
                SplitViewGrid.ColumnDefinitions[1].Width = new GridLength(0, GridUnitType.Auto);
                SplitViewGrid.ColumnDefinitions[2].Width = new GridLength(rightPanelWidth, GridUnitType.Pixel);
                SplitViewGrid.ColumnDefinitions[2].MinWidth = minWidth;

                _isPanelOpen = true;
            }
        }

        private void SavePanelWidthRatio()
        {
            if (_isPanelOpen && SplitViewGrid.ActualWidth > 0)
            {
                double currentRightWidth = SplitViewGrid.ColumnDefinitions[2].ActualWidth;
                if (currentRightWidth > 0)
                {
                    _rightPanelWidthRatio = currentRightWidth / SplitViewGrid.ActualWidth;
                    _rightPanelWidthRatio = System.Math.Clamp(_rightPanelWidthRatio, 0.2, 0.7);
                }
            }
        }

        private void HideRightPanel()
        {
            SplitViewGrid.ColumnDefinitions[2].MinWidth = 0;
            SplitViewGrid.ColumnDefinitions[1].Width = new GridLength(0);
            SplitViewGrid.ColumnDefinitions[2].Width = new GridLength(0);

            _isPanelOpen = false;
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
            rightPanelWidth = System.Math.Clamp(rightPanelWidth, minWidth, maxWidth);

            SplitViewGrid.ColumnDefinitions[2].Width = new GridLength(rightPanelWidth, GridUnitType.Pixel);
        }

        private void BackToConfiguration_Click(object sender, RoutedEventArgs e)
        {
            MainWindowViewModel? mainViewModel = App.Current.GetService<MainWindowViewModel>();
            mainViewModel?.BackToConfigurationCommand.Execute(null);
        }

        private void CloseGroupContentPanel_Click(object sender, RoutedEventArgs e)
        {
            ExitEditMode(false);
            ViewModel.SelectedCluster = null;
        }

        #endregion Panel Management

        #region Selection Handling

        /// <summary>
        /// Update SelectAllCheckBox and SelectionCountText to match current cluster's state.
        /// </summary>
        private void UpdateSelection()
        {
            if (ViewModel.SelectedCluster == null)
            {
                return;
            }

            SimilarityGroup cluster = ViewModel.SelectedCluster;

            // Update checkbox state to match cluster's IsSelected
            SelectAllCheckBox.IsChecked = cluster.IsSelected;

            SelectionCountText.Text = $"{cluster.SelectedCount} of {cluster.Count} selected";
        }

        /// <summary>
        /// Handle user clicking SelectAll checkbox.
        /// </summary>
        private void SelectAllGroupCheckBox_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.SelectedCluster == null)
            {
                return;
            }

            if (ViewModel.SelectedCluster.AllSelected)
            {
                ViewModel.SelectedCluster.DeselectAll();
            }
            else
            {
                ViewModel.SelectedCluster.SelectAll();
            }
        }

        /// <summary>
        /// Handle user clicking SelectAll checkbox.
        /// </summary>
        private void SelectAllCheckBox_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.ApplyStrategyToAllGroupsCommand.Execute(SelectionStrategy.KeepNone);
        }

        #endregion Selection Handling

        #region Sort Handlers

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

        #endregion Sort Handlers

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

        private void ClearGroupSelection_Click(object sender, RoutedEventArgs e)
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

        private void SelectAllSelections_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.ClearAllSelectionsCommand.Execute(null);
        }

        #endregion Auto Selection - All Groups

        #region Delete Functionality

        private async void DeleteSelectedFiles_Click(object sender, RoutedEventArgs e)
        {
            int count = ViewModel.TotalSelectedCount;
            if (count == 0)
            {
                return;
            }

            // Show confirmation dialog
            ContentDialog confirmDialog = new()
            {
                Title = "Confirm Deletion",
                Content = $"Are you sure you want to move {count} file{(count == 1 ? "" : "s")} to the Recycle Bin?\n\nThis action can be undone from the Recycle Bin.",
                PrimaryButtonText = "Delete",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.XamlRoot
            };

            ContentDialogResult result = await confirmDialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                await ViewModel.DeleteSelectedFilesCommand.ExecuteAsync(null);
            }
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

            // Show warning dialog for permanent deletion
            ContentDialog confirmDialog = new()
            {
                Title = "Permanent Deletion",
                Content = $"Are you sure you want to PERMANENTLY delete {count} file{(count == 1 ? "" : "s")}?\n\nThis action cannot be undone!",
                PrimaryButtonText = "Delete Permanently",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.XamlRoot
            };

            ContentDialogResult result = await confirmDialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                // TODO: Implement permanent deletion
                await ViewModel.DeletePermanentlyCommand.ExecuteAsync(null);
            }
        }

        #endregion Delete Functionality

        #region Move and Copy Functionality

        /// <summary>
        /// Show folder picker dialog and return selected folder path.
        /// </summary>
        /// <param name="title">Dialog title for context.</param>
        /// <returns>Selected folder path, or null if cancelled.</returns>
        private async System.Threading.Tasks.Task<string?> PickFolderAsync(string title)
        {
            FolderPicker folderPicker = new()
            {
                ViewMode = PickerViewMode.List,
                SuggestedStartLocation = PickerLocationId.PicturesLibrary,
                CommitButtonText = "Select Folder"
            };

            // FileTypeFilter is required even for folder pickers
            folderPicker.FileTypeFilter.Add("*");

            // Initialize with window handle
            nint windowHandle = WindowNative.GetWindowHandle(App.Window);
            InitializeWithWindow.Initialize(folderPicker, windowHandle);

            StorageFolder? folder = await folderPicker.PickSingleFolderAsync();
            return folder?.Path;
        }

        /// <summary>
        /// Show confirmation dialog for move/copy operations.
        /// </summary>
        private async System.Threading.Tasks.Task<bool> ShowMoveConfirmationAsync(string operation, string destination, int fileCount, int groupCount, bool isGrouped)
        {
            string groupInfo = isGrouped
                ? $"\n\nThis will create {groupCount} folder{(groupCount == 1 ? "" : "s")} named after each group."
                : "";

            string message = $"{operation} {fileCount} file{(fileCount == 1 ? "" : "s")} to:\n{destination}{groupInfo}";

            ContentDialog confirmDialog = new()
            {
                Title = $"Confirm {operation}",
                Content = message,
                PrimaryButtonText = operation,
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.XamlRoot
            };

            ContentDialogResult result = await confirmDialog.ShowAsync();
            return result == ContentDialogResult.Primary;
        }

        /// <summary>
        /// Show result dialog after operation.
        /// </summary>
        private async System.Threading.Tasks.Task ShowOperationResultAsync(string operation, FileOperationResult result)
        {
            if (result.HasFailures)
            {
                string message = $"Completed with some issues:\n\n" +
                                $"✓ {result.SuccessCount} file{(result.SuccessCount == 1 ? "" : "s")} {operation.ToLower()}d successfully\n" +
                                $"✗ {result.FailedCount} file{(result.FailedCount == 1 ? "" : "s")} failed";

                ContentDialog resultDialog = new()
                {
                    Title = $"{operation} Completed",
                    Content = message,
                    CloseButtonText = "OK",
                    XamlRoot = this.XamlRoot
                };

                await resultDialog.ShowAsync();
            }
        }

        /// <summary>
        /// Move all selected files to a single folder.
        /// </summary>
        private async void MoveToSingleFolder_Click(object sender, RoutedEventArgs e)
        {
            int count = ViewModel.TotalSelectedCount;
            if (count == 0 || !ViewModel.CanMoveOrCopy)
            {
                return;
            }

            // Pick destination folder
            string? folderPath = await PickFolderAsync("Select destination folder");
            if (string.IsNullOrEmpty(folderPath))
            {
                return;
            }

            // Show confirmation
            bool confirmed = await ShowMoveConfirmationAsync("Move", folderPath, count, 0, isGrouped: false);
            if (!confirmed)
            {
                return;
            }

            // Execute move
            FileOperationResult result = await ViewModel.MoveToSingleFolderAsync(folderPath);

            // Show result if there were failures
            await ShowOperationResultAsync("Move", result);
        }

        /// <summary>
        /// Move selected files into group-named subfolders.
        /// </summary>
        private async void MoveToGroupFolders_Click(object sender, RoutedEventArgs e)
        {
            int count = ViewModel.TotalSelectedCount;
            int groupCount = ViewModel.GroupsWithSelectionsCount;
            if (count == 0 || !ViewModel.CanMoveOrCopy)
            {
                return;
            }

            // Pick root folder
            string? folderPath = await PickFolderAsync("Select root folder for group organization");
            if (string.IsNullOrEmpty(folderPath))
            {
                return;
            }

            // Show confirmation
            bool confirmed = await ShowMoveConfirmationAsync("Move", folderPath, count, groupCount, isGrouped: true);
            if (!confirmed)
            {
                return;
            }

            // Execute move
            FileOperationResult result = await ViewModel.MoveToGroupFoldersAsync(folderPath);

            // Show result if there were failures
            await ShowOperationResultAsync("Move", result);
        }

        /// <summary>
        /// Copy all selected files to a single folder.
        /// </summary>
        private async void CopyToSingleFolder_Click(object sender, RoutedEventArgs e)
        {
            int count = ViewModel.TotalSelectedCount;
            if (count == 0 || !ViewModel.CanMoveOrCopy)
            {
                return;
            }

            // Pick destination folder
            string? folderPath = await PickFolderAsync("Select destination folder");
            if (string.IsNullOrEmpty(folderPath))
            {
                return;
            }

            // Show confirmation
            bool confirmed = await ShowMoveConfirmationAsync("Copy", folderPath, count, 0, isGrouped: false);
            if (!confirmed)
            {
                return;
            }

            // Execute copy
            FileOperationResult result = await ViewModel.CopyToSingleFolderAsync(folderPath);

            // Show result if there were failures
            await ShowOperationResultAsync("Copy", result);
        }

        /// <summary>
        /// Copy selected files into group-named subfolders.
        /// </summary>
        private async void CopyToGroupFolders_Click(object sender, RoutedEventArgs e)
        {
            int count = ViewModel.TotalSelectedCount;
            int groupCount = ViewModel.GroupsWithSelectionsCount;
            if (count == 0 || !ViewModel.CanMoveOrCopy)
            {
                return;
            }

            // Pick root folder
            string? folderPath = await PickFolderAsync("Select root folder for group organization");
            if (string.IsNullOrEmpty(folderPath))
            {
                return;
            }

            // Show confirmation
            bool confirmed = await ShowMoveConfirmationAsync("Copy", folderPath, count, groupCount, isGrouped: true);
            if (!confirmed)
            {
                return;
            }

            // Execute copy
            FileOperationResult result = await ViewModel.CopyToGroupFoldersAsync(folderPath);

            // Show result if there were failures
            await ShowOperationResultAsync("Copy", result);
        }

        #endregion Move and Copy Functionality

        #region Group Name Editing

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
            if (_isEditingGroupName && !_isConfirmingRename)
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    if (_isEditingGroupName && !_isConfirmingRename)
                    {
                        ExitEditMode(true);
                    }
                });
            }
        }

        private void EnterEditMode()
        {
            if (_isEditingGroupName || ViewModel.SelectedCluster == null)
            {
                return;
            }

            _isEditingGroupName = true;

            GroupNameTextBox.Text = ViewModel.SelectedCluster.Name;

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

            if (save && ViewModel.SelectedCluster != null)
            {
                string newName = GroupNameTextBox.Text?.Trim() ?? string.Empty;

                if (!string.IsNullOrWhiteSpace(newName) && newName != ViewModel.SelectedCluster.Name)
                {
                    if (FolderNameValidationService.Validate(newName))
                    {
                        ViewModel.SelectedCluster.Name = newName;
                    }
                    else
                    {
                        ViewModel.SelectedCluster.Name = FolderNameValidationService.Sanitize(newName);
                    }
                }
            }

            GroupNameEditPanel.Visibility = Visibility.Collapsed;
            GroupNameDisplayPanel.Visibility = Visibility.Visible;

            _isEditingGroupName = false;
            _isConfirmingRename = false;
        }

        #endregion Group Name Editing
    }
}