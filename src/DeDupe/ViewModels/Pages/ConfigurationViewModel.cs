using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeDupe.Constants;
using DeDupe.Enums;
using DeDupe.Helpers;
using DeDupe.Models;
using DeDupe.Models.Input;
using DeDupe.Models.Media;
using DeDupe.Models.Results;
using DeDupe.Services;
using DeDupe.Services.Analysis;
using DeDupe.Services.Model;
using DeDupe.Services.Processing;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using SixLabors.ImageSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace DeDupe.ViewModels.Pages
{
    public partial class ConfigurationViewModel : PageViewModelBase
    {
        private readonly IAppStateService _appStateService;
        private readonly ISettingsService _settingsService;
        private readonly IBundledModelService _bundledModelService;
        private readonly IFeatureExtractionService _featureExtractionService;
        private readonly ImageProcessingService _imageProcessingService;
        private readonly ILogger<ConfigurationViewModel> _logger;

        [ObservableProperty]
        public partial bool IncludeVideoFiles { get; set; }

        public ConfigurationViewModel(IAppStateService appStateService, ISettingsService settingsService, IBundledModelService bundledModelService, IFeatureExtractionService featureExtractionService, ImageProcessingService imageProcessingService, IBorderDetectionService borderDetectionService, ILogger<ConfigurationViewModel> logger, MainWindowViewModel mainWindowViewModel) : base(() => mainWindowViewModel.StartManagementModeCommand.Execute(null))
        {
            _appStateService = appStateService ?? throw new ArgumentNullException(nameof(appStateService));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _bundledModelService = bundledModelService ?? throw new ArgumentNullException(nameof(bundledModelService));
            _featureExtractionService = featureExtractionService ?? throw new ArgumentNullException(nameof(featureExtractionService));
            _imageProcessingService = imageProcessingService ?? throw new ArgumentNullException(nameof(imageProcessingService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            Title = "File Input";
            Status = "No files added";
            InputListItems.CollectionChanged += MediaPathItems_CollectionChanged;
            IsBusy = false;

            _settingsService.ModelConfigurationChanged += OnModelConfigurationChanged;
            _appStateService.SourceMediaChanged += OnSourceMediaChanged;
            PropertyChanged += OnOwnPropertyChanged;
        }

        #region Item Input

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsMediaListEmpty))]
        public partial ObservableCollection<InputListItem> InputListItems { get; set; } = [];

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsCancellable))]
        [NotifyCanExecuteChangedFor(nameof(CancelOperationCommand))]
        public partial bool IsScanning { get; set; }

        private CancellationTokenSource? _scanCts;

        private readonly Dictionary<string, HashSet<string>> _filePathToSourcesMap = new(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<string, SourceMedia> _loadedSourceMedia = new(StringComparer.OrdinalIgnoreCase);

        public int TotalFileCount => _appStateService.SourceMediaCount;

        public Visibility IsMediaListEmpty => InputListItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

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
                        LogFilesAdded(newSources.Count);
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

        public async Task ProcessFolderAsync(InputListItem folderItem)
        {
            if (!folderItem.IsFolder)
            {
                return;
            }

            // Cancel existing scan
            CancellationTokenSource? oldCts = Interlocked.Exchange(ref _scanCts, new CancellationTokenSource());
            if (oldCts is not null) await oldCts.CancelAsync();
            oldCts?.Dispose();
            CancellationToken ct = _scanCts.Token;

            IsBusy = true;
            IsScanning = true;
            Status = "Scanning folder...";
            LogFolderScanStarting(folderItem.Path);

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
                        LogSubdirectoryAccessDenied(folderItem.Path);
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
                            LogSourceMediaCreationSkipped(ex, filePath);
                            continue;
                        }

                        if (count % batchSize == 0)
                        {
                            DispatchToUI(() =>
                            {
                                Status = $"Found {count:N0} images...";
                            });
                        }
                    }
                }, ct);

                // Scan completed successfully
                UpdateAppStateSourceMedia();
                LogFolderScanCompleted(count, folderItem.Path);
                Status = $"Found {count:N0} images";
            }
            catch (OperationCanceledException)
            {
                LogFolderScanCancelled(folderItem.Path);
                RemoveSource(folderItem.Path);
                InputListItems.Remove(folderItem);

                UpdateAppStateSourceMedia();

                Status = "Scan cancelled";
            }
            catch (Exception ex)
            {
                Status = $"Error scanning folder: {ex.Message}";
                LogFolderScanAborted(ex, folderItem.Path);
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
                    LogSourceMediaLoadSkipped(ex, filePath);
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

        #endregion Item Input

        #region Processing

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(Status))]
        public partial int TotalItemsToProcess { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanStartProcessing), nameof(IsCancellable))]
        [NotifyCanExecuteChangedFor(nameof(ProcessCommand), nameof(CancelOperationCommand))]
        public partial bool IsProcessing { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(Status))]
        public partial int CurrentProcessedCount { get; set; }

        [ObservableProperty]
        public partial int ProcessingProgress { get; set; }

        [ObservableProperty]
        public partial bool HasProcessedItems { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(Status), nameof(CanNavigateToNext))]
        [NotifyCanExecuteChangedFor(nameof(NavigateToNextCommand))]
        public partial bool HasExtractedFeatures { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(Status))]
        public partial int ExtractedFeaturesCount { get; set; }

        private double _progressPercentage;
        private string _currentOperation = string.Empty;

        private CancellationTokenSource? _processingCts;

        public override bool CanNavigateToNext => HasExtractedFeatures;

        public bool IsCancellable => IsScanning || IsProcessing;

        public string ModelFilePath => _settingsService.UseBundledModel ? _bundledModelService.GetModelPath() : _settingsService.CustomModelFilePath;

        public bool CanStartProcessing
        {
            get
            {
                if (IsBusy || _appStateService.SourceMediaCount == 0)
                    return false;

                if (_settingsService.UseBundledModel)
                {
                    return _bundledModelService.IsModelAvailable();
                }

                return !string.IsNullOrEmpty(_settingsService.CustomModelFilePath) && File.Exists(_settingsService.CustomModelFilePath);
            }
        }

        public double ProgressPercentage
        {
            get => _progressPercentage;
            set => SetProperty(ref _progressPercentage, Math.Clamp(value, 0, 100));
        }

        public string CurrentOperation
        {
            get => _currentOperation;
            set => SetProperty(ref _currentOperation, value ?? string.Empty);
        }

        [RelayCommand(CanExecute = nameof(CanStartProcessing))]
        private async Task ProcessAsync()
        {
            // Cancel any existing processing
            if (_processingCts is not null)
            {
                await _processingCts.CancelAsync();
            }

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
                    // Update on UI thread
                    DispatchToUI(() =>
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
                LogProcessingPipelineStarting(analysisItems.Count);

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
                    DispatchToUI(() =>
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
                    LogProcessingPipelineCompleted(_appStateService.ProcessedItemCount, ExtractedFeaturesCount);
                }
                else
                {
                    Status = "Feature extraction failed: No features were extracted.";
                }
            }
            catch (OperationCanceledException)
            {
                LogProcessingPipelineCancelled();
                Status = "Processing cancelled";
            }
            catch (Exception ex)
            {
                Status = $"Error during processing: {ex.Message}";
                LogProcessingPipelineAborted(ex);
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

        private void OnOwnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(IsBusy))
            {
                OnPropertyChanged(nameof(CanStartProcessing));
                ProcessCommand.NotifyCanExecuteChanged();
            }
        }

        private void OnModelConfigurationChanged(object? sender, EventArgs e)
        {
            ResetProcessingState();
            OnPropertyChanged(nameof(CanStartProcessing));
            ProcessCommand.NotifyCanExecuteChanged();
        }

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
                    bool useGpu = _settingsService.EnableGpuAcceleration;
                    int batchSize = _settingsService.InferenceBatchSize;
                    TensorLayout tensorLayout = _settingsService.TensorLayout;
                    LogFeatureExtractionModelInitializing(modelPath);
                    await _featureExtractionService.InitializeAsync(modelPath, useGpu, batchSize, tensorLayout);
                    LogFeatureExtractionModelInitialized(_featureExtractionService.IsGpuEnabled);
                }
                else
                {
                    Status = "No valid model file found.";
                }
            }
            catch (Exception ex)
            {
                Status = $"Error initializing model: {ex.Message}";
                LogFeatureExtractionModelFailed(ex);
            }
        }

        #endregion Processing

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

        #region Logging

        // Scanning

        [LoggerMessage(Level = LogLevel.Information, Message = "Folder scan starting for {FolderPath}")]
        private partial void LogFolderScanStarting(string folderPath);

        [LoggerMessage(Level = LogLevel.Information, Message = "Folder scan completed, found {FileCount} images in {FolderPath}")]
        private partial void LogFolderScanCompleted(int fileCount, string folderPath);

        [LoggerMessage(Level = LogLevel.Information, Message = "Folder scan cancelled for {FolderPath}")]
        private partial void LogFolderScanCancelled(string folderPath);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Source media creation skipped for {FilePath}")]
        private partial void LogSourceMediaCreationSkipped(Exception ex, string filePath);

        [LoggerMessage(Level = LogLevel.Error, Message = "Folder scan aborted for {FolderPath}")]
        private partial void LogFolderScanAborted(Exception ex, string folderPath);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Source media load skipped for {FilePath}")]
        private partial void LogSourceMediaLoadSkipped(Exception ex, string filePath);

        [LoggerMessage(Level = LogLevel.Information, Message = "Individual files added, {FileCount} new items")]
        private partial void LogFilesAdded(int fileCount);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Subdirectory access denied for {FolderPath}, falling back to top-level scan")]
        private partial void LogSubdirectoryAccessDenied(string folderPath);

        // Processing

        [LoggerMessage(Level = LogLevel.Information, Message = "Processing pipeline starting for {ItemCount} items")]
        private partial void LogProcessingPipelineStarting(int itemCount);

        [LoggerMessage(Level = LogLevel.Information, Message = "Processing pipeline completed, {ProcessedCount} items preprocessed, {ExtractedCount} features extracted")]
        private partial void LogProcessingPipelineCompleted(int processedCount, int extractedCount);

        [LoggerMessage(Level = LogLevel.Information, Message = "Processing pipeline cancelled by user")]
        private partial void LogProcessingPipelineCancelled();

        [LoggerMessage(Level = LogLevel.Error, Message = "Processing pipeline aborted")]
        private partial void LogProcessingPipelineAborted(Exception ex);

        [LoggerMessage(Level = LogLevel.Information, Message = "Feature extraction model initializing from {ModelPath}")]
        private partial void LogFeatureExtractionModelInitializing(string modelPath);

        [LoggerMessage(Level = LogLevel.Information, Message = "Feature extraction model initialized (GPU: {GpuEnabled})")]
        private partial void LogFeatureExtractionModelInitialized(bool gpuEnabled);

        [LoggerMessage(Level = LogLevel.Error, Message = "Feature extraction model initialization failed")]
        private partial void LogFeatureExtractionModelFailed(Exception ex);

        #endregion Logging
    }
}