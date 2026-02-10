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
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace DeDupe.Views.Pages
{
    public sealed partial class ManagementPage : Page
    {
        private ManagementViewModel ViewModel { get; }

        public ManagementPage()
        {
            InitializeComponent();
            ViewModel = App.Current.GetService<ManagementViewModel>();
            DataContext = ViewModel;

            ViewModel.PropertyChanged += ViewModel_PropertyChanged;
            SplitViewGrid.SizeChanged += SplitViewGrid_SizeChanged;
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

            // Unsubscribe from group
            UnsubscribeFromGroup();

            if (ViewModel.SelectedGroup != null)
            {
                // Subscribe to group events
                SubscribeToGroup(ViewModel.SelectedGroup);

                SetupPanelForGroup();

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

        private void SubscribeToGroup(SimilarityGroup group)
        {
            _subscribedGroup = group;
            group.PropertyChanged += OnGroupPropertyChanged;
        }

        private void UnsubscribeFromGroup()
        {
            if (_subscribedGroup != null)
            {
                _subscribedGroup.PropertyChanged -= OnGroupPropertyChanged;
                _subscribedGroup = null;
            }
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
                    _rightPanelWidthRatio = System.Math.Clamp(_rightPanelWidthRatio, 0.2, 0.7);
                }
            }
        }

        private void SetupPanelForGroup()
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

        #region Selection

        private void OnGroupPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // Update UI on selection change
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

            // Update checkbox state to match group's IsSelected
            SelectAllCheckBox.IsChecked = group.IsSelected;

            SelectionCountText.Text = $"{group.SelectedCount} of {group.Count} selected";
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

        #endregion Auto Selection - All Groups

        #region File Operations

        private static async Task<string?> PickFolderAsync(string title)
        {
            FolderPicker folderPicker = new()
            {
                ViewMode = PickerViewMode.List,
                SuggestedStartLocation = PickerLocationId.PicturesLibrary,
                CommitButtonText = title,
            };

            // Initialize folder picker
            folderPicker.FileTypeFilter.Add("*");
            nint windowHandle = WindowNative.GetWindowHandle(App.Window);
            InitializeWithWindow.Initialize(folderPicker, windowHandle);

            StorageFolder? folder = await folderPicker.PickSingleFolderAsync();
            return folder?.Path;
        }

        private async Task<bool> ShowMoveConfirmationAsync(string operation, string destination, int fileCount, int groupCount, bool isGrouped)
        {
            string groupInfo = isGrouped ? $"\n\nThis will create {groupCount} folder{(groupCount == 1 ? "" : "s")} named after each group." : "";

            string message = $"{operation} {fileCount} file{(fileCount == 1 ? "" : "s")} to:\n{destination}{groupInfo}";

            ContentDialog confirmDialog = new()
            {
                Title = $"Confirm {operation}",
                Content = message,
                PrimaryButtonText = operation,
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = XamlRoot
            };

            ContentDialogResult result = await confirmDialog.ShowAsync();
            return result == ContentDialogResult.Primary;
        }

        private async Task ShowOperationResultAsync(string operation, FileOperationResult result)
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
                    XamlRoot = XamlRoot
                };

                await resultDialog.ShowAsync();
            }
        }

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

            // Show result
            await ShowOperationResultAsync("Move", result);
        }

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

            // Show result
            await ShowOperationResultAsync("Move", result);
        }

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

            // Show result
            await ShowOperationResultAsync("Copy", result);
        }

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

            // Show result
            await ShowOperationResultAsync("Copy", result);
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
                    ViewModel.SelectedGroup.Name = FolderNameValidationService.Validate(newName) ? newName : FolderNameValidationService.Sanitize(newName) ?? ViewModel.SelectedGroup.Name;
                }
            }

            GroupNameEditPanel.Visibility = Visibility.Collapsed;
            GroupNameDisplayPanel.Visibility = Visibility.Visible;

            _isEditingGroupName = false;
            _isConfirmingRename = false;
        }

        #endregion Group Renaming
    }
}