using DeDupe.Models.Configuration;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace DeDupe.Services
{
    public interface IModelDownloadService
    {
        /// <summary>
        /// Get local file path for model.
        /// </summary>
        string? GetLocalModelPath(BundledModelInfo model);

        /// <summary>
        /// Check if model file exists.
        /// </summary>
        bool IsModelAvailable(BundledModelInfo model);

        /// <summary>
        /// Download model to local cache.
        /// </summary>
        Task DownloadModelAsync(BundledModelInfo model, IProgress<ModelDownloadProgress>? progress = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Delete cached model.
        /// </summary>
        bool DeleteCachedModel(BundledModelInfo model);

        /// <summary>
        /// Ensure model is available, downloading if needed.
        /// </summary>
        Task<string> EnsureModelAvailableAsync(BundledModelInfo model, IProgress<ModelDownloadProgress>? progress = null, CancellationToken cancellationToken = default);
    }
}