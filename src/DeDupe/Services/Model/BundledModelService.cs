using DeDupe.Models.Configuration;
using System.Collections.Generic;

namespace DeDupe.Services.Model
{
    /// <summary>
    /// Service for accessing bundled ONNX models.
    /// </summary>
    public class BundledModelService(IModelDownloadService downloadService) : IBundledModelService
    {
        private readonly IModelDownloadService _downloadService = downloadService;

        public IReadOnlyList<BundledModelInfo> AvailableModels => BundledModelRegistry.All;

        public string GetModelPath(string modelId)
        {
            BundledModelInfo? model = BundledModelRegistry.GetById(modelId);
            if (model is null)
            {
                return string.Empty;
            }

            return _downloadService.GetLocalModelPath(model) ?? string.Empty;
        }

        public bool IsModelAvailable(string modelId)
        {
            BundledModelInfo? model = BundledModelRegistry.GetById(modelId);
            if (model is null)
            {
                return false;
            }

            return _downloadService.IsModelAvailable(model);
        }

        public bool NeedsDownload(string modelId)
        {
            BundledModelInfo? model = BundledModelRegistry.GetById(modelId);
            if (model is null)
            {
                return false;
            }

            return !_downloadService.IsModelAvailable(model);
        }

        public BundledModelInfo? GetModelInfo(string modelId) => BundledModelRegistry.GetById(modelId);
    }
}