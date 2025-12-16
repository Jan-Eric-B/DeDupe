using DeDupe.Models;
using DeDupe.Models.Analysis;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using Windows.Storage;

namespace DeDupe.Services
{
    /// <summary>
    /// Centralized state management service for the application.
    /// </summary>
    public partial class AppStateService : IAppStateService
    {
        #region Fields

        private readonly IBundledModelService _bundledModelService;

        private readonly List<string> _filePaths = [];
        private readonly List<ProcessedMedia> _processedImages = [];
        private readonly List<ExtractedFeatures> _extractedFeatures = [];

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

        #region Constructor

        public AppStateService(IBundledModelService bundledModelService)
        {
            _bundledModelService = bundledModelService ?? throw new ArgumentNullException(nameof(bundledModelService));
        }

        #endregion Constructor

        #region Properties

        public IReadOnlyCollection<string> FilePaths => _filePaths.AsReadOnly();

        public int FileCount => _filePaths.Count;

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

        public IReadOnlyCollection<ProcessedMedia> ProcessedImages => _processedImages.AsReadOnly();

        public int ProcessedImageCount => _processedImages.Count;

        public IReadOnlyCollection<ExtractedFeatures> ExtractedFeatures => _extractedFeatures.AsReadOnly();

        public int ExtractedFeaturesCount => _extractedFeatures.Count;

        #region Model Configuration Properties

        /// <summary>
        /// Gets or sets whether to use bundled model.
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
        /// Gets or sets custom model file path.
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
        /// Get model path based on current selection.
        /// </summary>
        public string ModelPath => UseBundledModel ? _bundledModelService.BundledModelPath : CustomModelFilePath;

        /// <summary>
        /// Gets or sets custom model file path.
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

        #endregion Properties

        #region Methods

        public void SetFilePaths(IEnumerable<string> filePaths)
        {
            _filePaths.Clear();
            _filePaths.AddRange(filePaths ?? []);

            OnPropertyChanged(nameof(FilePaths));
            OnPropertyChanged(nameof(FileCount));
            FilePathsChanged?.Invoke(this, EventArgs.Empty);
        }

        public void SetProcessedImages(IEnumerable<ProcessedMedia> processedImages)
        {
            _processedImages.Clear();
            _processedImages.AddRange(processedImages ?? []);

            OnPropertyChanged(nameof(ProcessedImages));
            OnPropertyChanged(nameof(ProcessedImageCount));
            ProcessedImagesChanged?.Invoke(this, EventArgs.Empty);
        }

        public void AddProcessedImage(ProcessedMedia processedImage)
        {
            if (processedImage != null)
            {
                _processedImages.Add(processedImage);
                OnPropertyChanged(nameof(ProcessedImages));
                OnPropertyChanged(nameof(ProcessedImageCount));
                ProcessedImagesChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public void ClearProcessedImages()
        {
            _processedImages.Clear();
            OnPropertyChanged(nameof(ProcessedImages));
            OnPropertyChanged(nameof(ProcessedImageCount));
            ProcessedImagesChanged?.Invoke(this, EventArgs.Empty);
        }

        public void SetExtractedFeatures(IEnumerable<ExtractedFeatures> features)
        {
            _extractedFeatures.Clear();
            _extractedFeatures.AddRange(features ?? []);

            OnPropertyChanged(nameof(ExtractedFeatures));
            OnPropertyChanged(nameof(ExtractedFeaturesCount));
            ExtractedFeaturesChanged?.Invoke(this, EventArgs.Empty);
        }

        public void ClearExtractedFeatures()
        {
            _extractedFeatures.Clear();
            OnPropertyChanged(nameof(ExtractedFeatures));
            OnPropertyChanged(nameof(ExtractedFeaturesCount));
            ExtractedFeaturesChanged?.Invoke(this, EventArgs.Empty);
        }

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

        #endregion Methods

        #region Events

        public event EventHandler? FilePathsChanged;

        public event EventHandler? ProcessedImagesChanged;

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