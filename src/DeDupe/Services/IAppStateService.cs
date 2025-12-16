using DeDupe.Models;
using DeDupe.Models.Analysis;
using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace DeDupe.Services
{
    /// <summary>
    /// Interface for centralized state management service.
    /// </summary>
    public interface IAppStateService : INotifyPropertyChanged
    {
        #region File Management

        /// <summary>
        /// File paths
        /// </summary>
        IReadOnlyCollection<string> FilePaths { get; }

        /// <summary>
        /// Count of files
        /// </summary>
        int FileCount { get; }


        /// <summary>
        /// Update file paths
        /// </summary>
        /// <param name="filePaths">New collection of file paths</param>
        void SetFilePaths(IEnumerable<string> filePaths);

        #endregion File Management

        #region Processed Images

        /// <summary>
        /// Processed files
        /// </summary>
        IReadOnlyCollection<ProcessedMedia> ProcessedImages { get; }

        /// <summary>
        /// Count of processed images
        /// </summary>
        int ProcessedImageCount { get; }

        /// <summary>
        /// Path of the temporary folder for processed images
        /// </summary>
        string TempFolderPath { get; set; }

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

        #endregion Processed Images

        #region Extracted Features

        /// <summary>
        /// Extracted features from images
        /// </summary>
        IReadOnlyCollection<ExtractedFeatures> ExtractedFeatures { get; }

        /// <summary>
        /// Count of extracted features
        /// </summary>
        int ExtractedFeaturesCount { get; }

        /// <summary>
        /// Set extracted features
        /// </summary>
        /// <param name="features">Collection of extracted features</param>
        void SetExtractedFeatures(IEnumerable<ExtractedFeatures> features);

        /// <summary>
        /// Clear extracted features
        /// </summary>
        void ClearExtractedFeatures();

        #endregion Extracted Features

        #region Model Configuration

        /// <summary>
        /// Gets or sets whether the bundled model is being used.
        /// </summary>
        bool UseBundledModel { get; set; }

        /// <summary>
        /// Gets or sets the custom model file path (used when UseBundledModel is false).
        /// </summary>
        string CustomModelFilePath { get; set; }

        /// <summary>
        /// Gets the effective model path (bundled or custom based on UseBundledModel).
        /// </summary>
        string ModelPath { get; }

        /// <summary>
        /// Gets or sets the model file path (for backward compatibility).
        /// Setting this will switch to custom model mode if different from bundled.
        /// </summary>
        string ModelFilePath { get; set; }

        #endregion Model Configuration

        #region Normalization Parameters

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

        #endregion Normalization Parameters

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
        /// Model Configuration settings changed
        /// </summary>
        event EventHandler? ModelConfigurationSettingsChanged;

        /// <summary>
        /// Model source changed
        /// </summary>
        event EventHandler? ModelSourceChanged;

        /// <summary>
        /// Extracted features changed
        /// </summary>
        event EventHandler? ExtractedFeaturesChanged;

        #endregion Events
    }
}