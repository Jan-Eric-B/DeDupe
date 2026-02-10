using DeDupe.Models;
using DeDupe.Models.Configuration;
using DeDupe.Models.Results;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DeDupe.Services.Analysis
{
    /// <summary>
    /// Service for extracting feature vectors from preprocessed images.
    /// </summary>
    public sealed partial class FeatureExtractionService : IFeatureExtractionService, IDisposable
    {
        #region Fields

        private InferenceSession? _session;
        private string? _inputName;
        private string? _outputName;
        private int[]? _inputDimensions;
        private string _modelPath = string.Empty;
        private bool _disposed;
        private bool _isGpuEnabled;
        private int _batchSize = 16;

        private DenseTensor<float>? _tensorBufferA;
        private DenseTensor<float>? _tensorBufferB;
        private int _allocatedBatchSize;
        private const int MaxIoConcurrency = 4;
        private readonly SemaphoreSlim _ioSemaphore = new(MaxIoConcurrency, MaxIoConcurrency);

        #endregion Fields

        #region Properties

        public string ModelPath => _modelPath;

        public bool IsInitialized => _session != null;

        public bool IsGpuEnabled => _isGpuEnabled;

        public int BatchSize => _batchSize;

        /// <summary>
        /// Expected input dimensions from the model [N, C, H, W].
        /// </summary>
        public (int Channels, int Height, int Width)? ExpectedDimensions => _inputDimensions != null
            ? (_inputDimensions[1], _inputDimensions[2], _inputDimensions[3])
            : null;

        #endregion Properties

        #region Initialization

        public async Task InitializeAsync(string modelPath, bool preferGpu = true, int batchSize = 16)
        {
            if (string.IsNullOrEmpty(modelPath))
            {
                throw new ArgumentException("Model path cannot be null or empty.", nameof(modelPath));
            }

            if (!File.Exists(modelPath))
            {
                throw new ArgumentException($"Model file does not exist: {modelPath}", nameof(modelPath));
            }

            _batchSize = Math.Clamp(batchSize, 1, 64);

            try
            {
                // Dispose session and buffers
                DisposeResources();

                SessionOptions sessionOptions = CreateSessionOptions(preferGpu);
                _session = new InferenceSession(modelPath, sessionOptions);
                _modelPath = modelPath;

                await Task.Run(ExtractModelMetadata);

                // Use models fixed batch dimension
                if (_inputDimensions != null && _inputDimensions.Length >= 4 && _inputDimensions[0] >= 1)
                {
                    int modelBatchSize = _inputDimensions[0];
                    if (_batchSize > modelBatchSize)
                    {
                        Debug.WriteLine($"Model has fixed batch dimension of {modelBatchSize}, overriding configured batch size {_batchSize}");
                        _batchSize = modelBatchSize;
                    }
                }

                AllocateTensorBuffers(_batchSize);

                Debug.WriteLine($"FeatureExtractionService initialized: GPU={_isGpuEnabled}, BatchSize={_batchSize}");
            }
            catch (Exception ex) when (ex is not ArgumentException)
            {
                throw new InvalidOperationException($"Failed to initialize ONNX model: {ex.Message}", ex);
            }
        }

        public Task InitializeAsync(string modelPath)
        {
            return InitializeAsync(modelPath, preferGpu: true, batchSize: 16);
        }

        private SessionOptions CreateSessionOptions(bool preferGpu)
        {
            SessionOptions sessionOptions = new()
            {
                GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
                EnableMemoryPattern = true,
            };

            _isGpuEnabled = false;

            if (preferGpu)
            {
                try
                {
                    sessionOptions.AppendExecutionProvider_DML(deviceId: 0);
                    _isGpuEnabled = true;

                    // Sequential execution for GPU
                    sessionOptions.ExecutionMode = ExecutionMode.ORT_SEQUENTIAL;

                    Debug.WriteLine("GPU acceleration enabled via DirectML");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"GPU acceleration not available, using CPU: {ex.Message}");
                }
            }

            // CPU fallback - parallel
            if (!_isGpuEnabled)
            {
                sessionOptions.ExecutionMode = ExecutionMode.ORT_PARALLEL;
            }

            sessionOptions.AppendExecutionProvider_CPU();

            return sessionOptions;
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

                Debug.WriteLine($"Model input: {_inputName}, dimensions: [{string.Join(", ", _inputDimensions)}]");
            }

            IReadOnlyDictionary<string, NodeMetadata> outputMetadata = _session.OutputMetadata;
            if (outputMetadata.Count > 0)
            {
                KeyValuePair<string, NodeMetadata> firstOutput = outputMetadata.First();
                _outputName = firstOutput.Key;

                Debug.WriteLine($"Model output: {_outputName}");
            }
        }

        private void AllocateTensorBuffers(int batchSize)
        {
            if (_inputDimensions == null || _inputDimensions.Length < 4) return;

            int channels = _inputDimensions[1];
            int height = _inputDimensions[2];
            int width = _inputDimensions[3];

            _tensorBufferA = new DenseTensor<float>([batchSize, channels, height, width]);
            _tensorBufferB = new DenseTensor<float>([batchSize, channels, height, width]);
            _allocatedBatchSize = batchSize;

            Debug.WriteLine($"Allocated tensor buffers: {batchSize}x{channels}x{height}x{width} " +
                          $"(~{batchSize * channels * height * width * 4 / 1024 / 1024:F1} MB each)");
        }

        private DenseTensor<float> GetOrCreateTensor(int requiredBatchSize, bool useBufferA)
        {
            if (_inputDimensions == null) throw new InvalidOperationException("Model not initialized");

            int channels = _inputDimensions[1];
            int height = _inputDimensions[2];
            int width = _inputDimensions[3];

            // If required size matches allocated size, reuse buffer
            if (requiredBatchSize == _allocatedBatchSize)
            {
                DenseTensor<float>? buffer = useBufferA ? _tensorBufferA : _tensorBufferB;
                if (buffer != null)
                {
                    // Clear buffer for reuse
                    buffer.Buffer.Span.Clear();
                    return buffer;
                }
            }

            // Create new tensor for non-standard batch size
            return new DenseTensor<float>([requiredBatchSize, channels, height, width]);
        }

        #endregion Initialization

        #region Feature Extraction

        /// <inheritdoc />
        public async Task ExtractFeaturesAsync(IReadOnlyCollection<AnalysisItem> items, NormalizationSettings normalization, IProgress<ProgressInfo>? progress = null, CancellationToken cancellationToken = default)
        {
            if (!IsInitialized)
            {
                throw new InvalidOperationException("Service is not initialized. Call InitializeAsync first.");
            }

            ArgumentNullException.ThrowIfNull(items);
            ArgumentNullException.ThrowIfNull(normalization);

            List<AnalysisItem> itemList = [.. items.Where(i => i.IsProcessed)];

            if (itemList.Count == 0)
            {
                return;
            }

            // Convert to float
            NormalizationSettingsFloat normFloat = normalization.ToFloat();

            await ExtractFeaturesWithDoubleBufferingAsync(itemList, normFloat, progress, cancellationToken);
        }

        private async Task ExtractFeaturesWithDoubleBufferingAsync(List<AnalysisItem> items, NormalizationSettingsFloat normalization, IProgress<ProgressInfo>? progress, CancellationToken ct)
        {
            if (_session == null || _inputName == null || _outputName == null || _inputDimensions == null)
            {
                return;
            }

            int height = _inputDimensions[2];
            int width = _inputDimensions[3];

            // Create batch ranges
            List<(int Start, int Count)> batches = [];
            for (int i = 0; i < items.Count; i += _batchSize)
            {
                int count = Math.Min(_batchSize, items.Count - i);
                batches.Add((i, count));
            }

            if (batches.Count == 0) return;

            int totalItems = items.Count;
            int processedItems = 0;

            progress?.Report(new ProgressInfo(0, totalItems, "Extracting features"));

            // Track using buffer
            bool useBufferA = true;

            // Prepare first batch
            List<AnalysisItem> currentBatch = [.. items.Skip(batches[0].Start).Take(batches[0].Count)];
            Task<(DenseTensor<float> Tensor, List<AnalysisItem> Items, List<int> ValidIndices)> prepareTask = PrepareBatchAsync(currentBatch, height, width, normalization, useBufferA, ct);

            for (int batchIndex = 0; batchIndex < batches.Count; batchIndex++)
            {
                ct.ThrowIfCancellationRequested();

                // Wait for current batch preparation to complete
                (DenseTensor<float> tensor, List<AnalysisItem> batchItems, List<int> validIndices) = await prepareTask;

                // Start preparing next batch (double-buffering)
                Task<(DenseTensor<float>, List<AnalysisItem>, List<int>)>? nextPrepareTask = null;
                if (batchIndex + 1 < batches.Count)
                {
                    useBufferA = !useBufferA; // Swap buffer
                    List<AnalysisItem> nextBatch = [.. items
                    .Skip(batches[batchIndex + 1].Start)
                    .Take(batches[batchIndex + 1].Count)];
                    nextPrepareTask = PrepareBatchAsync(nextBatch, height, width, normalization, useBufferA, ct);
                }

                // Run inference on current batch
                if (validIndices.Count > 0)
                {
                    try
                    {
                        await RunInferenceAsync(tensor, batchItems, validIndices, ct);
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Batch inference failed, falling back to individual processing: {ex.Message}");

                        // Fallback - Process items individually
                        List<AnalysisItem> failedItems = validIndices.Select(i => batchItems[i]).ToList();
                        await ProcessItemsIndividuallyAsync(failedItems, normalization, ct);
                    }
                }

                // Update progress after each batch
                processedItems += batchItems.Count;
                progress?.Report(new ProgressInfo(processedItems, totalItems, "Extracting features"));

                // Set up for next iteration
                if (nextPrepareTask != null)
                {
                    prepareTask = nextPrepareTask;
                }
            }

            progress?.Report(new ProgressInfo(totalItems, totalItems, "Feature extraction complete"));
        }

        private async Task<(DenseTensor<float> Tensor, List<AnalysisItem> Items, List<int> ValidIndices)> PrepareBatchAsync(List<AnalysisItem> batchItems, int height, int width, NormalizationSettingsFloat normalization, bool useBufferA, CancellationToken ct)
        {
            DenseTensor<float> tensor = GetOrCreateTensor(batchItems.Count, useBufferA);
            List<int> validIndices = [];
            object lockObj = new();

            // Load images with limited I/O parallelism
            Task[] loadTasks = new Task[batchItems.Count];

            for (int i = 0; i < batchItems.Count; i++)
            {
                int index = i;
                AnalysisItem item = batchItems[i];

                loadTasks[i] = Task.Run(async () =>
                {
                    // Limit concurrent I/O operations
                    await _ioSemaphore.WaitAsync(ct);
                    try
                    {
                        if (string.IsNullOrEmpty(item.ProcessedFilePath))
                        {
                            Debug.WriteLine($"Skipping item with no processed file path");
                            return;
                        }

                        // Load image
                        using Image<Rgb24> image = await Image.LoadAsync<Rgb24>(item.ProcessedFilePath, ct);

                        // Validate dimensions
                        if (image.Width != width || image.Height != height)
                        {
                            Debug.WriteLine($"Dimension mismatch for {item.ProcessedFilePath}: expected {width}x{height}, got {image.Width}x{image.Height}");
                            return;
                        }

                        // Fill tensor slice
                        FillTensorFromImage(tensor, index, image, height, width, normalization);

                        lock (lockObj)
                        {
                            validIndices.Add(index);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error loading image {item.ProcessedFilePath}: {ex.Message}");
                    }
                    finally
                    {
                        _ioSemaphore.Release();
                    }
                }, ct);
            }

            await Task.WhenAll(loadTasks);

            // Sort valid indices for consistent ordering
            validIndices.Sort();

            return (tensor, batchItems, validIndices);
        }

        private async Task RunInferenceAsync(DenseTensor<float> tensor, List<AnalysisItem> batchItems, List<int> validIndices, CancellationToken ct)
        {
            if (_session == null || _inputName == null) return;

            ct.ThrowIfCancellationRequested();

            // Run inference
            IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results = await Task.Run(() =>
            {
                List<NamedOnnxValue> inputs =
                [
                    NamedOnnxValue.CreateFromTensor(_inputName, tensor)
                ];
                return _session.Run(inputs);
            }, ct);

            using (results)
            {
                Tensor<float> output = results[0].AsTensor<float>();
                int featureDim = output.Dimensions[1];
                int[] featureDimensions = [1, featureDim];

                // Extract feature vectors
                foreach (int batchIndex in validIndices)
                {
                    float[] features = new float[featureDim];
                    for (int k = 0; k < featureDim; k++)
                    {
                        features[k] = output[batchIndex, k];
                    }
                    batchItems[batchIndex].SetFeatures(features, featureDimensions);
                }
            }
        }

        private static void FillTensorFromImage(DenseTensor<float> tensor, int batchIndex, Image<Rgb24> image, int height, int width, NormalizationSettingsFloat normalization)
        {
            // Inverse std
            float invStdR = normalization.InvStdR;
            float invStdG = normalization.InvStdG;
            float invStdB = normalization.InvStdB;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Rgb24 pixel = image[x, y];

                    // Normalize to [0, 1] then apply normalization
                    float r = pixel.R / 255.0f;
                    float g = pixel.G / 255.0f;
                    float b = pixel.B / 255.0f;

                    tensor[batchIndex, 0, y, x] = (r - normalization.MeanR) * invStdR;
                    tensor[batchIndex, 1, y, x] = (g - normalization.MeanG) * invStdG;
                    tensor[batchIndex, 2, y, x] = (b - normalization.MeanB) * invStdB;
                }
            }
        }

        private async Task ProcessItemsIndividuallyAsync(List<AnalysisItem> items, NormalizationSettingsFloat normalization, CancellationToken ct)
        {
            if (_session == null || _inputName == null || _inputDimensions == null) return;

            int channels = _inputDimensions[1];
            int height = _inputDimensions[2];
            int width = _inputDimensions[3];

            // Reuse single-item tensor
            DenseTensor<float> singleTensor = new([1, channels, height, width]);

            foreach (AnalysisItem item in items)
            {
                ct.ThrowIfCancellationRequested();

                if (string.IsNullOrEmpty(item.ProcessedFilePath)) continue;

                try
                {
                    using Image<Rgb24> image = await Image.LoadAsync<Rgb24>(item.ProcessedFilePath, ct);

                    // Validate dimensions
                    if (image.Width != width || image.Height != height)
                    {
                        Debug.WriteLine($"Skipping {item.ProcessedFilePath}: dimension mismatch");
                        continue;
                    }

                    // Clear and fill tensor
                    singleTensor.Buffer.Span.Clear();
                    FillTensorFromImage(singleTensor, 0, image, height, width, normalization);

                    // Run inference
                    List<NamedOnnxValue> inputs =
                    [
                        NamedOnnxValue.CreateFromTensor(_inputName, singleTensor)
                    ];

                    using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results = _session.Run(inputs);
                    Tensor<float> output = results[0].AsTensor<float>();

                    float[] featureVector = [.. output];
                    int[] featureDimensions = output.Dimensions.ToArray();

                    item.SetFeatures(featureVector, featureDimensions);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to extract features from {item.ProcessedFilePath}: {ex.Message}");
                }
            }
        }

        #endregion Feature Extraction

        #region Cleanup

        private void DisposeResources()
        {
            _session?.Dispose();
            _session = null;
            _tensorBufferA = null;
            _tensorBufferB = null;
            _allocatedBatchSize = 0;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                DisposeResources();
                _ioSemaphore.Dispose();
                _disposed = true;
            }
        }

        #endregion Cleanup
    }
}