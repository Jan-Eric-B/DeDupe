using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace DeDupe.Models.Analysis
{
    /// <summary>
    /// Cluster of similar images
    /// </summary>
    public partial class ImageCluster : INotifyPropertyChanged
    {
        #region Fields

        private string _name;
        private bool? _isSelected = false;
        private bool _isBatchUpdating = false;
        private bool _isInternalUpdate = false;

        #endregion Fields

        #region Events

        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Event raised when group selection changes
        /// </summary>
        public event EventHandler? GroupSelectionChanged;

        #endregion Events

        #region Properties

        /// <summary>
        /// Unique identifier
        /// </summary>
        public int Id { get; }

        /// <summary>
        /// Display name
        /// </summary>
        public string Name
        {
            get => _name;
            set
            {
                if (_name != value)
                {
                    _name = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Extracted features of cluster (original list)
        /// </summary>
        public List<ExtractedFeatures> Images { get; }

        /// <summary>
        /// Selectable images for UI binding
        /// </summary>
        public ObservableCollection<SelectableImage> SelectableImages { get; }

        /// <summary>
        /// Group selection (null = partial)
        /// </summary>
        public bool? IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value)
                {
                    return;
                }

                // RecalculateGroupSelectionState update
                if (_isInternalUpdate)
                {
                    _isSelected = value;
                    OnPropertyChanged();
                    GroupSelectionChanged?.Invoke(this, EventArgs.Empty);
                    return;
                }

                // UI update
                if (AllSelected)
                {
                    DeselectAll();
                }
                else
                {
                    SelectAll();
                }
            }
        }

        /// <summary>
        /// Number of selected images
        /// </summary>
        public int SelectedCount => SelectableImages.Count(x => x.IsSelected);

        /// <summary>
        /// If all images selected
        /// </summary>
        public bool AllSelected => SelectableImages.Count > 0 && SelectableImages.All(x => x.IsSelected);

        /// <summary>
        /// No images selected
        /// </summary>
        public bool NoneSelected => SelectableImages.All(x => !x.IsSelected);

        /// <summary>
        /// Average similarity within cluster
        /// </summary>
        public double AverageSimilarity { get; set; } = 0.0;

        /// <summary>
        /// Number of images in cluster
        /// </summary>
        public int Count => Images.Count;

        /// <summary>
        /// Contains duplicates (more than 1 image)
        /// </summary>
        public bool IsDuplicateGroup => Images.Count > 1;

        #endregion Properties

        #region Constructors

        /// <summary>
        /// Create new cluster with images.
        /// </summary>
        public ImageCluster(int id, List<ExtractedFeatures> images, string? name = null)
        {
            Id = id;
            _name = name ?? $"Group {id + 1}";
            Images = images ?? [];

            // Create selectable wrappers and subscribe to selection changes
            SelectableImages = new ObservableCollection<SelectableImage>(
                Images.Select(img => CreateSelectableImage(img))
            );
        }

        /// <summary>
        /// Create a new cluster with a single image.
        /// </summary>
        public ImageCluster(int id, ExtractedFeatures image, string? name = null)
            : this(id, [image], name)
        {
        }

        #endregion Constructors

        #region Private Methods

        private SelectableImage CreateSelectableImage(ExtractedFeatures features)
        {
            SelectableImage selectableImage = new(features);
            selectableImage.SelectionChanged += OnIndividualImageSelectionChanged;
            return selectableImage;
        }

        private void OnIndividualImageSelectionChanged(object? sender, EventArgs e)
        {
            // Don't recalculate during batch updates
            if (_isBatchUpdating)
            {
                return;
            }

            RecalculateGroupSelectionState();
        }

        private void RecalculateGroupSelectionState()
        {
            // Calculate new state
            bool? newState;

            if (NoneSelected)
            {
                newState = false;
            }
            else if (AllSelected)
            {
                newState = true;
            }
            else
            {
                newState = null; // Partial
            }

            // Set internal update flag for setter
            _isInternalUpdate = true;
            try
            {
                IsSelected = newState;
            }
            finally
            {
                _isInternalUpdate = false;
            }

            OnPropertyChanged(nameof(SelectedCount));
            OnPropertyChanged(nameof(AllSelected));
            OnPropertyChanged(nameof(NoneSelected));
        }

        #endregion Private Methods

        #region Public Selection Methods

        /// <summary>
        /// Select all images in group
        /// </summary>
        public void SelectAll()
        {
            SetAllImagesSelection(true);
        }

        /// <summary>
        /// Deselect all images in group
        /// </summary>
        public void DeselectAll()
        {
            SetAllImagesSelection(false);
        }

        /// <summary>
        /// Toggle selection of all images
        /// </summary>
        public void ToggleSelection()
        {
            if (AllSelected)
            {
                DeselectAll();
            }
            else
            {
                SelectAll();
            }
        }

        /// <summary>
        /// Set selection state for group checkbox
        /// </summary>
        public void SetGroupSelection(bool selected)
        {
            SetAllImagesSelection(selected);
        }

        /// <summary>
        /// Set selection state for all images.
        /// </summary>
        private void SetAllImagesSelection(bool selected)
        {
            // Prevent triggering individual change handlers
            _isBatchUpdating = true;

            try
            {
                foreach (SelectableImage image in SelectableImages)
                {
                    image.IsSelected = selected;
                }
            }
            finally
            {
                _isBatchUpdating = false;
            }

            RecalculateGroupSelectionState();
        }

        /// <summary>
        /// Get all selected images
        /// </summary>
        public IEnumerable<SelectableImage> GetSelectedImages()
        {
            return SelectableImages.Where(x => x.IsSelected);
        }

        /// <summary>
        /// Get file paths of selected images
        /// </summary>
        public List<string> GetSelectedFilePaths()
        {
            return [.. SelectableImages.Where(x => x.IsSelected).Select(x => x.FilePath)];
        }

        #endregion Public Selection Methods

        #region Path Helper Methods

        /// <summary>
        /// File paths of all images in cluster
        /// </summary>
        public List<string> GetOriginalFilePaths()
        {
            return [.. Images.Select(img => img.OriginalFilePath)];
        }

        /// <summary>
        /// Get up to 4 image paths for group card
        /// </summary>
        public List<string> GetPreviewImagePaths()
        {
            return [.. Images.Take(4).Select(img => img.OriginalFilePath)];
        }

        /// <summary>
        /// First image of cluster
        /// </summary>
        public ExtractedFeatures? GetFirstImage()
        {
            return Images.FirstOrDefault();
        }

        #endregion Path Helper Methods

        #region Cleanup

        /// <summary>
        /// Unsubscribe from all image selection events.
        /// </summary>
        public void Cleanup()
        {
            foreach (SelectableImage image in SelectableImages)
            {
                image.SelectionChanged -= OnIndividualImageSelectionChanged;
            }
        }

        #endregion Cleanup

        #region Object Overrides

        /// <summary>
        /// String representation of cluster
        /// </summary>
        public override string ToString()
        {
            return $"Cluster {Id} ({Name}): {Count} image{(Count == 1 ? "" : "s")} (Avg Similarity: {AverageSimilarity:F3})";
        }

        #endregion Object Overrides

        #region INotifyPropertyChanged

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion INotifyPropertyChanged
    }
}