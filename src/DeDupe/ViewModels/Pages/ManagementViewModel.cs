using CommunityToolkit.Mvvm.Input;
using DeDupe.Enums;
using DeDupe.Models;
using DeDupe.Models.Analysis;
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
        #region Fields

        private readonly IAppStateService _appStateService;
        private readonly ISettingsService _settingsService;
        private readonly IBundledModelService _bundledModelService;
        private readonly ISimilarityAnalysisService _similarityAnalysisService;
        private readonly IAutoSelectionService _autoSelectionService;
        private readonly ILogger<ManagementViewModel> _logger;

        private bool _isAnalyzingSimilarity;
        private bool _hasSimilarityResults;
        private double _similarityThreshold = 0.85;
        private SimilarityResult? _similarityResult;
        private SimilarityGroup? _selectedCluster;
        private bool _isDeleting;
        private bool _isMovingOrCopying;
        private GroupSortingOption _currentSortOption = GroupSortingOption.Similarity;

        #endregion Fields

        #region Properties

        public bool IsAnalyzingSimilarity
        {
            get => _isAnalyzingSimilarity;
            set
            {
                if (SetProperty(ref _isAnalyzingSimilarity, value))
                {
                    OnPropertyChanged(nameof(CanStartSimilarityAnalysis));
                    AnalyzeSimilarityCommand.NotifyCanExecuteChanged();
                }
            }
        }

        public bool HasSimilarityResults
        {
            get => _hasSimilarityResults;
            set
            {
                if (SetProperty(ref _hasSimilarityResults, value))
                {
                    OnPropertyChanged(nameof(ShowEmptyState));
                }
            }
        }

        public bool ShowEmptyState => !HasSimilarityResults;

        public double SimilarityThreshold
        {
            get => _similarityThreshold;
            set
            {
                if (SetProperty(ref _similarityThreshold, value) && HasSimilarityResults)
                {
                    Status = "Threshold changed. Click Analyze to update results.";
                }
            }
        }

        public SimilarityResult? SimilarityResult
        {
            get => _similarityResult;
            set
            {
                if (SetProperty(ref _similarityResult, value))
                {
                    UpdateClusterGroups();
                    OnPropertyChanged(nameof(TotalClusters));
                    OnPropertyChanged(nameof(DuplicateGroupsCount));
                    OnPropertyChanged(nameof(TotalItems));
                    OnPropertyChanged(nameof(ResultSummary));
                }
            }
        }

        public ObservableCollection<SimilarityGroup> ClusterGroups { get; } = [];

        public SimilarityGroup? SelectedCluster
        {
            get => _selectedCluster;
            set
            {
                if (_selectedCluster != null)
                {
                    _selectedCluster.GroupSelectionChanged -= OnGroupSelectionChanged;
                }

                if (SetProperty(ref _selectedCluster, value))
                {
                    if (_selectedCluster != null)
                    {
                        _selectedCluster.GroupSelectionChanged += OnGroupSelectionChanged;
                    }

                    UpdateSelectionCommands();
                }
            }
        }

        public bool CanStartSimilarityAnalysis => !IsAnalyzingSimilarity && _appStateService.ExtractedFeaturesCount > 0;

        public bool HasSelectedItems => SelectedCluster?.SelectedCount > 0;

        public bool HasAnySelectedItems => ClusterGroups.Any(g => g.SelectedCount > 0);

        public bool IsDeleting
        {
            get => _isDeleting;
            set
            {
                if (SetProperty(ref _isDeleting, value))
                {
                    DeleteSelectedFilesCommand.NotifyCanExecuteChanged();
                    DeletePermanentlyCommand.NotifyCanExecuteChanged();
                    OnPropertyChanged(nameof(CanMoveOrCopy));
                }
            }
        }

        public bool IsMovingOrCopying
        {
            get => _isMovingOrCopying;
            set
            {
                if (SetProperty(ref _isMovingOrCopying, value))
                {
                    OnPropertyChanged(nameof(CanMoveOrCopy));
                }
            }
        }

        /// <summary>
        /// If move/copy operation can be started.
        /// </summary>
        public bool CanMoveOrCopy => !IsDeleting && !IsMovingOrCopying && HasAnySelectedItems;

        public GroupSortingOption CurrentSortOption
        {
            get => _currentSortOption;
            set
            {
                if (SetProperty(ref _currentSortOption, value))
                {
                    ApplyCurrentSort();
                }
            }
        }

        // Statistics
        public int TotalClusters => SimilarityResult?.TotalClusters ?? 0;

        public int DuplicateGroupsCount => SimilarityResult?.DuplicateGroupsCount ?? 0;
        public int TotalItems => SimilarityResult?.TotalItemsAnalyzed ?? 0;
        public string ResultSummary => SimilarityResult?.GetSummary() ?? string.Empty;
        public int TotalSelectedCount => ClusterGroups.Sum(g => g.SelectedCount);
        public int GroupsWithSelectionsCount => ClusterGroups.Count(g => g.SelectedCount > 0);

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

        // Configuration Info
        public int TotalFileCount => _appStateService.SourceCount;

        public int ExtractedFeaturesCount => _appStateService.ExtractedFeaturesCount;

        public string ModelName
        {
            get
            {
                if (string.IsNullOrEmpty(_settingsService.UseBundledModel ? _bundledModelService.BundledModelPath : _settingsService.CustomModelFilePath))
                    return "No model";

                return Path.GetFileNameWithoutExtension(_settingsService.UseBundledModel ? _bundledModelService.BundledModelPath : _settingsService.CustomModelFilePath);
            }
        }

        #endregion Properties

        #region Commands

        [RelayCommand(CanExecute = nameof(CanStartSimilarityAnalysis))]
        private async Task AnalyzeSimilarityAsync()
        {
            try
            {
                IsAnalyzingSimilarity = true;
                IsBusy = true;
                Status = "Analyzing similarities...";

                // Get items with features
                IReadOnlyCollection<AnalysisItem> itemsWithFeatures = _appStateService.ItemsWithFeatures;

                if (itemsWithFeatures.Count == 0)
                {
                    Status = "No features available. Please extract features first.";
                    return;
                }

                // Perform clustering
                SimilarityResult = await _similarityAnalysisService.ClusterAsync(itemsWithFeatures, SimilarityThreshold);

                HasSimilarityResults = true;
                Status = $"Analysis complete: {ResultSummary}";

                _logger.LogInformation("Similarity analysis completed: {Summary}", ResultSummary);
            }
            catch (Exception ex)
            {
                Status = $"Error during similarity analysis: {ex.Message}";
                _logger.LogError(ex, "Error during similarity analysis");
                HasSimilarityResults = false;
            }
            finally
            {
                IsAnalyzingSimilarity = false;
                IsBusy = false;
            }
        }

        /// <summary>
        /// Apply selection strategy to selected group.
        /// </summary>
        [RelayCommand]
        private void ApplyStrategyToCurrentGroup(SelectionStrategy strategy)
        {
            if (SelectedCluster == null)
            {
                return;
            }

            _autoSelectionService.ApplyStrategy(SelectedCluster, strategy);
            UpdateSelectionSummary();

            _logger.LogInformation("Applied strategy {Strategy} to group {GroupId}", strategy, SelectedCluster.Id);
        }

        /// <summary>
        /// Apply selection strategy to all groups.
        /// </summary>
        [RelayCommand]
        private void ApplyStrategyToAllGroups(SelectionStrategy strategy)
        {
            if (ClusterGroups.Count == 0)
            {
                return;
            }

            _autoSelectionService.ApplyStrategyToAll(ClusterGroups, strategy);
            UpdateSelectionSummary();

            _logger.LogInformation("Applied strategy {Strategy} to all {Count} groups", strategy, ClusterGroups.Count);
        }

        /// <summary>
        /// Clear all selections in group.
        /// </summary>
        [RelayCommand]
        private void ClearCurrentGroupSelection()
        {
            SelectedCluster?.DeselectAll();
            UpdateSelectionSummary();
        }

        /// <summary>
        /// Clear all selections in all groups.
        /// </summary>
        [RelayCommand]
        private void ClearAllSelections()
        {
            foreach (SimilarityGroup group in ClusterGroups)
            {
                group.DeselectAll();
            }

            UpdateSelectionSummary();
            Status = "All selections cleared";
        }

        /// <summary>
        /// Delete all selected files to Recycle Bin.
        /// </summary>
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

        private bool CanDeleteSelectedFiles() => !IsDeleting && TotalSelectedCount > 0;

        /// <summary>
        /// Core file deletion logic used by both recycle bin and permanent delete operations.
        /// </summary>
        /// <param name="permanentDelete">If true, files are permanently deleted. If false, files are moved to Recycle Bin.</param>
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
                foreach (SimilarityGroup group in ClusterGroups)
                {
                    filesToDelete.AddRange(group.GetSelectedFilePaths());
                }

                if (filesToDelete.Count == 0)
                {
                    return;
                }

                string deletionType = permanentDelete ? "Permanently deleting" : "Moving to Recycle Bin";
                Status = $"{deletionType} {filesToDelete.Count} files...";

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
                                _logger.LogWarning("File not found for deletion: {FilePath}", filePath);
                                fail++;
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to delete file: {FilePath}", filePath);
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

                _logger.LogInformation("Deletion completed (permanent={Permanent}): {Success} succeeded, {Failed} failed", permanentDelete, successCount, failCount);
            }
            catch (Exception ex)
            {
                Status = $"Error during deletion: {ex.Message}";
                _logger.LogError(ex, "Error during file deletion");
            }
            finally
            {
                IsDeleting = false;
                IsBusy = false;
                UpdateSelectionSummary();
            }
        }

        /// <summary>
        /// Get all selected file paths for deletion confirmation.
        /// </summary>
        public List<string> GetAllSelectedFilePaths()
        {
            List<string> paths = [];
            foreach (SimilarityGroup group in ClusterGroups)
            {
                paths.AddRange(group.GetSelectedFilePaths());
            }
            return paths;
        }

        #endregion Commands

        #region Move and Copy Operations

        /// <summary>
        /// Get selected file paths organized by group name.
        /// </summary>
        public Dictionary<string, List<string>> GetSelectedFilePathsByGroup()
        {
            Dictionary<string, List<string>> result = [];
            foreach (SimilarityGroup group in ClusterGroups)
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

        /// <summary>
        /// Execute file operation for grouped files (creates subfolders per group).
        /// </summary>
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

                FileOperationResult result = await Task.Run(() =>
                {
                    int totalSuccess = 0;
                    int totalFailed = 0;
                    List<string> successfulPaths = [];
                    List<string> failedPaths = [];

                    foreach (KeyValuePair<string, List<string>> groupEntry in filesByGroup)
                    {
                        string groupName = FolderNameValidationService.Sanitize(groupEntry.Key);
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
                            _logger.LogError(ex, "Failed to create group folder: {FolderPath}", groupFolder);
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
                                _logger.LogWarning("Failed to {Operation} file {Path}: {Error}", operationType, sourcePath, error);
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
                _logger.LogError(ex, "Error during {Operation} operation", operationType);
                return new FileOperationResult(0, TotalSelectedCount, [], GetAllSelectedFilePaths());
            }
            finally
            {
                IsMovingOrCopying = false;
                IsBusy = false;
                UpdateSelectionSummary();
            }
        }

        /// <summary>
        /// Execute file operation for flat list of files.
        /// </summary>
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
                            _logger.LogWarning("Failed to {Operation} file {Path}: {Error}", operationType, sourcePath, error);
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
                _logger.LogError(ex, "Error during {Operation} operation", operationType);
                return new FileOperationResult(0, filePaths.Count, [], filePaths);
            }
            finally
            {
                IsMovingOrCopying = false;
                IsBusy = false;
                UpdateSelectionSummary();
            }
        }

        /// <summary>
        /// Process single file.
        /// </summary>
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

        /// <summary>
        /// Get unique file path by adding suffix if file already exist.
        /// </summary>
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

        /// <summary>
        /// Update status message after file operation.
        /// </summary>
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

            _logger.LogInformation("{Operation} completed: {Success} succeeded, {Failed} failed", operationType, result.SuccessCount, result.FailedCount);
        }

        #endregion Move and Copy Operations

        #region Sort Methods

        /// <summary>
        /// Sort groups by specified option.
        /// </summary>
        public void SortGroups(GroupSortingOption sortOption)
        {
            CurrentSortOption = sortOption;
        }

        /// <summary>
        /// Apply current sort option to cluster groups.
        /// </summary>
        private void ApplyCurrentSort()
        {
            if (ClusterGroups.Count == 0)
            {
                return;
            }

            List<SimilarityGroup> sorted = CurrentSortOption switch
            {
                GroupSortingOption.Similarity => [.. ClusterGroups.OrderByDescending(g => g.AverageSimilarity)],
                GroupSortingOption.ImageCount => [.. ClusterGroups.OrderByDescending(g => g.Count)],
                GroupSortingOption.Name => [.. ClusterGroups.OrderBy(g => g.Name)],
                _ => [.. ClusterGroups]
            };

            // Rebuild collection in new order
            ClusterGroups.Clear();
            foreach (SimilarityGroup group in sorted)
            {
                ClusterGroups.Add(group);
            }

            _logger.LogInformation("Sorted groups by {SortOption}", CurrentSortOption);
        }

        #endregion Sort Methods

        #region Constructor

        public ManagementViewModel(IAppStateService appStateService, ISettingsService settingsService, IBundledModelService bundledModelService, ISimilarityAnalysisService similarityAnalysisService, IAutoSelectionService autoSelectionService, ILogger<ManagementViewModel> logger) : base(3)
        {
            _appStateService = appStateService ?? throw new ArgumentNullException(nameof(appStateService));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _bundledModelService = bundledModelService ?? throw new ArgumentNullException(nameof(bundledModelService));
            _similarityAnalysisService = similarityAnalysisService ?? throw new ArgumentNullException(nameof(similarityAnalysisService));
            _autoSelectionService = autoSelectionService ?? throw new ArgumentNullException(nameof(autoSelectionService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            Title = "Duplicate Management";
            Status = "Ready to analyze similarities";

            _appStateService.ExtractedFeaturesChanged += OnExtractedFeaturesChanged;

            UpdateCanAnalyze();
        }

        #endregion Constructor

        #region Methods

        private void UpdateClusterGroups()
        {
            // Unsubscribe from existing groups
            foreach (SimilarityGroup group in ClusterGroups)
            {
                group.GroupSelectionChanged -= OnAnyGroupSelectionChanged;
            }

            ClusterGroups.Clear();

            if (SimilarityResult == null)
            {
                return;
            }

            // Show duplicate groups
            foreach (SimilarityGroup cluster in SimilarityResult.DuplicateGroups)
            {
                cluster.GroupSelectionChanged += OnAnyGroupSelectionChanged;
                ClusterGroups.Add(cluster);
            }

            // Apply current sort
            ApplyCurrentSort();

            UpdateSelectionSummary();
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

        private void UpdateAfterDeletion(List<string> deletedPaths)
        {
            List<SimilarityGroup> groupsToRemove = [];
            int totalItemsRemoved = 0;

            // Snapshot of groups to iterate
            List<SimilarityGroup> groupsSnapshot = [.. ClusterGroups];

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
                ClusterGroups.Remove(group);

                // Clear selected cluster
                if (SelectedCluster == group)
                {
                    SelectedCluster = null;
                }
            }

            // Update UI
            OnPropertyChanged(nameof(DuplicateGroupsCount));
            UpdateSelectionSummary();

            _logger.LogInformation("Refreshed after deletion: Removed {ItemCount} items from groups, removed {GroupCount} groups", totalItemsRemoved, groupsToRemove.Count);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _appStateService.ExtractedFeaturesChanged -= OnExtractedFeaturesChanged;

                if (_selectedCluster != null)
                {
                    _selectedCluster.GroupSelectionChanged -= OnGroupSelectionChanged;
                }

                foreach (SimilarityGroup group in ClusterGroups)
                {
                    group.GroupSelectionChanged -= OnAnyGroupSelectionChanged;
                    group.Cleanup();
                }
            }
            base.Dispose(disposing);
        }

        #endregion Methods
    }
}