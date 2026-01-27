using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage;

namespace DeDupe.Services
{
    /// <summary>
    /// Service for accessing bundled ONNX models.
    /// </summary>
    public class BundledModelService : IBundledModelService
    {
        private const string BundledModelRelativePath = @"Resources\Models\resnet50-v2-7.onnx";
        private const string ModelDisplayName = "ResNet50-v2";

        private string? _cachedModelPath;
        private bool? _isAvailable;

        public string BundledModelPath
        {
            get
            {
                if (_cachedModelPath == null)
                {
                    _cachedModelPath = GetBundledModelPath();
                }
                return _cachedModelPath;
            }
        }

        public string BundledModelName => ModelDisplayName;

        public bool IsBundledModelAvailable
        {
            get
            {
                if (!_isAvailable.HasValue)
                {
                    _isAvailable = !string.IsNullOrEmpty(BundledModelPath) && File.Exists(BundledModelPath);
                    Debug.WriteLine($"BundledModelService: IsBundledModelAvailable = {_isAvailable}, Path = {BundledModelPath}");
                }
                return _isAvailable.Value;
            }
        }

        public async Task<bool> ValidateBundledModelAsync()
        {
            try
            {
                if (IsBundledModelAvailable)
                {
                    StorageFile file = await StorageFile.GetFileFromPathAsync(BundledModelPath);
                    Windows.Storage.FileProperties.BasicProperties props = await file.GetBasicPropertiesAsync();
                    return props.Size > 1024;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error validating bundled model: {ex.Message}");
            }

            return false;
        }

        private static string GetBundledModelPath()
        {
            string basePath = AppContext.BaseDirectory;
            string modelPath = Path.Combine(basePath, BundledModelRelativePath);
            Debug.WriteLine($"BundledModelService: Trying AppContext.BaseDirectory: {modelPath}");

            if (File.Exists(modelPath))
            {
                Debug.WriteLine($"BundledModelService: Found at AppContext.BaseDirectory");
                return modelPath;
            }

            Debug.WriteLine($"BundledModelService: Model not found in any location");
            return string.Empty;
        }
    }
}