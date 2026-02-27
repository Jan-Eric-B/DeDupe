using DeDupe.Enums;
using DeDupe.Models;
using DeDupe.Models.Configuration;
using DeDupe.Models.Results;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DeDupe.Services.Analysis
{
    /// <summary>
    /// Extracts feature vectors from images using an ONNX model for duplicate detection.
    /// </summary>
    public interface IFeatureExtractionService : IDisposable
    {
        string ModelPath { get; }

        bool IsInitialized { get; }

        bool IsGpuEnabled { get; }

        int BatchSize { get; }

        TensorLayout TensorLayout { get; }

        /// <summary>
        /// Expected model input dimensions.
        /// </summary>
        (int Channels, int Height, int Width)? ExpectedDimensions { get; }

        /// <summary>
        /// Loads a model and prepares the inference session. Disposes any previously loaded session.
        /// </summary>
        Task InitializeAsync(string modelPath, bool preferGpu, int batchSize, TensorLayout tensorLayout);

        /// <summary>
        /// Extracts feature vectors from items that have been processed.
        /// </summary>
        Task ExtractFeaturesAsync(IReadOnlyCollection<AnalysisItem> items, NormalizationSettings normalization, IProgress<ProgressInfo>? progress = null, CancellationToken cancellationToken = default);
    }
}