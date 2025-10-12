using DeDupe.Enums.Approach;
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

        private readonly List<string> _filePaths = [];
        private readonly List<ProcessedMedia> _processedImages = [];
        private readonly List<ExtractedFeatures> _extractedFeatures = [];

        private string _tempFolderPath = ApplicationData.Current.TemporaryFolder.Path + Path.DirectorySeparatorChar + "ProcessedImages";

        private ApproachType _selectedApproach = ApproachType.DeepLearning;
        private string _modelFilePath = string.Empty;
        private double _meanR = 0.485;
        private double _meanG = 0.456;
        private double _meanB = 0.406;
        private double _stdR = 0.229;
        private double _stdG = 0.224;
        private double _stdB = 0.225;

        #endregion Fields

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

        public ApproachType SelectedApproach
        {
            get => _selectedApproach;
            set
            {
                if (_selectedApproach != value)
                {
                    _selectedApproach = value;
                    OnPropertyChanged();
                    ApproachSettingsChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public string ModelFilePath
        {
            get => _modelFilePath;
            set
            {
                if (_modelFilePath != value)
                {
                    _modelFilePath = value;
                    OnPropertyChanged();
                    ApproachSettingsChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public double MeanR
        {
            get => _meanR;
            set
            {
                if (_meanR != value)
                {
                    _meanR = value;
                    OnPropertyChanged();
                    ApproachSettingsChanged?.Invoke(this, EventArgs.Empty);
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
                    ApproachSettingsChanged?.Invoke(this, EventArgs.Empty);
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
                    ApproachSettingsChanged?.Invoke(this, EventArgs.Empty);
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
                    ApproachSettingsChanged?.Invoke(this, EventArgs.Empty);
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
                    ApproachSettingsChanged?.Invoke(this, EventArgs.Empty);
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
                    ApproachSettingsChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

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

        public event EventHandler? ApproachSettingsChanged;

        public event EventHandler? ExtractedFeaturesChanged;

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion Events
    }
}