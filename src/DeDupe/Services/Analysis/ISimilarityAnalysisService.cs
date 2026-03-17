using DeDupe.Localization;
using DeDupe.Models;
using DeDupe.Models.Analysis;
using DeDupe.Models.Results;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DeDupe.Services.Analysis
{
    /// <summary>
    /// Service for analyzing similarity between items and clustering/grouping them.
    /// </summary>
    public interface ISimilarityAnalysisService
    {
        /// <summary>
        /// Perform hierarchical clustering on analysis items using cosine similarity.
        /// </summary>
        Task<SimilarityResult> ClusterAsync(IEnumerable<AnalysisItem> items, double similarityThreshold, ILocalizer localizer, IProgress<ProgressInfo>? progress = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Calculate cosine similarity between two feature vectors.
        /// </summary>
        double CalculateCosineSimilarity(float[] vectorA, float[] vectorB);
    }
}