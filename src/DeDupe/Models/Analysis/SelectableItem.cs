using CommunityToolkit.Mvvm.ComponentModel;
using DeDupe.Models.Media;
using DeDupe.Models.Results;
using System;
using System.ComponentModel;

namespace DeDupe.Models.Analysis
{
    /// <summary>
    /// Wrapper for AnalysisItem that adds selection state for UI binding.
    /// </summary>
    /// <remarks>
    /// Create a selectable wrapper for an analysis item.
    /// </remarks>
    /// <param name="item">The item to wrap.</param>
    /// <exception cref="ArgumentNullException">Thrown when item is null.</exception>
    public partial class SelectableItem(AnalysisItem item) : ObservableObject
    {
        #region Fields

        private bool _isSelected;

        #endregion Fields

        #region Events

        public event EventHandler? SelectionChanged;

        #endregion Events

        #region Properties

        /// <summary>
        /// The underlying analysis item.
        /// </summary>
        public AnalysisItem Item { get; } = item ?? throw new ArgumentNullException(nameof(item));

        /// <summary>
        /// Whether this item is currently selected.
        /// </summary>
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged();
                    SelectionChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        #endregion Properties

        #region Convenience Accessors

        // These provide direct access to commonly used properties from the underlying item.
        // They avoid verbose chains like selectableItem.Item.Source.Metadata.FileName

        /// <summary>
        /// Path to the original source file.
        /// </summary>
        public string FilePath => Item.Source.Metadata.FilePath;

        /// <summary>
        /// Metadata of the source file.
        /// </summary>
        public MediaMetadata Metadata => Item.Source.Metadata;

        /// <summary>
        /// Image-specific metadata (null for video frames).
        /// </summary>
        public ImageMetadata? ImageMetadata => Item.Source.Metadata as ImageMetadata;

        /// <summary>
        /// Video-specific metadata (null for images).
        /// </summary>
        public VideoMetadata? VideoMetadata => Item.Source.Metadata as VideoMetadata;

        /// <summary>
        /// Display name for the item.
        /// </summary>
        public string DisplayName => Item.Source.Metadata.GetDisplayName();

        /// <summary>
        /// Whether this item is a video frame.
        /// </summary>
        public bool IsVideoFrame => Item.IsVideoFrame;

        /// <summary>
        /// Frame index for video frames (null for images).
        /// </summary>
        public int? FrameIndex => Item.FrameIndex;

        /// <summary>
        /// Unique identifier of the source media.
        /// </summary>
        public Guid SourceId => Item.SourceId;

        /// <summary>
        /// Extracted feature vector (null if not yet extracted).
        /// </summary>
        public float[]? FeatureVector => Item.FeatureVector;

        #endregion Convenience Accessors
    }
}