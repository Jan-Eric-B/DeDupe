using DeDupe.Models.Analysis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DeDupe.Services.Analysis
{
    public class SimilarityAnalysisService
    {
        /// <summary>
        /// Hierarchical clustering on extracted features using cosine similarity
        /// </summary>
        public static async Task<SimilarityResult> PerformClusteringAsync(List<ExtractedFeatures> extractedFeatures, double similarityThreshold)
        {
            if (extractedFeatures == null || extractedFeatures.Count == 0)
            {
                return new SimilarityResult([], similarityThreshold, 0);
            }

            try
            {
                // Step 1 - Similarity matrix
                double[,] similarityMatrix = await CalculateSimilarityMatrixAsync(extractedFeatures);

                // Step 2 - Hierarchical clustering
                List<ImageCluster> clusters = await PerformHierarchicalClusteringAsync(extractedFeatures, similarityMatrix, similarityThreshold);

                // Step 3 - Cluster statistics
                CalculateClusterStatistics(clusters);

                return new SimilarityResult(clusters, similarityThreshold, extractedFeatures.Count);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error during clustering: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Calculate cosine similarity matrix for all feature vectors
        /// </summary>
        private static async Task<double[,]> CalculateSimilarityMatrixAsync(List<ExtractedFeatures> features)
        {
            int count = features.Count;
            double[,] matrix = new double[count, count];

            int completedComparisons = 0;

            // Calculate similarities (symmetric matrix - only upper triangle)
            for (int i = 0; i < count; i++)
            {
                matrix[i, i] = 1.0; // Similarity with self is always 1.0

                for (int j = i + 1; j < count; j++)
                {
                    double similarity = CalculateCosineSimilarity(features[i].FeatureVector, features[j].FeatureVector);
                    matrix[i, j] = similarity;
                    matrix[j, i] = similarity; // Matrix is symmetric

                    completedComparisons++;

                    // Allow cancellation and UI updates
                    if (completedComparisons % 1000 == 0)
                    {
                        await Task.Delay(1);
                    }
                }
            }

            return matrix;
        }

        /// <summary>
        /// Calculate cosine similarity between two feature vectors
        /// </summary>
        private static double CalculateCosineSimilarity(float[] vectorA, float[] vectorB)
        {
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
                return 0.0; // Cannot calculate similarity for zero vectors
            }

            return dotProduct / (magnitudeA * magnitudeB);
        }

        /// <summary>
        /// Perform agglomerative hierarchical clustering
        /// </summary>
        private static async Task<List<ImageCluster>> PerformHierarchicalClusteringAsync(List<ExtractedFeatures> features, double[,] similarityMatrix, double similarityThreshold)
        {
            int count = features.Count;

            // Initialize each feature as its own cluster
            List<List<int>> clusters = [];
            for (int i = 0; i < count; i++)
            {
                clusters.Add([i]);
            }

            // Keep track of active clusters
            bool[] activeClusters = new bool[count];
            for (int i = 0; i < count; i++)
            {
                activeClusters[i] = true;
            }

            int mergeOperations = 0;

            // Agglomerative clustering - merge closest clusters
            while (true)
            {
                // Find pair of clusters with highest similarity
                double maxSimilarity = -1.0;
                int clusterI = -1, clusterJ = -1;

                for (int i = 0; i < clusters.Count; i++)
                {
                    if (!activeClusters[i])
                    {
                        continue;
                    }

                    for (int j = i + 1; j < clusters.Count; j++)
                    {
                        if (!activeClusters[j])
                        {
                            continue;
                        }

                        double similarity = CalculateClusterSimilarity(clusters[i], clusters[j], similarityMatrix);

                        if (similarity > maxSimilarity)
                        {
                            maxSimilarity = similarity;
                            clusterI = i;
                            clusterJ = j;
                        }
                    }
                }

                // Stop clustering - No pair found or similarity below threshold
                if (clusterI == -1 || maxSimilarity < similarityThreshold)
                {
                    break;
                }

                // Merge two clusters
                clusters[clusterI].AddRange(clusters[clusterJ]);
                activeClusters[clusterJ] = false; // Mark cluster j as inactive

                mergeOperations++;

                // Allow UI updates
                if (mergeOperations % 10 == 0)
                {
                    await Task.Delay(1);
                }
            }

            // Convert clusters to ImageCluster objects
            List<ImageCluster> imageClusters = [];
            int clusterId = 0;

            for (int i = 0; i < clusters.Count; i++)
            {
                if (!activeClusters[i])
                {
                    continue;
                }

                List<ExtractedFeatures> clusterFeatures = [.. clusters[i].Select(idx => features[idx])];
                ImageCluster imageCluster = new(clusterId++, clusterFeatures);
                imageClusters.Add(imageCluster);
            }

            return imageClusters;
        }

        /// <summary>
        /// Calculate similarity between two clusters using average linkage
        /// </summary>
        private static double CalculateClusterSimilarity(List<int> clusterA, List<int> clusterB, double[,] similarityMatrix)
        {
            double totalSimilarity = 0.0;
            int pairCount = 0;

            foreach (int i in clusterA)
            {
                foreach (int j in clusterB)
                {
                    totalSimilarity += similarityMatrix[i, j];
                    pairCount++;
                }
            }

            return pairCount > 0 ? totalSimilarity / pairCount : 0.0;
        }

        /// <summary>
        /// Calculate statistics for each cluster
        /// </summary>
        private static void CalculateClusterStatistics(List<ImageCluster> clusters)
        {
            foreach (ImageCluster cluster in clusters)
            {
                if (cluster.Count <= 1)
                {
                    cluster.AverageSimilarity = 1.0; // Single image clusters
                    continue;
                }

                double totalSimilarity = 0.0;
                int pairCount = 0;

                // Calculate average pairwise similarity within cluster
                for (int i = 0; i < cluster.Images.Count; i++)
                {
                    for (int j = i + 1; j < cluster.Images.Count; j++)
                    {
                        // Find indices of images in original similarity matrix
                        // TODO - Maintain index mappings
                        double similarity = CalculateCosineSimilarity(cluster.Images[i].FeatureVector, cluster.Images[j].FeatureVector);

                        totalSimilarity += similarity;
                        pairCount++;
                    }
                }

                cluster.AverageSimilarity = pairCount > 0 ? totalSimilarity / pairCount : 1.0;
            }
        }
    }
}