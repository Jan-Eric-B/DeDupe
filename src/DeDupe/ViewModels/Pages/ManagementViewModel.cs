using CommunityToolkit.Mvvm.Input;
using DeDupe.Models.Analysis;
using DeDupe.Services;
using DeDupe.Services.Analysis;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace DeDupe.ViewModels.Pages
{
    public partial class ManagementViewModel : PageViewModelBase
    {
        #region Fields

        private readonly IAppStateService _appStateService;
        private readonly ILogger<ManagementViewModel> _logger;

        private bool _isAnalyzingSimilarity;
        private bool _hasSimilarityResults;
        private double _similarityThreshold = 0.85;
        private SimilarityResult? _similarityResult;
        private ImageCluster? _selectedCluster;

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
                    OnPropertyChanged(nameof(TotalImages));
                    OnPropertyChanged(nameof(ResultSummary));
                }
            }
        }

        public ObservableCollection<ImageCluster> ClusterGroups { get; } = [];

        public ImageCluster? SelectedCluster
        {
            get => _selectedCluster;
            set
            {
                // Unsubscribe from old cluster
                if (_selectedCluster != null)
                {
                    _selectedCluster.GroupSelectionChanged -= OnGroupSelectionChanged;
                }

                if (SetProperty(ref _selectedCluster, value))
                {
                    // Subscribe to new cluster
                    if (_selectedCluster != null)
                    {
                        _selectedCluster.GroupSelectionChanged += OnGroupSelectionChanged;
                    }

                    UpdateSelectionCommands();
                }
            }
        }

        public bool CanStartSimilarityAnalysis => !IsAnalyzingSimilarity && _appStateService.ExtractedFeaturesCount > 0;

        public bool HasSelectedImages => SelectedCluster?.SelectedCount > 0;

        // Statistics Properties
        public int TotalClusters => SimilarityResult?.TotalClusters ?? 0;

        public int DuplicateGroupsCount => SimilarityResult?.DuplicateGroupsCount ?? 0;
        public int TotalImages => SimilarityResult?.TotalImagesAnalyzed ?? 0;
        public string ResultSummary => SimilarityResult?.GetSummary() ?? string.Empty;

        // Configuration Info Properties
        public int TotalFileCount => _appStateService.FileCount;

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
                Status = "Analyzing image similarities...";

                // Get extracted features
                List<ExtractedFeatures> features = [.. _appStateService.ExtractedFeatures];

                if (features.Count == 0)
                {
                    Status = "No features available. Please extract features first.";
                    return;
                }

                // Perform clustering
                SimilarityResult = await SimilarityAnalysisService.PerformClusteringAsync(features, SimilarityThreshold);

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

        public ManagementViewModel(
            IAppStateService appStateService,
            ILogger<ManagementViewModel> logger) : base(3)
        {
            _appStateService = appStateService ?? throw new ArgumentNullException(nameof(appStateService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            Title = "Duplicate Management";
            Status = "Ready to analyze similarities";

            // Subscribe to feature changes
            _appStateService.ExtractedFeaturesChanged += OnExtractedFeaturesChanged;

            UpdateCanAnalyze();
        }

        #endregion Constructor

        #region Methods

        private void UpdateClusterGroups()
        {
            ClusterGroups.Clear();

            if (SimilarityResult == null)
                return;

            // Show duplicate groups
            foreach (ImageCluster cluster in SimilarityResult.DuplicateGroups)
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
            OnPropertyChanged(nameof(HasSelectedImages));
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