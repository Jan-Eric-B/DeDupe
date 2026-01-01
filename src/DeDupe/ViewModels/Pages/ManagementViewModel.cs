using CommunityToolkit.Mvvm.Input;
using DeDupe.Enums;
using DeDupe.Models;
using DeDupe.Models.Analysis;
using DeDupe.Services;
using DeDupe.Services.Analysis;
using Microsoft.Extensions.Logging;
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
        private readonly ISimilarityAnalysisService _similarityAnalysisService;
        private readonly IAutoSelectionService _autoSelectionService;
        private readonly ILogger<ManagementViewModel> _logger;

        private bool _isAnalyzingSimilarity;
        private bool _hasSimilarityResults;
        private double _similarityThreshold = 0.85;
        private SimilarityResult? _similarityResult;
        private SimilarityGroup? _selectedCluster;
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
                if (string.IsNullOrEmpty(_appStateService.ModelFilePath))
                    return "No model";

                return Path.GetFileNameWithoutExtension(_appStateService.ModelFilePath);
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
        }

        #endregion Commands


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

        public ManagementViewModel(IAppStateService appStateService, ISimilarityAnalysisService similarityAnalysisService, IAutoSelectionService autoSelectionService, ILogger<ManagementViewModel> logger) : base(3)
        {
            _appStateService = appStateService ?? throw new ArgumentNullException(nameof(appStateService));
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