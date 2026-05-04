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
using DeDupe.Collections;
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
        private readonly ITaskbarProgressService _taskbarProgressService;

        public ManagementViewModel(IAppStateService appStateService, ISettingsService settingsService, ISimilarityAnalysisService similarityAnalysisService, IAutoSelectionService autoSelectionService, IFileOperationService fileOperationService, IDialogService dialogService, ILocalizer localizer, ITaskbarProgressService taskbarProgressService, ILogger<ManagementViewModel> logger, MainWindowViewModel mainWindowViewModel) : base(localizer, () => mainWindowViewModel.StartManagementModeCommand.Execute(null))
        {
            _appStateService = appStateService ?? throw new ArgumentNullException(nameof(appStateService));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _similarityAnalysisService = similarityAnalysisService ?? throw new ArgumentNullException(nameof(similarityAnalysisService));
            _autoSelectionService = autoSelectionService ?? throw new ArgumentNullException(nameof(autoSelectionService));
            _fileOperationService = fileOperationService ?? throw new ArgumentNullException(nameof(fileOperationService));
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _taskbarProgressService = taskbarProgressService ?? throw new ArgumentNullException(nameof(taskbarProgressService));

            Title = L("ManagementPage_Title");
            Status = L("ManagementPage_Status_Ready");

            _appStateService.ExtractedFeaturesChanged += OnExtractedFeaturesChanged;
            Localizer.LanguageChanged += OnLanguageChanged;

            SimilarityThreshold = _settingsService.SimilarityThreshold;

            UpdateCanAnalyze();
        }

        #region Localization

        private void OnLanguageChanged(object? sender, LanguageChangedEventArgs e)
        {
            Title = L("ManagementPage_Title");
            OnPropertyChanged(nameof(CurrentSortOptionDisplay));
            OnPropertyChanged(nameof(CurrentFilterOptionDisplay));
            UpdateSelectionSummary();
        }

        #endregion Localization


        #region View

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsGridView))]
        public partial bool IsListView { get; set; } = false;

        public bool IsGridView => !IsListView;

        #endregion View

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

        public string ResultSummary => SimilarityResult?.GetSummary(Localizer) ?? string.Empty;

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
                        return L("ManagementPage_NoModelSelected");
                    }
                    return Path.GetFileNameWithoutExtension(_settingsService.CustomModelFilePath);
                }
            }
        }

        private readonly List<SimilarityGroup> _allDuplicateGroups = [];

        public BulkObservableCollection<SimilarityGroup> SimilarityGroups { get; } = [];

        public bool CanStartSimilarityAnalysis => !IsAnalyzingSimilarity && _appStateService.ExtractedFeaturesCount > 0;

        public bool ShowEmptyState => !HasSimilarityResults;

        [RelayCommand(CanExecute = nameof(CanStartSimilarityAnalysis))]
        private async Task AnalyzeSimilarityAsync()
        {
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
                Status = L("ManagementPage_Status_Analyzing");

                IReadOnlyCollection<AnalysisItem> itemsWithFeatures = _appStateService.ItemsWithFeatures;

                if (itemsWithFeatures.Count == 0)
                {
                    Status = L("ManagementPage_Status_NoFeatures");
                    return;
                }

                LogSimilarityAnalysisStarting(itemsWithFeatures.Count, SimilarityThreshold);

                // Create progress handler
                Progress<ProgressInfo> analysisProgress = new(info =>
                {
                    AnalysisProgressPercentage = info.Percentage;
                    Status = info.StatusText;

                    // Sync with taskbar
                    _taskbarProgressService.SetProgress(info.Percentage, 100);
                });

                SimilarityResult = await _similarityAnalysisService.ClusterAsync(
                    itemsWithFeatures, SimilarityThreshold, Localizer, analysisProgress, ct);

                HasSimilarityResults = true;
                AnalysisProgressPercentage = 100;
                Status = L("ManagementPage_Status_AnalysisComplete", ResultSummary);

                LogSimilarityAnalysisCompleted(ResultSummary);
            }
            catch (OperationCanceledException)
            {
                _taskbarProgressService.SetPaused();
                Status = L("ManagementPage_Status_AnalysisCancelled");
                AnalysisProgressPercentage = 0;
                LogSimilarityAnalysisCancelled();
            }
            catch (Exception ex)
            {
                _taskbarProgressService.SetError();
                Status = L("ManagementPage_Status_AnalysisError", ex.Message);
                LogSimilarityAnalysisAborted(ex);
                HasSimilarityResults = false;
            }
            finally
            {
                IsAnalyzingSimilarity = false;
                IsBusy = false;

                // Clear taskbar progress
                _taskbarProgressService.Clear();

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
            Status = L("ManagementPage_Status_CancellingAnalysis");
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
                Status = L("ManagementPage_Status_ThresholdChanged");
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
                    return L("ManagementPage_Selection_None");
                }

                return L("ManagementPage_Selection_Summary", total, groups);
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
            Status = L("ManagementPage_Status_AllSelectionsCleared");
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

        public string CurrentSortOptionDisplay => Localizer.GetLocalizedString($"GroupSortingOption_{CurrentSortOption}") is string s && !string.IsNullOrEmpty(s) ? s : CurrentSortOption.ToString();

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

            SimilarityGroups.ReplaceAll(sorted);

            LogGroupsSorted(CurrentSortOption);
        }

        partial void OnCurrentSortOptionChanged(GroupSortingOption value)
        {
            ApplyCurrentSort();
            OnPropertyChanged(nameof(CurrentSortOptionDisplay));
        }

        #endregion Sorting

        #region Filtering

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(FilteredGroupCount))]
        public partial GroupFilterOption CurrentFilterOption { get; set; } = GroupFilterOption.All;

        public string CurrentFilterOptionDisplay => Localizer.GetLocalizedString($"GroupFilterOption_{CurrentFilterOption}") is string s && !string.IsNullOrEmpty(s) ? s : CurrentFilterOption.ToString();

        public int FilteredGroupCount => SimilarityGroups.Count;

        public void FilterGroups(GroupFilterOption filterOption)
        {
            CurrentFilterOption = filterOption;
        }

        partial void OnCurrentFilterOptionChanged(GroupFilterOption value)
        {
            ApplyCurrentFilter();
            OnPropertyChanged(nameof(CurrentFilterOptionDisplay));
        }

        private void ApplyCurrentFilter()
        {
            foreach (SimilarityGroup group in SimilarityGroups)
            {
                group.GroupSelectionChanged -= OnAnyGroupSelectionChanged;
            }

            // Apply filter
            const double exactMatchThreshold = 0.9999;

            IEnumerable<SimilarityGroup> filtered = CurrentFilterOption switch
            {
                GroupFilterOption.ExactMatchesOnly => _allDuplicateGroups.Where(g => g.AverageSimilarity >= exactMatchThreshold),
                GroupFilterOption.SimilarOnly => _allDuplicateGroups.Where(g => g.AverageSimilarity < exactMatchThreshold),
                _ => _allDuplicateGroups
            };

            // Sort before adding to avoid a second collection rebuild
            List<SimilarityGroup> sorted = CurrentSortOption switch
            {
                GroupSortingOption.Similarity => [.. filtered.OrderByDescending(g => g.AverageSimilarity)],
                GroupSortingOption.ImageCount => [.. filtered.OrderByDescending(g => g.Count)],
                GroupSortingOption.Name => [.. filtered.OrderBy(g => g.Name)],
                _ => [.. filtered]
            };

            // Subscribe to selection events
            foreach (SimilarityGroup group in sorted)
            {
                group.GroupSelectionChanged += OnAnyGroupSelectionChanged;
            }

            // Single bulk update — fires one Reset notification instead of N Add notifications
            SimilarityGroups.ReplaceAll(sorted);

            // Close detail panel if selected group was filtered out
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

                string statusKey = operationType == FileOperationType.Move
                    ? "ManagementPage_Status_MovingFiles"
                    : "ManagementPage_Status_CopyingFiles";
                Status = L(statusKey, filePaths.Count);

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
                Status = L("ManagementPage_Status_OperationError", operationType.ToString().ToLower(), ex.Message);
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
                string statusKey = operationType == FileOperationType.Move
                    ? "ManagementPage_Status_MovingToGroups"
                    : "ManagementPage_Status_CopyingToGroups";
                Status = L(statusKey);

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
                Status = L("ManagementPage_Status_OperationError", operationType.ToString().ToLower(), ex.Message);
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

                string statusKey = permanentDelete
                    ? "ManagementPage_Status_PermanentlyDeleting"
                    : "ManagementPage_Status_DeletingToRecycleBin";
                Status = L(statusKey, filesToDelete.Count);

                FileOperationResult result = await _fileOperationService.DeleteAsync(filesToDelete, permanentDelete);

                if (result.SuccessfulPaths.Count > 0)
                {
                    UpdateAfterRemoval(result.SuccessfulPaths);
                }

                string successKey = permanentDelete
                    ? "ManagementPage_Status_PermanentDeleteSuccess"
                    : "ManagementPage_Status_RecycleBinSuccess";
                string partialKey = permanentDelete
                    ? "ManagementPage_Status_PermanentDeletePartial"
                    : "ManagementPage_Status_RecycleBinPartial";

                if (result.FailedCount == 0)
                {
                    Status = L(successKey, result.SuccessCount);
                }
                else
                {
                    Status = L(partialKey, result.SuccessCount, result.FailedCount);
                }
            }
            catch (Exception ex)
            {
                Status = L("ManagementPage_Status_DeletionError", ex.Message);
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
            string successKey = operationType == FileOperationType.Move
                ? "ManagementPage_Status_MoveSuccess"
                : "ManagementPage_Status_CopySuccess";
            string failAllKey = operationType == FileOperationType.Move
                ? "ManagementPage_Status_MoveFailedAll"
                : "ManagementPage_Status_CopyFailedAll";
            string partialKey = operationType == FileOperationType.Move
                ? "ManagementPage_Status_MovePartial"
                : "ManagementPage_Status_CopyPartial";

            if (result.FailedCount == 0)
            {
                Status = L(successKey, result.SuccessCount);
            }
            else if (result.SuccessCount == 0)
            {
                Status = L(failAllKey, result.FailedCount);
            }
            else
            {
                Status = L(partialKey, result.SuccessCount, result.FailedCount);
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
                Localizer.LanguageChanged -= OnLanguageChanged;

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