using CommunityToolkit.Mvvm.Input;
using DeDupe.Constants;
using DeDupe.Helpers;
using DeDupe.Models;
using DeDupe.Models.Configuration;
using DeDupe.Models.Input;
using DeDupe.Models.Media;
using DeDupe.Models.Results;
using DeDupe.Services;
using DeDupe.Services.Analysis;
using DeDupe.Services.Processing;
using Microsoft.UI.Xaml;
using SixLabors.ImageSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;
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
        private readonly IModelDownloadService _modelDownloadService;
        private readonly ImageProcessingService _imageProcessingService;

        private int _processingProgress;
        private bool _hasProcessedItems;
        private bool _isProcessing;
        private bool _hasExtractedFeatures;
        private int _extractedFeaturesCount;

        private int _currentProcessedCount;
        private int _totalItemsToProcess;
        private double _progressPercentage;
        private string _currentOperation = string.Empty;

        private bool _includeVideoFiles;
        private ObservableCollection<InputListItem> _inputListItems = [];

        // Track which files came from which source (for removal)
        private readonly Dictionary<string, HashSet<string>> _filePathToSourcesMap = new(StringComparer.OrdinalIgnoreCase);

        // Loaded SourceMedia objects
        private readonly Dictionary<string, SourceMedia> _loadedSourceMedia = new(StringComparer.OrdinalIgnoreCase);

        private CancellationTokenSource? _scanCts;
        private CancellationTokenSource? _processingCts;

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

        private bool _isScanning;

        public bool IsScanning
        {
            get => _isScanning;
            set
            {
                if (SetProperty(ref _isScanning, value))
                {
                    OnPropertyChanged(nameof(IsCancellable));
                    CancelOperationCommand.NotifyCanExecuteChanged();
                }
            }
        }

        public bool IsCancellable => IsScanning || IsProcessing;

        public int ProcessingProgress
        {
            get => _processingProgress;
            set => SetProperty(ref _processingProgress, value);
        }

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

        public bool CanStartProcessing
        {
            get
            {
                if (IsBusy || _appStateService.SourceCount == 0)
                    return false;

                if (_settingsService.UseBundledModel)
                {
                    // Check model ID is valid
                    return BundledModelRegistry.Exists(_settingsService.SelectedBundledModelId);
                }

                return !string.IsNullOrEmpty(_settingsService.CustomModelFilePath) && File.Exists(_settingsService.CustomModelFilePath);
            }
        }

        public bool CanOpenTempFolder => HasProcessedItems && !string.IsNullOrEmpty(_settingsService.TempFolderPath);

        public string ModelFilePath => _settingsService.UseBundledModel ? _bundledModelService.GetModelPath(_settingsService.SelectedBundledModelId) : _settingsService.CustomModelFilePath;

        public bool IsProcessing
        {
            get => _isProcessing;
            set
            {
                if (SetProperty(ref _isProcessing, value))
                {
                    OnPropertyChanged(nameof(CanStartProcessing));
                    OnPropertyChanged(nameof(IsCancellable));
                    ProcessCommand.NotifyCanExecuteChanged();
                    CancelOperationCommand.NotifyCanExecuteChanged();
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

        public override bool CanNavigateToNext => HasExtractedFeatures;

        /// <summary>
        /// Current number of processed items in the active operation.
        /// </summary>
        public int CurrentProcessedCount
        {
            get => _currentProcessedCount;
            set
            {
                if (SetProperty(ref _currentProcessedCount, value))
                {
                    OnPropertyChanged(nameof(Status));
                }
            }
        }

        /// <summary>
        /// Total number of items to process in the active operation.
        /// </summary>
        public int TotalItemsToProcess
        {
            get => _totalItemsToProcess;
            set
            {
                if (SetProperty(ref _totalItemsToProcess, value))
                {
                    OnPropertyChanged(nameof(Status));
                }
            }
        }

        /// <summary>
        /// Progress percentage.
        /// </summary>
        public double ProgressPercentage
        {
            get => _progressPercentage;
            set => SetProperty(ref _progressPercentage, Math.Clamp(value, 0, 100));
        }

        /// <summary>
        /// Name of operation.
        /// </summary>
        public string CurrentOperation
        {
            get => _currentOperation;
            set => SetProperty(ref _currentOperation, value ?? string.Empty);
        }

        #endregion Properties

        #region Constructor

        public ConfigurationViewModel(IAppStateService appStateService, ISettingsService settingsService, IBundledModelService bundledModelService, IFeatureExtractionService featureExtractionService, IBorderDetectionService borderDetectionService, IModelDownloadService modelDownloadService)
        {
            _appStateService = appStateService ?? throw new ArgumentNullException(nameof(appStateService));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _bundledModelService = bundledModelService ?? throw new ArgumentNullException(nameof(bundledModelService));
            _featureExtractionService = featureExtractionService ?? throw new ArgumentNullException(nameof(featureExtractionService));
            _modelDownloadService = modelDownloadService ?? throw new ArgumentNullException(nameof(modelDownloadService));
            _imageProcessingService = new ImageProcessingService(_appStateService, _settingsService, borderDetectionService);

            Title = "File Input";
            Status = "No files added";
            InputListItems.CollectionChanged += MediaPathItems_CollectionChanged;
            IsBusy = false;

            _settingsService.ModelConfigurationChanged += OnModelConfigurationChanged;
            _appStateService.SourceMediaChanged += OnSourceMediaChanged;
            PropertyChanged += OnOwnPropertyChanged;
        }

        #endregion Constructor

        #region Event Handlers

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
        private async Task AddFolderAsync()
        {
            FolderPicker folderPicker = new()
            {
                ViewMode = PickerViewMode.List,
                SuggestedStartLocation = PickerLocationId.ComputerFolder
            };
            folderPicker.InitializeForCurrentWindow();

            // Show folder picker
            StorageFolder? folder = await folderPicker.PickSingleFolderAsync();

            // Folder is already in list
            if (folder != null && !InputListItems.Any(item => string.Equals(item.Path, folder.Path, StringComparison.OrdinalIgnoreCase)))
            {
                // Folder has subdirectories
                bool hasSubdirectories = false;
                try
                {
                    hasSubdirectories = Directory.EnumerateDirectories(folder.Path).Any();
                }
                catch
                {
                    // Ignore access errors
                }

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
        private async Task AddFilesAsync()
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
                Status = $"Adding {files.Count} files...";

                try
                {
                    List<SourceMedia> newSources = [];

                    foreach (StorageFile file in files)
                    {
                        if (SupportedFileExtensions.IsImageFile(file.FileType) && !InputListItems.Any(item => string.Equals(item.Path, file.Path, StringComparison.OrdinalIgnoreCase)))
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
                            SourceMedia source = SourceMedia.CreateLightweight(file.Path);
                            AddSourceToTracking(file.Path, file.Path, source);
                            newSources.Add(source);
                        }
                    }

                    // Batch update AppState
                    if (newSources.Count > 0)
                    {
                        UpdateAppStateSourceMedia();
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

        [RelayCommand(CanExecute = nameof(IsCancellable))]
        private void CancelOperation()
        {
            if (IsScanning)
            {
                _scanCts?.Cancel();
                Status = "Cancelling scan...";
            }
            else if (IsProcessing)
            {
                _processingCts?.Cancel();
                Status = "Cancelling processing...";
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
                Debug.WriteLine($"Error opening temp folder: {ex.Message}");
            }
        }

        [RelayCommand(CanExecute = nameof(CanStartProcessing))]
        private async Task ProcessAsync()
        {
            // Cancel any existing processing
            _processingCts?.Cancel();
            _processingCts = new CancellationTokenSource();
            CancellationToken ct = _processingCts.Token;

            try
            {
                IsBusy = true;
                IsProcessing = true;
                ProcessingProgress = 0;
                ProgressPercentage = 0;
                CurrentProcessedCount = 0;
                TotalItemsToProcess = 0;

                // Reset previous state if re-processing
                HasProcessedItems = false;
                HasExtractedFeatures = false;
                ExtractedFeaturesCount = 0;

                // Step 0 - Model Download
                if (_settingsService.UseBundledModel)
                {
                    string modelId = _settingsService.SelectedBundledModelId;
                    BundledModelInfo? modelInfo = _bundledModelService.GetModelInfo(modelId);

                    if (modelInfo != null && !_bundledModelService.IsModelAvailable(modelId))
                    {
                        CurrentOperation = "Downloading model";
                        Status = $"Downloading {modelInfo.DisplayName}...";

                        Progress<ModelDownloadProgress> downloadProgress = new(info =>
                        {
                            App.Window.DispatcherQueue.TryEnqueue(() =>
                            {
                                ProgressPercentage = info.Percentage * 100;
                                Status = info.StatusText;
                            });
                        });

                        await _modelDownloadService.EnsureModelAvailableAsync(modelInfo, downloadProgress, ct);

                        ct.ThrowIfCancellationRequested();

                        if (!_bundledModelService.IsModelAvailable(modelId))
                        {
                            Status = "Model download failed. Please try again.";
                            return;
                        }

                        // Reset progress for next step
                        ProgressPercentage = 0;
                    }
                }

                // Step 1 - Image Processing
                IReadOnlyCollection<AnalysisItem> analysisItems = _appStateService.AnalysisItems;

                if (analysisItems.Count == 0)
                {
                    Status = "No items to process";
                    return;
                }

                // Create progress handler for image processing
                Progress<ProgressInfo> imageProcessingProgress = new(info =>
                {
                    // Update on UI thread via dispatcher
                    App.Window.DispatcherQueue.TryEnqueue(() =>
                    {
                        CurrentOperation = info.OperationName;
                        CurrentProcessedCount = info.CurrentItem;
                        TotalItemsToProcess = info.TotalItems;
                        ProgressPercentage = info.Percentage;
                        Status = info.StatusText;
                    });
                });

                CurrentOperation = "Processing images";
                TotalItemsToProcess = analysisItems.Count;
                Status = $"Processing {analysisItems.Count} images...";

                await _imageProcessingService.ProcessItemsAsync(analysisItems, imageProcessingProgress, ct);

                ct.ThrowIfCancellationRequested();

                if (_appStateService.ProcessedItemCount == 0)
                {
                    Status = "Processing failed: No items were processed.";
                    return;
                }

                HasProcessedItems = true;

                // Step 2 - Feature Extraction

                // Initialize model if needed
                if (!_featureExtractionService.IsInitialized)
                {
                    CurrentOperation = "Initializing model";
                    Status = "Initializing AI model...";
                    ProgressPercentage = 0;

                    await InitializeFeatureExtractionAsync();

                    if (!_featureExtractionService.IsInitialized)
                    {
                        Status = "Cannot extract features: Model failed to load.";
                        return;
                    }
                }

                ct.ThrowIfCancellationRequested();

                // Get processed items for feature extraction
                IReadOnlyCollection<AnalysisItem> processedItems = _appStateService.ProcessedItems;

                if (processedItems.Count == 0)
                {
                    Status = "No processed items available for feature extraction.";
                    return;
                }

                // Create progress handler for feature extraction
                Progress<ProgressInfo> featureExtractionProgress = new(info =>
                {
                    App.Window.DispatcherQueue.TryEnqueue(() =>
                    {
                        CurrentOperation = info.OperationName;
                        CurrentProcessedCount = info.CurrentItem;
                        TotalItemsToProcess = info.TotalItems;
                        ProgressPercentage = info.Percentage;
                        Status = info.StatusText;
                    });
                });

                CurrentOperation = "Extracting features";
                TotalItemsToProcess = processedItems.Count;
                CurrentProcessedCount = 0;
                ProgressPercentage = 0;
                Status = $"Extracting features from {processedItems.Count} items...";

                // Extract features
                await _featureExtractionService.ExtractFeaturesAsync(processedItems, _settingsService.Normalization, featureExtractionProgress, ct);
                _appStateService.NotifyFeaturesExtracted();

                // Release ImageSharp's pooled memory.
                Configuration.Default.MemoryAllocator.ReleaseRetainedResources();

                // Update results
                ExtractedFeaturesCount = _appStateService.ExtractedFeaturesCount;

                if (ExtractedFeaturesCount > 0)
                {
                    HasExtractedFeatures = true;
                    ProgressPercentage = 100;
                    Status = $"Ready! Extracted {ExtractedFeaturesCount} feature vectors. Click 'Find Duplicates' to continue.";
                }
                else
                {
                    Status = "Feature extraction failed: No features were extracted.";
                }
            }
            catch (OperationCanceledException)
            {
                Status = "Processing cancelled";
            }
            catch (Exception ex)
            {
                Status = $"Error during processing: {ex.Message}";
                Debug.WriteLine($"Processing error: {ex}");
            }
            finally
            {
                IsProcessing = false;
                IsBusy = false;
                ProcessingProgress = 0;

                // Reset progress display after a short delay to show completion
                await Task.Delay(2000);
                if (!IsProcessing)
                {
                    CurrentProcessedCount = 0;
                    TotalItemsToProcess = 0;
                    ProgressPercentage = 0;
                    CurrentOperation = string.Empty;
                }
            }
        }

        #endregion Commands

        #region Methods

        private void UpdateStatus()
        {
            if (IsBusy || IsScanning)
            {
                return;
            }

            if (TotalFileCount == 0)
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

        public async Task ProcessFolderAsync(InputListItem folderItem)
        {
            if (!folderItem.IsFolder)
            {
                return;
            }

            // Cancel existing scan
            CancellationTokenSource? oldCts = Interlocked.Exchange(ref _scanCts, new CancellationTokenSource());
            oldCts?.Cancel();
            oldCts?.Dispose();
            CancellationToken ct = _scanCts.Token;

            IsBusy = true;
            IsScanning = true;
            Status = "Scanning folder...";

            try
            {
                // Remove existing files from this folder source
                RemoveSource(folderItem.Path);

                SearchOption searchOption = folderItem.IncludeSubdirectories ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

                int count = 0;
                int batchSize = 100;

                await Task.Run(() =>
                {
                    IEnumerable<string> files;
                    try
                    {
                        files = Directory.EnumerateFiles(folderItem.Path, "*.*", searchOption)
                            .Where(f => SupportedFileExtensions.IsImageFile(Path.GetExtension(f)));
                    }
                    catch (UnauthorizedAccessException)
                    {
                        files = Directory.EnumerateFiles(folderItem.Path, "*.*", SearchOption.TopDirectoryOnly)
                            .Where(f => SupportedFileExtensions.IsImageFile(Path.GetExtension(f)));
                    }

                    foreach (string filePath in files)
                    {
                        ct.ThrowIfCancellationRequested();

                        string normalizedPath = filePath.ToLowerInvariant();

                        if (_loadedSourceMedia.ContainsKey(normalizedPath))
                        {
                            if (!_filePathToSourcesMap.TryGetValue(normalizedPath, out HashSet<string>? sources))
                            {
                                sources = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                                _filePathToSourcesMap[normalizedPath] = sources;
                            }
                            sources.Add(folderItem.Path);
                            continue;
                        }

                        try
                        {
                            SourceMedia source = SourceMedia.CreateLightweight(filePath);
                            AddSourceToTrackingInternal(normalizedPath, folderItem.Path, source);
                            count++;
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Failed to create source for {filePath}: {ex.Message}");
                            continue;
                        }

                        if (count % batchSize == 0)
                        {
                            App.Window.DispatcherQueue.TryEnqueue(() =>
                            {
                                Status = $"Found {count:N0} images...";
                            });
                        }
                    }
                }, ct);

                // Scan completed successfully
                UpdateAppStateSourceMedia();
                Status = $"Found {count:N0} images";
            }
            catch (OperationCanceledException)
            {
                RemoveSource(folderItem.Path);
                InputListItems.Remove(folderItem);

                UpdateAppStateSourceMedia();

                Status = "Scan cancelled";
            }
            catch (Exception ex)
            {
                Status = $"Error scanning folder: {ex.Message}";
                Debug.WriteLine($"Error processing folder {folderItem.Path}: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
                IsScanning = false;
                UpdateStatus();
            }
        }

        public void AddFile(string filePath, string sourcePath)
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
                    SourceMedia source = SourceMedia.CreateLightweight(filePath);
                    _loadedSourceMedia[normalizedPath] = source;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to load source media for {filePath}: {ex.Message}");
                }
            }

            UpdateAppStateSourceMedia();
        }

        private void AddSourceToTracking(string filePath, string sourcePath, SourceMedia source)
        {
            string normalizedPath = filePath.ToLowerInvariant();
            AddSourceToTrackingInternal(normalizedPath, sourcePath, source);
        }

        private void AddSourceToTrackingInternal(string normalizedPath, string sourcePath, SourceMedia source)
        {
            if (!_filePathToSourcesMap.TryGetValue(normalizedPath, out HashSet<string>? sources))
            {
                sources = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                _filePathToSourcesMap[normalizedPath] = sources;
            }
            sources.Add(sourcePath);
            _loadedSourceMedia[normalizedPath] = source;
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
                    // Use settings for GPU and batch size
                    bool useGpu = _settingsService.EnableGpuAcceleration;
                    int batchSize = _settingsService.InferenceBatchSize;

                    await _featureExtractionService.InitializeAsync(modelPath, useGpu, batchSize);

                    // Optionally update status to show GPU state
                    if (_featureExtractionService.IsGpuEnabled)
                    {
                        Debug.WriteLine("Feature extraction initialized with GPU acceleration");
                    }
                    else
                    {
                        Debug.WriteLine("Feature extraction initialized with CPU (GPU not available or disabled)");
                    }
                }
                else
                {
                    Status = "No valid model file found.";
                }
            }
            catch (Exception ex)
            {
                Status = $"Error initializing model: {ex.Message}";
                Debug.WriteLine($"Model initialization error: {ex}");
            }
        }

        #endregion Methods

        #region Cleanup

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _scanCts?.Cancel();
                _scanCts?.Dispose();

                _processingCts?.Cancel();
                _processingCts?.Dispose();

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