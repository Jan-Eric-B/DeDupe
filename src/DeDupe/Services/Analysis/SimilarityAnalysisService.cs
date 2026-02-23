using DeDupe.Models;
using DeDupe.Models.Analysis;
using DeDupe.Models.Results;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DeDupe.Services.Analysis
{
    /// <inheritdoc />
    public partial class SimilarityAnalysisService(ILogger<SimilarityAnalysisService> logger) : ISimilarityAnalysisService
    {
        private readonly ILogger<SimilarityAnalysisService> _logger = logger;

        /// <inheritdoc />
        public async Task<SimilarityResult> ClusterAsync(IEnumerable<AnalysisItem> items, double similarityThreshold, IProgress<ProgressInfo>? progress = null, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(items);

            List<AnalysisItem> itemList = [.. items.Where(i => i.HasFeatures)];

            if (itemList.Count == 0)
            {
                return SimilarityResult.Empty(similarityThreshold);
            }

            Stopwatch stopwatch = Stopwatch.StartNew();
            LogClusteringStarting(itemList.Count, similarityThreshold);

            try
            {
                progress?.Report(new ProgressInfo(0, 100, "Building similarity matrix"));

                // Build similarity matrix
                double[,] similarityMatrix = await Task.Run(() => CalculateSimilarityMatrix(itemList, progress, cancellationToken), cancellationToken);

                // Hierarchical clustering
                progress?.Report(new ProgressInfo(70, 100, "Clustering similar items"));

                List<SimilarityGroup> clusters = await Task.Run(() => PerformHierarchicalClustering(itemList, similarityMatrix, similarityThreshold, progress, cancellationToken), cancellationToken);

                // Calculate cluster statistics
                progress?.Report(new ProgressInfo(95, 100, "Calculating statistics"));
                CalculateClusterStatistics(clusters, itemList, similarityMatrix);

                stopwatch.Stop();
                int duplicateGroupCount = clusters.Count(c => c.Count > 1);
                LogClusteringCompleted(itemList.Count, clusters.Count, duplicateGroupCount, stopwatch.Elapsed.TotalSeconds);

                progress?.Report(new ProgressInfo(100, 100, "Analysis complete"));

                return new SimilarityResult(clusters, similarityThreshold, itemList.Count);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                LogClusteringFailed(itemList.Count, ex);
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

        private double[,] CalculateSimilarityMatrix(List<AnalysisItem> items, IProgress<ProgressInfo>? progress, CancellationToken cancellationToken)
        {
            int count = items.Count;
            double[,] matrix = new double[count, count];

            long totalComparisons = (long)count * (count - 1) / 2;
            Stopwatch stopwatch = Stopwatch.StartNew();

            int lastReportedPercentage = -1;

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
                }

                int percentage = (int)((double)(i + 1) / count * 70);
                if (percentage > lastReportedPercentage)
                {
                    lastReportedPercentage = percentage;
                    progress?.Report(new ProgressInfo(percentage, 100, "Building similarity matrix", $"Row {i + 1:N0}/{count:N0}"));
                }
            }

            stopwatch.Stop();
            LogSimilarityMatrixCalculated(count, totalComparisons, stopwatch.Elapsed.TotalSeconds);

            return matrix;
        }

        /// <summary>
        /// Perform agglomerative hierarchical clustering.
        /// </summary>
        private List<SimilarityGroup> PerformHierarchicalClustering(List<AnalysisItem> items, double[,] similarityMatrix, double similarityThreshold, IProgress<ProgressInfo>? progress, CancellationToken cancellationToken)
        {
            int count = items.Count;

            // Initialize item as its own cluster
            List<List<int>> groups = [];
            for (int i = 0; i < count; i++)
            {
                groups.Add([i]);
            }

            // Track active groups
            bool[] activeClusters = new bool[count];
            Array.Fill(activeClusters, true);

            int mergeOperations = 0;
            int maxPossibleMerges = count - 1; // upper bound

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

                int percentage = 70 + (int)((double)mergeOperations / maxPossibleMerges * 25);
                progress?.Report(new ProgressInfo(Math.Min(percentage, 95), 100, "Clustering similar items", $"Merge {mergeOperations:N0}"));
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

            LogHierarchicalClusteringCompleted(count, mergeOperations, result.Count);

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

        private static void CalculateClusterStatistics(List<SimilarityGroup> groups, List<AnalysisItem> originalItems, double[,] similarityMatrix)
        {
            Dictionary<AnalysisItem, int> indexMap = [];
            for (int i = 0; i < originalItems.Count; i++)
            {
                indexMap[originalItems[i]] = i;
            }

            foreach (SimilarityGroup group in groups)
            {
                if (group.Count <= 1)
                {
                    group.AverageSimilarity = 1.0;
                    continue;
                }

                double totalSimilarity = 0.0;
                int pairCount = 0;

                for (int i = 0; i < group.Items.Count; i++)
                {
                    for (int j = i + 1; j < group.Items.Count; j++)
                    {
                        int idxI = indexMap[group.Items[i]];
                        int idxJ = indexMap[group.Items[j]];

                        totalSimilarity += similarityMatrix[idxI, idxJ];
                        pairCount++;
                    }
                }

                group.AverageSimilarity = pairCount > 0 ? totalSimilarity / pairCount : 1.0;
            }
        }

        #region Logging

        [LoggerMessage(Level = LogLevel.Information, Message = "Similarity clustering starting for {ItemCount} items (Threshold={SimilarityThreshold:F2})")]
        private partial void LogClusteringStarting(int itemCount, double similarityThreshold);

        [LoggerMessage(Level = LogLevel.Information, Message = "Similarity clustering completed: {ItemCount} items → {GroupCount} groups ({DuplicateGroupCount} duplicate groups) in {ElapsedSeconds:F1}s")]
        private partial void LogClusteringCompleted(int itemCount, int groupCount, int duplicateGroupCount, double elapsedSeconds);

        [LoggerMessage(Level = LogLevel.Error, Message = "Similarity clustering failed for {ItemCount} items")]
        private partial void LogClusteringFailed(int itemCount, Exception ex);

        [LoggerMessage(Level = LogLevel.Debug, Message = "Similarity matrix calculated: {ItemCount} items, {ComparisonCount} comparisons in {ElapsedSeconds:F1}s")]
        private partial void LogSimilarityMatrixCalculated(int itemCount, long comparisonCount, double elapsedSeconds);

        [LoggerMessage(Level = LogLevel.Debug, Message = "Hierarchical clustering finished: {ItemCount} items, {MergeCount} merges, {ResultGroupCount} final groups")]
        private partial void LogHierarchicalClusteringCompleted(int itemCount, int mergeCount, int resultGroupCount);

        #endregion Logging
    }
}