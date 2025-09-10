using DeDupe.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace DeDupe.Services
{
    /// <summary>
    /// Centralized state management service for the application.
    /// </summary>
    public interface IAppStateService : INotifyPropertyChanged
    {
        #region FileInput Data

        /// <summary>
        /// File paths
        /// </summary>
        IReadOnlyCollection<string> FilePaths { get; }

        /// <summary>
        /// Update file paths
        /// </summary>
        /// <param name="filePaths">New collection of file paths</param>
        void SetFilePaths(IEnumerable<string> filePaths);

        /// <summary>
        /// Count of files
        /// </summary>
        int FileCount { get; }

        #endregion FileInput Data

        #region PreProcessing Data

        string TempFolderPath { get; set; }

        /// <summary>
        /// Processed files
        /// </summary>
        IReadOnlyCollection<ProcessedMedia> ProcessedImages { get; }

        /// <summary>
        /// Update processed files
        /// </summary>
        /// <param name="processedImages">New collection of processed images</param>
        void SetProcessedImages(IEnumerable<ProcessedMedia> processedImages);

        /// <summary>
        /// Add processed file
        /// </summary>
        /// <param name="processedImage">Adds processed image</param>
        void AddProcessedImage(ProcessedMedia processedImage);

        /// <summary>
        /// Clear processed images
        /// </summary>
        void ClearProcessedImages();

        /// <summary>
        /// Count of processed images
        /// </summary>
        int ProcessedImageCount { get; }

        #endregion PreProcessing Data

        #region Events

        /// <summary>
        /// File paths collection changes
        /// </summary>
        event EventHandler? FilePathsChanged;

        /// <summary>
        /// Processed images collection changes
        /// </summary>
        event EventHandler? ProcessedImagesChanged;

        /// <summary>
        /// Temp folder changes
        /// </summary>
        event EventHandler? TempFolderPathChanged;

        #endregion Events
    }
}