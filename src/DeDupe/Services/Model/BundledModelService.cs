using DeDupe.Models.Configuration;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;

namespace DeDupe.Services.Model
{
    /// <inheritdoc/>
    public partial class BundledModelService(IModelDownloadService downloadService, ILogger<BundledModelService> logger) : IBundledModelService
    {
        private readonly IModelDownloadService _downloadService = downloadService;
        private readonly ILogger<BundledModelService> _logger = logger;

        /// <inheritdoc/>
        public IReadOnlyList<BundledModelInfo> AvailableModels => BundledModelRegistry.All;

        /// <inheritdoc/>
        public string GetModelPath(string modelId)
        {
            BundledModelInfo? model = BundledModelRegistry.GetById(modelId);
            if (model is null)
            {
                LogModelNotFound(modelId);
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
                LogModelNotFound(modelId);
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
                LogModelNotFound(modelId);
                return false;
            }

            return !_downloadService.IsModelAvailable(model);
        }

        /// <inheritdoc/>
        public BundledModelInfo? GetModelInfo(string modelId) => BundledModelRegistry.GetById(modelId);

        #region Logging

        [LoggerMessage(Level = LogLevel.Warning, Message = "Bundled model lookup failed, unknown ModelId '{ModelId}'")]
        private partial void LogModelNotFound(string modelId);

        #endregion Logging
    }
}