using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeDupe.Enums;
using DeDupe.Localization;
using DeDupe.Models;
using DeDupe.Models.Analysis;
using DeDupe.Models.Configuration;
using DeDupe.Models.Results;
using DeDupe.Services;
using DeDupe.Services.Analysis;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DeDupe.ViewModels.Pages
{
    public partial class ManagementViewModel : PageViewModelBase
    {
        private readonly IAppStateService _appStateService;
        private readonly ISettingsService _settingsService;
        private readonly ISimilarityAnalysisService _similarityAnalysisService;
        private readonly IAutoSelectionService _autoSelectionService;
        private readonly IFileOperationService _fileOperationService;
        private readonly IDialogService _dialogService;
        private readonly ILogger<ManagementViewModel> _logger;

        public ManagementViewModel(IAppStateService appStateService, ISettingsService settingsService, ISimilarityAnalysisService similarityAnalysisService, IAutoSelectionService autoSelectionService, IFileOperationService fileOperationService, IDialogService dialogService, ILocalizer localizer, ILogger<ManagementViewModel> logger, MainWindowViewModel mainWindowViewModel) : base(localizer, () => mainWindowViewModel.StartManagementModeCommand.Execute(null))
        {
            _appStateService = appStateService ?? throw new ArgumentNullException(nameof(appStateService));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _similarityAnalysisService = similarityAnalysisService ?? throw new ArgumentNullException(nameof(similarityAnalysisService));
            _autoSelectionService = autoSelectionService ?? throw new ArgumentNullException(nameof(autoSelectionService));
            _fileOperationService = fileOperationService ?? throw new ArgumentNullException(nameof(fileOperationService));
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            Title = "Duplicate Management";
            Status = "Ready to analyze similarities";

            _appStateService.ExtractedFeaturesChanged += OnExtractedFeaturesChanged;

            SimilarityThreshold = _settingsService.SimilarityThreshold;

            UpdateCanAnalyze();
        }

        #region Localization

        private void OnLanguageChanged(object? sender, LanguageChangedEventArgs e)
        {
            Title = "Duplicate Management";
        }

        #endregion Localization

        #region Similarity Analysis

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanStartSimilarityAnalysis), nameof(IsCancellable))]
        [NotifyCanExecuteChangedFor(nameof(AnalyzeSimilarityCommand), nameof(CancelAnalysisCommand))]
        public partial bool IsAnalyzingSimilarity { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ShowEmptyState))]
        public partial bool HasSimilarityResults { get; set; }

        [ObservableProperty]
        public partial double SimilarityThreshold { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(TotalGroups), nameof(DuplicateGroupsCount), nameof(TotalItems), nameof(ResultSummary))]
        public partial SimilarityResult? SimilarityResult { get; set; }

        [ObservableProperty]
        public partial double AnalysisProgressPercentage { get; set; }

        public int TotalGroups => SimilarityResult?.TotalClusters ?? 0;

        public int DuplicateGroupsCount => SimilarityResult?.DuplicateGroupsCount ?? 0;

        public int TotalItems => SimilarityResult?.TotalItemsAnalyzed ?? 0;

        public string ResultSummary => SimilarityResult?.GetSummary() ?? string.Empty;

        public int TotalFileCount => _appStateService.SourceMediaCount;

        public int ExtractedFeaturesCount => _appStateService.ExtractedFeaturesCount;

        private CancellationTokenSource? _analysisCts;

        public bool IsCancellable => IsAnalyzingSimilarity;

        public string ModelName
        {
            get
            {
                if (_settingsService.UseBundledModel)
                {
                    return BundledModelInfo.DisplayName;
                }
                else
                {
                    if (string.IsNullOrEmpty(_settingsService.CustomModelFilePath))
                    {
                        return "No model selected";
                    }
                    return Path.GetFileNameWithoutExtension(_settingsService.CustomModelFilePath);
                }
            }
        }

        private readonly List<SimilarityGroup> _allDuplicateGroups = [];

        public ObservableCollection<SimilarityGroup> SimilarityGroups { get; } = [];

        public bool CanStartSimilarityAnalysis => !IsAnalyzingSimilarity && _appStateService.ExtractedFeaturesCount > 0;

        public bool ShowEmptyState => !HasSimilarityResults;

        [RelayCommand(CanExecute = nameof(CanStartSimilarityAnalysis))]
        private async Task AnalyzeSimilarityAsync()
        {
            // Cancel any existing analysis
            if (_analysisCts is not null)
            {
                await _analysisCts.CancelAsync();
            }

            _analysisCts = new CancellationTokenSource();
            CancellationToken ct = _analysisCts.Token;

            try
            {
                IsAnalyzingSimilarity = true;
                IsBusy = true;
                AnalysisProgressPercentage = 0;
                Status = "Analyzing similarities...";

                // Get items with features
                IReadOnlyCollection<AnalysisItem> itemsWithFeatures = _appStateService.ItemsWithFeatures;

                if (itemsWithFeatures.Count == 0)
                {
                    Status = "No features available. Please extract features first.";
                    return;
                }

                LogSimilarityAnalysisStarting(itemsWithFeatures.Count, SimilarityThreshold);

                // Create progress handler
                Progress<ProgressInfo> analysisProgress = new(info =>
                {
                    AnalysisProgressPercentage = info.Percentage;
                    Status = info.StatusText;
                });

                // Perform clustering with cancellation token
                SimilarityResult = await _similarityAnalysisService.ClusterAsync(itemsWithFeatures, SimilarityThreshold, analysisProgress, ct);

                HasSimilarityResults = true;
                AnalysisProgressPercentage = 100;
                Status = $"Analysis complete: {ResultSummary}";

                LogSimilarityAnalysisCompleted(ResultSummary);
            }
            catch (OperationCanceledException)
            {
                Status = "Analysis cancelled";
                AnalysisProgressPercentage = 0;
                LogSimilarityAnalysisCancelled();
            }
            catch (Exception ex)
            {
                Status = $"Error during similarity analysis: {ex.Message}";
                LogSimilarityAnalysisAborted(ex);
                HasSimilarityResults = false;
            }
            finally
            {
                IsAnalyzingSimilarity = false;
                IsBusy = false;

                // Reset progress
                await Task.Delay(2000);
                if (!IsAnalyzingSimilarity)
                {
                    AnalysisProgressPercentage = 0;
                }
            }
        }

        [RelayCommand(CanExecute = nameof(IsCancellable))]
        private void CancelAnalysis()
        {
            _analysisCts?.Cancel();
            Status = "Cancelling analysis...";
        }

        private void UpdateSimilarityGroups()
        {
            // Unsubscribe from existing groups
            foreach (SimilarityGroup group in SimilarityGroups)
            {
                group.GroupSelectionChanged -= OnAnyGroupSelectionChanged;
            }

            SimilarityGroups.Clear();
            _allDuplicateGroups.Clear();

            if (SimilarityResult == null)
            {
                OnPropertyChanged(nameof(FilteredGroupCount));
                return;
            }

            // Store duplicate groups in backing list
            _allDuplicateGroups.AddRange(SimilarityResult.DuplicateGroups);

            // Reset filter to All
            CurrentFilterOption = GroupFilterOption.All;

            ApplyCurrentFilter();
        }

        private void OnExtractedFeaturesChanged(object? sender, EventArgs e)
        {
            UpdateCanAnalyze();
        }

        private void UpdateCanAnalyze()
        {
            OnPropertyChanged(nameof(CanStartSimilarityAnalysis));
            AnalyzeSimilarityCommand.NotifyCanExecuteChanged();
        }

        partial void OnSimilarityThresholdChanged(double value)
        {
            if (HasSimilarityResults)
            {
                Status = "Threshold changed. Click Analyze to update results.";
            }
        }

        partial void OnSimilarityResultChanged(SimilarityResult? value)
        {
            UpdateSimilarityGroups();
        }

        public async Task TryAutoAnalyzeAsync()
        {
            if (_settingsService.AutoAnalyzeSimilarity && CanStartSimilarityAnalysis && !HasSimilarityResults)
            {
                await AnalyzeSimilarityAsync();
            }
        }

        #endregion Similarity Analysis

        #region Selection

        [ObservableProperty]
        public partial SimilarityGroup? SelectedGroup { get; set; }

        public int TotalSelectedCount => SimilarityGroups.Sum(g => g.SelectedCount);

        public int GroupsWithSelectionsCount => SimilarityGroups.Count(g => g.SelectedCount > 0);

        public string SelectionSummary
        {
            get
            {
                int total = TotalSelectedCount;
                int groups = GroupsWithSelectionsCount;

                if (total == 0)
                {
                    return "No files selected";
                }

                return $"{total} file{(total == 1 ? "" : "s")} selected from {groups} group{(groups == 1 ? "" : "s")}";
            }
        }

        public bool HasSelectedItems => SelectedGroup?.SelectedCount > 0;

        public bool HasAnySelectedItems => SimilarityGroups.Any(g => g.SelectedCount > 0);

        [RelayCommand]
        private void ApplyStrategyToCurrentGroup(SelectionStrategy strategy)
        {
            if (SelectedGroup == null)
            {
                return;
            }

            _autoSelectionService.ApplyStrategy(SelectedGroup, strategy);
            UpdateSelectionSummary();

            LogStrategyAppliedToGroup(strategy, SelectedGroup.Id);
        }

        /// <summary>
        /// Apply selection strategy to all groups.
        /// </summary>
        [RelayCommand]
        private void ApplyStrategyToAllGroups(SelectionStrategy strategy)
        {
            if (SimilarityGroups.Count == 0)
            {
                return;
            }

            _autoSelectionService.ApplyStrategyToAll(SimilarityGroups, strategy);
            UpdateSelectionSummary();

            LogStrategyAppliedToAll(strategy, SimilarityGroups.Count);
        }

        /// <summary>
        /// Clear all selections in group.
        /// </summary>
        [RelayCommand]
        private void ClearCurrentGroupSelection()
        {
            SelectedGroup?.DeselectAll();
            UpdateSelectionSummary();
        }

        /// <summary>
        /// Clear all selections in all groups.
        /// </summary>
        [RelayCommand]
        private void ClearAllSelections()
        {
            foreach (SimilarityGroup group in SimilarityGroups)
            {
                group.DeselectAll();
            }

            UpdateSelectionSummary();
            Status = "All selections cleared";
        }

        public List<string> GetAllSelectedFilePaths()
        {
            List<string> paths = [];
            foreach (SimilarityGroup group in SimilarityGroups)
            {
                paths.AddRange(group.GetSelectedFilePaths());
            }
            return paths;
        }

        public Dictionary<string, List<string>> GetSelectedFilePathsByGroup()
        {
            Dictionary<string, List<string>> result = [];
            foreach (SimilarityGroup group in SimilarityGroups)
            {
                List<string> selectedPaths = group.GetSelectedFilePaths();
                if (selectedPaths.Count > 0)
                {
                    result[group.Name] = selectedPaths;
                }
            }
            return result;
        }

        private void OnGroupSelectionChanged(object? sender, EventArgs e)
        {
            UpdateSelectionCommands();
            UpdateSelectionSummary();
        }

        private void OnAnyGroupSelectionChanged(object? sender, EventArgs e)
        {
            UpdateSelectionSummary();
        }

        private void UpdateSelectionCommands()
        {
            OnPropertyChanged(nameof(HasSelectedItems));
        }

        private void UpdateSelectionSummary()
        {
            OnPropertyChanged(nameof(TotalSelectedCount));
            OnPropertyChanged(nameof(GroupsWithSelectionsCount));
            OnPropertyChanged(nameof(SelectionSummary));
            OnPropertyChanged(nameof(HasAnySelectedItems));
            OnPropertyChanged(nameof(CanMoveOrCopy));
            DeleteSelectedFilesCommand.NotifyCanExecuteChanged();
            DeletePermanentlyCommand.NotifyCanExecuteChanged();
        }

        partial void OnSelectedGroupChanged(SimilarityGroup? oldValue, SimilarityGroup? newValue)
        {
            if (oldValue != null)
            {
                oldValue.GroupSelectionChanged -= OnGroupSelectionChanged;
            }

            if (newValue != null)
            {
                newValue.GroupSelectionChanged += OnGroupSelectionChanged;
            }

            UpdateSelectionCommands();
        }

        #endregion Selection

        #region Sorting

        [ObservableProperty]
        public partial GroupSortingOption CurrentSortOption { get; set; } = GroupSortingOption.Similarity;

        public void SortGroups(GroupSortingOption sortOption)
        {
            CurrentSortOption = sortOption;
        }

        private void ApplyCurrentSort()
        {
            if (SimilarityGroups.Count == 0)
            {
                return;
            }

            List<SimilarityGroup> sorted = CurrentSortOption switch
            {
                GroupSortingOption.Similarity => [.. SimilarityGroups.OrderByDescending(g => g.AverageSimilarity)],
                GroupSortingOption.ImageCount => [.. SimilarityGroups.OrderByDescending(g => g.Count)],
                GroupSortingOption.Name => [.. SimilarityGroups.OrderBy(g => g.Name)],
                _ => [.. SimilarityGroups]
            };

            // Rebuild collection in new order
            SimilarityGroups.Clear();
            foreach (SimilarityGroup group in sorted)
            {
                SimilarityGroups.Add(group);
            }

            LogGroupsSorted(CurrentSortOption);
        }

        partial void OnCurrentSortOptionChanged(GroupSortingOption value)
        {
            ApplyCurrentSort();
        }

        #endregion Sorting

        #region Filtering

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(FilteredGroupCount))]
        public partial GroupFilterOption CurrentFilterOption { get; set; } = GroupFilterOption.All;

        public int FilteredGroupCount => SimilarityGroups.Count;

        public void FilterGroups(GroupFilterOption filterOption)
        {
            CurrentFilterOption = filterOption;
        }

        partial void OnCurrentFilterOptionChanged(GroupFilterOption value)
        {
            ApplyCurrentFilter();
        }

        private void ApplyCurrentFilter()
        {
            foreach (SimilarityGroup group in SimilarityGroups)
            {
                group.GroupSelectionChanged -= OnAnyGroupSelectionChanged;
            }

            SimilarityGroups.Clear();

            // Apply filter
            const double exactMatchThreshold = 0.9999;

            IEnumerable<SimilarityGroup> filtered = CurrentFilterOption switch
            {
                GroupFilterOption.ExactMatchesOnly => _allDuplicateGroups.Where(g => g.AverageSimilarity >= exactMatchThreshold),
                GroupFilterOption.SimilarOnly => _allDuplicateGroups.Where(g => g.AverageSimilarity < exactMatchThreshold),
                _ => _allDuplicateGroups
            };

            foreach (SimilarityGroup group in filtered)
            {
                group.GroupSelectionChanged += OnAnyGroupSelectionChanged;
                SimilarityGroups.Add(group);
            }

            // Re-apply current sort order
            ApplyCurrentSort();

            // Close detail panel
            if (SelectedGroup != null && !SimilarityGroups.Contains(SelectedGroup))
            {
                SelectedGroup = null;
            }

            OnPropertyChanged(nameof(FilteredGroupCount));
            OnPropertyChanged(nameof(DuplicateGroupsCount));
            UpdateSelectionSummary();

            LogGroupsFiltered(CurrentFilterOption.ToString(), SimilarityGroups.Count, _allDuplicateGroups.Count);
        }

        #endregion Filtering

        #region File Operations

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanMoveOrCopy))]
        public partial bool IsMovingOrCopying { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanMoveOrCopy))]
        [NotifyCanExecuteChangedFor(nameof(DeleteSelectedFilesCommand), nameof(DeletePermanentlyCommand))]
        public partial bool IsDeleting { get; set; }

        public bool CanMoveOrCopy => !IsDeleting && !IsMovingOrCopying && HasAnySelectedItems;

        private bool CanDeleteSelectedFiles => !IsDeleting && TotalSelectedCount > 0;

        /// <summary>
        /// Move selected files to single folder.
        /// </summary>
        public async Task<FileOperationResult> MoveToSingleFolderAsync(string destinationFolder)
        {
            return await ExecuteMoveOrCopyAsync(GetAllSelectedFilePaths(), destinationFolder, FileOperationType.Move);
        }

        /// <summary>
        /// Move selected files into group-named subfolders.
        /// </summary>
        public async Task<FileOperationResult> MoveToGroupFoldersAsync(string rootFolder)
        {
            return await ExecuteGroupedMoveOrCopyAsync(rootFolder, FileOperationType.Move);
        }

        /// <summary>
        /// Copy selected files to single folder.
        /// </summary>
        public async Task<FileOperationResult> CopyToSingleFolderAsync(string destinationFolder)
        {
            return await ExecuteMoveOrCopyAsync(GetAllSelectedFilePaths(), destinationFolder, FileOperationType.Copy);
        }

        /// <summary>
        /// Copy selected files into group-named subfolders.
        /// </summary>
        public async Task<FileOperationResult> CopyToGroupFoldersAsync(string rootFolder)
        {
            return await ExecuteGroupedMoveOrCopyAsync(rootFolder, FileOperationType.Copy);
        }

        [RelayCommand(CanExecute = nameof(CanDeleteSelectedFiles))]
        private async Task DeleteSelectedFilesAsync()
        {
            await ExecuteFileDeletionAsync(permanentDelete: false);
        }

        [RelayCommand(CanExecute = nameof(CanDeleteSelectedFiles))]
        private async Task DeletePermanentlyAsync()
        {
            await ExecuteFileDeletionAsync(permanentDelete: true);
        }

        private async Task<FileOperationResult> ExecuteMoveOrCopyAsync(List<string> filePaths, string destinationFolder, FileOperationType operationType)
        {
            if (filePaths.Count == 0 || string.IsNullOrEmpty(destinationFolder))
            {
                return FileOperationResult.Empty;
            }

            try
            {
                IsMovingOrCopying = true;
                IsBusy = true;

                string operationName = operationType == FileOperationType.Move ? "Moving" : "Copying";
                Status = $"{operationName} {filePaths.Count} files...";

                FileOperationResult result = await _fileOperationService.ExecuteAsync(filePaths, destinationFolder, operationType);

                // For move operations, refresh UI to remove moved files
                if (operationType == FileOperationType.Move && result.SuccessfulPaths.Count > 0)
                {
                    UpdateAfterRemoval(result.SuccessfulPaths);
                }

                UpdateStatusAfterOperation(operationType, result);
                return result;
            }
            catch (Exception ex)
            {
                Status = $"Error during {operationType.ToString().ToLower()} operation: {ex.Message}";
                return new FileOperationResult(0, filePaths.Count, [], filePaths);
            }
            finally
            {
                IsMovingOrCopying = false;
                IsBusy = false;
                UpdateSelectionSummary();
            }
        }

        private async Task<FileOperationResult> ExecuteGroupedMoveOrCopyAsync(string rootFolder, FileOperationType operationType)
        {
            if (TotalSelectedCount == 0 || string.IsNullOrEmpty(rootFolder))
            {
                return FileOperationResult.Empty;
            }

            try
            {
                IsMovingOrCopying = true;
                IsBusy = true;

                Dictionary<string, List<string>> filesByGroup = GetSelectedFilePathsByGroup();
                string operationName = operationType == FileOperationType.Move ? "Moving" : "Copying";
                Status = $"{operationName} files to group folders...";

                FileOperationResult result = await _fileOperationService.ExecuteGroupedAsync(filesByGroup, rootFolder, operationType);

                if (operationType == FileOperationType.Move && result.SuccessfulPaths.Count > 0)
                {
                    UpdateAfterRemoval(result.SuccessfulPaths);
                }

                UpdateStatusAfterOperation(operationType, result);
                return result;
            }
            catch (Exception ex)
            {
                Status = $"Error during {operationType.ToString().ToLower()} operation: {ex.Message}";
                LogFileOperationAborted(ex, operationType);
                return new FileOperationResult(0, TotalSelectedCount, [], GetAllSelectedFilePaths());
            }
            finally
            {
                IsMovingOrCopying = false;
                IsBusy = false;
                UpdateSelectionSummary();
            }
        }

        private async Task ExecuteFileDeletionAsync(bool permanentDelete)
        {
            if (TotalSelectedCount == 0)
            {
                return;
            }

            try
            {
                IsDeleting = true;
                IsBusy = true;

                List<string> filesToDelete = GetAllSelectedFilePaths();

                if (filesToDelete.Count == 0)
                {
                    return;
                }

                string deletionType = permanentDelete ? "Permanently deleting" : "Moving to Recycle Bin";
                Status = $"{deletionType} {filesToDelete.Count} files...";

                FileOperationResult result = await _fileOperationService.DeleteAsync(filesToDelete, permanentDelete);

                if (result.SuccessfulPaths.Count > 0)
                {
                    UpdateAfterRemoval(result.SuccessfulPaths);
                }

                string actionDescription = permanentDelete ? "permanently deleted" : "moved to Recycle Bin";
                if (result.FailedCount == 0)
                {
                    Status = $"Successfully {actionDescription} {result.SuccessCount} file{(result.SuccessCount == 1 ? "" : "s")}";
                }
                else
                {
                    Status = $"{(permanentDelete ? "Deleted" : "Moved")} {result.SuccessCount} file{(result.SuccessCount == 1 ? "" : "s")}, {result.FailedCount} failed";
                }
            }
            catch (Exception ex)
            {
                Status = $"Error during deletion: {ex.Message}";
                LogDeletionAborted(ex);
            }
            finally
            {
                IsDeleting = false;
                IsBusy = false;
                UpdateSelectionSummary();
            }
        }

        private void UpdateStatusAfterOperation(FileOperationType operationType, FileOperationResult result)
        {
            string operationPastTense = operationType == FileOperationType.Move ? "moved" : "copied";

            if (result.FailedCount == 0)
            {
                Status = $"Successfully {operationPastTense} {result.SuccessCount} file{(result.SuccessCount == 1 ? "" : "s")}";
            }
            else if (result.SuccessCount == 0)
            {
                Status = $"Failed to {operationType.ToString().ToLower()} all {result.FailedCount} files";
            }
            else
            {
                Status = $"{operationPastTense[..1].ToUpper() + operationPastTense[1..]} {result.SuccessCount} file{(result.SuccessCount == 1 ? "" : "s")}, {result.FailedCount} failed";
            }
        }

        private void UpdateAfterRemoval(List<string> removedPaths)
        {
            List<SimilarityGroup> groupsToRemove = [];
            int totalItemsRemoved = 0;

            // Snapshot of groups to iterate
            List<SimilarityGroup> groupsSnapshot = [.. SimilarityGroups];

            foreach (SimilarityGroup group in groupsSnapshot)
            {
                int removedCount = group.RemoveItemsByPath(removedPaths);
                totalItemsRemoved += removedCount;

                // No longer valid duplicate group
                if (!group.IsDuplicateGroup)
                {
                    groupsToRemove.Add(group);
                }
            }

            // Remove empty and single-item groups
            foreach (SimilarityGroup group in groupsToRemove)
            {
                group.GroupSelectionChanged -= OnAnyGroupSelectionChanged;
                group.Cleanup();
                SimilarityGroups.Remove(group);
                _allDuplicateGroups.Remove(group);

                // Clear selected cluster
                if (SelectedGroup == group)
                {
                    SelectedGroup = null;
                }
            }

            // Remove items from AppStateService
            int removedFromState = _appStateService.RemoveAnalysisItemsByPath(removedPaths);

            // Update Configuration Info displays
            OnPropertyChanged(nameof(TotalFileCount));
            OnPropertyChanged(nameof(ExtractedFeaturesCount));

            // Update UI
            OnPropertyChanged(nameof(DuplicateGroupsCount));
            UpdateSelectionSummary();

            LogPostRemovalRefresh(totalItemsRemoved, groupsToRemove.Count, removedFromState);
        }

        #endregion File Operations

        #region Cleanup

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _analysisCts?.Cancel();
                _analysisCts?.Dispose();

                _appStateService.ExtractedFeaturesChanged -= OnExtractedFeaturesChanged;

                SelectedGroup?.GroupSelectionChanged -= OnGroupSelectionChanged;

                foreach (SimilarityGroup group in SimilarityGroups)
                {
                    group.GroupSelectionChanged -= OnAnyGroupSelectionChanged;
                    group.Cleanup();
                }

                // Clean up any invisible groups (filtered out)
                foreach (SimilarityGroup group in _allDuplicateGroups)
                {
                    if (!SimilarityGroups.Contains(group))
                    {
                        group.Cleanup();
                    }
                }
            }
            base.Dispose(disposing);
        }

        #endregion Cleanup

        #region Logging

        // Similarity Analysis

        [LoggerMessage(Level = LogLevel.Information, Message = "Similarity analysis starting for {ItemCount} items at {Threshold} threshold")]
        private partial void LogSimilarityAnalysisStarting(int itemCount, double threshold);

        [LoggerMessage(Level = LogLevel.Information, Message = "Similarity analysis completed: {Summary}")]
        private partial void LogSimilarityAnalysisCompleted(string summary);

        [LoggerMessage(Level = LogLevel.Information, Message = "Similarity analysis cancelled by user")]
        private partial void LogSimilarityAnalysisCancelled();

        [LoggerMessage(Level = LogLevel.Error, Message = "Similarity analysis aborted")]
        private partial void LogSimilarityAnalysisAborted(Exception ex);

        // Selection

        [LoggerMessage(Level = LogLevel.Debug, Message = "Selection strategy {Strategy} applied to group {GroupId}")]
        private partial void LogStrategyAppliedToGroup(SelectionStrategy strategy, int groupId);

        [LoggerMessage(Level = LogLevel.Debug, Message = "Selection strategy {Strategy} applied to all {GroupCount} groups")]
        private partial void LogStrategyAppliedToAll(SelectionStrategy strategy, int groupCount);

        [LoggerMessage(Level = LogLevel.Debug, Message = "Groups sorted by {SortOption}")]
        private partial void LogGroupsSorted(GroupSortingOption sortOption);

        [LoggerMessage(Level = LogLevel.Debug, Message = "Groups filtered by {FilterOption}: showing {VisibleCount} of {TotalCount} groups")]
        private partial void LogGroupsFiltered(string filterOption, int visibleCount, int totalCount);

        // File Operations
        [LoggerMessage(Level = LogLevel.Error, Message = "{Operation} operation aborted")]
        private partial void LogFileOperationAborted(Exception ex, FileOperationType operation);

        [LoggerMessage(Level = LogLevel.Error, Message = "Deletion operation aborted")]
        private partial void LogDeletionAborted(Exception ex);

        [LoggerMessage(Level = LogLevel.Debug, Message = "Post-removal refresh: {ItemCount} items removed from groups, {GroupCount} groups dissolved, {StateCount} items removed from state")]
        private partial void LogPostRemovalRefresh(int itemCount, int groupCount, int stateCount);

        #endregion Logging
    }
}