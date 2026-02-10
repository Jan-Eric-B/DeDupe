using DeDupe.Models.Media;
using DeDupe.Models.Results;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace DeDupe.Services
{
    /// <inheritdoc/>
    public partial class AppStateService() : IAppStateService
    {
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

            foreach ((int frameIndex, TimeSpan timestamp) in frames)
            {
                _analysisItems.Add(new AnalysisItem(videoSource, frameIndex, timestamp));
            }

            OnPropertyChanged(nameof(AnalysisItems));
            OnPropertyChanged(nameof(AnalysisItemCount));
            AnalysisItemsChanged?.Invoke(this, EventArgs.Empty);
        }

        public void ClearSourceMedia()
        {
            _sourceMedia.Clear();
            _analysisItems.Clear();

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

            OnPropertyChanged(nameof(ItemsWithFeatures));
            OnPropertyChanged(nameof(ExtractedFeaturesCount));
            ExtractedFeaturesChanged?.Invoke(this, EventArgs.Empty);
        }

        public event EventHandler? ExtractedFeaturesChanged;

        #endregion Extracted Features
    }
}