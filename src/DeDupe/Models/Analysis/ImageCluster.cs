using System.Collections.Generic;
using System.Linq;

namespace DeDupe.Models.Analysis
{
    /// <summary>
    /// Cluster of similar images
    /// </summary>
    /// <remarks>
    /// Create new cluster with list of images
    /// </remarks>
    public class ImageCluster(int id, List<ExtractedFeatures> images, string? name = null)  // <-- No parameters here!
    {
        /// <summary>
        /// Internal unique identifier (stable, not for display)
        /// </summary>
        public int Id { get; } = id;

        /// <summary>
        /// User-editable display name
        /// </summary>
        public string Name { get; set; } = name ?? $"Group {id + 1}";

        /// <summary>
        /// List of extracted features of the cluster
        /// </summary>
        public List<ExtractedFeatures> Images { get; } = images ?? [];

        /// <summary>
        /// Average similarity within the cluster
        /// </summary>
        public double AverageSimilarity { get; set; } = 0.0;

        /// <summary>
        /// Number of images in the cluster
        /// </summary>
        public int Count => Images.Count;

        /// <summary>
        /// Contains potential duplicates (more than 1 image)
        /// </summary>
        public bool IsDuplicateGroup => Images.Count > 1;

        /// <summary>
        /// Create new cluster with single image
        /// </summary>
        public ImageCluster(int id, ExtractedFeatures image, string? name = null) : this(id, [image], name)
        {
        }

        /// <summary>
        /// Original file paths of all images in the cluster
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
        public ExtractedFeatures? GetRepresentativeImage()
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
    }
}