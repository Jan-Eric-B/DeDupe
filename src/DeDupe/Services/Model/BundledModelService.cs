using DeDupe.Models.Configuration;
using System.Collections.Generic;

namespace DeDupe.Services.Model
{
    /// <inheritdoc/>
    public class BundledModelService(IModelDownloadService downloadService) : IBundledModelService
    {
        private readonly IModelDownloadService _downloadService = downloadService;

        /// <inheritdoc/>
        public IReadOnlyList<BundledModelInfo> AvailableModels => BundledModelRegistry.All;

        /// <inheritdoc/>
        public string GetModelPath(string modelId)
        {
            BundledModelInfo? model = BundledModelRegistry.GetById(modelId);
            if (model is null)
            {
                return string.Empty;
            }

            return _downloadService.GetLocalModelPath(model) ?? string.Empty;
        }

        /// <inheritdoc/>
        public bool IsModelAvailable(string modelId)
        {
            BundledModelInfo? model = BundledModelRegistry.GetById(modelId);
            if (model is null)
            {
                return false;
            }

            return _downloadService.IsModelAvailable(model);
        }

        /// <inheritdoc/>
        public bool NeedsDownload(string modelId)
        {
            BundledModelInfo? model = BundledModelRegistry.GetById(modelId);
            if (model is null)
            {
                return false;
            }

            return !_downloadService.IsModelAvailable(model);
        }

        /// <inheritdoc/>
        public BundledModelInfo? GetModelInfo(string modelId) => BundledModelRegistry.GetById(modelId);
    }
}