using DeDupe.Models.Configuration;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace DeDupe.Services.Model
{
    /// <summary>
    /// Service for managing downloading and caching of bundled model files.
    /// </summary>
    public interface IModelDownloadService
    {
        /// <summary>
        /// Returns local cache path if model file exists.
        /// </summary>
        string? GetLocalModelPath(BundledModelInfo model);

        /// <summary>
        /// Checks if model file exists in local cache.
        /// </summary>
        bool IsModelAvailable(BundledModelInfo model);

        /// <summary>
        /// Downloads model to local cache. Verifies SHA-256 hash if provided.
        /// </summary>
        Task DownloadModelAsync(BundledModelInfo model, IProgress<ModelDownloadProgress>? progress = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns local model path, downloading model first if not already cached.
        /// </summary>
        Task<string> EnsureModelAvailableAsync(BundledModelInfo model, IProgress<ModelDownloadProgress>? progress = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes cached model file.
        /// </summary>
        bool DeleteCachedModel(BundledModelInfo model);
    }
}