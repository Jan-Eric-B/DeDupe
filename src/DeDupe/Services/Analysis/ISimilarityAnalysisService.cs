using DeDupe.Models;
using DeDupe.Models.Analysis;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DeDupe.Services.Analysis
{
    /// <summary>
    /// Service for analyzing similarity between items and clustering them.
    /// </summary>
    public interface ISimilarityAnalysisService
    {
        /// <summary>
        /// Perform hierarchical clustering on analysis items using cosine similarity.
        /// </summary>
        Task<SimilarityResult> ClusterAsync(
            IEnumerable<AnalysisItem> items,
            double similarityThreshold,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Calculate cosine similarity between two feature vectors.
        /// </summary>
        double CalculateCosineSimilarity(float[] vectorA, float[] vectorB);
    }
}