using CommunityToolkit.Mvvm.Input;
using DeDupe.Constants;
using DeDupe.Enums;
using DeDupe.Models;
using DeDupe.Models.Input;
using DeDupe.Services;
using DeDupe.Services.Analysis;
using DeDupe.Services.PreProcessing;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Search;
using Windows.System;
using WinRT.Interop;

namespace DeDupe.ViewModels.Pages
{
    public partial class ConfigurationViewModel : PageViewModelBase
    {
        #region Fields

        private readonly IAppStateService _appStateService;
        private readonly ISettingsService _settingsService;
        private readonly IBundledModelService _bundledModelService;
        private readonly IFeatureExtractionService _featureExtractionService;
        private readonly ImageProcessingService _imageProcessingService;

        private int _processingProgress;
        private bool _hasProcessedItems;
        private bool _isProcessing;
        private bool _hasExtractedFeatures;
        private int _extractedFeaturesCount;

        private bool _includeVideoFiles;
        private ObservableCollection<InputListItem> _inputListItems = [];

        // Track which files came from which source (for removal)
        private readonly Dictionary<string, HashSet<string>> _filePathToSourcesMap = new(StringComparer.OrdinalIgnoreCase);

        // Loaded SourceMedia objects
        private readonly Dictionary<string, SourceMedia> _loadedSourceMedia = new(StringComparer.OrdinalIgnoreCase);

        #endregion Fields

        #region Properties

        public bool IncludeVideoFiles
        {
            get => _includeVideoFiles;
            set
            {
                if (_includeVideoFiles != value)
                {
                    _includeVideoFiles = value;
                    OnPropertyChanged();
                }
            }
        }

        public ObservableCollection<InputListItem> InputListItems
        {
            get => _inputListItems;
            set
            {
                if (SetProperty(ref _inputListItems, value))
                {
                    OnPropertyChanged(nameof(IsMediaListEmpty));
                }
            }
        }

        public int TotalFileCount => _appStateService.SourceCount;

        public Visibility IsMediaListEmpty => InputListItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        public int ProcessingProgress
        {
            get => _processingProgress;
            set => SetProperty(ref _processingProgress, value);
        }

        /// <summary>
        /// Whether preprocessing has completed.
        /// </summary>
        public bool HasProcessedItems
        {
            get => _hasProcessedItems;
            set
            {
                if (SetProperty(ref _hasProcessedItems, value))
                {
                    OnPropertyChanged(nameof(CanOpenTempFolder));
                    OpenTempFolderCommand.NotifyCanExecuteChanged();
                }
            }
        }

        /// <summary>
        /// Whether the Process button can be clicked.
        /// Requires: not busy, has source files, and model is available.
        /// </summary>
        public bool CanStartProcessing
        {
            get
            {
                if (IsBusy || _appStateService.SourceCount == 0)
                {
                    return false;
                }

                // Check if model is available
                if (_settingsService.UseBundledModel)
                {
                    return _bundledModelService.IsBundledModelAvailable;
                }

                return !string.IsNullOrEmpty(_settingsService.CustomModelFilePath)
                    && File.Exists(_settingsService.CustomModelFilePath);
            }
        }

        public bool CanOpenTempFolder => HasProcessedItems && !string.IsNullOrEmpty(_settingsService.TempFolderPath);

        // Available options for dropdowns
        public IEnumerable<InterpolationMethod> InterpolationMethods => Enum.GetValues<InterpolationMethod>();

        public IEnumerable<ResizeMethod> ResizeMethods => Enum.GetValues<ResizeMethod>();
        public IEnumerable<OutputFormat> OutputFormats => Enum.GetValues<OutputFormat>();
        public IEnumerable<ColorFormat> BitDepths => Enum.GetValues<ColorFormat>();

        #region Model Selection Properties

        /// <summary>
        /// Gets model file path (bundled or custom).
        /// </summary>
        public string ModelFilePath => _settingsService.UseBundledModel
            ? _bundledModelService.BundledModelPath
            : _settingsService.CustomModelFilePath;

        #endregion Model Selection Properties

        #region Processing State Properties

        /// <summary>
        /// Whether processing (preprocess + extraction) is in progress.
        /// </summary>
        public bool IsProcessing
        {
            get => _isProcessing;
            set
            {
                if (SetProperty(ref _isProcessing, value))
                {
                    OnPropertyChanged(nameof(CanStartProcessing));
                    ProcessCommand.NotifyCanExecuteChanged();
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
                    OnPropertyChanged(nameof(CanNavigateToNext));
                    NavigateToNextCommand.NotifyCanExecuteChanged();
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

        #endregion Processing State Properties

        /// <summary>
        /// Can navigate to management page only when features have been extracted.
        /// </summary>
        public override bool CanNavigateToNext => HasExtractedFeatures;

        #endregion Properties

        #region Constructor

        public ConfigurationViewModel(
            IAppStateService appStateService,
            ISettingsService settingsService,
            IBundledModelService bundledModelService,
            IFeatureExtractionService featureExtractionService,
            IBorderDetectionService borderDetectionService,
            IImageFormatService imageFormatService,
            IImageResizeService imageResizeService) : base(0)
        {
            _appStateService = appStateService ?? throw new ArgumentNullException(nameof(appStateService));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _bundledModelService = bundledModelService ?? throw new ArgumentNullException(nameof(bundledModelService));
            _featureExtractionService = featureExtractionService ?? throw new ArgumentNullException(nameof(featureExtractionService));
            _imageProcessingService = new ImageProcessingService(
                _appStateService,
                _settingsService,
                borderDetectionService,
                imageFormatService,
                imageResizeService);

            Title = "File Input";
            Status = "No files added";
            InputListItems.CollectionChanged += MediaPathItems_CollectionChanged;
            IsBusy = false;

            _settingsService.ModelConfigurationChanged += OnModelConfigurationChanged;
            _appStateService.SourceMediaChanged += OnSourceMediaChanged;

            // Subscribe to own property changes to update CanStartProcessing when IsBusy changes
            PropertyChanged += OnOwnPropertyChanged;
        }

        #endregion Constructor

        #region Event Handlers

        /// <summary>
        /// Handle own property changes to update dependent properties.
        /// </summary>
        private void OnOwnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(IsBusy))
            {
                OnPropertyChanged(nameof(CanStartProcessing));
                ProcessCommand.NotifyCanExecuteChanged();
            }
        }

        private void OnSourceMediaChanged(object? sender, EventArgs e)
        {
            OnPropertyChanged(nameof(TotalFileCount));
            OnPropertyChanged(nameof(CanStartProcessing));
            ProcessCommand.NotifyCanExecuteChanged();
            UpdateStatus();
        }

        private void MediaPathItems_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            OnPropertyChanged(nameof(IsMediaListEmpty));

            if (e.NewItems != null)
            {
                foreach (InputListItem item in e.NewItems)
                {
                    item.PropertyChanged += MediaPathItem_PropertyChanged;
                }
            }

            if (e.OldItems != null)
            {
                foreach (InputListItem item in e.OldItems)
                {
                    item.PropertyChanged -= MediaPathItem_PropertyChanged;
                }
            }
        }

        private async void MediaPathItem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(InputListItem.IncludeSubdirectories)
                && sender is InputListItem item
                && item.IsFolder)
            {
                await ProcessFolderAsync(item);
            }
        }

        private void OnModelConfigurationChanged(object? sender, EventArgs e)
        {
            ResetProcessingState();
            OnPropertyChanged(nameof(CanStartProcessing));
            ProcessCommand.NotifyCanExecuteChanged();
        }

        #endregion Event Handlers

        #region Commands

        [RelayCommand]
        private async Task AddFolder()
        {
            FolderPicker folderPicker = new()
            {
                ViewMode = PickerViewMode.List,
                SuggestedStartLocation = PickerLocationId.ComputerFolder
            };

            // Initialize folder picker
            nint windowHandle = WindowNative.GetWindowHandle(App.Window);
            InitializeWithWindow.Initialize(folderPicker, windowHandle);

            // Show folder picker
            StorageFolder? folder = await folderPicker.PickSingleFolderAsync();

            // Folder is already in list
            if (folder != null && !InputListItems.Any(item => string.Equals(item.Path, folder.Path, StringComparison.OrdinalIgnoreCase)))
            {
                // Folder has subdirectories
                IReadOnlyList<StorageFolder> subfolders = await folder.GetFoldersAsync();
                bool hasSubdirectories = subfolders.Count > 0;

                // Create new input list item for folder
                InputListItem inputListItem = new()
                {
                    Path = folder.Path,
                    IsFolder = true,
                    HasSubdirectories = hasSubdirectories,
                    IncludeSubdirectories = hasSubdirectories
                };

                InputListItems.Add(inputListItem);

                await ProcessFolderAsync(inputListItem);
            }
        }

        [RelayCommand]
        private async Task AddFiles()
        {
            FileOpenPicker fileOpenPicker = new()
            {
                ViewMode = PickerViewMode.Thumbnail,
                SuggestedStartLocation = PickerLocationId.PicturesLibrary
            };

            foreach (string ext in SupportedFileExtensions.SupportedImageExtensions)
            {
                fileOpenPicker.FileTypeFilter.Add(ext);
            }

            // Initialize file picker
            nint windowHandle = WindowNative.GetWindowHandle(App.Window);
            InitializeWithWindow.Initialize(fileOpenPicker, windowHandle);

            IReadOnlyList<StorageFile> files = await fileOpenPicker.PickMultipleFilesAsync();

            if (files != null && files.Count > 0)
            {
                IsBusy = true;
                UpdateStatus();

                try
                {
                    foreach (StorageFile file in files)
                    {
                        if (IsImageFile(file.FileType) && !InputListItems.Any(item => string.Equals(item.Path, file.Path, StringComparison.OrdinalIgnoreCase)))
                        {
                            InputListItem inputListItem = new()
                            {
                                Path = file.Path,
                                IsFolder = false,
                                HasSubdirectories = false,
                                IncludeSubdirectories = false
                            };

                            InputListItems.Add(inputListItem);

                            // Add to unique file paths
                            await AddFileAsync(file.Path, file.Path);
                        }
                    }
                }
                finally
                {
                    IsBusy = false;
                    UpdateStatus();
                }
            }
        }

        [RelayCommand]
        private void RemoveItem(InputListItem item)
        {
            if (item != null)
            {
                InputListItems.Remove(item);
                RemoveSource(item.Path);
            }
        }

        [RelayCommand(CanExecute = nameof(CanOpenTempFolder))]
        private async Task OpenTempFolderAsync()
        {
            try
            {
                if (!string.IsNullOrEmpty(_settingsService.TempFolderPath) && Directory.Exists(_settingsService.TempFolderPath))
                {
                    await Launcher.LaunchFolderPathAsync(_settingsService.TempFolderPath);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error opening temp folder: {ex.Message}");
            }
        }

        /// <summary>
        /// Combined command that preprocesses images and extracts features in one operation.
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanStartProcessing))]
        private async Task ProcessAsync()
        {
            try
            {
                IsBusy = true;
                IsProcessing = true;
                ProcessingProgress = 0;

                // Reset previous state if re-processing
                HasProcessedItems = false;
                HasExtractedFeatures = false;
                ExtractedFeaturesCount = 0;

                // ===== STEP 1: PREPROCESSING =====
                IReadOnlyCollection<AnalysisItem> analysisItems = _appStateService.AnalysisItems;

                if (analysisItems.Count == 0)
                {
                    Status = "No items to process";
                    return;
                }

                Status = $"Preprocessing {analysisItems.Count} items...";

                await _imageProcessingService.ProcessItemsAsync(analysisItems);

                if (_appStateService.ProcessedItemCount == 0)
                {
                    Status = "Preprocessing failed: No items were processed.";
                    return;
                }

                HasProcessedItems = true;
                Status = $"Preprocessed {_appStateService.ProcessedItemCount} items. Extracting features...";

                // ===== STEP 2: FEATURE EXTRACTION =====

                // Initialize model if needed
                if (!_featureExtractionService.IsInitialized)
                {
                    Status = "Initializing model...";
                    await InitializeFeatureExtractionAsync();

                    if (!_featureExtractionService.IsInitialized)
                    {
                        Status = "Cannot extract features: Model failed to load.";
                        return;
                    }
                }

                // Get processed items for feature extraction
                IReadOnlyCollection<AnalysisItem> processedItems = _appStateService.ProcessedItems;

                if (processedItems.Count == 0)
                {
                    Status = "No processed items available for feature extraction.";
                    return;
                }

                Status = $"Extracting features from {processedItems.Count} items...";

                // Build normalization tuple
                (float MeanR, float MeanG, float MeanB, float StdR, float StdG, float StdB) normalization = (
                    MeanR: (float)_settingsService.MeanR,
                    MeanG: (float)_settingsService.MeanG,
                    MeanB: (float)_settingsService.MeanB,
                    StdR: (float)_settingsService.StdR,
                    StdG: (float)_settingsService.StdG,
                    StdB: (float)_settingsService.StdB
                );

                // Extract features
                await _featureExtractionService.ExtractFeaturesAsync(processedItems, normalization);

                // Notify state service
                _appStateService.NotifyFeaturesExtracted();

                // Update results
                ExtractedFeaturesCount = _appStateService.ExtractedFeaturesCount;

                if (ExtractedFeaturesCount > 0)
                {
                    HasExtractedFeatures = true;
                    Status = $"Ready! Extracted {ExtractedFeaturesCount} feature vectors. Click 'Find Duplicates' to continue.";

                    // Log feature info for debugging
                    AnalysisItem? firstItem = _appStateService.ItemsWithFeatures.FirstOrDefault();
                    if (firstItem != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"Feature vector size: {firstItem.FeatureCount}");
                        System.Diagnostics.Debug.WriteLine($"Feature dimensions: [{string.Join(", ", firstItem.FeatureDimensions ?? [])}]");
                    }
                }
                else
                {
                    Status = "Feature extraction failed: No features were extracted.";
                }
            }
            catch (Exception ex)
            {
                Status = $"Error during processing: {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"Processing error: {ex}");
            }
            finally
            {
                IsProcessing = false;
                IsBusy = false;
                ProcessingProgress = 0;
            }
        }

        #endregion Commands

        #region Methods

        private void UpdateStatus()
        {
            if (IsBusy)
            {
                // Don't override status while busy - let the operation set it
                return;
            }
            else if (TotalFileCount == 0)
            {
                Status = "No files added";
            }
            else if (HasExtractedFeatures)
            {
                Status = $"Ready! {ExtractedFeaturesCount} feature vectors extracted. Click 'Find Duplicates' to continue.";
            }
            else if (HasProcessedItems)
            {
                Status = $"{_appStateService.ProcessedItemCount} items preprocessed.";
            }
            else
            {
                Status = $"{TotalFileCount} file{(TotalFileCount == 1 ? "" : "s")} ready to process";
            }
        }

        /// <summary>
        /// Add file and load its SourceMedia asynchronously.
        /// </summary>
        public async Task AddFileAsync(string filePath, string sourcePath)
        {
            string normalizedPath = filePath.ToLowerInvariant();

            // Track source
            if (!_filePathToSourcesMap.TryGetValue(normalizedPath, out HashSet<string>? sources))
            {
                sources = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                _filePathToSourcesMap[normalizedPath] = sources;
            }
            sources.Add(sourcePath);

            // Load SourceMedia if not loaded
            if (!_loadedSourceMedia.ContainsKey(normalizedPath))
            {
                try
                {
                    SourceMedia? sourceMedia = await SourceMedia.CreateAsync(filePath, loadFullMetadata: true);
                    if (sourceMedia != null)
                    {
                        _loadedSourceMedia[normalizedPath] = sourceMedia;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to load source media for {filePath}: {ex.Message}");
                }
            }

            UpdateAppStateSourceMedia();
        }

        public static bool IsImageFile(string extension)
        {
            return SupportedFileExtensions.IsImageFile(extension);
        }

        public async Task ProcessFolderAsync(InputListItem folderItem)
        {
            if (!folderItem.IsFolder)
                return;

            IsBusy = true;
            Status = "Scanning folder...";

            try
            {
                // Remove existing files from folder source
                RemoveSource(folderItem.Path);

                StorageFolder folder = await StorageFolder.GetFolderFromPathAsync(folderItem.Path);
                if (folder != null)
                {
                    QueryOptions queryOptions = new(CommonFileQuery.DefaultQuery, SupportedFileExtensions.SupportedImageExtensions)
                    {
                        FolderDepth = folderItem.IncludeSubdirectories ? FolderDepth.Deep : FolderDepth.Shallow
                    };

                    StorageFileQueryResult query = folder.CreateFileQueryWithOptions(queryOptions);
                    IReadOnlyList<StorageFile> files = await query.GetFilesAsync();

                    foreach (StorageFile file in files)
                    {
                        await AddFileAsync(file.Path, folderItem.Path);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error processing folder {folderItem.Path}: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
                UpdateStatus();
            }
        }

        private void RemoveSource(string sourcePath)
        {
            List<string> pathsToRemove = [];

            foreach (KeyValuePair<string, HashSet<string>> entry in _filePathToSourcesMap.ToList())
            {
                entry.Value.Remove(sourcePath);

                if (entry.Value.Count == 0)
                {
                    _filePathToSourcesMap.Remove(entry.Key);
                    _loadedSourceMedia.Remove(entry.Key);
                    pathsToRemove.Add(entry.Key);
                }
            }

            if (pathsToRemove.Count > 0)
            {
                UpdateAppStateSourceMedia();
            }
        }

        /// <summary>
        /// Update AppStateService with current SourceMedia collection.
        /// </summary>
        private void UpdateAppStateSourceMedia()
        {
            List<SourceMedia> allSources = [];

            foreach (string path in _filePathToSourcesMap.Keys)
            {
                if (_loadedSourceMedia.TryGetValue(path, out SourceMedia? source))
                {
                    allSources.Add(source);
                }
            }

            _appStateService.SetSourceMedia(allSources);
        }

        private void ResetProcessingState()
        {
            if (HasExtractedFeatures || HasProcessedItems)
            {
                // Clear feature state in all items
                _appStateService.ClearFeatureState();
                _appStateService.ClearProcessedState();

                HasExtractedFeatures = false;
                HasProcessedItems = false;
                ExtractedFeaturesCount = 0;

                UpdateStatus();
            }
        }

        private async Task InitializeFeatureExtractionAsync()
        {
            try
            {
                string modelPath = ModelFilePath;

                if (!string.IsNullOrEmpty(modelPath) && File.Exists(modelPath))
                {
                    await _featureExtractionService.InitializeAsync(modelPath);
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

        #region Cleanup

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                PropertyChanged -= OnOwnPropertyChanged;
                _appStateService.SourceMediaChanged -= OnSourceMediaChanged;
                InputListItems.CollectionChanged -= MediaPathItems_CollectionChanged;
                _settingsService.ModelConfigurationChanged -= OnModelConfigurationChanged;

                foreach (InputListItem item in InputListItems)
                {
                    item.PropertyChanged -= MediaPathItem_PropertyChanged;
                }
            }
            base.Dispose(disposing);
        }

        #endregion Cleanup
    }
}