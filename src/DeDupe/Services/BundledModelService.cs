using DeDupe.Models.Configuration;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace DeDupe.Services
{
    /// <summary>
    /// Service for accessing bundled ONNX models.
    /// </summary>
    public class BundledModelService : IBundledModelService
    {
        private readonly Dictionary<string, string> _pathCache = [];
        private readonly Dictionary<string, bool> _availabilityCache = [];

        #region Properties

        /// <inheritdoc />
        public IReadOnlyList<BundledModelInfo> AvailableModels => BundledModelRegistry.All;

        /// <inheritdoc />
        public BundledModelInfo DefaultModel => BundledModelRegistry.ResNet50;

        #endregion Properties

        #region Public Methods

        /// <inheritdoc />
        public string GetModelPath(string modelId)
        {
            if (string.IsNullOrEmpty(modelId))
            {
                Debug.WriteLine("Model ID is null or empty");
                return string.Empty;
            }

            if (_pathCache.TryGetValue(modelId, out string? cachedPath))
            {
                return cachedPath;
            }

            BundledModelInfo? modelInfo = BundledModelRegistry.GetById(modelId);
            if (modelInfo == null)
            {
                Debug.WriteLine($"Unknown model ID: {modelId}");
                return string.Empty;
            }

            string path = ResolvePath(modelInfo.RelativePath);
            _pathCache[modelId] = path;

            return path;
        }

        /// <inheritdoc />
        public bool IsModelAvailable(string modelId)
        {
            if (string.IsNullOrEmpty(modelId))
            {
                return false;
            }

            if (_availabilityCache.TryGetValue(modelId, out bool cached))
            {
                return cached;
            }

            string path = GetModelPath(modelId);
            bool available = !string.IsNullOrEmpty(path) && File.Exists(path);

            _availabilityCache[modelId] = available;
            Debug.WriteLine($"Model '{modelId}' available: {available}");

            return available;
        }

        /// <inheritdoc />
        public BundledModelInfo? GetModelInfo(string modelId)
        {
            return BundledModelRegistry.GetById(modelId);
        }

        #endregion Public Methods

        #region Private Methods

        private static string ResolvePath(string relativePath)
        {
            string basePath = AppContext.BaseDirectory;
            string modelPath = Path.Combine(basePath, relativePath);

            Debug.WriteLine($"Checking path: {modelPath}");

            if (File.Exists(modelPath))
            {
                Debug.WriteLine($"Found model at: {modelPath}");
                return modelPath;
            }

            Debug.WriteLine($"Model not found at: {modelPath}");
            return string.Empty;
        }

        #endregion Private Methods
    }
}