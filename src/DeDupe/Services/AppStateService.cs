using DeDupe.Models;
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

        private string _tempFolderPath = ApplicationData.Current.TemporaryFolder.Path + Path.DirectorySeparatorChar + "ProcessedImages";

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

        #endregion Methods

        #region Events

        public event EventHandler? FilePathsChanged;

        public event EventHandler? ProcessedImagesChanged;

        public event EventHandler? TempFolderPathChanged;

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion Events
    }
}