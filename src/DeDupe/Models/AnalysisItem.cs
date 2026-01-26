using DeDupe.Enums;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DeDupe.Models
{
    /// <summary>
    /// Represents single item going through analysis.
    /// Images: 1x SourceMedia -> 1x AnalysisItem
    /// Videos: 1x SourceMedia -> multiple AnalysisItems (for each extracted frame)
    /// </summary>
    public partial class AnalysisItem : INotifyPropertyChanged
    {
        #region Fields

        private string? _processedFilePath;
        private float[]? _featureVector;
        private int[]? _featureDimensions;

        #endregion Fields

        #region Source Properties

        /// <summary>
        /// Original source media.
        /// </summary>
        public SourceMedia Source { get; }

        /// <summary>
        /// Unique identifier of the source for preventing grouping from the same source (multiple frames of a video).
        /// </summary>
        public Guid SourceId => Source.Id;

        #endregion Source Properties

        #region Frame Properties

        /// <summary>
        /// Frame index (null for images).
        /// </summary>
        public int? FrameIndex { get; }

        /// <summary>
        /// Timestamp of frame (null for images).
        /// </summary>
        public TimeSpan? FrameTimestamp { get; }

        /// <summary>
        /// This item is a video frame.
        /// </summary>
        public bool IsVideoFrame => FrameIndex.HasValue;

        #endregion Frame Properties

        #region Processing Properties

        /// <summary>
        /// Processed image file Path.
        /// </summary>
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

        /// <summary>
        /// Processing has been completed.
        /// </summary>
        public bool IsProcessed => !string.IsNullOrEmpty(ProcessedFilePath);

        #endregion Processing Properties

        #region Feature Extraction Properties

        /// <summary>
        /// Feature vector extracted from model.
        /// </summary>
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

        /// <summary>
        /// Dimensions of feature vector.
        /// </summary>
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

        /// <summary>
        /// Feature extraction has been completed.
        /// </summary>
        public bool HasFeatures => _featureVector != null && _featureVector.Length > 0;

        /// <summary>
        /// Number of features in vector.
        /// </summary>
        public int FeatureCount => _featureVector?.Length ?? 0;

        #endregion Feature Extraction Properties

        #region Constructors

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

        #endregion Constructors

        #region Methods

        /// <summary>
        /// Set processing result.
        /// </summary>
        public void SetProcessed(string processedFilePath)
        {
            ProcessedFilePath = processedFilePath ?? throw new ArgumentNullException(nameof(processedFilePath));
        }

        /// <summary>
        /// Set feature extraction result.
        /// </summary>
        public void SetFeatures(float[] featureVector, int[] dimensions)
        {
            FeatureVector = featureVector ?? throw new ArgumentNullException(nameof(featureVector));
            FeatureDimensions = dimensions ?? throw new ArgumentNullException(nameof(dimensions));
        }

        /// <summary>
        /// Clear processing and feature data.
        /// </summary>
        public void ResetPipelineState()
        {
            ProcessedFilePath = null;
            FeatureVector = null;
            FeatureDimensions = null;
        }

        #endregion Methods

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion INotifyPropertyChanged
    }
}