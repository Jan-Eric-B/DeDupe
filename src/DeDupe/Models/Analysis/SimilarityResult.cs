using DeDupe.Localization;
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

        public int TotalClusters => Groups.Count + SingletonGroupCount;

        /// <summary>
        /// Number of clusters with potential duplicates (more than 1 item).
        /// </summary>
        public int DuplicateGroupsCount => Groups.Count;

        /// <summary>
        /// Number of singleton clusters (items with no similar matches).
        /// </summary>
        public int SingletonGroupCount { get; }

        public int TotalDuplicateItems => Groups.Sum(c => c.Count);

        public IReadOnlyList<SimilarityGroup> DuplicateGroups { get; }

        public bool IsEmpty => TotalItemsAnalyzed == 0;

        #endregion Properties

        public SimilarityResult(IEnumerable<SimilarityGroup> duplicateGroups, double similarityThreshold, int totalItemsAnalyzed, int singletonCount)
        {
            ArgumentNullException.ThrowIfNull(duplicateGroups);

            // Only duplicate groups are stored — singletons are tracked as a count only
            List<SimilarityGroup> groupList = [.. duplicateGroups];
            Groups = groupList.AsReadOnly();

            DuplicateGroups = groupList
                .OrderByDescending(c => c.AverageSimilarity)
                .ToList()
                .AsReadOnly();

            SimilarityThreshold = similarityThreshold;
            TotalItemsAnalyzed = totalItemsAnalyzed;
            SingletonGroupCount = singletonCount;
            AnalysisCompletedAt = DateTime.Now;
        }

        private SimilarityResult(double similarityThreshold)
        {
            Groups = [];
            DuplicateGroups = [];
            SimilarityThreshold = similarityThreshold;
            TotalItemsAnalyzed = 0;
            SingletonGroupCount = 0;
            AnalysisCompletedAt = DateTime.Now;
        }

        public static SimilarityResult Empty(double similarityThreshold = 0.0)
        {
            return new SimilarityResult(similarityThreshold);
        }

        public string GetSummary(ILocalizer localizer)
        {
            if (IsEmpty)
            {
                return localizer.GetLocalizedString("SimilarityResult_Summary_Empty");
            }
            if (DuplicateGroupsCount == 0)
            {
                return string.Format(localizer.GetLocalizedString("SimilarityResult_Summary_NoDuplicates"), TotalItemsAnalyzed);
            }
            return string.Format(localizer.GetLocalizedString("SimilarityResult_Summary_WithDuplicates"), DuplicateGroupsCount, TotalDuplicateItems, SingletonGroupCount);
        }
    }
}