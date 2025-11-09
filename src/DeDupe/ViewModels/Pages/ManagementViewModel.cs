using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeDupe.Models.Analysis;
using DeDupe.Services;
using DeDupe.Services.Analysis;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace DeDupe.ViewModels.Pages
{
    public partial class ManagementViewModel : ObservableObject
    {
        #region Fields

        private readonly IAppStateService _appStateService;

        private SimilarityResult? _similarityResult;
        private double _similarityThreshold = 0.85;
        private bool _isAnalyzingSimilarity;
        private bool _hasSimilarityResults;
        private string _status = string.Empty;
        private string _resultsMessage = string.Empty;

        #endregion Fields

        #region Properties

        public int ExtractedFeaturesCount => _appStateService.ExtractedFeaturesCount;

        public bool HasExtractedFeatures => _appStateService.ExtractedFeaturesCount > 0;

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
                    OnPropertyChanged(nameof(ResultsMessage));
                }
            }
        }

        public double SimilarityThreshold
        {
            get => _similarityThreshold;
            set => SetProperty(ref _similarityThreshold, value);
        }

        public bool CanStartSimilarityAnalysis => !IsAnalyzingSimilarity && HasExtractedFeatures;

        public string Status
        {
            get => _status;
            set => SetProperty(ref _status, value);
        }

        public string ResultsMessage
        {
            get => _resultsMessage;
            set => SetProperty(ref _resultsMessage, value);
        }

        public SimilarityResult? SimilarityResult => _similarityResult;

        #endregion Properties

        #region Constructor

        public ManagementViewModel(IAppStateService appStateService)
        {
            _appStateService = appStateService ?? throw new ArgumentNullException(nameof(appStateService));

            // Subscribe to extracted features changes
            _appStateService.ExtractedFeaturesChanged += OnExtractedFeaturesChanged;

            // Update UI with current state
            UpdateExtractedFeaturesStatus();
        }

        #endregion Constructor

        #region Commands

        [RelayCommand(CanExecute = nameof(CanStartSimilarityAnalysis))]
        private async Task AnalyzeSimilarityAsync()
        {
            try
            {
                IsAnalyzingSimilarity = true;
                Status = "Starting similarity analysis...";
                ResultsMessage = string.Empty;

                var extractedFeatures = _appStateService.ExtractedFeatures.ToList();

                if (extractedFeatures.Count == 0)
                {
                    Status = "No extracted features available for similarity analysis";
                    return;
                }

                Status = $"Analyzing similarity between {extractedFeatures.Count} images...";

                // Perform clustering
                _similarityResult = await SimilarityAnalysisService.PerformClusteringAsync(extractedFeatures, SimilarityThreshold);

                // Update results
                if (_similarityResult != null)
                {
                    HasSimilarityResults = true;
                    Status = "Similarity analysis completed!";
                    ResultsMessage = _similarityResult.GetSummary();

                    // Log clustering results
                    System.Diagnostics.Debug.WriteLine($"Clustering completed:");
                    System.Diagnostics.Debug.WriteLine($"Total clusters: {_similarityResult.TotalClusters}");
                    System.Diagnostics.Debug.WriteLine($"Duplicate groups: {_similarityResult.DuplicateGroupsCount}");
                    System.Diagnostics.Debug.WriteLine($"Singleton clusters: {_similarityResult.SingletonClustersCount}");

                    foreach (ImageCluster cluster in _similarityResult.DuplicateGroups)
                    {
                        System.Diagnostics.Debug.WriteLine($"Cluster {cluster.Id}: {cluster.Count} images, avg similarity: {cluster.AverageSimilarity:F3}");
                    }
                }
                else
                {
                    Status = "Similarity analysis failed: No results generated.";
                    ResultsMessage = "Analysis did not produce any results.";
                }
            }
            catch (Exception ex)
            {
                Status = $"Error during similarity analysis: {ex.Message}";
                ResultsMessage = "An error occurred during analysis.";
                System.Diagnostics.Debug.WriteLine($"Similarity analysis error: {ex}");
            }
            finally
            {
                IsAnalyzingSimilarity = false;
            }
        }

        #endregion Commands

        #region Methods

        private void UpdateExtractedFeaturesStatus()
        {
            if (HasExtractedFeatures)
            {
                Status = $"{ExtractedFeaturesCount} feature vectors loaded. Ready to analyze similarity.";
            }
            else
            {
                Status = "No extracted features available.";
            }

            OnPropertyChanged(nameof(HasExtractedFeatures));
            OnPropertyChanged(nameof(ExtractedFeaturesCount));
            OnPropertyChanged(nameof(CanStartSimilarityAnalysis));
            AnalyzeSimilarityCommand.NotifyCanExecuteChanged();
        }

        private void OnExtractedFeaturesChanged(object? sender, EventArgs e)
        {
            UpdateExtractedFeaturesStatus();
        }

        public void Cleanup()
        {
            _appStateService.ExtractedFeaturesChanged -= OnExtractedFeaturesChanged;
        }

        #endregion Methods
    }
}