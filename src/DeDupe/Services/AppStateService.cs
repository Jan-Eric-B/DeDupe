using DeDupe.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Windows.Storage;

namespace DeDupe.Services
{
    /// <summary>
    /// Centralized state management service for the application.
    /// </summary>
    public partial class AppStateService(IBundledModelService bundledModelService) : IAppStateService
    {
        #region Fields

        private readonly IBundledModelService _bundledModelService = bundledModelService ?? throw new ArgumentNullException(nameof(bundledModelService));

        // Source media files (original images and videos)
        private readonly List<SourceMedia> _sourceMedia = [];

        // Analysis items
        private readonly List<AnalysisItem> _analysisItems = [];

        private string _tempFolderPath = ApplicationData.Current.TemporaryFolder.Path + Path.DirectorySeparatorChar + "ProcessedImages";

        // Model configuration
        private bool _useBundledModel = true;

        private string _customModelFilePath = string.Empty;

        // Normalization parameters
        private double _meanR = 0.485;

        private double _meanG = 0.456;
        private double _meanB = 0.406;
        private double _stdR = 0.229;
        private double _stdG = 0.224;
        private double _stdB = 0.225;

        #endregion Fields

        #region Source Media Properties

        /// <summary>
        /// All source media files.
        /// </summary>
        public IReadOnlyCollection<SourceMedia> SourceMedia => _sourceMedia.AsReadOnly();

        /// <summary>
        /// Number of source media files.
        /// </summary>
        public int SourceCount => _sourceMedia.Count;

        #endregion Source Media Properties

        #region Analysis Items Properties

        /// <summary>
        /// All analysis items (images and video frames).
        /// </summary>
        public IReadOnlyCollection<AnalysisItem> AnalysisItems => _analysisItems.AsReadOnly();

        /// <summary>
        /// Number of analysis items.
        /// </summary>
        public int AnalysisItemCount => _analysisItems.Count;

        /// <summary>
        /// Analysis items that have been preprocessed.
        /// </summary>
        public IReadOnlyCollection<AnalysisItem> ProcessedItems => _analysisItems.Where(i => i.IsProcessed).ToList().AsReadOnly();

        /// <summary>
        /// Number of preprocessed items.
        /// </summary>
        public int ProcessedItemCount => _analysisItems.Count(i => i.IsProcessed);

        /// <summary>
        /// Analysis items that have features extracted.
        /// </summary>
        public IReadOnlyCollection<AnalysisItem> ItemsWithFeatures => _analysisItems.Where(i => i.HasFeatures).ToList().AsReadOnly();

        /// <summary>
        /// Number of items with features.
        /// </summary>
        public int ExtractedFeaturesCount => _analysisItems.Count(i => i.HasFeatures);

        #endregion Analysis Items Properties

        #region Temp Folder Properties

        public string TempFolderPath
        {
            get => _tempFolderPath;
            set
            {
                if (_tempFolderPath != value)
                {
                    _tempFolderPath = value;
                    OnPropertyChanged();
                    TempFolderPathChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        #endregion Temp Folder Properties

        #region Model Configuration Properties

        /// <summary>
        /// Use bundled model.
        /// </summary>
        public bool UseBundledModel
        {
            get => _useBundledModel;
            set
            {
                if (_useBundledModel != value)
                {
                    _useBundledModel = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ModelPath));
                    OnPropertyChanged(nameof(ModelFilePath));
                    ModelSourceChanged?.Invoke(this, EventArgs.Empty);
                    ModelConfigurationSettingsChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        /// <summary>
        /// Custom model file path.
        /// </summary>
        public string CustomModelFilePath
        {
            get => _customModelFilePath;
            set
            {
                if (_customModelFilePath != value)
                {
                    _customModelFilePath = value;
                    OnPropertyChanged();
                    if (!UseBundledModel)
                    {
                        OnPropertyChanged(nameof(ModelPath));
                        OnPropertyChanged(nameof(ModelFilePath));
                        ModelConfigurationSettingsChanged?.Invoke(this, EventArgs.Empty);
                    }
                }
            }
        }

        /// <summary>
        /// Current model path based on selection.
        /// </summary>
        public string ModelPath => UseBundledModel ? _bundledModelService.BundledModelPath : CustomModelFilePath;

        /// <summary>
        /// Model file path (ModelPath alias).
        /// </summary>
        public string ModelFilePath
        {
            get => ModelPath;
            set
            {
                if (value != _bundledModelService.BundledModelPath)
                {
                    CustomModelFilePath = value;
                    if (!string.IsNullOrEmpty(value))
                    {
                        UseBundledModel = false;
                    }
                }
                else
                {
                    UseBundledModel = true;
                }
            }
        }

        #endregion Model Configuration Properties

        #region Normalization Properties

        public double MeanR
        {
            get => _meanR;
            set
            {
                if (_meanR != value)
                {
                    _meanR = value;
                    OnPropertyChanged();
                    ModelConfigurationSettingsChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public double MeanG
        {
            get => _meanG;
            set
            {
                if (_meanG != value)
                {
                    _meanG = value;
                    OnPropertyChanged();
                    ModelConfigurationSettingsChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public double MeanB
        {
            get => _meanB;
            set
            {
                if (_meanB != value)
                {
                    _meanB = value;
                    OnPropertyChanged();
                    ModelConfigurationSettingsChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public double StdR
        {
            get => _stdR;
            set
            {
                if (_stdR != value)
                {
                    _stdR = value;
                    OnPropertyChanged();
                    ModelConfigurationSettingsChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public double StdG
        {
            get => _stdG;
            set
            {
                if (_stdG != value)
                {
                    _stdG = value;
                    OnPropertyChanged();
                    ModelConfigurationSettingsChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public double StdB
        {
            get => _stdB;
            set
            {
                if (_stdB != value)
                {
                    _stdB = value;
                    OnPropertyChanged();
                    ModelConfigurationSettingsChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        #endregion Normalization Properties

        #region Source Media Methods

        /// <summary>
        /// Set source media from file paths.
        /// </summary>
        public void SetSourceMedia(IEnumerable<SourceMedia> sources)
        {
            _sourceMedia.Clear();
            _analysisItems.Clear();

            if (sources != null)
            {
                foreach (SourceMedia source in sources)
                {
                    _sourceMedia.Add(source);

                    // For images create one AnalysisItem per source
                    // For videos AnalysisItems will be created during frame extraction
                    if (source.IsImage)
                    {
                        _analysisItems.Add(new AnalysisItem(source));
                    }
                }
            }

            OnPropertyChanged(nameof(SourceMedia));
            OnPropertyChanged(nameof(SourceCount));
            OnPropertyChanged(nameof(AnalysisItems));
            OnPropertyChanged(nameof(AnalysisItemCount));
            SourceMediaChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Add source media.
        /// </summary>
        public void AddSourceMedia(SourceMedia source)
        {
            if (source == null) return;

            _sourceMedia.Add(source);

            if (source.IsImage)
            {
                _analysisItems.Add(new AnalysisItem(source));
            }

            OnPropertyChanged(nameof(SourceMedia));
            OnPropertyChanged(nameof(SourceCount));
            OnPropertyChanged(nameof(AnalysisItems));
            OnPropertyChanged(nameof(AnalysisItemCount));
            SourceMediaChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Add video frames as analysis items.
        /// </summary>
        public void AddVideoFrames(SourceMedia videoSource, IEnumerable<(int frameIndex, TimeSpan timestamp)> frames)
        {
            if (videoSource == null || !videoSource.IsVideo) return;

            foreach ((int frameIndex, TimeSpan timestamp) in frames)
            {
                _analysisItems.Add(new AnalysisItem(videoSource, frameIndex, timestamp));
            }

            OnPropertyChanged(nameof(AnalysisItems));
            OnPropertyChanged(nameof(AnalysisItemCount));
            AnalysisItemsChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Clear all source media and analysis items.
        /// </summary>
        public void ClearSourceMedia()
        {
            _sourceMedia.Clear();
            _analysisItems.Clear();

            OnPropertyChanged(nameof(SourceMedia));
            OnPropertyChanged(nameof(SourceCount));
            OnPropertyChanged(nameof(AnalysisItems));
            OnPropertyChanged(nameof(AnalysisItemCount));
            SourceMediaChanged?.Invoke(this, EventArgs.Empty);
        }

        #endregion Source Media Methods

        #region State Methods

        /// <summary>
        /// Reset preprocessing state for all items.
        /// </summary>
        public void ClearProcessedState()
        {
            foreach (AnalysisItem item in _analysisItems)
            {
                item.ProcessedFilePath = null;
            }

            OnPropertyChanged(nameof(ProcessedItems));
            OnPropertyChanged(nameof(ProcessedItemCount));
            ProcessingStateChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Reset feature extraction state for all items.
        /// </summary>
        public void ClearFeatureState()
        {
            foreach (AnalysisItem item in _analysisItems)
            {
                item.FeatureVector = null;
                item.FeatureDimensions = null;
            }

            OnPropertyChanged(nameof(ItemsWithFeatures));
            OnPropertyChanged(nameof(ExtractedFeaturesCount));
            ExtractedFeaturesChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Notify processing state has changed.
        /// </summary>
        public void NotifyProcessingComplete()
        {
            OnPropertyChanged(nameof(ProcessedItems));
            OnPropertyChanged(nameof(ProcessedItemCount));
            ProcessingStateChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Notify feature extraction has completed.
        /// </summary>
        public void NotifyFeaturesExtracted()
        {
            OnPropertyChanged(nameof(ItemsWithFeatures));
            OnPropertyChanged(nameof(ExtractedFeaturesCount));
            ExtractedFeaturesChanged?.Invoke(this, EventArgs.Empty);
        }

        #endregion State Methods

        #region Normalization Methods

        public (double, double, double, double, double, double) GetNormalization()
        {
            return (MeanR, MeanG, MeanB, StdR, StdG, StdB);
        }

        public void ResetNormalization()
        {
            MeanR = 0.485;
            MeanG = 0.456;
            MeanB = 0.406;
            StdR = 0.229;
            StdG = 0.224;
            StdB = 0.225;
        }

        #endregion Normalization Methods

        #region Events

        public event EventHandler? SourceMediaChanged;

        public event EventHandler? AnalysisItemsChanged;

        public event EventHandler? ProcessingStateChanged;

        public event EventHandler? TempFolderPathChanged;

        public event EventHandler? ModelConfigurationSettingsChanged;

        public event EventHandler? ModelSourceChanged;

        public event EventHandler? ExtractedFeaturesChanged;

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion Events
    }
}