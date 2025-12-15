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
        private bool _isUpdatingSelection = false;

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
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged();
                    GroupSelectionChanged?.Invoke(this, EventArgs.Empty);
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
        public bool AllSelected => SelectableImages.All(x => x.IsSelected);

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
        /// Create new cluster with images
        /// </summary>
        public ImageCluster(int id, List<ExtractedFeatures> images, string? name = null)
        {
            Id = id;
            _name = name ?? $"Group {id + 1}";
            Images = images ?? [];

            // Create selectable wrappers
            SelectableImages = new ObservableCollection<SelectableImage>(Images.Select(img => new SelectableImage(img)));

            // Subscribe to individual image selection changes
            foreach (SelectableImage selectableImage in SelectableImages)
            {
                selectableImage.SelectionChanged += OnImageSelectionChanged;
            }
        }

        /// <summary>
        /// Create new cluster with single image
        /// </summary>
        public ImageCluster(int id, ExtractedFeatures image, string? name = null) : this(id, [image], name)
        {
        }

        #endregion Constructors

        #region Selection Methods

        /// <summary>
        /// Select all images in group
        /// </summary>
        public void SelectAll()
        {
            SetAllSelection(true);
        }

        /// <summary>
        /// Deselect all images in group
        /// </summary>
        public void DeselectAll()
        {
            SetAllSelection(false);
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
            SetAllSelection(selected);
        }

        private void SetAllSelection(bool selected)
        {
            _isUpdatingSelection = true;

            foreach (SelectableImage image in SelectableImages)
            {
                image.IsSelected = selected;
            }

            _isUpdatingSelection = false;
            UpdateGroupSelectionState();
        }

        private void OnImageSelectionChanged(object? sender, EventArgs e)
        {
            if (!_isUpdatingSelection)
            {
                UpdateGroupSelectionState();
            }
        }

        private void UpdateGroupSelectionState()
        {
            if (NoneSelected)
            {
                IsSelected = false;
            }
            else if (AllSelected)
            {
                IsSelected = true;
            }
            else
            {
                IsSelected = null; // partial
            }

            OnPropertyChanged(nameof(SelectedCount));
            OnPropertyChanged(nameof(AllSelected));
            OnPropertyChanged(nameof(NoneSelected));
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

        #endregion Selection Methods

        #region Path Methods

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

        #endregion Path Methods

        /// <summary>
        /// String representation of cluster
        /// </summary>
        public override string ToString()
        {
            return $"Cluster {Id} ({Name}): {Count} image{(Count == 1 ? "" : "s")} (Avg Similarity: {AverageSimilarity:F3})";
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}