using DeDupe.Models.Analysis;
using DeDupe.Models.Results;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DeDupe.Services.Analysis
{
    /// <summary>
    /// Service for analyzing similarity between items and clustering them.
    /// </summary>
    public class SimilarityAnalysisService : ISimilarityAnalysisService
    {
        public async Task<SimilarityResult> ClusterAsync(IEnumerable<AnalysisItem> items, double similarityThreshold, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(items);

            List<AnalysisItem> itemList = [.. items.Where(i => i.HasFeatures)];

            if (itemList.Count == 0)
            {
                return SimilarityResult.Empty(similarityThreshold);
            }

            try
            {
                // Step 1 - Build similarity matrix
                double[,] similarityMatrix = await CalculateSimilarityMatrixAsync(itemList, cancellationToken);

                // Step 2 - Hierarchical clustering
                List<SimilarityGroup> clusters = await PerformHierarchicalClusteringAsync(itemList, similarityMatrix, similarityThreshold, cancellationToken);

                // Step 3 - Calculate cluster statistics
                CalculateClusterStatistics(clusters);

                return new SimilarityResult(clusters, similarityThreshold, itemList.Count);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error during clustering: {ex.Message}");
                throw;
            }
        }

        public double CalculateCosineSimilarity(float[] vectorA, float[] vectorB)
        {
            ArgumentNullException.ThrowIfNull(vectorA);

            ArgumentNullException.ThrowIfNull(vectorB);

            if (vectorA.Length != vectorB.Length)
            {
                throw new ArgumentException("Feature vectors must have the same length");
            }

            double dotProduct = 0.0;
            double magnitudeA = 0.0;
            double magnitudeB = 0.0;

            for (int i = 0; i < vectorA.Length; i++)
            {
                dotProduct += vectorA[i] * vectorB[i];
                magnitudeA += vectorA[i] * vectorA[i];
                magnitudeB += vectorB[i] * vectorB[i];
            }

            magnitudeA = Math.Sqrt(magnitudeA);
            magnitudeB = Math.Sqrt(magnitudeB);

            if (magnitudeA == 0.0 || magnitudeB == 0.0)
            {
                return 0.0;
            }

            return dotProduct / (magnitudeA * magnitudeB);
        }

        /// <summary>
        /// Calculate cosine similarity matrix for all items.
        /// </summary>
        private async Task<double[,]> CalculateSimilarityMatrixAsync(List<AnalysisItem> items, CancellationToken cancellationToken)
        {
            int count = items.Count;
            double[,] matrix = new double[count, count];

            int completedComparisons = 0;

            // Calculate similarities (symmetric matrix - only upper triangle)
            for (int i = 0; i < count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                matrix[i, i] = 1.0; // Similarity with self is always 1.0

                for (int j = i + 1; j < count; j++)
                {
                    // If items are from same source (video frames) set similarity to 0
                    if (items[i].SourceId == items[j].SourceId)
                    {
                        matrix[i, j] = 0.0;
                        matrix[j, i] = 0.0;
                    }
                    else
                    {
                        double similarity = CalculateCosineSimilarity(items[i].FeatureVector!, items[j].FeatureVector!);
                        matrix[i, j] = similarity;
                        matrix[j, i] = similarity; // Matrix is symmetric
                    }

                    completedComparisons++;

                    // Yield for UI updates and cancellation checks
                    if (completedComparisons % 1000 == 0)
                    {
                        await Task.Delay(1, cancellationToken);
                    }
                }
            }

            return matrix;
        }

        /// <summary>
        /// Perform agglomerative hierarchical clustering.
        /// </summary>
        private static async Task<List<SimilarityGroup>> PerformHierarchicalClusteringAsync(List<AnalysisItem> items, double[,] similarityMatrix, double similarityThreshold, CancellationToken cancellationToken)
        {
            int count = items.Count;

            // Initialize each item as its own cluster
            List<List<int>> groups = [];
            for (int i = 0; i < count; i++)
            {
                groups.Add([i]);
            }

            // Track active groups
            bool[] activeClusters = new bool[count];
            Array.Fill(activeClusters, true);

            int mergeOperations = 0;

            // Agglomerative clustering - merge closest clusters
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Find pair of clusters with highest similarity
                double maxSimilarity = -1.0;
                int clusterI = -1, clusterJ = -1;

                for (int i = 0; i < groups.Count; i++)
                {
                    if (!activeClusters[i])
                    {
                        continue;
                    }

                    for (int j = i + 1; j < groups.Count; j++)
                    {
                        if (!activeClusters[j])
                        {
                            continue;
                        }

                        if (WouldViolateSourceConstraint(groups[i], groups[j], items))
                        {
                            continue;
                        }

                        double similarity = CalculateClusterSimilarity(groups[i], groups[j], similarityMatrix);

                        if (similarity > maxSimilarity)
                        {
                            maxSimilarity = similarity;
                            clusterI = i;
                            clusterJ = j;
                        }
                    }
                }

                // Stop if no valid pair or similarity below threshold
                if (clusterI == -1 || maxSimilarity < similarityThreshold)
                {
                    break;
                }

                // Merge clusters
                groups[clusterI].AddRange(groups[clusterJ]);
                activeClusters[clusterJ] = false;

                mergeOperations++;

                if (mergeOperations % 10 == 0)
                {
                    await Task.Delay(1, cancellationToken);
                }
            }

            // Convert to SimilarityGroup objects
            List<SimilarityGroup> result = [];
            int clusterId = 0;
            int duplicateGroupNumber = 1;

            for (int i = 0; i < groups.Count; i++)
            {
                if (!activeClusters[i])
                {
                    continue;
                }

                List<AnalysisItem> clusterItems = [.. groups[i].Select(idx => items[idx])];

                string? groupName = clusterItems.Count > 1 ? $"Group {duplicateGroupNumber++}" : null;

                SimilarityGroup group = new(clusterId++, clusterItems, groupName);
                result.Add(group);
            }

            return result;
        }

        /// <summary>
        /// Check if merging two clusters would put items from the same source together.
        /// </summary>
        private static bool WouldViolateSourceConstraint(List<int> groupA, List<int> groupB, List<AnalysisItem> items)
        {
            // Get all source IDs in group A
            HashSet<Guid> sourceIdsA = [.. groupA.Select(idx => items[idx].SourceId)];

            // Check if any item in group B shares a source with group A
            foreach (int idx in groupB)
            {
                if (sourceIdsA.Contains(items[idx].SourceId))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Calculate similarity between two clusters using average linkage.
        /// </summary>
        private static double CalculateClusterSimilarity(List<int> groupA, List<int> groupB, double[,] similarityMatrix)
        {
            double totalSimilarity = 0.0;
            int pairCount = 0;

            foreach (int i in groupA)
            {
                foreach (int j in groupB)
                {
                    totalSimilarity += similarityMatrix[i, j];
                    pairCount++;
                }
            }

            return pairCount > 0 ? totalSimilarity / pairCount : 0.0;
        }

        /// <summary>
        /// Calculate average similarity within each cluster.
        /// </summary>
        private void CalculateClusterStatistics(List<SimilarityGroup> groups)
        {
            foreach (SimilarityGroup group in groups)
            {
                if (group.Count <= 1)
                {
                    group.AverageSimilarity = 1.0; // Single image clusters
                    continue;
                }

                double totalSimilarity = 0.0;
                int pairCount = 0;

                for (int i = 0; i < group.Items.Count; i++)
                {
                    for (int j = i + 1; j < group.Items.Count; j++)
                    {
                        // Find indices of images in original similarity matrix
                        // TODO - Maintain index mappings
                        double similarity = CalculateCosineSimilarity(group.Items[i].FeatureVector!, group.Items[j].FeatureVector!);

                        totalSimilarity += similarity;
                        pairCount++;
                    }
                }

                group.AverageSimilarity = pairCount > 0 ? totalSimilarity / pairCount : 1.0;
            }
        }
    }
}