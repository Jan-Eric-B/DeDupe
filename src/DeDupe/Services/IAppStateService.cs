using DeDupe.Models.Media;
using DeDupe.Models.Results;
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
        #region Source Media

        /// <summary>
        /// All source media files (images and videos).
        /// </summary>
        IReadOnlyCollection<SourceMedia> SourceMedia { get; }

        int SourceMediaCount { get; }

        void SetSourceMedia(IEnumerable<SourceMedia> sources);

        void AddSourceMedia(SourceMedia source);

        /// <summary>
        /// Add video frames as analysis items.
        /// </summary>
        void AddVideoFrames(SourceMedia videoSource, IEnumerable<(int frameIndex, TimeSpan timestamp)> frames);

        void ClearSourceMedia();

        event EventHandler? SourceMediaChanged;

        #endregion Source Media

        #region Analysis Items

        /// <summary>
        /// All analysis items (images and video frames).
        /// </summary>
        IReadOnlyCollection<AnalysisItem> AnalysisItems { get; }

        int AnalysisItemCount { get; }

        /// <summary>
        /// Remove analysis items by source file paths.
        /// </summary>
        int RemoveAnalysisItemsByPath(IEnumerable<string> filePaths);

        event EventHandler? AnalysisItemsChanged;

        #endregion Analysis Items

        #region Processing State

        IReadOnlyCollection<AnalysisItem> ProcessedItems { get; }

        int ProcessedItemCount { get; }

        void NotifyProcessingComplete();

        void ClearProcessedState();

        event EventHandler? ProcessingStateChanged;

        #endregion Processing State

        #region Extracted Features

        IReadOnlyCollection<AnalysisItem> ItemsWithFeatures { get; }

        int ExtractedFeaturesCount { get; }

        void NotifyFeaturesExtracted();

        void ClearFeatureState();

        event EventHandler? ExtractedFeaturesChanged;

        #endregion Extracted Features
    }
}