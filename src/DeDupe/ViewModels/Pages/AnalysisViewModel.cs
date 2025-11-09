using CommunityToolkit.Mvvm.Input;
using DeDupe.Models.Analysis;
using DeDupe.Services;
using DeDupe.Services.Analysis;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace DeDupe.ViewModels.Pages
{
    public partial class AnalysisViewModel : PageViewModelBase
    {
        #region Fields

        private readonly IAppStateService _appStateService;

        private SimilarityResult? _similarityResult;

        private double _similarityThreshold = 0.85;

        private bool _isAnalyzingSimilarity;

        private bool _hasSimilarityResults;

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
                    OnPropertyChanged(nameof(Status));
                    OnPropertyChanged(nameof(CanEnterManagementMode));
                    UpdateCompletionStatus();
                }
            }
        }

        public double SimilarityThreshold
        {
            get => _similarityThreshold;
            set => SetProperty(ref _similarityThreshold, value);
        }

        public bool CanStartSimilarityAnalysis => !IsAnalyzingSimilarity && HasExtractedFeatures;

        public bool CanEnterManagementMode => HasSimilarityResults;

        #endregion Properties

        #region Constructor

        public AnalysisViewModel(IAppStateService appStateService) : base(3)
        {
            _appStateService = appStateService ?? throw new ArgumentNullException(nameof(appStateService));

            Title = "Similarity Analysis";

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
                IsBusy = true;
                Status = "Starting similarity analysis...";

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
                    Status = $"Similarity analysis completed! {_similarityResult.GetSummary()}";

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
                }
            }
            catch (Exception ex)
            {
                Status = $"Error during similarity analysis: {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"Similarity analysis error: {ex}");
            }
            finally
            {
                IsAnalyzingSimilarity = false;
                IsBusy = false;
            }
        }

        [RelayCommand]
        private static void EnterManagementMode()
        {
            // Get the MainWindowViewModel from the service provider and trigger management mode
            var mainViewModel = App.Current.GetService<MainWindowViewModel>();
            mainViewModel?.StartManagementModeCommand.Execute(null);
        }

        #endregion Commands

        #region Methods

        private void UpdateCompletionStatus()
        {
            IsComplete = HasExtractedFeatures && HasSimilarityResults;
        }

        private void UpdateExtractedFeaturesStatus()
        {
            if (HasExtractedFeatures)
            {
                Status = $"{ExtractedFeaturesCount} feature vectors loaded. Ready to analyze similarity.";
            }
            else
            {
                Status = "No extracted features available. Please complete feature extraction first.";
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

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _appStateService.ExtractedFeaturesChanged -= OnExtractedFeaturesChanged;
            }
            base.Dispose(disposing);
        }

        #endregion Methods
    }
}