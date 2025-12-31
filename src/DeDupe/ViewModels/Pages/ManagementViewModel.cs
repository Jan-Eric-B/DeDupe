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
        private readonly ILogger<ManagementViewModel> _logger;

        private bool _isAnalyzingSimilarity;
        private bool _hasSimilarityResults;
        private double _similarityThreshold = 0.85;
        private SimilarityResult? _similarityResult;
        private SimilarityGroup? _selectedCluster;

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

        // Statistics
        public int TotalClusters => SimilarityResult?.TotalClusters ?? 0;

        public int DuplicateGroupsCount => SimilarityResult?.DuplicateGroupsCount ?? 0;
        public int TotalItems => SimilarityResult?.TotalItemsAnalyzed ?? 0;
        public string ResultSummary => SimilarityResult?.GetSummary() ?? string.Empty;

        // Configuration Info
        public int TotalFileCount => _appStateService.SourceCount;

        public int ExtractedFeaturesCount => _appStateService.ExtractedFeaturesCount;

        public string ModelName
        {
            get
            {
                if (string.IsNullOrEmpty(_appStateService.ModelFilePath))
                    return "No model";

                return System.IO.Path.GetFileNameWithoutExtension(_appStateService.ModelFilePath);
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

        #endregion Commands

        #region Constructor

        public ManagementViewModel(IAppStateService appStateService, ISimilarityAnalysisService similarityAnalysisService, ILogger<ManagementViewModel> logger) : base(3)
        {
            _appStateService = appStateService ?? throw new ArgumentNullException(nameof(appStateService));
            _similarityAnalysisService = similarityAnalysisService ?? throw new ArgumentNullException(nameof(similarityAnalysisService));
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
            ClusterGroups.Clear();

            if (SimilarityResult == null)
            {
                return;
            }

            // Show duplicate groups
            foreach (SimilarityGroup cluster in SimilarityResult.DuplicateGroups)
            {
                ClusterGroups.Add(cluster);
            }
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
        }

        private void UpdateSelectionCommands()
        {
            OnPropertyChanged(nameof(HasSelectedItems));
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
            }
            base.Dispose(disposing);
        }

        #endregion Methods
    }
}