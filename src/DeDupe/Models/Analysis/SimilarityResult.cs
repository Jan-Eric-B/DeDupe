using System;
using System.Collections.Generic;
using System.Linq;

namespace DeDupe.Models.Analysis
{
    public sealed class SimilarityResult
    {
        #region Properties

        public IReadOnlyList<SimilarityGroup> Groups { get; }

        public double SimilarityThreshold { get; }

        public int TotalItemsAnalyzed { get; }

        public DateTime AnalysisCompletedAt { get; }

        public int TotalClusters => Groups.Count;

        /// <summary>
        /// Number of clusters with potential duplicates (more than 1 item).
        /// </summary>
        public int DuplicateGroupsCount => Groups.Count(c => c.IsDuplicateGroup);

        /// <summary>
        /// Number of singleton clusters (items with no similar matches).
        /// </summary>
        public int SingletonGroupCount => Groups.Count(c => !c.IsDuplicateGroup);

        public int TotalDuplicateItems => Groups.Where(c => c.IsDuplicateGroup).Sum(c => c.Count);

        public IReadOnlyList<SimilarityGroup> DuplicateGroups { get; }

        public bool IsEmpty => TotalItemsAnalyzed == 0;

        #endregion Properties

        public SimilarityResult(IEnumerable<SimilarityGroup> clusters, double similarityThreshold, int totalItemsAnalyzed)
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

        private SimilarityResult(double similarityThreshold)
        {
            Groups = [];
            DuplicateGroups = [];
            SimilarityThreshold = similarityThreshold;
            TotalItemsAnalyzed = 0;
            AnalysisCompletedAt = DateTime.Now;
        }

        public static SimilarityResult Empty(double similarityThreshold = 0.0)
        {
            return new SimilarityResult(similarityThreshold);
        }

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

            return $"Found {DuplicateGroupsCount} duplicate group{(DuplicateGroupsCount == 1 ? "" : "s")} containing {TotalDuplicateItems} item{(TotalDuplicateItems == 1 ? "" : "s")}. {SingletonGroupCount} item{(SingletonGroupCount == 1 ? "" : "s")} unique.";
        }
    }
}