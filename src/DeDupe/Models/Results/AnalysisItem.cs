using CommunityToolkit.Mvvm.ComponentModel;
using DeDupe.Enums;
using DeDupe.Models.Media;
using System;

namespace DeDupe.Models.Results
{
    /// <summary>
    /// Represents single item going through analysis. Images: 1x SourceMedia -> 1x AnalysisItem; Videos: 1x SourceMedia
    /// -> multiple AnalysisItems (for each extracted frame)
    /// </summary>
    public partial class AnalysisItem : ObservableObject
    {
        /// <summary>
        /// Create AnalysisItem for image.
        /// </summary>
        public AnalysisItem(SourceMedia source)
        {
            Source = source ?? throw new ArgumentNullException(nameof(source));
            FrameIndex = null;
            FrameTimestamp = null;
        }

        /// <summary>
        /// Create AnalysisItem for video frame.
        /// </summary>
        public AnalysisItem(SourceMedia source, int frameIndex, TimeSpan frameTimestamp)
        {
            Source = source ?? throw new ArgumentNullException(nameof(source));

            if (source.MediaType != MediaType.Video)
            {
                throw new ArgumentException("Frame info can only be set for video sources", nameof(source));
            }

            FrameIndex = frameIndex;
            FrameTimestamp = frameTimestamp;
        }

        #region Media Source

        public SourceMedia Source { get; }

        /// <summary>
        /// Unique identifier of the source for preventing grouping from the same source (multiple frames of a video).
        /// </summary>
        public Guid SourceId => Source.Id;

        #endregion Media Source

        #region Frame

        public int? FrameIndex { get; }

        public TimeSpan? FrameTimestamp { get; }

        public bool IsVideoFrame => FrameIndex.HasValue;

        #endregion Frame

        #region Processing

        private string? _processedFilePath;

        public string? ProcessedFilePath
        {
            get => _processedFilePath;
            set
            {
                if (_processedFilePath != value)
                {
                    _processedFilePath = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsProcessed));
                }
            }
        }

        public bool IsProcessed => !string.IsNullOrEmpty(ProcessedFilePath);

        public void SetProcessed(string processedFilePath)
        {
            ProcessedFilePath = processedFilePath ?? throw new ArgumentNullException(nameof(processedFilePath));
        }

        #endregion Processing

        #region Feature Extraction

        private float[]? _featureVector;

        private int[]? _featureDimensions;

        public float[]? FeatureVector
        {
            get => _featureVector;
            set
            {
                if (_featureVector != value)
                {
                    _featureVector = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(HasFeatures));
                    OnPropertyChanged(nameof(FeatureCount));
                }
            }
        }

        public int[]? FeatureDimensions
        {
            get => _featureDimensions;
            set
            {
                if (_featureDimensions != value)
                {
                    _featureDimensions = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool HasFeatures => _featureVector != null && _featureVector.Length > 0;

        public int FeatureCount => _featureVector?.Length ?? 0;

        public void SetFeatures(float[] featureVector, int[] dimensions)
        {
            FeatureVector = featureVector ?? throw new ArgumentNullException(nameof(featureVector));
            FeatureDimensions = dimensions ?? throw new ArgumentNullException(nameof(dimensions));
        }

        #endregion Feature Extraction
    }
}