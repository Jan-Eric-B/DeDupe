using DeDupe.Localization;
using DeDupe.Models;
using DeDupe.Models.Analysis;
using DeDupe.Models.Results;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

namespace DeDupe.Services.Analysis
{
    /// <inheritdoc />
    public partial class SimilarityAnalysisService(ILogger<SimilarityAnalysisService> logger) : ISimilarityAnalysisService
    {
        private readonly ILogger<SimilarityAnalysisService> _logger = logger;

        /// <inheritdoc />
        public async Task<SimilarityResult> ClusterAsync(IEnumerable<AnalysisItem> items, double similarityThreshold, ILocalizer localizer, IProgress<ProgressInfo>? progress = null, CancellationToken cancellationToken = default)
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
                progress?.Report(new ProgressInfo(0, 100, localizer.GetLocalizedString("SimilarityAnalysis_Progress_BuildingMatrix")));

                // Build similarity matrix
                double[,] similarityMatrix = await Task.Run(() => CalculateSimilarityMatrix(itemList, localizer, progress, cancellationToken), cancellationToken);

                // Hierarchical clustering
                progress?.Report(new ProgressInfo(70, 100, localizer.GetLocalizedString("SimilarityAnalysis_Progress_Clustering")));

                List<SimilarityGroup> clusters = await Task.Run(() => PerformHierarchicalClustering(itemList, similarityMatrix, similarityThreshold, localizer, progress, cancellationToken), cancellationToken);

                // Calculate cluster statistics
                progress?.Report(new ProgressInfo(95, 100, localizer.GetLocalizedString("SimilarityAnalysis_Progress_Statistics")));
                CalculateClusterStatistics(clusters, itemList, similarityMatrix);

                stopwatch.Stop();
                int duplicateGroupCount = clusters.Count(c => c.Count > 1);
                LogClusteringCompleted(itemList.Count, clusters.Count, duplicateGroupCount, stopwatch.Elapsed.TotalSeconds);

                progress?.Report(new ProgressInfo(100, 100, localizer.GetLocalizedString("SimilarityAnalysis_Progress_Complete")));

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

            int length = vectorA.Length;
            float dotProduct = 0f;
            float magnitudeA = 0f;
            float magnitudeB = 0f;

            int i = 0;

            // SIMD-accelerated path using hardware vectors
            int vectorSize = Vector<float>.Count;
            if (Vector.IsHardwareAccelerated && length >= vectorSize)
            {
                Vector<float> sumDot = Vector<float>.Zero;
                Vector<float> sumA = Vector<float>.Zero;
                Vector<float> sumB = Vector<float>.Zero;

                int lastBlockIndex = length - (length % vectorSize);

                for (; i < lastBlockIndex; i += vectorSize)
                {
                    Vector<float> va = new(vectorA, i);
                    Vector<float> vb = new(vectorB, i);

                    sumDot += va * vb;
                    sumA += va * va;
                    sumB += vb * vb;
                }

                dotProduct = Vector.Dot(sumDot, Vector<float>.One);
                magnitudeA = Vector.Dot(sumA, Vector<float>.One);
                magnitudeB = Vector.Dot(sumB, Vector<float>.One);
            }

            // Scalar remainder
            for (; i < length; i++)
            {
                dotProduct += vectorA[i] * vectorB[i];
                magnitudeA += vectorA[i] * vectorA[i];
                magnitudeB += vectorB[i] * vectorB[i];
            }

            double magA = Math.Sqrt(magnitudeA);
            double magB = Math.Sqrt(magnitudeB);

            if (magA == 0.0 || magB == 0.0)
            {
                return 0.0;
            }

            return dotProduct / (magA * magB);
        }

        private double[,] CalculateSimilarityMatrix(List<AnalysisItem> items, ILocalizer localizer, IProgress<ProgressInfo>? progress, CancellationToken cancellationToken)
        {
            int count = items.Count;
            double[,] matrix = new double[count, count];

            long totalComparisons = (long)count * (count - 1) / 2;
            Stopwatch stopwatch = Stopwatch.StartNew();

            string matrixStatusText = localizer.GetLocalizedString("SimilarityAnalysis_Progress_BuildingMatrix");
            string matrixRowDetailFormat = localizer.GetLocalizedString("SimilarityAnalysis_Progress_MatrixRowDetail");

            int completedRows = 0;
            int lastReportedPercentage = -1;

            // Pre-extract source IDs for fast lookup
            Guid[] sourceIds = new Guid[count];
            for (int i = 0; i < count; i++)
            {
                sourceIds[i] = items[i].SourceId;
            }

            // Parallel row computation for the upper triangle
            Parallel.For(0, count, new ParallelOptions { CancellationToken = cancellationToken }, i =>
            {
                matrix[i, i] = 1.0;

                float[] vectorI = items[i].FeatureVector!;
                Guid sourceI = sourceIds[i];

                for (int j = i + 1; j < count; j++)
                {
                    if (sourceI == sourceIds[j])
                    {
                        // Same source (video frames) — no similarity
                        matrix[i, j] = 0.0;
                        matrix[j, i] = 0.0;
                    }
                    else
                    {
                        double similarity = CalculateCosineSimilarity(vectorI, items[j].FeatureVector!);
                        matrix[i, j] = similarity;
                        matrix[j, i] = similarity;
                    }
                }

                int done = Interlocked.Increment(ref completedRows);
                int percentage = (int)((double)done / count * 70);
                if (percentage > lastReportedPercentage)
                {
                    Interlocked.Exchange(ref lastReportedPercentage, percentage);
                    progress?.Report(new ProgressInfo(percentage, 100, matrixStatusText, string.Format(matrixRowDetailFormat, $"{done:N0}", $"{count:N0}")));
                }
            });

            stopwatch.Stop();
            LogSimilarityMatrixCalculated(count, totalComparisons, stopwatch.Elapsed.TotalSeconds);

            return matrix;
        }

        /// <summary>
        /// Perform agglomerative hierarchical clustering using a max-heap priority queue.
        /// This reduces the naive O(N³) approach to O(N² log N) by maintaining a sorted
        /// set of candidate merges instead of rescanning all pairs each iteration.
        /// </summary>
        private List<SimilarityGroup> PerformHierarchicalClustering(List<AnalysisItem> items, double[,] similarityMatrix, double similarityThreshold, ILocalizer localizer, IProgress<ProgressInfo>? progress, CancellationToken cancellationToken)
        {
            int count = items.Count;

            // Initialize each item as its own cluster
            List<int>[] clusters = new List<int>[count];
            bool[] active = new bool[count];

            // Cache source IDs per cluster to avoid repeated HashSet allocations
            HashSet<Guid>[] clusterSourceIds = new HashSet<Guid>[count];

            for (int i = 0; i < count; i++)
            {
                clusters[i] = [i];
                active[i] = true;
                clusterSourceIds[i] = [items[i].SourceId];
            }

            // Build max-heap of all candidate pairs above threshold
            // Using a sorted set with a comparer that orders by similarity descending
            PriorityQueue<(int I, int J), double> heap = new(Comparer<double>.Create((a, b) => b.CompareTo(a)));

            for (int i = 0; i < count; i++)
            {
                for (int j = i + 1; j < count; j++)
                {
                    double sim = similarityMatrix[i, j];
                    if (sim >= similarityThreshold)
                    {
                        heap.Enqueue((i, j), sim);
                    }
                }
            }

            int mergeOperations = 0;
            int maxPossibleMerges = count - 1;

            string clusteringStatusText = localizer.GetLocalizedString("SimilarityAnalysis_Progress_Clustering");
            string mergeDetailFormat = localizer.GetLocalizedString("SimilarityAnalysis_Progress_MergeDetail");

            // Process merges from highest similarity downward
            while (heap.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();

                heap.TryDequeue(out var pair, out double pairSimilarity);

                // Skip if either cluster has been merged away
                if (!active[pair.I] || !active[pair.J])
                {
                    continue;
                }

                // Recalculate actual inter-cluster similarity (average linkage)
                // since clusters may have grown since this pair was enqueued
                double actualSimilarity = CalculateClusterSimilarity(clusters[pair.I], clusters[pair.J], similarityMatrix);

                if (actualSimilarity < similarityThreshold)
                {
                    continue;
                }

                // Check source constraint using cached sets
                if (SourceSetsOverlap(clusterSourceIds[pair.I], clusterSourceIds[pair.J]))
                {
                    continue;
                }

                // Merge J into I
                clusters[pair.I].AddRange(clusters[pair.J]);
                clusterSourceIds[pair.I].UnionWith(clusterSourceIds[pair.J]);
                active[pair.J] = false;

                mergeOperations++;

                int percentage = 70 + (int)((double)mergeOperations / maxPossibleMerges * 25);
                progress?.Report(new ProgressInfo(Math.Min(percentage, 95), 100, clusteringStatusText, string.Format(mergeDetailFormat, $"{mergeOperations:N0}")));

                // Re-enqueue pairs between the merged cluster I and all other active clusters
                for (int k = 0; k < count; k++)
                {
                    if (!active[k] || k == pair.I)
                    {
                        continue;
                    }

                    double sim = CalculateClusterSimilarity(clusters[pair.I], clusters[k], similarityMatrix);
                    if (sim >= similarityThreshold)
                    {
                        int lo = Math.Min(pair.I, k);
                        int hi = Math.Max(pair.I, k);
                        heap.Enqueue((lo, hi), sim);
                    }
                }
            }

            // Convert to SimilarityGroup objects
            List<SimilarityGroup> result = [];
            int clusterId = 0;
            int duplicateGroupNumber = 1;

            for (int i = 0; i < count; i++)
            {
                if (!active[i])
                {
                    continue;
                }

                List<AnalysisItem> clusterItems = [.. clusters[i].Select(idx => items[idx])];

                string? groupName = clusterItems.Count > 1 ? string.Format(localizer.GetLocalizedString("SimilarityAnalysis_GroupDefaultName"), duplicateGroupNumber++) : null;

                SimilarityGroup group = new(clusterId++, clusterItems, groupName);
                result.Add(group);
            }

            LogHierarchicalClusteringCompleted(count, mergeOperations, result.Count);

            return result;
        }

        /// <summary>
        /// Fast overlap check using pre-cached HashSets instead of allocating new ones.
        /// </summary>
        private static bool SourceSetsOverlap(HashSet<Guid> setA, HashSet<Guid> setB)
        {
            // Iterate over the smaller set for efficiency
            HashSet<Guid> smaller = setA.Count <= setB.Count ? setA : setB;
            HashSet<Guid> larger = setA.Count <= setB.Count ? setB : setA;

            foreach (Guid id in smaller)
            {
                if (larger.Contains(id))
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