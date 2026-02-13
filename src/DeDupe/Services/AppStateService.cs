using DeDupe.Models.Media;
using DeDupe.Models.Results;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace DeDupe.Services
{
    /// <inheritdoc/>
    public partial class AppStateService(ILogger<AppStateService> logger) : IAppStateService
    {
        private readonly ILogger<AppStateService> _logger = logger;

        #region Source Media

        private readonly List<SourceMedia> _sourceMedia = [];

        /// <inheritdoc/>
        public IReadOnlyCollection<SourceMedia> SourceMedia => _sourceMedia.AsReadOnly();

        public int SourceMediaCount => _sourceMedia.Count;

        public void SetSourceMedia(IEnumerable<SourceMedia> sources)
        {
            _sourceMedia.Clear();
            _analysisItems.Clear();

            if (sources != null)
            {
                foreach (SourceMedia source in sources)
                {
                    _sourceMedia.Add(source);

                    // Images - Create one AnalysisItem per source
                    // Videos - AnalysisItems will be created during frame extraction
                    if (source.IsImage)
                    {
                        _analysisItems.Add(new AnalysisItem(source));
                    }
                }
            }

            int imageCount = _analysisItems.Count;
            int videoCount = _sourceMedia.Count - imageCount;
            LogSourceMediaSet(_sourceMedia.Count, imageCount, videoCount);

            OnPropertyChanged(nameof(SourceMedia));
            OnPropertyChanged(nameof(SourceMediaCount));
            OnPropertyChanged(nameof(AnalysisItems));
            OnPropertyChanged(nameof(AnalysisItemCount));
            SourceMediaChanged?.Invoke(this, EventArgs.Empty);
        }

        public void AddSourceMedia(SourceMedia source)
        {
            if (source == null) return;

            _sourceMedia.Add(source);

            if (source.IsImage)
            {
                _analysisItems.Add(new AnalysisItem(source));
            }

            LogSourceMediaAdded(source.IsImage ? "image" : "video", _sourceMedia.Count);

            OnPropertyChanged(nameof(SourceMedia));
            OnPropertyChanged(nameof(SourceMediaCount));
            OnPropertyChanged(nameof(AnalysisItems));
            OnPropertyChanged(nameof(AnalysisItemCount));
            SourceMediaChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <inheritdoc/>
        public void AddVideoFrames(SourceMedia videoSource, IEnumerable<(int frameIndex, TimeSpan timestamp)> frames)
        {
            if (videoSource == null || !videoSource.IsVideo) return;

            int addedCount = 0;
            foreach ((int frameIndex, TimeSpan timestamp) in frames)
            {
                _analysisItems.Add(new AnalysisItem(videoSource, frameIndex, timestamp));
                addedCount++;
            }

            LogVideoFramesAdded(addedCount, _analysisItems.Count);

            OnPropertyChanged(nameof(AnalysisItems));
            OnPropertyChanged(nameof(AnalysisItemCount));
            AnalysisItemsChanged?.Invoke(this, EventArgs.Empty);
        }

        public void ClearSourceMedia()
        {
            int previousCount = _sourceMedia.Count;
            _sourceMedia.Clear();
            _analysisItems.Clear();

            LogSourceMediaCleared(previousCount);

            OnPropertyChanged(nameof(SourceMedia));
            OnPropertyChanged(nameof(SourceMediaCount));
            OnPropertyChanged(nameof(AnalysisItems));
            OnPropertyChanged(nameof(AnalysisItemCount));
            SourceMediaChanged?.Invoke(this, EventArgs.Empty);
        }

        public event EventHandler? SourceMediaChanged;

        #endregion Source Media

        #region Analysis Items

        private readonly List<AnalysisItem> _analysisItems = [];

        /// <inheritdoc/>
        public IReadOnlyCollection<AnalysisItem> AnalysisItems => _analysisItems.AsReadOnly();

        public int AnalysisItemCount => _analysisItems.Count;

        /// <inheritdoc/>
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

            LogAnalysisItemsRemoved(itemsToRemove.Count, _analysisItems.Count);

            // Notify changes
            OnPropertyChanged(nameof(AnalysisItems));
            OnPropertyChanged(nameof(AnalysisItemCount));
            OnPropertyChanged(nameof(ProcessedItems));
            OnPropertyChanged(nameof(ProcessedItemCount));
            OnPropertyChanged(nameof(ItemsWithFeatures));
            OnPropertyChanged(nameof(ExtractedFeaturesCount));
            OnPropertyChanged(nameof(SourceMedia));
            OnPropertyChanged(nameof(SourceMediaCount));

            AnalysisItemsChanged?.Invoke(this, EventArgs.Empty);
            ExtractedFeaturesChanged?.Invoke(this, EventArgs.Empty);
            SourceMediaChanged?.Invoke(this, EventArgs.Empty);

            return itemsToRemove.Count;
        }

        public event EventHandler? AnalysisItemsChanged;

        #endregion Analysis Items

        #region Processing State

        public IReadOnlyCollection<AnalysisItem> ProcessedItems => _analysisItems.Where(i => i.IsProcessed).ToList().AsReadOnly();

        public int ProcessedItemCount => _analysisItems.Count(i => i.IsProcessed);

        public void NotifyProcessingComplete()
        {
            LogProcessingStateCompleted(ProcessedItemCount, _analysisItems.Count);

            OnPropertyChanged(nameof(ProcessedItems));
            OnPropertyChanged(nameof(ProcessedItemCount));
            ProcessingStateChanged?.Invoke(this, EventArgs.Empty);
        }

        public void ClearProcessedState()
        {
            foreach (AnalysisItem item in _analysisItems)
            {
                item.ProcessedFilePath = null;
            }

            LogProcessingStateCleared(_analysisItems.Count);

            OnPropertyChanged(nameof(ProcessedItems));
            OnPropertyChanged(nameof(ProcessedItemCount));
            ProcessingStateChanged?.Invoke(this, EventArgs.Empty);
        }

        public event EventHandler? ProcessingStateChanged;

        #endregion Processing State

        #region Extracted Features

        public IReadOnlyCollection<AnalysisItem> ItemsWithFeatures => _analysisItems.Where(i => i.HasFeatures).ToList().AsReadOnly();

        public int ExtractedFeaturesCount => _analysisItems.Count(i => i.HasFeatures);

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void NotifyFeaturesExtracted()
        {
            LogFeatureExtractionNotified(ExtractedFeaturesCount, _analysisItems.Count);

            OnPropertyChanged(nameof(ItemsWithFeatures));
            OnPropertyChanged(nameof(ExtractedFeaturesCount));
            ExtractedFeaturesChanged?.Invoke(this, EventArgs.Empty);
        }

        public void ClearFeatureState()
        {
            foreach (AnalysisItem item in _analysisItems)
            {
                item.FeatureVector = null;
                item.FeatureDimensions = null;
            }

            LogFeatureStateCleared(_analysisItems.Count);

            OnPropertyChanged(nameof(ItemsWithFeatures));
            OnPropertyChanged(nameof(ExtractedFeaturesCount));
            ExtractedFeaturesChanged?.Invoke(this, EventArgs.Empty);
        }

        public event EventHandler? ExtractedFeaturesChanged;

        #endregion Extracted Features

        #region Logging

        [LoggerMessage(Level = LogLevel.Information, Message = "Source media set: {TotalCount} item(s) ({ImageCount} images, {VideoCount} videos)")]
        private partial void LogSourceMediaSet(int totalCount, int imageCount, int videoCount);

        [LoggerMessage(Level = LogLevel.Debug, Message = "Source media added ({MediaType}), total count: {TotalCount}")]
        private partial void LogSourceMediaAdded(string mediaType, int totalCount);

        [LoggerMessage(Level = LogLevel.Debug, Message = "Video frames added: {FrameCount} frames, total analysis items: {TotalItemCount}")]
        private partial void LogVideoFramesAdded(int frameCount, int totalItemCount);

        [LoggerMessage(Level = LogLevel.Information, Message = "Source media cleared, removed {PreviousCount} item(s)")]
        private partial void LogSourceMediaCleared(int previousCount);

        [LoggerMessage(Level = LogLevel.Information, Message = "Analysis items removed: {RemovedCount} item(s), {RemainingCount} remaining")]
        private partial void LogAnalysisItemsRemoved(int removedCount, int remainingCount);

        [LoggerMessage(Level = LogLevel.Information, Message = "Processing state completed: {ProcessedCount} of {TotalCount} items processed")]
        private partial void LogProcessingStateCompleted(int processedCount, int totalCount);

        [LoggerMessage(Level = LogLevel.Debug, Message = "Processing state cleared for {ItemCount} item(s)")]
        private partial void LogProcessingStateCleared(int itemCount);

        [LoggerMessage(Level = LogLevel.Information, Message = "Feature extraction notified: {ExtractedCount} of {TotalCount} items have features")]
        private partial void LogFeatureExtractionNotified(int extractedCount, int totalCount);

        [LoggerMessage(Level = LogLevel.Debug, Message = "Feature state cleared for {ItemCount} item(s)")]
        private partial void LogFeatureStateCleared(int itemCount);

        #endregion Logging
    }
}