using DeDupe.Models.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.IO;

namespace DeDupe.Services.Model
{
    /// <inheritdoc/>
    public partial class BundledModelService(ILogger<BundledModelService> logger) : IBundledModelService
    {
        private readonly ILogger<BundledModelService> _logger = logger;

        private static readonly string _modelPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Models", BundledModelInfo.FileName);

        /// <inheritdoc/>
        public string GetModelPath()
        {
            if (!File.Exists(_modelPath))
            {
                LogBundledModelNotFound(_modelPath);
            }

            return _modelPath;
        }

        /// <inheritdoc/>
        public bool IsModelAvailable()
        {
            bool exists = File.Exists(_modelPath);

            if (!exists)
            {
                LogBundledModelNotFound(_modelPath);
            }

            return exists;
        }

        #region Logging

        [LoggerMessage(Level = LogLevel.Warning, Message = "Bundled model not found at expected path '{ModelPath}'")]
        private partial void LogBundledModelNotFound(string modelPath);

        #endregion Logging
    }
}