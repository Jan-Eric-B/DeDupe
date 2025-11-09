using DeDupe.Enums.Approach;
using DeDupe.Models;
using DeDupe.Models.Analysis;
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

        #region Approach Data

        /// <summary>
        /// Selected approach type
        /// </summary>
        ApproachType SelectedApproach { get; set; }

        /// <summary>
        /// Model file path for deep learning approach
        /// </summary>
        string ModelFilePath { get; set; }

        /// <summary>
        /// Mean R value for normalization
        /// </summary>
        double MeanR { get; set; }

        /// <summary>
        /// Mean G value for normalization
        /// </summary>
        double MeanG { get; set; }

        /// <summary>
        /// Mean B value for normalization
        /// </summary>
        double MeanB { get; set; }

        /// <summary>
        /// Standard deviation R value for normalization
        /// </summary>
        double StdR { get; set; }

        /// <summary>
        /// Standard deviation G value for normalization
        /// </summary>
        double StdG { get; set; }

        /// <summary>
        /// Standard deviation B value for normalization
        /// </summary>
        double StdB { get; set; }

        /// <summary>
        /// Get normalization values as a tuple
        /// </summary>
        (double, double, double, double, double, double) GetNormalization();

        /// <summary>
        /// Reset normalization values to ImageNet defaults
        /// </summary>
        void ResetNormalization();

        #endregion Approach Data

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

        #region Feature Extraction Data

        /// <summary>
        /// Extracted features from images
        /// </summary>
        IReadOnlyCollection<ExtractedFeatures> ExtractedFeatures { get; }

        /// <summary>
        /// Set extracted features
        /// </summary>
        /// <param name="features">Collection of extracted features</param>
        void SetExtractedFeatures(IEnumerable<ExtractedFeatures> features);

        /// <summary>
        /// Clear extracted features
        /// </summary>
        void ClearExtractedFeatures();

        /// <summary>
        /// Count of extracted features
        /// </summary>
        int ExtractedFeaturesCount { get; }

        #endregion Feature Extraction Data

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

        /// <summary>
        /// Approach settings changed
        /// </summary>
        event EventHandler? ApproachSettingsChanged;

        /// <summary>
        /// Extracted features changed
        /// </summary>
        event EventHandler? ExtractedFeaturesChanged;

        #endregion Events
    }
}