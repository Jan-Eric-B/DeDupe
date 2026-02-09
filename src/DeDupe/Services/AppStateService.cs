using DeDupe.Models.Media;
using DeDupe.Models.Results;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace DeDupe.Services
{
    /// <summary>
    /// Centralized state management service for the application.
    /// </summary>
    public partial class AppStateService() : IAppStateService
    {
        #region Fields

        // Source media files (original images and videos)
        private readonly List<SourceMedia> _sourceMedia = [];

        // Analysis items
        private readonly List<AnalysisItem> _analysisItems = [];

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
        /// Reset processing state for all items.
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

        /// <summary>
        /// Remove analysis items by source file paths.
        /// </summary>
        public int RemoveAnalysisItemsByPath(IEnumerable<string> filePaths)
        {
            if (filePaths == null)
            {
                return 0;
            }

            HashSet<string> pathSet = [.. filePaths];

            if (pathSet.Count == 0)
            {
                return 0;
            }

            // Find items to remove
            List<AnalysisItem> itemsToRemove = [.. _analysisItems.Where(item => pathSet.Contains(item.Source.Metadata.FilePath))];

            if (itemsToRemove.Count == 0)
            {
                return 0;
            }

            // Remove items
            foreach (AnalysisItem item in itemsToRemove)
            {
                _analysisItems.Remove(item);

                // Also remove from source media
                if (!_analysisItems.Any(a => a.Source.Id == item.Source.Id))
                {
                    _sourceMedia.Remove(item.Source);
                }
            }

            // Notify changes
            OnPropertyChanged(nameof(AnalysisItems));
            OnPropertyChanged(nameof(AnalysisItemCount));
            OnPropertyChanged(nameof(ProcessedItems));
            OnPropertyChanged(nameof(ProcessedItemCount));
            OnPropertyChanged(nameof(ItemsWithFeatures));
            OnPropertyChanged(nameof(ExtractedFeaturesCount));
            OnPropertyChanged(nameof(SourceMedia));
            OnPropertyChanged(nameof(SourceCount));

            AnalysisItemsChanged?.Invoke(this, EventArgs.Empty);
            ExtractedFeaturesChanged?.Invoke(this, EventArgs.Empty);
            SourceMediaChanged?.Invoke(this, EventArgs.Empty);

            return itemsToRemove.Count;
        }

        #endregion State Methods

        #region Events

        public event EventHandler? SourceMediaChanged;

        public event EventHandler? AnalysisItemsChanged;

        public event EventHandler? ProcessingStateChanged;

        public event EventHandler? ExtractedFeaturesChanged;

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion Events
    }
}