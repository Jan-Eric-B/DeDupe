using DeDupe.Enums;
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
using System.ComponentModel;

namespace DeDupe.Views.Pages
{
    public sealed partial class ManagementPage : Page
    {
        private readonly ILogger<ManagementPage> _logger = App.Current.GetService<ILogger<ManagementPage>>();
        private readonly IDialogService _dialogService = App.Current.GetService<IDialogService>();

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

        private async void MoveToSingleFolder_Click(object sender, RoutedEventArgs e)
        {
            int count = ViewModel.TotalSelectedCount;
            if (count == 0 || !ViewModel.CanMoveOrCopy)
            {
                return;
            }

            string? folderPath = await _dialogService.PickFolderAsync("Select destination folder");
            if (string.IsNullOrEmpty(folderPath))
            {
                return;
            }

            bool confirmed = await _dialogService.ShowConfirmationAsync("Confirm Move", $"Move {count} file{(count == 1 ? "" : "s")} to:\n{folderPath}", "Move");
            if (!confirmed)
            {
                return;
            }

            LogFileOperationStarting("Move", count, folderPath);

            FileOperationResult result = await ViewModel.MoveToSingleFolderAsync(folderPath);

            await _dialogService.ShowOperationResultAsync("Move", result.SuccessCount, result.FailedCount);
        }

        private async void MoveToGroupFolders_Click(object sender, RoutedEventArgs e)
        {
            int count = ViewModel.TotalSelectedCount;
            int groupCount = ViewModel.GroupsWithSelectionsCount;
            if (count == 0 || !ViewModel.CanMoveOrCopy)
            {
                return;
            }

            string? folderPath = await _dialogService.PickFolderAsync("Select root folder for group organization");
            if (string.IsNullOrEmpty(folderPath))
            {
                return;
            }

            string groupInfo = $"\n\nThis will create {groupCount} folder{(groupCount == 1 ? "" : "s")} named after each group.";
            bool confirmed = await _dialogService.ShowConfirmationAsync("Confirm Move", $"Move {count} file{(count == 1 ? "" : "s")} to:\n{folderPath}{groupInfo}", "Move");
            if (!confirmed)
            {
                return;
            }

            LogFileOperationStarting("Move to group folders", count, folderPath);

            FileOperationResult result = await ViewModel.MoveToGroupFoldersAsync(folderPath);

            await _dialogService.ShowOperationResultAsync("Move", result.SuccessCount, result.FailedCount);
        }

        private async void CopyToSingleFolder_Click(object sender, RoutedEventArgs e)
        {
            int count = ViewModel.TotalSelectedCount;
            if (count == 0 || !ViewModel.CanMoveOrCopy)
            {
                return;
            }

            string? folderPath = await _dialogService.PickFolderAsync("Select destination folder");
            if (string.IsNullOrEmpty(folderPath))
            {
                return;
            }

            bool confirmed = await _dialogService.ShowConfirmationAsync("Confirm Copy", $"Copy {count} file{(count == 1 ? "" : "s")} to:\n{folderPath}", "Copy");
            if (!confirmed)
            {
                return;
            }

            LogFileOperationStarting("Copy", count, folderPath);

            FileOperationResult result = await ViewModel.CopyToSingleFolderAsync(folderPath);

            await _dialogService.ShowOperationResultAsync("Copy", result.SuccessCount, result.FailedCount);
        }

        private async void CopyToGroupFolders_Click(object sender, RoutedEventArgs e)
        {
            int count = ViewModel.TotalSelectedCount;
            int groupCount = ViewModel.GroupsWithSelectionsCount;
            if (count == 0 || !ViewModel.CanMoveOrCopy)
            {
                return;
            }

            string? folderPath = await _dialogService.PickFolderAsync("Select root folder for group organization");
            if (string.IsNullOrEmpty(folderPath))
            {
                return;
            }

            string groupInfo = $"\n\nThis will create {groupCount} folder{(groupCount == 1 ? "" : "s")} named after each group.";
            bool confirmed = await _dialogService.ShowConfirmationAsync("Confirm Copy", $"Copy {count} file{(count == 1 ? "" : "s")} to:\n{folderPath}{groupInfo}", "Copy");
            if (!confirmed)
            {
                return;
            }

            LogFileOperationStarting("Copy to group folders", count, folderPath);

            FileOperationResult result = await ViewModel.CopyToGroupFoldersAsync(folderPath);

            await _dialogService.ShowOperationResultAsync("Copy", result.SuccessCount, result.FailedCount);
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

            bool confirmed = await _dialogService.ShowConfirmationAsync("Permanent Deletion", $"Are you sure you want to PERMANENTLY delete {count} file{(count == 1 ? "" : "s")}?\n\nThis action cannot be undone!", "Delete Permanently", "Cancel", destructive: true);

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

            bool confirmed = await _dialogService.ShowConfirmationAsync("Confirm Deletion", $"Are you sure you want to move {count} file{(count == 1 ? "" : "s")} to the Recycle Bin?\n\nThis action can be undone from the Recycle Bin.", "Delete", "Cancel", destructive: true);

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

        #region Logging

        [LoggerMessage(Level = LogLevel.Information, Message = "File operation starting: {Operation} for {FileCount} files to {DestinationPath}")]
        private partial void LogFileOperationStarting(string operation, int fileCount, string destinationPath);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Group name sanitized from {OriginalName} to {SanitizedName}")]
        private partial void LogGroupNameSanitized(string originalName, string sanitizedName);

        #endregion Logging
    }
}