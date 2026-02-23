using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeDupe.Enums;
using DeDupe.Models;
using DeDupe.Models.Analysis;
using DeDupe.Models.Configuration;
using DeDupe.Models.Results;
using DeDupe.Services;
using DeDupe.Services.Analysis;
using Microsoft.Extensions.Logging;
using Microsoft.VisualBasic.FileIO;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace DeDupe.ViewModels.Pages
{
    public partial class ManagementViewModel : PageViewModelBase
    {
        private readonly IAppStateService _appStateService;
        private readonly ISettingsService _settingsService;
        private readonly ISimilarityAnalysisService _similarityAnalysisService;
        private readonly IAutoSelectionService _autoSelectionService;
        private readonly ILogger<ManagementViewModel> _logger;

        public ManagementViewModel(IAppStateService appStateService, ISettingsService settingsService, ISimilarityAnalysisService similarityAnalysisService, IAutoSelectionService autoSelectionService, ILogger<ManagementViewModel> logger, MainWindowViewModel mainWindowViewModel) : base(() => mainWindowViewModel.StartManagementModeCommand.Execute(null))
        {
            _appStateService = appStateService ?? throw new ArgumentNullException(nameof(appStateService));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _similarityAnalysisService = similarityAnalysisService ?? throw new ArgumentNullException(nameof(similarityAnalysisService));
            _autoSelectionService = autoSelectionService ?? throw new ArgumentNullException(nameof(autoSelectionService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            Title = "Duplicate Management";
            Status = "Ready to analyze similarities";

            _appStateService.ExtractedFeaturesChanged += OnExtractedFeaturesChanged;

            SimilarityThreshold = _settingsService.SimilarityThreshold;

            UpdateCanAnalyze();
        }

        #region Similarity Analysis

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanStartSimilarityAnalysis))]
        [NotifyCanExecuteChangedFor(nameof(AnalyzeSimilarityCommand))]
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

                // Perform clustering
                SimilarityResult = await _similarityAnalysisService.ClusterAsync(itemsWithFeatures, SimilarityThreshold, analysisProgress);

                HasSimilarityResults = true;
                AnalysisProgressPercentage = 100;
                Status = $"Analysis complete: {ResultSummary}";

                LogSimilarityAnalysisCompleted(ResultSummary);
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

        [RelayCommand(CanExecute = nameof(CanDeleteSelectedFiles))]
        private async Task DeleteSelectedFilesAsync()
        {
            await ExecuteFileDeletionAsync(permanentDelete: false);
        }

        /// <summary>
        /// Permanently delete all selected files.
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanDeleteSelectedFiles))]
        private async Task DeletePermanentlyAsync()
        {
            await ExecuteFileDeletionAsync(permanentDelete: true);
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

        /// <summary>
        /// Move all selected files to single folder.
        /// </summary>
        public async Task<FileOperationResult> MoveToSingleFolderAsync(string destinationFolder)
        {
            return await ExecuteFileOperationAsync(destinationFolder, GetAllSelectedFilePaths(), FileOperationType.Move);
        }

        /// <summary>
        /// Move selected files into group-named subfolders.
        /// </summary>
        public async Task<FileOperationResult> MoveToGroupFoldersAsync(string rootFolder)
        {
            return await ExecuteGroupedFileOperationAsync(rootFolder, FileOperationType.Move);
        }

        /// <summary>
        /// Copy all selected files to single folder.
        /// </summary>
        public async Task<FileOperationResult> CopyToSingleFolderAsync(string destinationFolder)
        {
            return await ExecuteFileOperationAsync(destinationFolder, GetAllSelectedFilePaths(), FileOperationType.Copy);
        }

        /// <summary>
        /// Copy selected files into group-named subfolders.
        /// </summary>
        public async Task<FileOperationResult> CopyToGroupFoldersAsync(string rootFolder)
        {
            return await ExecuteGroupedFileOperationAsync(rootFolder, FileOperationType.Copy);
        }

        private async Task<FileOperationResult> ExecuteFileOperationAsync(string destinationFolder, List<string> filePaths, FileOperationType operationType)
        {
            if (filePaths.Count == 0 || string.IsNullOrEmpty(destinationFolder))
            {
                return new FileOperationResult(0, 0, [], []);
            }

            try
            {
                IsMovingOrCopying = true;
                IsBusy = true;

                string operationName = operationType == FileOperationType.Move ? "Moving" : "Copying";
                Status = $"{operationName} {filePaths.Count} files...";
                LogFileOperationStarting(operationType, filePaths.Count);

                // Ensure destination folder exists
                if (!Directory.Exists(destinationFolder))
                {
                    Directory.CreateDirectory(destinationFolder);
                }

                FileOperationResult result = await Task.Run(() =>
                {
                    int successCount = 0;
                    int failCount = 0;
                    List<string> successfulPaths = [];
                    List<string> failedPaths = [];

                    foreach (string sourcePath in filePaths)
                    {
                        (bool success, string? error) = ProcessSingleFile(sourcePath, destinationFolder, operationType);
                        if (success)
                        {
                            successCount++;
                            successfulPaths.Add(sourcePath);
                        }
                        else
                        {
                            failCount++;
                            failedPaths.Add(sourcePath);
                            LogFileOperationSkipped(operationType, sourcePath, error);
                        }
                    }

                    return new FileOperationResult(successCount, failCount, successfulPaths, failedPaths);
                });

                // For move operations, refresh UI to remove moved files
                if (operationType == FileOperationType.Move && result.SuccessfulPaths.Count > 0)
                {
                    UpdateAfterDeletion(result.SuccessfulPaths);
                }

                UpdateStatusAfterOperation(operationType, result);
                return result;
            }
            catch (Exception ex)
            {
                Status = $"Error during {operationType.ToString().ToLower()} operation: {ex.Message}";
                LogFileOperationAborted(ex, operationType);
                return new FileOperationResult(0, filePaths.Count, [], filePaths);
            }
            finally
            {
                IsMovingOrCopying = false;
                IsBusy = false;
                UpdateSelectionSummary();
            }
        }

        private async Task<FileOperationResult> ExecuteGroupedFileOperationAsync(string rootFolder, FileOperationType operationType)
        {
            if (TotalSelectedCount == 0 || string.IsNullOrEmpty(rootFolder))
            {
                return new FileOperationResult(0, 0, [], []);
            }

            try
            {
                IsMovingOrCopying = true;
                IsBusy = true;

                Dictionary<string, List<string>> filesByGroup = GetSelectedFilePathsByGroup();
                string operationName = operationType == FileOperationType.Move ? "Moving" : "Copying";
                Status = $"{operationName} files to group folders...";
                LogFileOperationStarting(operationType, filesByGroup.Values.Sum(v => v.Count));

                FileOperationResult result = await Task.Run(() =>
                {
                    int totalSuccess = 0;
                    int totalFailed = 0;
                    List<string> successfulPaths = [];
                    List<string> failedPaths = [];

                    foreach (KeyValuePair<string, List<string>> groupEntry in filesByGroup)
                    {
                        string? groupName = FolderNameValidationService.Sanitize(groupEntry.Key);

                        if (string.IsNullOrEmpty(groupName))
                        {
                            LogInvalidGroupName(groupEntry.Key);
                            totalFailed += groupEntry.Value.Count;
                            failedPaths.AddRange(groupEntry.Value);
                            continue;
                        }

                        string groupFolder = Path.Combine(rootFolder, groupName);

                        // Create group folder if it doesn't exist
                        try
                        {
                            if (!Directory.Exists(groupFolder))
                            {
                                Directory.CreateDirectory(groupFolder);
                            }
                        }
                        catch (Exception ex)
                        {
                            LogGroupFolderCreationFailed(ex, groupFolder);
                            totalFailed += groupEntry.Value.Count;
                            failedPaths.AddRange(groupEntry.Value);
                            continue;
                        }

                        // Process files in this group
                        foreach (string sourcePath in groupEntry.Value)
                        {
                            (bool success, string? error) = ProcessSingleFile(sourcePath, groupFolder, operationType);
                            if (success)
                            {
                                totalSuccess++;
                                successfulPaths.Add(sourcePath);
                            }
                            else
                            {
                                totalFailed++;
                                failedPaths.Add(sourcePath);
                                LogFileOperationSkipped(operationType, sourcePath, error);
                            }
                        }
                    }

                    return new FileOperationResult(totalSuccess, totalFailed, successfulPaths, failedPaths);
                });

                // Refresh UI to remove moved files
                if (operationType == FileOperationType.Move && result.SuccessfulPaths.Count > 0)
                {
                    UpdateAfterDeletion(result.SuccessfulPaths);
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

        private static (bool Success, string? Error) ProcessSingleFile(string sourcePath, string destinationFolder, FileOperationType operationType)
        {
            try
            {
                if (!File.Exists(sourcePath))
                {
                    return (false, "Source file not found");
                }

                string fileName = Path.GetFileName(sourcePath);
                string destinationPath = Path.Combine(destinationFolder, fileName);

                // Handle file name conflicts
                destinationPath = GetUniqueFilePath(destinationPath);

                if (operationType == FileOperationType.Move)
                {
                    File.Move(sourcePath, destinationPath);
                }
                else
                {
                    File.Copy(sourcePath, destinationPath, overwrite: false);
                }

                return (true, null);
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        private static string GetUniqueFilePath(string filePath)
        {
            if (!File.Exists(filePath))
            {
                return filePath;
            }

            string directory = Path.GetDirectoryName(filePath) ?? string.Empty;
            string fileNameWithoutExt = Path.GetFileNameWithoutExtension(filePath);
            string extension = Path.GetExtension(filePath);

            int counter = 1;
            string newPath;
            do
            {
                newPath = Path.Combine(directory, $"{fileNameWithoutExt} ({counter}){extension}");
                counter++;
            } while (File.Exists(newPath));

            return newPath;
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
                Status = $"{operationPastTense.First().ToString().ToUpper() + operationPastTense[1..]} {result.SuccessCount} file{(result.SuccessCount == 1 ? "" : "s")}, {result.FailedCount} failed";
            }

            LogFileOperationCompleted(operationType, result.SuccessCount, result.FailedCount);
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

                // Collect all selected file paths
                List<string> filesToDelete = [];
                foreach (SimilarityGroup group in SimilarityGroups)
                {
                    filesToDelete.AddRange(group.GetSelectedFilePaths());
                }

                if (filesToDelete.Count == 0)
                {
                    return;
                }

                string deletionType = permanentDelete ? "Permanently deleting" : "Moving to Recycle Bin";
                Status = $"{deletionType} {filesToDelete.Count} files...";
                LogDeletionStarting(permanentDelete, filesToDelete.Count);

                // Perform file deletion on background thread
                (int successCount, int failCount, List<string> successfullyDeleted) = await Task.Run(() =>
                {
                    int success = 0;
                    int fail = 0;
                    List<string> deleted = [];

                    foreach (string filePath in filesToDelete)
                    {
                        try
                        {
                            if (File.Exists(filePath))
                            {
                                if (permanentDelete)
                                {
                                    // Permanent deletion
                                    File.Delete(filePath);
                                }
                                else
                                {
                                    // Move to recycle bin
                                    FileSystem.DeleteFile(filePath, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
                                }

                                success++;
                                deleted.Add(filePath);
                            }
                            else
                            {
                                LogFileNotFoundForDeletion(filePath);
                                fail++;
                            }
                        }
                        catch (Exception ex)
                        {
                            LogFileDeletionFailed(ex, filePath);
                            fail++;
                        }
                    }

                    return (success, fail, deleted);
                });

                // Update UI
                if (successfullyDeleted.Count > 0)
                {
                    UpdateAfterDeletion(successfullyDeleted);
                }

                // Update status
                string actionDescription = permanentDelete ? "permanently deleted" : "moved to Recycle Bin";
                if (failCount == 0)
                {
                    Status = $"Successfully {actionDescription} {successCount} file{(successCount == 1 ? "" : "s")}";
                }
                else
                {
                    Status = $"{(permanentDelete ? "Deleted" : "Moved")} {successCount} file{(successCount == 1 ? "" : "s")}, {failCount} failed";
                }

                LogDeletionCompleted(permanentDelete, successCount, failCount);
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

        private void UpdateAfterDeletion(List<string> deletedPaths)
        {
            List<SimilarityGroup> groupsToRemove = [];
            int totalItemsRemoved = 0;

            // Snapshot of groups to iterate
            List<SimilarityGroup> groupsSnapshot = [.. SimilarityGroups];

            foreach (SimilarityGroup group in groupsSnapshot)
            {
                int removedCount = group.RemoveItemsByPath(deletedPaths);
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
            int removedFromState = _appStateService.RemoveAnalysisItemsByPath(deletedPaths);

            // Update Configuration Info displays
            OnPropertyChanged(nameof(TotalFileCount));
            OnPropertyChanged(nameof(ExtractedFeaturesCount));

            // Update UI
            OnPropertyChanged(nameof(DuplicateGroupsCount));
            UpdateSelectionSummary();

            LogPostDeletionRefresh(totalItemsRemoved, groupsToRemove.Count, removedFromState);
        }

        #endregion File Operations

        #region Cleanup

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
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

        // File Operations - Move/Copy

        [LoggerMessage(Level = LogLevel.Information, Message = "{Operation} starting for {FileCount} files")]
        private partial void LogFileOperationStarting(FileOperationType operation, int fileCount);

        [LoggerMessage(Level = LogLevel.Information, Message = "{Operation} completed, {SuccessCount} succeeded, {FailedCount} failed")]
        private partial void LogFileOperationCompleted(FileOperationType operation, int successCount, int failedCount);

        [LoggerMessage(Level = LogLevel.Error, Message = "{Operation} operation aborted")]
        private partial void LogFileOperationAborted(Exception ex, FileOperationType operation);

        [LoggerMessage(Level = LogLevel.Warning, Message = "File {Operation} skipped for {FilePath}: {Reason}")]
        private partial void LogFileOperationSkipped(FileOperationType operation, string filePath, string? reason);

        [LoggerMessage(Level = LogLevel.Error, Message = "Group folder creation failed for {FolderPath}")]
        private partial void LogGroupFolderCreationFailed(Exception ex, string folderPath);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Group name '{OriginalName}' produced invalid folder name, group skipped")]
        private partial void LogInvalidGroupName(string originalName);

        // File Operations - Deletion

        [LoggerMessage(Level = LogLevel.Information, Message = "Deletion starting (Permanent: {Permanent}) for {FileCount} files")]
        private partial void LogDeletionStarting(bool permanent, int fileCount);

        [LoggerMessage(Level = LogLevel.Information, Message = "Deletion completed (Permanent: {Permanent}), {SuccessCount} succeeded, {FailedCount} failed")]
        private partial void LogDeletionCompleted(bool permanent, int successCount, int failedCount);

        [LoggerMessage(Level = LogLevel.Error, Message = "Deletion operation aborted")]
        private partial void LogDeletionAborted(Exception ex);

        [LoggerMessage(Level = LogLevel.Warning, Message = "File not found for deletion: {FilePath}")]
        private partial void LogFileNotFoundForDeletion(string filePath);

        [LoggerMessage(Level = LogLevel.Error, Message = "File deletion failed for {FilePath}")]
        private partial void LogFileDeletionFailed(Exception ex, string filePath);

        [LoggerMessage(Level = LogLevel.Debug, Message = "Post-deletion refresh, {ItemCount} items removed from groups, {GroupCount} groups dissolved, {StateCount} items removed from state")]
        private partial void LogPostDeletionRefresh(int itemCount, int groupCount, int stateCount);

        #endregion Logging
    }
}