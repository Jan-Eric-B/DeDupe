using DeDupe.Models.Results;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DeDupe.Services.Analysis
{
    /// <summary>
    /// Interface for feature extraction from preprocessed images.
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
        /// Whether GPU acceleration is enabled.
        /// </summary>
        bool IsGpuEnabled { get; }

        /// <summary>
        /// Current batch size for inference.
        /// </summary>
        int BatchSize { get; }

        /// <summary>
        /// Expected input dimensions from the model (Channels, Height, Width).
        /// </summary>
        (int Channels, int Height, int Width)? ExpectedDimensions { get; }

        /// <summary>
        /// Initialize service with ONNX model.
        /// </summary>
        Task InitializeAsync(string modelPath, bool preferGpu = true, int batchSize = 16);

        /// <summary>
        /// Initialize service with default settings.
        /// </summary>
        Task InitializeAsync(string modelPath);

        /// <summary>
        /// Extract feature vectors from processed items.
        /// </summary>
        Task ExtractFeaturesAsync(IReadOnlyCollection<AnalysisItem> items, (float MeanR, float MeanG, float MeanB, float StdR, float StdG, float StdB) normalization, CancellationToken cancellationToken = default);
    }
}