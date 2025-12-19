using DeDupe.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace DeDupe.Services
{
    /// <summary>
    /// Interface for centralized application state management.
    /// </summary>
    public interface IAppStateService : INotifyPropertyChanged
    {
        #region Source Media

        /// <summary>
        /// All source media files (images and videos).
        /// </summary>
        IReadOnlyCollection<SourceMedia> SourceMedia { get; }

        /// <summary>
        /// Number of source media files.
        /// </summary>
        int SourceCount { get; }

        /// <summary>
        /// Set source media collection.
        /// </summary>
        void SetSourceMedia(IEnumerable<SourceMedia> sources);

        /// <summary>
        /// Add a single source media.
        /// </summary>
        void AddSourceMedia(SourceMedia source);

        /// <summary>
        /// Add video frames as analysis items.
        /// </summary>
        void AddVideoFrames(SourceMedia videoSource, IEnumerable<(int frameIndex, TimeSpan timestamp)> frames);

        /// <summary>
        /// Clear all source media.
        /// </summary>
        void ClearSourceMedia();

        #endregion Source Media

        #region Analysis Items

        /// <summary>
        /// All analysis items (images and video frames).
        /// </summary>
        IReadOnlyCollection<AnalysisItem> AnalysisItems { get; }

        /// <summary>
        /// Number of analysis items.
        /// </summary>
        int AnalysisItemCount { get; }

        /// <summary>
        /// Items that have been preprocessed.
        /// </summary>
        IReadOnlyCollection<AnalysisItem> ProcessedItems { get; }

        /// <summary>
        /// Number of preprocessed items.
        /// </summary>
        int ProcessedItemCount { get; }

        /// <summary>
        /// Items that have features extracted.
        /// </summary>
        IReadOnlyCollection<AnalysisItem> ItemsWithFeatures { get; }

        /// <summary>
        /// Number of items with features.
        /// </summary>
        int ExtractedFeaturesCount { get; }

        #endregion Analysis Items

        #region Pipeline State

        /// <summary>
        /// Clear preprocessing state for all items.
        /// </summary>
        void ClearProcessedState();

        /// <summary>
        /// Clear feature extraction state for all items.
        /// </summary>
        void ClearFeatureState();

        /// <summary>
        /// Notify that processing has completed.
        /// </summary>
        void NotifyProcessingComplete();

        /// <summary>
        /// Notify that feature extraction has completed.
        /// </summary>
        void NotifyFeaturesExtracted();

        #endregion Pipeline State

        #region Temp Folder

        /// <summary>
        /// Path to temporary folder for processed images.
        /// </summary>
        string TempFolderPath { get; set; }

        #endregion Temp Folder

        #region Model Configuration

        /// <summary>
        /// Whether to use bundled model.
        /// </summary>
        bool UseBundledModel { get; set; }

        /// <summary>
        /// Custom model file path.
        /// </summary>
        string CustomModelFilePath { get; set; }

        /// <summary>
        /// Current model path.
        /// </summary>
        string ModelPath { get; }

        /// <summary>
        /// Model file path with setter logic.
        /// </summary>
        string ModelFilePath { get; set; }

        #endregion Model Configuration

        #region Normalization

        double MeanR { get; set; }
        double MeanG { get; set; }
        double MeanB { get; set; }
        double StdR { get; set; }
        double StdG { get; set; }
        double StdB { get; set; }

        /// <summary>
        /// Get all normalization parameters.
        /// </summary>
        (double, double, double, double, double, double) GetNormalization();

        /// <summary>
        /// Reset normalization to ImageNet defaults.
        /// </summary>
        void ResetNormalization();

        #endregion Normalization

        #region Events

        /// <summary>
        /// Raised when source media collection changes.
        /// </summary>
        event EventHandler? SourceMediaChanged;

        /// <summary>
        /// Raised when analysis items collection changes.
        /// </summary>
        event EventHandler? AnalysisItemsChanged;

        /// <summary>
        /// Raised when processing state changes.
        /// </summary>
        event EventHandler? ProcessingStateChanged;

        /// <summary>
        /// Raised when temp folder path changes.
        /// </summary>
        event EventHandler? TempFolderPathChanged;

        /// <summary>
        /// Raised when model configuration changes.
        /// </summary>
        event EventHandler? ModelConfigurationSettingsChanged;

        /// <summary>
        /// Raised when model source changes.
        /// </summary>
        event EventHandler? ModelSourceChanged;

        /// <summary>
        /// Raised when extracted features change.
        /// </summary>
        event EventHandler? ExtractedFeaturesChanged;

        #endregion Events
    }
}