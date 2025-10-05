using CommunityToolkit.Mvvm.Input;
using DeDupe.Models.Analysis;
using DeDupe.Services;
using DeDupe.Services.Analysis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DeDupe.ViewModels.Pages
{
    public partial class AnalysisViewModel : PageViewModelBase
    {
        #region Fields

        private readonly IAppStateService _appStateService;

        private readonly FeatureExtractionService _featureExtractionService;

        private List<ExtractedFeatures> _extractedFeatures = [];

        private SimilarityResult? _similarityResult;

        private bool _isExtracting;

        private double _similarityThreshold = 0.85;

        private int _extractedFeaturesCount;

        private bool _hasExtractedFeatures;

        private bool _isAnalyzingSimilarity;

        private bool _hasSimilarityResults;

        #endregion Fields

        #region Properties

        public bool IsExtracting
        {
            get => _isExtracting;
            set
            {
                if (SetProperty(ref _isExtracting, value))
                {
                    OnPropertyChanged(nameof(CanStartExtraction));
                    OnPropertyChanged(nameof(CanStartSimilarityAnalysis));
                    ExtractFeaturesCommand.NotifyCanExecuteChanged();
                    AnalyzeSimilarityCommand.NotifyCanExecuteChanged();
                }
            }
        }

        public int ExtractedFeaturesCount
        {
            get => _extractedFeaturesCount;
            set
            {
                if (SetProperty(ref _extractedFeaturesCount, value))
                {
                    OnPropertyChanged(nameof(Status));
                }
            }
        }

        public bool HasExtractedFeatures
        {
            get => _hasExtractedFeatures;
            set
            {
                if (SetProperty(ref _hasExtractedFeatures, value))
                {
                    OnPropertyChanged(nameof(Status));
                    OnPropertyChanged(nameof(CanStartSimilarityAnalysis));
                    AnalyzeSimilarityCommand.NotifyCanExecuteChanged();
                    UpdateCompletionStatus();
                }
            }
        }

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
                    UpdateCompletionStatus();
                }
            }
        }

        public double SimilarityThreshold
        {
            get => _similarityThreshold;
            set => SetProperty(ref _similarityThreshold, value);
        }

        public bool CanStartExtraction => !IsExtracting && !IsAnalyzingSimilarity && HasProcessedImages();

        public bool CanStartSimilarityAnalysis => !IsExtracting && !IsAnalyzingSimilarity && HasExtractedFeatures;

        #endregion Properties

        #region Constructor

        public AnalysisViewModel(IAppStateService appStateService, FeatureExtractionService featureExtractionService) : base(3)
        {
            _appStateService = appStateService ?? throw new ArgumentNullException(nameof(appStateService));
            _featureExtractionService = featureExtractionService;

            Title = "Feature Extraction and Similarity Analysis";
        }

        #endregion Constructor

        #region Commands

        [RelayCommand]
        private async Task ExtractFeaturesAsync()
        {
            try
            {
                IsExtracting = true;
                Status = "Starting feature extraction...";
                ExtractedFeaturesCount = 0;

                // Check if the service is initialized
                if (!_featureExtractionService.IsInitialized)
                {
                    await InitializeFeatureExtractionAsync();
                    if (!_featureExtractionService.IsInitialized)
                    {
                        Status = "Cannot extract features: Model not loaded";
                        return;
                    }
                }

                // Get processed images from preprocessing view model
                IReadOnlyCollection<Models.ProcessedMedia> processedImages = _appStateService.ProcessedImages;

                if (processedImages.Count == 0)
                {
                    Status = "No processed images available for feature extraction";
                    return;
                }

                Status = $"Extracting features from {processedImages.Count} images...";

                // Extract features
                _extractedFeatures = await _featureExtractionService.ExtractFeaturesAsync(processedImages);

                // Update results
                ExtractedFeaturesCount = _extractedFeatures.Count;

                if (_extractedFeatures.Count > 0)
                {
                    HasExtractedFeatures = true;
                    Status = $"Feature extraction completed! {ExtractedFeaturesCount} feature vectors extracted.";

                    // Reset similarity analysis results since we have new features
                    _similarityResult = null;
                    HasSimilarityResults = false;
                    Status = "Ready to analyze similarity";

                    // Log some information about the extracted features
                    ExtractedFeatures firstFeature = _extractedFeatures.First();
                    System.Diagnostics.Debug.WriteLine($"Feature vector size: {firstFeature.FeatureCount}");
                    System.Diagnostics.Debug.WriteLine($"Feature dimensions: [{string.Join(", ", firstFeature.FeatureDimensions)}]");
                }
                else
                {
                    Status = "Feature extraction failed: No features were extracted.";
                }
            }
            catch (Exception ex)
            {
                Status = $"Error during feature extraction: {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"Feature extraction error: {ex}");
            }
            finally
            {
                IsExtracting = false;
            }
        }

        [RelayCommand]
        private async Task AnalyzeSimilarityAsync()
        {
            try
            {
                IsAnalyzingSimilarity = true;
                Status = "Starting similarity analysis...";

                if (!HasExtractedFeatures || _extractedFeatures.Count == 0)
                {
                    Status = "No extracted features available for similarity analysis";
                    return;
                }

                Status = $"Analyzing similarity between {_extractedFeatures.Count} images...";

                // Perform clustering
                _similarityResult = await SimilarityAnalysisService.PerformClusteringAsync(_extractedFeatures, SimilarityThreshold);

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
            }
        }

        #endregion Commands

        #region Methods

        private bool HasProcessedImages()
        {
            return _appStateService?.ProcessedImageCount != 0;
        }

        private void UpdateCompletionStatus()
        {
            IsComplete = HasExtractedFeatures && HasSimilarityResults;
        }

        private async Task InitializeFeatureExtractionAsync()
        {
            try
            {
                // Get model file path and validate
                if (!string.IsNullOrEmpty(_appStateService.ModelFilePath) && System.IO.File.Exists(_appStateService.ModelFilePath))
                {
                    Status = "Initializing model...";

                    // Get normalization vales
                    (double meanR, double meanG, double meanB, double stdR, double stdG, double stdB) = _appStateService.GetNormalization();

                    // Initialize feature extraction service
                    await _featureExtractionService.InitializeAsync(_appStateService.ModelFilePath, (float)meanR, (float)meanG, (float)meanB, (float)stdR, (float)stdG, (float)stdB);

                    Status = "Model loaded successfully. Ready to extract features.";

                    // Update command availability
                    ExtractFeaturesCommand.NotifyCanExecuteChanged();
                }
                else
                {
                    Status = "No valid model file found.";
                }
            }
            catch (Exception ex)
            {
                Status = $"Error initializing model: {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"Model initialization error: {ex}");
            }
        }

        #endregion Methods
    }
}