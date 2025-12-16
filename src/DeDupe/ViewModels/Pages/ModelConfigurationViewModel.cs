using CommunityToolkit.Mvvm.Input;
using DeDupe.Models;
using DeDupe.Models.Analysis;
using DeDupe.Services;
using DeDupe.Services.Analysis;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace DeDupe.ViewModels.Pages
{
    public partial class ModelConfigurationViewModel : PageViewModelBase
    {
        #region Fields

        private readonly IAppStateService _appStateService;
        private readonly IBundledModelService _bundledModelService;
        private readonly FeatureExtractionService _featureExtractionService;

        private List<ExtractedFeatures> _extractedFeatures = [];
        private bool _isExtracting;
        private bool _hasExtractedFeatures;
        private int _extractedFeaturesCount;

        #endregion Fields

        #region Properties

        #region Model Selection Properties

        /// <summary>
        /// Gets or sets whether to use bundled model.
        /// </summary>
        public bool UseBundledModel
        {
            get => _appStateService.UseBundledModel;
            set
            {
                if (_appStateService.UseBundledModel != value)
                {
                    _appStateService.UseBundledModel = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(UseCustomModel));
                    OnPropertyChanged(nameof(ModelFilePath));
                    OnPropertyChanged(nameof(DirectoryPath));
                    OnPropertyChanged(nameof(FileName));
                    OnPropertyChanged(nameof(ModelDisplayName));
                    OnPropertyChanged(nameof(CanStartExtraction));
                    OnPropertyChanged(nameof(IsCustomModelSectionVisible));
                    OnPropertyChanged(nameof(ShowBundledModelAvailable));
                    OnPropertyChanged(nameof(ShowBundledModelNotFound));

                    ResetExtractionState();
                    UpdateCompletionStatus();
                    ExtractFeaturesCommand.NotifyCanExecuteChanged();
                }
            }
        }

        /// <summary>
        /// Gets or sets whether to use custom model.
        /// </summary>
        public bool UseCustomModel
        {
            get => !UseBundledModel;
            set => UseBundledModel = !value;
        }

        /// <summary>
        /// Gets whether bundled model is available.
        /// </summary>
        public bool IsBundledModelAvailable => _bundledModelService.IsBundledModelAvailable;

        /// <summary>
        /// Gets display name of bundled model.
        /// </summary>
        public string BundledModelName => _bundledModelService.BundledModelName;

        /// <summary>
        /// Gets whether custom model section should be expanded.
        /// </summary>
        public bool IsCustomModelSectionVisible => !UseBundledModel;

        /// <summary>
        /// Gets whether to show "bundled model available" success message.
        /// </summary>
        public bool ShowBundledModelAvailable => UseBundledModel && IsBundledModelAvailable;

        /// <summary>
        /// Gets whether to show "bundled model not found" error message.
        /// </summary>
        public bool ShowBundledModelNotFound => UseBundledModel && !IsBundledModelAvailable;

        /// <summary>
        /// Gets display name for currently selected model.
        /// </summary>
        public string ModelDisplayName
        {
            get
            {
                if (UseBundledModel)
                {
                    return BundledModelName;
                }
                return !string.IsNullOrEmpty(CustomModelFilePath) ? Path.GetFileName(CustomModelFilePath) : "No model selected";
            }
        }

        /// <summary>
        /// Gets or sets custom model file path.
        /// </summary>
        public string CustomModelFilePath
        {
            get => _appStateService.CustomModelFilePath;
            set
            {
                if (_appStateService.CustomModelFilePath != value)
                {
                    _appStateService.CustomModelFilePath = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(CustomDirectoryPath));
                    OnPropertyChanged(nameof(CustomFileName));
                    OnPropertyChanged(nameof(ModelDisplayName));
                    OnPropertyChanged(nameof(CanStartExtraction));

                    if (!UseBundledModel)
                    {
                        OnPropertyChanged(nameof(ModelFilePath));
                        OnPropertyChanged(nameof(DirectoryPath));
                        OnPropertyChanged(nameof(FileName));
                    }

                    ResetExtractionState();
                    UpdateCompletionStatus();
                    ExtractFeaturesCommand.NotifyCanExecuteChanged();
                }
            }
        }

        /// <summary>
        /// Gets model file path (bundled or custom).
        /// </summary>
        public string ModelFilePath => _appStateService.ModelPath;

        #endregion Model Selection Properties

        #region Display Properties

        public string DirectoryPath
        {
            get
            {
                string path = ModelFilePath;
                return !string.IsNullOrEmpty(path)
                    ? Path.GetDirectoryName(path) + Path.DirectorySeparatorChar
                    : string.Empty;
            }
        }

        public string FileName
        {
            get
            {
                if (UseBundledModel)
                {
                    return IsBundledModelAvailable ? BundledModelName : "Bundled model not found";
                }
                return !string.IsNullOrEmpty(CustomModelFilePath)
                    ? Path.GetFileName(CustomModelFilePath)
                    : "Select a model file...";
            }
        }

        public string CustomDirectoryPath
        {
            get
            {
                return !string.IsNullOrEmpty(CustomModelFilePath)
                    ? Path.GetDirectoryName(CustomModelFilePath) + Path.DirectorySeparatorChar
                    : string.Empty;
            }
        }

        public string CustomFileName
        {
            get
            {
                return !string.IsNullOrEmpty(CustomModelFilePath)
                    ? Path.GetFileName(CustomModelFilePath)
                    : "Select a model file...";
            }
        }

        #endregion Display Properties

        #region Extraction State Properties

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
                    OnPropertyChanged(nameof(CanCloseConfiguration));
                    NavigateToManagementPageCommand.NotifyCanExecuteChanged();
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

        public bool CanStartExtraction
        {
            get
            {
                if (IsExtracting || !HasProcessedImages())
                    return false;

                if (UseBundledModel)
                    return IsBundledModelAvailable;

                return !string.IsNullOrEmpty(CustomModelFilePath) && File.Exists(CustomModelFilePath);
            }
        }

        public bool CanCloseConfiguration => HasExtractedFeatures;

        public List<ExtractedFeatures> ExtractedFeatures => _extractedFeatures;

        #endregion Extraction State Properties

        #region Normalization Properties

        public double MeanR
        {
            get => _appStateService.MeanR;
            set
            {
                if (_appStateService.MeanR != value)
                {
                    _appStateService.MeanR = value;
                    OnPropertyChanged();
                    ResetExtractionState();
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

        #endregion Normalization Properties

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
                IReadOnlyCollection<ProcessedMedia>? processedImages = _appStateService.ProcessedImages;

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

                    // Store in AppStateService
                    _appStateService.SetExtractedFeatures(_extractedFeatures);
                    Status = $"Successfully extracted {ExtractedFeaturesCount} feature vectors.";

                    // Log feature info
                    if (_extractedFeatures.Count > 0)
                    {
                        ExtractedFeatures firstFeature = _extractedFeatures[0];
                        System.Diagnostics.Debug.WriteLine($"Feature vector size: {firstFeature.FeatureCount}");
                        System.Diagnostics.Debug.WriteLine($"Feature dimensions: [{string.Join(", ", firstFeature.FeatureDimensions)}]");
                    }
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
        private async Task SelectCustomModelFileAsync()
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
                CustomModelFilePath = file.Path;
                UseBundledModel = false;
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

        [RelayCommand(CanExecute = nameof(CanCloseConfiguration))]
        private void NavigateToManagementPage()
        {
            MainWindowViewModel mainViewModel = App.Current.GetService<MainWindowViewModel>();
            mainViewModel?.StartManagementModeCommand.Execute(null);
        }

        #endregion Commands

        #region Constructor

        public ModelConfigurationViewModel(
            IAppStateService appStateService,
            IBundledModelService bundledModelService,
            FeatureExtractionService featureExtractionService) : base(2)
        {
            _appStateService = appStateService ?? throw new ArgumentNullException(nameof(appStateService));
            _bundledModelService = bundledModelService ?? throw new ArgumentNullException(nameof(bundledModelService));
            _featureExtractionService = featureExtractionService ?? throw new ArgumentNullException(nameof(featureExtractionService));

            Title = "Model Configuration";

            // Subscribe to app state changes
            _appStateService.ModelConfigurationSettingsChanged += OnModelConfigurationSettingsChanged;
            _appStateService.ModelSourceChanged += OnModelSourceChanged;

            UpdateCompletionStatus();
        }

        #endregion Constructor

        #region Methods

        public override bool CanNavigateToNext => false;

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
            IsComplete = HasExtractedFeatures;
            NavigateToNextCommand.NotifyCanExecuteChanged();
        }

        private async Task InitializeFeatureExtractionAsync()
        {
            try
            {
                string modelPath = ModelFilePath;

                if (!string.IsNullOrEmpty(modelPath) && File.Exists(modelPath))
                {
                    Status = "Initializing Model...";

                    // Get normalization values
                    (double meanR, double meanG, double meanB, double stdR, double stdG, double stdB) = _appStateService.GetNormalization();

                    // Initialize feature extraction service
                    await _featureExtractionService.InitializeAsync(
                        modelPath,
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

        private void OnModelConfigurationSettingsChanged(object? sender, EventArgs e)
        {
            // Update UI when app state changes
            OnPropertyChanged(nameof(ModelFilePath));
            OnPropertyChanged(nameof(MeanR));
            OnPropertyChanged(nameof(MeanG));
            OnPropertyChanged(nameof(MeanB));
            OnPropertyChanged(nameof(StdR));
            OnPropertyChanged(nameof(StdG));
            OnPropertyChanged(nameof(StdB));

            UpdateCompletionStatus();
        }

        private void OnModelSourceChanged(object? sender, EventArgs e)
        {
            OnPropertyChanged(nameof(UseBundledModel));
            OnPropertyChanged(nameof(UseCustomModel));
            OnPropertyChanged(nameof(ModelFilePath));
            OnPropertyChanged(nameof(DirectoryPath));
            OnPropertyChanged(nameof(FileName));
            OnPropertyChanged(nameof(ModelDisplayName));
            OnPropertyChanged(nameof(IsCustomModelSectionVisible));
            OnPropertyChanged(nameof(CanStartExtraction));

            ExtractFeaturesCommand.NotifyCanExecuteChanged();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _appStateService.ModelConfigurationSettingsChanged -= OnModelConfigurationSettingsChanged;
                _appStateService.ModelSourceChanged -= OnModelSourceChanged;
            }
            base.Dispose(disposing);
        }

        #endregion Methods
    }
}