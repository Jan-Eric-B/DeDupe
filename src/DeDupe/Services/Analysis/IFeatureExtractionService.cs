using DeDupe.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DeDupe.Services.Analysis
{
    /// <summary>
    /// Service for extracting feature vectors from preprocessed images using ONNX models.
    /// </summary>
    public interface IFeatureExtractionService : IDisposable
    {
        /// <summary>
        /// Path to loaded model.
        /// </summary>
        string ModelPath { get; }

        /// <summary>
        /// Service has been initialized with model.
        /// </summary>
        bool IsInitialized { get; }

        /// <summary>
        /// Initialize service with ONNX model.
        /// </summary>
        Task InitializeAsync(string modelPath);

        /// <summary>
        /// Extract features from all processed analysis items.
        /// </summary>
        Task ExtractFeaturesAsync(IReadOnlyCollection<AnalysisItem> items, (float MeanR, float MeanG, float MeanB, float StdR, float StdG, float StdB) normalization, CancellationToken cancellationToken = default);
    }
}