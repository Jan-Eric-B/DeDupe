using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace DeDupe.Models.Analysis
{
    /// <summary>
    /// Cluster of similar images
    /// </summary>
    /// <remarks>
    /// Create new cluster with images
    /// </remarks>
    public partial class ImageCluster(int id, List<ExtractedFeatures> images, string? name = null) : INotifyPropertyChanged
    {
        private string _name = name ?? $"Group {id + 1}";

        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Unique identifier
        /// </summary>
        public int Id { get; } = id;

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
        /// Extracted features of cluster
        /// </summary>
        public List<ExtractedFeatures> Images { get; } = images ?? [];

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

        /// <summary>
        /// Create new cluster with single image
        /// </summary>
        public ImageCluster(int id, ExtractedFeatures image, string? name = null) : this(id, [image], name)
        {
        }

        /// <summary>
        /// File paths of all images in the cluster
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
        /// First image of the cluster
        /// </summary>
        public ExtractedFeatures? GetFirstImage()
        {
            return Images.FirstOrDefault();
        }

        /// <summary>
        /// String representation of the cluster
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