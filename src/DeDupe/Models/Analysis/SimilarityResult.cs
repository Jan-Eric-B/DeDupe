using System;
using System.Collections.Generic;
using System.Linq;

namespace DeDupe.Models.Analysis
{
    /// <summary>
    /// Immutable result of similarity analysis and clustering.
    /// </summary>
    public sealed class SimilarityResult
    {
        #region Properties

        /// <summary>
        /// All clusters (immutable).
        /// </summary>
        public IReadOnlyList<SimilarityGroup> Groups { get; }

        /// <summary>
        /// Similarity threshold used for clustering.
        /// </summary>
        public double SimilarityThreshold { get; }

        /// <summary>
        /// Total number of items that were analyzed.
        /// </summary>
        public int TotalItemsAnalyzed { get; }

        /// <summary>
        /// Timestamp when analysis was completed.
        /// </summary>
        public DateTime AnalysisCompletedAt { get; }

        /// <summary>
        /// Total number of clusters.
        /// </summary>
        public int TotalClusters => Groups.Count;

        /// <summary>
        /// Number of clusters with potential duplicates (more than 1 item).
        /// </summary>
        public int DuplicateGroupsCount => Groups.Count(c => c.IsDuplicateGroup);

        /// <summary>
        /// Number of singleton clusters (items with no similar matches).
        /// </summary>
        public int SingletonGroupCount => Groups.Count(c => !c.IsDuplicateGroup);

        /// <summary>
        /// Total number of items in duplicate groups.
        /// </summary>
        public int TotalDuplicateItems => Groups.Where(c => c.IsDuplicateGroup).Sum(c => c.Count);

        /// <summary>
        /// Clusters that contain potential duplicates, ordered by similarity.
        /// </summary>
        public IReadOnlyList<SimilarityGroup> DuplicateGroups { get; }

        /// <summary>
        /// Whether this result contains any data.
        /// </summary>
        public bool IsEmpty => TotalItemsAnalyzed == 0;

        #endregion Properties

        #region Constructors

        /// <summary>
        /// Create a new similarity result.
        /// </summary>
        /// <param name="clusters">Clusters found during analysis.</param>
        /// <param name="similarityThreshold">Threshold used for grouping.</param>
        /// <param name="totalItemsAnalyzed">Number of items that were analyzed.</param>
        public SimilarityResult(
            IEnumerable<SimilarityGroup> clusters,
            double similarityThreshold,
            int totalItemsAnalyzed)
        {
            ArgumentNullException.ThrowIfNull(clusters);

            // Create immutable copies
            List<SimilarityGroup> clusterList = [.. clusters];
            Groups = clusterList.AsReadOnly();

            DuplicateGroups = clusterList
                .Where(c => c.IsDuplicateGroup)
                .OrderByDescending(c => c.AverageSimilarity)
                .ToList()
                .AsReadOnly();

            SimilarityThreshold = similarityThreshold;
            TotalItemsAnalyzed = totalItemsAnalyzed;
            AnalysisCompletedAt = DateTime.Now;
        }

        /// <summary>
        /// Private constructor for creating empty results.
        /// </summary>
        private SimilarityResult(double similarityThreshold)
        {
            Groups = [];
            DuplicateGroups = [];
            SimilarityThreshold = similarityThreshold;
            TotalItemsAnalyzed = 0;
            AnalysisCompletedAt = DateTime.Now;
        }

        #endregion Constructors

        #region Factory Methods

        /// <summary>
        /// Create an empty result.
        /// </summary>
        public static SimilarityResult Empty(double similarityThreshold = 0.0)
        {
            return new SimilarityResult(similarityThreshold);
        }

        #endregion Factory Methods

        #region Methods

        /// <summary>
        /// Get a human-readable summary of the results.
        /// </summary>
        public string GetSummary()
        {
            if (IsEmpty)
            {
                return "No items were analyzed.";
            }

            if (DuplicateGroupsCount == 0)
            {
                return $"No duplicate groups found. All {TotalItemsAnalyzed} items are unique.";
            }

            return $"Found {DuplicateGroupsCount} duplicate group{(DuplicateGroupsCount == 1 ? "" : "s")} " +
                   $"containing {TotalDuplicateItems} item{(TotalDuplicateItems == 1 ? "" : "s")}. " +
                   $"{SingletonGroupCount} item{(SingletonGroupCount == 1 ? "" : "s")} unique.";
        }

        public override string ToString()
        {
            return $"SimilarityResult: {GetSummary()}";
        }

        #endregion Methods
    }
}