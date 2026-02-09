using DeDupe.Models.Configuration;
using System.Collections.Generic;

namespace DeDupe.Services.Model
{
    /// <summary>
    /// Service interface for accessing bundled ONNX models.
    /// </summary>
    public interface IBundledModelService
    {
        /// <summary>
        /// All bundled models in the application.
        /// </summary>
        IReadOnlyList<BundledModelInfo> AvailableModels { get; }

        /// <summary>
        /// Get full file path of bundled model.
        /// </summary>
        string GetModelPath(string modelId);

        /// <summary>
        /// Check if specific model file exists.
        /// </summary>
        bool IsModelAvailable(string modelId);

        /// <summary>
        /// Get model metadata by ID.
        /// </summary>
        BundledModelInfo? GetModelInfo(string modelId);

        /// <summary>
        /// Check if file needs to be downloaded.
        /// </summary>
        bool NeedsDownload(string modelId);
    }
}