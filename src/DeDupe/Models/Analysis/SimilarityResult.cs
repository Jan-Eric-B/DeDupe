using System;
using System.Collections.Generic;
using System.Linq;

namespace DeDupe.Models.Analysis
{
    /// <summary>
    /// Result of similarity analysis and clustering
    /// </summary>
    public class SimilarityResult(List<ImageCluster> clusters, double similarityThreshold, int totalImagesAnalyzed)
    {
        /// <summary>
        /// Get all clusters
        /// </summary>
        public List<ImageCluster> Clusters { get; } = clusters ?? [];

        /// <summary>
        /// Get similarity threshold
        /// </summary>
        public double SimilarityThreshold { get; } = similarityThreshold;

        /// <summary>
        /// Get total number of analyzed images
        /// </summary>
        public int TotalImagesAnalyzed { get; } = totalImagesAnalyzed;

        /// <summary>
        /// Get timestamp when analysis was completed
        /// </summary>
        public DateTime AnalysisCompletedAt { get; } = DateTime.Now;

        /// <summary>
        /// Get total number of clusters
        /// </summary>
        public int TotalClusters => Clusters.Count;

        /// <summary>
        /// Get number of clusters with potential duplicates (more than 1 image)
        /// </summary>
        public int DuplicateGroupsCount => Clusters.Count(c => c.IsDuplicateGroup);

        /// <summary>
        /// Get number of singleton clusters (images with no similar matches)
        /// </summary>
        public int SingletonClustersCount => Clusters.Count(c => !c.IsDuplicateGroup);

        /// <summary>
        /// Get total number of duplicate images (images in clusters with more than 1 image)
        /// </summary>
        public int TotalDuplicateImages => Clusters.Where(c => c.IsDuplicateGroup).Sum(c => c.Count);

        /// <summary>
        /// Get cluster that contain potential duplicates
        /// Ordered by cluster size descending
        /// </summary>
        public List<ImageCluster> DuplicateGroups => [.. Clusters
            .Where(c => c.IsDuplicateGroup)
            .OrderByDescending(c => c.Count)];

        /// <summary>
        /// Get summary string of the results
        /// </summary>
        public string GetSummary()
        {
            if (TotalClusters == 0)
            {
                return "No clusters found.";
            }

            if (DuplicateGroupsCount == 0)
            {
                return $"No duplicate groups found. All {TotalImagesAnalyzed} images are unique.";
            }

            return $"Found {DuplicateGroupsCount} duplicate group{(DuplicateGroupsCount == 1 ? "" : "s")} " +
                   $"containing {TotalDuplicateImages} image{(TotalDuplicateImages == 1 ? "" : "s")}. " +
                   $"{SingletonClustersCount} image{(SingletonClustersCount == 1 ? "" : "s")} unique.";
        }

        /// <summary>
        /// Return string representation of analysis result
        /// </summary>
        public override string ToString()
        {
            return $"Similarity Analysis Result: {GetSummary()}";
        }
    }
}