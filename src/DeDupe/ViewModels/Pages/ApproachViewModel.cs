using CommunityToolkit.Mvvm.Input;
using DeDupe.Enums.Approach;
using DeDupe.Models.Analysis;
using DeDupe.Services;
using DeDupe.Services.Analysis;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace DeDupe.ViewModels.Pages
{
    public partial class ApproachViewModel : PageViewModelBase
    {
        #region Fields

        private readonly IAppStateService _appStateService;
        private readonly FeatureExtractionService _featureExtractionService;

        private List<ExtractedFeatures> _extractedFeatures = [];
        private bool _isExtracting;
        private bool _hasExtractedFeatures;
        private int _extractedFeaturesCount;

        #endregion Fields

        #region Properties

        public ApproachType SelectedApproach
        {
            get => _appStateService.SelectedApproach;
            set
            {
                if (_appStateService.SelectedApproach != value)
                {
                    _appStateService.SelectedApproach = value;

                    // Notify radio buttons
                    OnPropertyChanged(nameof(IsDeepLearningSelected));
                    OnPropertyChanged(nameof(IsPerceptualHashingSelected));
                    OnPropertyChanged(nameof(IsColorHistogramSelected));
                    OnPropertyChanged(nameof(IsSiftSurfSelected));
                    OnPropertyChanged(nameof(IsTemplateMatchingSelected));
                    OnPropertyChanged(nameof(IsSemanticSimilaritySelected));
                    OnPropertyChanged(nameof(IsOtherApproachSelected));

                    // Reset extraction state
                    ResetExtractionState();
                    UpdateCompletionStatus();
                }
            }
        }

        public string ModelFilePath
        {
            get => _appStateService.ModelFilePath;
            set
            {
                if (_appStateService.ModelFilePath != value)
                {
                    _appStateService.ModelFilePath = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(DirectoryPath));
                    OnPropertyChanged(nameof(FileName));
                    OnPropertyChanged(nameof(CanStartExtraction));

                    // Reset extraction state
                    ResetExtractionState();
                    UpdateCompletionStatus();
                    ExtractFeaturesCommand.NotifyCanExecuteChanged();
                }
            }
        }

        public bool IsExtracting
        {
            get => _isExtracting;
            set
            {
                if (SetProperty(ref _isExtracting, value))
                {
                    OnPropertyChanged(nameof(CanStartExtraction));
                    ExtractFeaturesCommand.NotifyCanExecuteChanged();
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
                    UpdateCompletionStatus();
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

        public bool CanStartExtraction =>
            !IsExtracting &&
            IsDeepLearningSelected &&
            !string.IsNullOrEmpty(ModelFilePath) &&
            File.Exists(ModelFilePath) &&
            HasProcessedImages();

        public List<ExtractedFeatures> ExtractedFeatures => _extractedFeatures;

        public bool IsDeepLearningSelected
        {
            get => SelectedApproach == ApproachType.DeepLearning;
            set { if (value) SelectedApproach = ApproachType.DeepLearning; }
        }

        public bool IsPerceptualHashingSelected
        {
            get => SelectedApproach == ApproachType.PerceptualHashing;
            set { if (value) SelectedApproach = ApproachType.PerceptualHashing; }
        }

        public bool IsColorHistogramSelected
        {
            get => SelectedApproach == ApproachType.ColorHistogram;
            set { if (value) SelectedApproach = ApproachType.ColorHistogram; }
        }

        public bool IsSiftSurfSelected
        {
            get => SelectedApproach == ApproachType.SiftSurf;
            set { if (value) SelectedApproach = ApproachType.SiftSurf; }
        }

        public bool IsTemplateMatchingSelected
        {
            get => SelectedApproach == ApproachType.TemplateMatching;
            set { if (value) SelectedApproach = ApproachType.TemplateMatching; }
        }

        public bool IsSemanticSimilaritySelected
        {
            get => SelectedApproach == ApproachType.SemanticSimilarity;
            set { if (value) SelectedApproach = ApproachType.SemanticSimilarity; }
        }

        public Visibility IsOtherApproachSelected => !IsDeepLearningSelected ? Visibility.Visible : Visibility.Collapsed;

        public double MeanR
        {
            get => _appStateService.MeanR;
            set
            {
                if (_appStateService.MeanR != value)
                {
                    _appStateService.MeanR = value;
                    OnPropertyChanged();
                    ResetExtractionState(); // Normalization changed, need to re-extract
                }
            }
        }

        public double MeanG
        {
            get => _appStateService.MeanG;
            set
            {
                if (_appStateService.MeanG != value)
                {
                    _appStateService.MeanG = value;
                    OnPropertyChanged();
                    ResetExtractionState();
                }
            }
        }

        public double MeanB
        {
            get => _appStateService.MeanB;
            set
            {
                if (_appStateService.MeanB != value)
                {
                    _appStateService.MeanB = value;
                    OnPropertyChanged();
                    ResetExtractionState();
                }
            }
        }

        public double StdR
        {
            get => _appStateService.StdR;
            set
            {
                if (_appStateService.StdR != value)
                {
                    _appStateService.StdR = value;
                    OnPropertyChanged();
                    ResetExtractionState();
                }
            }
        }

        public double StdG
        {
            get => _appStateService.StdG;
            set
            {
                if (_appStateService.StdG != value)
                {
                    _appStateService.StdG = value;
                    OnPropertyChanged();
                    ResetExtractionState();
                }
            }
        }

        public double StdB
        {
            get => _appStateService.StdB;
            set
            {
                if (_appStateService.StdB != value)
                {
                    _appStateService.StdB = value;
                    OnPropertyChanged();
                    ResetExtractionState();
                }
            }
        }

        public string DirectoryPath => !string.IsNullOrEmpty(ModelFilePath) ? Path.GetDirectoryName(ModelFilePath) + Path.DirectorySeparatorChar : string.Empty;

        public string FileName => !string.IsNullOrEmpty(ModelFilePath) ? Path.GetFileName(ModelFilePath) : "Select a model file or drag and drop here...";

        #endregion Properties

        #region Commands

        [RelayCommand(CanExecute = nameof(CanStartExtraction))]
        private async Task ExtractFeaturesAsync()
        {
            try
            {
                IsExtracting = true;
                IsBusy = true;
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

                // Get processed images
                var processedImages = _appStateService.ProcessedImages;

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

                    // Store features in AppStateService for use in next pages
                    _appStateService.SetExtractedFeatures(_extractedFeatures);

                    Status = $"Successfully extracted {ExtractedFeaturesCount} feature vectors. Ready to proceed.";

                    // Log feature info
                    var firstFeature = _extractedFeatures.First();
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
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task SelectModelFileAsync()
        {
            FileOpenPicker fileOpenPicker = new()
            {
                ViewMode = PickerViewMode.List,
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary
            };

            fileOpenPicker.FileTypeFilter.Add(".onnx");

            // Initialize file picker
            nint windowHandle = WindowNative.GetWindowHandle(App.Window);
            InitializeWithWindow.Initialize(fileOpenPicker, windowHandle);

            StorageFile? file = await fileOpenPicker.PickSingleFileAsync();
            if (file != null)
            {
                ModelFilePath = file.Path;
            }
        }

        [RelayCommand]
        private void ResetNormalization()
        {
            _appStateService.ResetNormalization();

            // Notify UI of changes
            OnPropertyChanged(nameof(MeanR));
            OnPropertyChanged(nameof(MeanG));
            OnPropertyChanged(nameof(MeanB));
            OnPropertyChanged(nameof(StdR));
            OnPropertyChanged(nameof(StdG));
            OnPropertyChanged(nameof(StdB));
        }

        #endregion Commands

        #region Constructor

        public ApproachViewModel(IAppStateService appStateService, FeatureExtractionService featureExtractionService) : base(2)
        {
            _appStateService = appStateService ?? throw new ArgumentNullException(nameof(appStateService));
            _featureExtractionService = featureExtractionService ?? throw new ArgumentNullException(nameof(featureExtractionService));

            Title = "Model Configuration";

            // Subscribe to app state changes
            _appStateService.ApproachSettingsChanged += OnApproachSettingsChanged;

            UpdateCompletionStatus();
        }

        #endregion Constructor

        #region Methods

        public override bool CanNavigateToNext
        {
            get
            {
                if (IsDeepLearningSelected)
                {
                    // Require model file AND extracted features
                    return !string.IsNullOrEmpty(_appStateService.ModelFilePath) &&
                           File.Exists(_appStateService.ModelFilePath) &&
                           HasExtractedFeatures;
                }
                else
                {
                    // TODO Implement other approaches
                    return IsPerceptualHashingSelected || IsColorHistogramSelected ||
                           IsSiftSurfSelected || IsTemplateMatchingSelected || IsSemanticSimilaritySelected;
                }
            }
        }

        private bool HasProcessedImages()
        {
            return _appStateService?.ProcessedImageCount > 0;
        }

        private void ResetExtractionState()
        {
            if (HasExtractedFeatures)
            {
                _extractedFeatures.Clear();
                HasExtractedFeatures = false;
                ExtractedFeaturesCount = 0;
                Status = "Configuration changed. Please extract features again.";
                UpdateCompletionStatus();
            }
        }

        private void UpdateCompletionStatus()
        {
            IsComplete = CanNavigateToNext;
            NavigateToNextCommand.NotifyCanExecuteChanged();
        }

        private async Task InitializeFeatureExtractionAsync()
        {
            try
            {
                if (!string.IsNullOrEmpty(_appStateService.ModelFilePath) && File.Exists(_appStateService.ModelFilePath))
                {
                    Status = "Initializing model...";

                    // Get normalization values
                    (double meanR, double meanG, double meanB, double stdR, double stdG, double stdB) = _appStateService.GetNormalization();

                    // Initialize feature extraction service
                    await _featureExtractionService.InitializeAsync(
                        _appStateService.ModelFilePath,
                        (float)meanR, (float)meanG, (float)meanB,
                        (float)stdR, (float)stdG, (float)stdB);

                    Status = "Model loaded successfully. Ready to extract features.";
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

        private void OnApproachSettingsChanged(object? sender, EventArgs e)
        {
            // Update UI when app state changes
            OnPropertyChanged(nameof(SelectedApproach));
            OnPropertyChanged(nameof(ModelFilePath));
            OnPropertyChanged(nameof(IsDeepLearningSelected));
            OnPropertyChanged(nameof(IsPerceptualHashingSelected));
            OnPropertyChanged(nameof(IsColorHistogramSelected));
            OnPropertyChanged(nameof(IsSiftSurfSelected));
            OnPropertyChanged(nameof(IsTemplateMatchingSelected));
            OnPropertyChanged(nameof(IsSemanticSimilaritySelected));
            OnPropertyChanged(nameof(IsOtherApproachSelected));
            OnPropertyChanged(nameof(MeanR));
            OnPropertyChanged(nameof(MeanG));
            OnPropertyChanged(nameof(MeanB));
            OnPropertyChanged(nameof(StdR));
            OnPropertyChanged(nameof(StdG));
            OnPropertyChanged(nameof(StdB));

            UpdateCompletionStatus();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _appStateService.ApproachSettingsChanged -= OnApproachSettingsChanged;
            }
            base.Dispose(disposing);
        }

        #endregion Methods
    }
}