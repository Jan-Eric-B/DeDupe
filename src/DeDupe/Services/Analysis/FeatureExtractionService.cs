using DeDupe.Models;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DeDupe.Services.Analysis
{
    /// <summary>
    /// Service for extracting feature vectors from preprocessed images.
    /// </summary>
    public sealed partial class FeatureExtractionService : IFeatureExtractionService
    {
        #region Fields

        private InferenceSession? _session;
        private string? _inputName;
        private string? _outputName;
        private int[]? _inputDimensions;
        private string _modelPath = string.Empty;
        private bool _disposed;

        #endregion Fields

        #region Properties

        public string ModelPath => _modelPath;

        public bool IsInitialized => _session != null;

        #endregion Properties

        #region Initialization

        public async Task InitializeAsync(string modelPath)
        {
            if (string.IsNullOrEmpty(modelPath))
            {
                throw new ArgumentException("Model path cannot be null or empty.", nameof(modelPath));
            }

            if (!File.Exists(modelPath))
            {
                throw new ArgumentException($"Model file does not exist: {modelPath}", nameof(modelPath));
            }

            try
            {
                _session?.Dispose();

                SessionOptions sessionOptions = new();
                sessionOptions.AppendExecutionProvider_CPU();

                _session = new InferenceSession(modelPath, sessionOptions);
                _modelPath = modelPath;

                await Task.Run(ExtractModelMetadata);
            }
            catch (Exception ex) when (ex is not ArgumentException)
            {
                throw new InvalidOperationException($"Failed to initialize ONNX model: {ex.Message}", ex);
            }
        }

        private void ExtractModelMetadata()
        {
            if (_session == null) return;

            IReadOnlyDictionary<string, NodeMetadata> inputMetadata = _session.InputMetadata;
            if (inputMetadata.Count > 0)
            {
                KeyValuePair<string, NodeMetadata> firstInput = inputMetadata.First();
                _inputName = firstInput.Key;
                _inputDimensions = [.. firstInput.Value.Dimensions];
            }

            IReadOnlyDictionary<string, NodeMetadata> outputMetadata = _session.OutputMetadata;
            if (outputMetadata.Count > 0)
            {
                KeyValuePair<string, NodeMetadata> firstOutput = outputMetadata.First();
                _outputName = firstOutput.Key;
            }
        }

        #endregion Initialization

        #region Feature Extraction

        /// <inheritdoc />
        public async Task ExtractFeaturesAsync(IEnumerable<AnalysisItem> items, (float MeanR, float MeanG, float MeanB, float StdR, float StdG, float StdB) normalization)
        {
            if (!IsInitialized)
            {
                throw new InvalidOperationException("Service is not initialized. Call InitializeAsync first.");
            }

            ArgumentNullException.ThrowIfNull(items);

            List<AnalysisItem> itemList = [.. items.Where(i => i.IsProcessed)];

            if (itemList.Count == 0)
            {
                return;
            }

            using SemaphoreSlim semaphore = new(Environment.ProcessorCount);

            await Parallel.ForEachAsync(itemList, new ParallelOptions
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount,
                CancellationToken = CancellationToken.None
            },
            async (item, ct) =>
            {
                await semaphore.WaitAsync(ct);
                try
                {
                    await ExtractFeaturesFromItemAsync(item, normalization);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error extracting features from {item.ProcessedFilePath}: {ex.Message}");
                }
                finally
                {
                    semaphore.Release();
                }
            });
        }

        private async Task ExtractFeaturesFromItemAsync(AnalysisItem item, (float MeanR, float MeanG, float MeanB, float StdR, float StdG, float StdB) normalization)
        {
            if (_session == null || _inputName == null || _outputName == null || _inputDimensions == null)
            {
                return;
            }

            if (string.IsNullOrEmpty(item.ProcessedFilePath))
            {
                return;
            }

            try
            {
                // Load and preprocess image
                using Image<Rgb24> image = await Image.LoadAsync<Rgb24>(item.ProcessedFilePath);

                // Get expected input dimensions from model
                int batchSize = _inputDimensions[0] == -1 ? 1 : _inputDimensions[0];
                int channels = _inputDimensions[1];
                int height = _inputDimensions[2];
                int width = _inputDimensions[3];

                DenseTensor<float> inputTensor = ConvertImageToTensor(image, batchSize, channels, height, width, normalization);

                // Create input for model
                List<NamedOnnxValue> inputs =
                [
                    NamedOnnxValue.CreateFromTensor(_inputName, inputTensor)
                ];

                // Run inference
                using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results = _session.Run(inputs);
                Tensor<float> output = results[0].AsTensor<float>();

                // Extract feature vector
                float[] featureVector = [.. output];
                int[] featureDimensions = output.Dimensions.ToArray();

                item.SetFeatures(featureVector, featureDimensions);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"Failed to extract features from {item.ProcessedFilePath}: {ex.Message}");
            }
        }

        /// <summary>
        /// Converts image to tensor for ONNX model input.
        /// </summary>
        private static DenseTensor<float> ConvertImageToTensor(Image<Rgb24> image, int batchSize, int channels, int height, int width, (float MeanR, float MeanG, float MeanB, float StdR, float StdG, float StdB) normalization)
        {
            DenseTensor<float> tensor = new([batchSize, channels, height, width]);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Rgb24 pixel = image[x, y];

                    float r = (pixel.R / 255.0f - normalization.MeanR) / normalization.StdR;
                    float g = (pixel.G / 255.0f - normalization.MeanG) / normalization.StdG;
                    float b = (pixel.B / 255.0f - normalization.MeanB) / normalization.StdB;

                    // CHW format
                    tensor[0, 0, y, x] = r; // Red
                    tensor[0, 1, y, x] = g; // Green
                    tensor[0, 2, y, x] = b; // Blue
                }
            }

            return tensor;
        }

        #endregion Feature Extraction

        #region IDisposable

        public void Dispose()
        {
            if (!_disposed)
            {
                _session?.Dispose();
                _session = null;
                _disposed = true;
            }
        }

        #endregion IDisposable
    }
}