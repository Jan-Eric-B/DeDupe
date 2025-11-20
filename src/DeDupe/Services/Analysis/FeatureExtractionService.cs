using DeDupe.Models;
using DeDupe.Models.Analysis;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DeDupe.Services.Analysis
{
    public partial class FeatureExtractionService : IDisposable
    {
        #region Fields

        private InferenceSession? _session;
        private string? _inputName;
        private string? _outputName;
        private int[]? _inputDimensions;

        // Normalization parameters
        private float _meanR = 0.485f;

        private float _meanG = 0.456f;
        private float _meanB = 0.406f;
        private float _stdR = 0.229f;
        private float _stdG = 0.224f;
        private float _stdB = 0.225f;

        private string _modelPath = string.Empty;
        private bool _disposed = false;

        #endregion Fields

        #region Properties

        public string ModelPath
        {
            get => _modelPath;
            private set => _modelPath = value;
        }

        public bool IsInitialized => _session != null;

        public (float R, float G, float B) MeanValues => (_meanR, _meanG, _meanB);

        public (float R, float G, float B) StdValues => (_stdR, _stdG, _stdB);

        #endregion Properties

        #region Methods

        public async Task InitializeAsync(string modelPath, float meanR, float meanG, float meanB, float stdR, float stdG, float stdB)
        {
            if (string.IsNullOrEmpty(modelPath) || !File.Exists(modelPath))
            {
                throw new ArgumentException("Model file does not exist", nameof(modelPath));
            }

            try
            {
                // Dispose previous session
                _session?.Dispose();

                // Set normalization parameters
                UpdateNormalizationParameters(meanR, meanG, meanB, stdR, stdG, stdB);

                // Create session options
                SessionOptions sessionOptions = new();

                // Use CPU execution provider
                sessionOptions.AppendExecutionProvider_CPU();

                // Load model
                _session = new InferenceSession(modelPath, sessionOptions);
                ModelPath = modelPath;

                // Get input/output metadata
                await Task.Run(() => ExtractModelMetadata());
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to initialize ONNX model: {ex.Message}", ex);
            }
        }

        public void UpdateNormalizationParameters(float meanR, float meanG, float meanB, float stdR, float stdG, float stdB)
        {
            _meanR = meanR;
            _meanG = meanG;
            _meanB = meanB;
            _stdR = stdR;
            _stdG = stdG;
            _stdB = stdB;
        }

        private void ExtractModelMetadata()
        {
            if (_session == null) return;

            // Get input metadata
            IReadOnlyDictionary<string, NodeMetadata> inputMetadata = _session.InputMetadata;
            if (inputMetadata.Any())
            {
                KeyValuePair<string, NodeMetadata> firstInput = inputMetadata.First();
                _inputName = firstInput.Key;
                _inputDimensions = [.. firstInput.Value.Dimensions];
            }

            // Get output metadata
            IReadOnlyDictionary<string, NodeMetadata> outputMetadata = _session.OutputMetadata;
            if (outputMetadata.Any())
            {
                KeyValuePair<string, NodeMetadata> firstOutput = outputMetadata.First();
                _outputName = firstOutput.Key;
            }
        }

        /// <summary>
        /// Extract features from collection of processed images
        /// </summary>
        public async Task<List<ExtractedFeatures>> ExtractFeaturesAsync(IEnumerable<ProcessedMedia> processedImages)
        {
            if (!IsInitialized)
            {
                throw new InvalidOperationException("Service is not initialized. Call InitializeAsync first.");
            }

            List<ProcessedMedia> imageList = [.. processedImages];

            ConcurrentBag<ExtractedFeatures> results = [];

            // Limit concurrent model executions and limit memory exhaustion from too many tensors
            using SemaphoreSlim semaphore = new(Environment.ProcessorCount);

            // Async parallel processing
            await Parallel.ForEachAsync(imageList, new ParallelOptions
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount,
                CancellationToken = CancellationToken.None
            },
                async (processedImage, ct) =>
                {
                    await semaphore.WaitAsync(ct);
                    try
                    {
                        ExtractedFeatures? features = await ExtractFeaturesFromImageAsync(processedImage);
                        if (features != null)
                        {
                            results.Add(features);
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error extracting features from {processedImage.ProcessedImagePath}: {ex.Message}");
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                });

            return [.. results];
        }

        /// <summary>
        /// Extract features from single processed image
        /// </summary>
        private async Task<ExtractedFeatures?> ExtractFeaturesFromImageAsync(ProcessedMedia processedImage)
        {
            if (_session == null || _inputName == null || _outputName == null || _inputDimensions == null)
            {
                return null;
            }

            try
            {
                // Load and preprocess the image
                using Image<Rgb24>? originalImage = await Image.LoadAsync<Rgb24>(processedImage.ProcessedImagePath);

                // Get expected input dimensions from model
                int batchSize = _inputDimensions[0] == -1 ? 1 : _inputDimensions[0];
                int channels = _inputDimensions[1];
                int height = _inputDimensions[2];
                int width = _inputDimensions[3];

                // Convert image to tensor
                DenseTensor<float>? inputTensor = ConvertImageToTensor(originalImage, batchSize, channels, height, width);

                // Create input for the model
                List<NamedOnnxValue>? inputs =
                [
                    NamedOnnxValue.CreateFromTensor(_inputName, inputTensor)
                ];

                // Run inference
                using IDisposableReadOnlyCollection<DisposableNamedOnnxValue>? results = _session.Run(inputs);
                Tensor<float>? output = results[0].AsTensor<float>();

                // Extract feature vector
                float[]? featureVector = [.. output];
                int[]? featureDimensions = output.Dimensions.ToArray();

                return new ExtractedFeatures(processedImage, featureVector, featureDimensions);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to extract features from {processedImage.ProcessedImagePath}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Converts an ImageSharp image to a tensor for ONNX model input
        /// </summary>
        private DenseTensor<float> ConvertImageToTensor(Image<Rgb24> image, int batchSize, int channels, int height, int width)
        {
            DenseTensor<float>? tensor = new([batchSize, channels, height, width]);

            // Process each pixel
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Rgb24 pixel = image[x, y];

                    // Normalize pixel values to [0, 1] and apply ImageNet normalization
                    float r = (pixel.R / 255.0f - _meanR) / _stdR;
                    float g = (pixel.G / 255.0f - _meanG) / _stdG;
                    float b = (pixel.B / 255.0f - _meanB) / _stdB;

                    // CHW format
                    tensor[0, 0, y, x] = r;  // Red
                    tensor[0, 1, y, x] = g;  // Green
                    tensor[0, 2, y, x] = b;  // Blue
                }
            }
            return tensor;
        }

        #endregion Methods

        #region IDisposable

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                _session?.Dispose();
                _session = null;
                _disposed = true;
            }
        }

        ~FeatureExtractionService()
        {
            Dispose(false);
        }

        #endregion IDisposable
    }
}