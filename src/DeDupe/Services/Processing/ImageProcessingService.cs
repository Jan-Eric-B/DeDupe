using DeDupe.Enums;
using DeDupe.Models;
using DeDupe.Models.Results;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Transforms;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace DeDupe.Services.Processing
{
    /// <inheritdoc />
    public partial class ImageProcessingService : IImageProcessingService
    {
        private readonly IAppStateService _appStateService;
        private readonly ISettingsService _settingsService;
        private readonly IBorderDetectionService _borderDetectionService;
        private readonly ILogger<ImageProcessingService> _logger;

        public ImageProcessingService(IAppStateService appStateService, ISettingsService settingsService, IBorderDetectionService borderDetectionService, ILogger<ImageProcessingService> logger)
        {
            _appStateService = appStateService ?? throw new ArgumentNullException(nameof(appStateService));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _borderDetectionService = borderDetectionService ?? throw new ArgumentNullException(nameof(borderDetectionService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            InitializeTempFolder();
        }

        public async Task ProcessItemsAsync(IEnumerable<AnalysisItem> items, IProgress<ProgressInfo>? progress = null, CancellationToken cancellationToken = default)
        {
            // Clear processing state
            _appStateService.ClearProcessedState();
            InitializeTempFolder();

            List<AnalysisItem> itemList = [.. items];
            int totalCount = itemList.Count;

            if (totalCount == 0)
            {
                return;
            }

            int maxParallelism = Math.Max(1, _settingsService.ParallelProcessingCores);

            LogImageProcessingStarting(totalCount, maxParallelism);

            // Track progress
            int processedCount = 0;
            int successCount = 0;
            int failedCount = 0;
            object progressLock = new();

            Stopwatch stopwatch = Stopwatch.StartNew();

            // Report initial progress
            progress?.Report(new ProgressInfo(0, totalCount, "Processing images"));

            await Parallel.ForEachAsync(
                itemList,
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = maxParallelism,
                    CancellationToken = cancellationToken
                },
                async (item, ct) =>
                {
                    try
                    {
                        string? processedPath = await ProcessSingleItemAsync(item, ct);

                        lock (progressLock)
                        {
                            processedCount++;

                            if (!string.IsNullOrEmpty(processedPath))
                            {
                                item.SetProcessed(processedPath);
                                successCount++;
                            }
                            else
                            {
                                failedCount++;
                            }

                            // Report progress
                            if (processedCount % 5 == 0 || processedCount == totalCount)
                            {
                                progress?.Report(new ProgressInfo(processedCount, totalCount, "Processing images", item.Source?.Metadata?.FileName));
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        lock (progressLock)
                        {
                            failedCount++;
                            processedCount++;

                            if (processedCount % 5 == 0 || processedCount == totalCount)
                            {
                                progress?.Report(new ProgressInfo(processedCount, totalCount, "Processing images"));
                            }
                        }
                        LogImageItemFailed(item.Source?.Metadata?.FileName ?? "unknown", ex);
                    }
                });

            stopwatch.Stop();

            // Final progress report
            progress?.Report(new ProgressInfo(totalCount, totalCount, "Processing complete"));

            LogImageProcessingCompleted(successCount, totalCount, failedCount, stopwatch.Elapsed.TotalSeconds);
        }

        private async Task<string?> ProcessSingleItemAsync(AnalysisItem item, CancellationToken ct)
        {
            if (item == null || string.IsNullOrEmpty(_settingsService.TempFolderPath))
            {
                return null;
            }

            // Determine source path
            string sourcePath = item.IsVideoFrame
                ? throw new NotImplementedException("Video frame processing not yet implemented")
                : item.Source.FilePath;

            // Create output filename with correct extension
            string extension = GetFileExtension(_settingsService.OutputFormat);
            string outputPath = Path.Combine(_settingsService.TempFolderPath, $"{Guid.NewGuid()}{extension}");

            try
            {
                // Load image with ImageSharp
                using Image<Rgba32> image = await Image.LoadAsync<Rgba32>(sourcePath, ct);

                // Apply EXIF orientation
                image.Mutate(x => x.AutoOrient());

                // Border Detection and Removal
                if (_settingsService.EnableBorderDetection)
                {
                    ApplyBorderDetection(image);
                }

                // Resizing
                if (_settingsService.EnableResizing)
                {
                    int targetSize = (int)_settingsService.ResizeSize;

                    ResizeOptions resizeOptions = new()
                    {
                        Size = new Size(targetSize, targetSize),
                        Mode = GetResizeMode(_settingsService.ResizeMethod),
                        Sampler = GetResampler(image, targetSize),
                        Compand = _settingsService.Compand,
                        PadColor = new Rgba32(
                            _settingsService.PaddingColor.R,
                            _settingsService.PaddingColor.G,
                            _settingsService.PaddingColor.B,
                            _settingsService.PaddingColor.A
                        )
                    };

                    image.Mutate(x => x.Resize(resizeOptions));
                }

                // Save in configured format
                await SaveImageAsync(image, outputPath, _settingsService.OutputFormat, ct);

                return outputPath;
            }
            catch (Exception ex)
            {
                LogImageItemFailed(sourcePath, ex);
                return null;
            }
        }

        #region Resize

        private static ResizeMode GetResizeMode(ResizeMethod method)
        {
            // Map ResizeMethod enum to ImageSharp ResizeMode.
            return method switch
            {
                ResizeMethod.Crop => ResizeMode.Crop,
                ResizeMethod.Padding => ResizeMode.Pad,
                ResizeMethod.Stretch => ResizeMode.Stretch,
                _ => ResizeMode.Pad
            };
        }

        private IResampler GetResampler(Image<Rgba32> image, int targetSize)
        {
            int currentSize = _settingsService.ResizeMethod switch
            {
                ResizeMethod.Padding => Math.Max(image.Width, image.Height),
                ResizeMethod.Crop => Math.Min(image.Width, image.Height),
                ResizeMethod.Stretch => Math.Max(image.Width, image.Height),
                _ => Math.Max(image.Width, image.Height)
            };

            InterpolationMethod method = currentSize > targetSize
                ? _settingsService.DownsamplingMethod
                : _settingsService.UpsamplingMethod;

            return method switch
            {
                InterpolationMethod.NearestNeighbor => KnownResamplers.NearestNeighbor,
                InterpolationMethod.Bilinear => KnownResamplers.Triangle,
                InterpolationMethod.Bicubic => KnownResamplers.Bicubic,
                InterpolationMethod.Lanczos => KnownResamplers.Lanczos3,
                InterpolationMethod.MitchellNetravali => KnownResamplers.MitchellNetravali,
                _ => KnownResamplers.Lanczos3
            };
        }

        #endregion Resize

        #region Border Detection

        private void ApplyBorderDetection(Image<Rgba32> image)
        {
            int originalWidth = image.Width;
            int originalHeight = image.Height;

            // Skip small images
            if (originalWidth < 20 || originalHeight < 20)
            {
                return;
            }

            Rectangle bounds = _borderDetectionService.DetectBorders(image, _settingsService.BorderDetectionTolerance);

            // Crop if borders detected
            if (bounds.Width < originalWidth || bounds.Height < originalHeight)
            {
                image.Mutate(x => x.Crop(bounds));
                LogBorderRemoved(originalWidth, originalHeight, image.Width, image.Height);
            }
        }

        #endregion Border Detection

        #region Output

        private static string GetFileExtension(OutputFormat format)
        {
            return format switch
            {
                OutputFormat.PNG => ".png",
                OutputFormat.JPEG => ".jpg",
                OutputFormat.BMP => ".bmp",
                OutputFormat.TIFF => ".tiff",
                OutputFormat.WebP => ".webp",
                _ => ".png"
            };
        }

        private static async Task SaveImageAsync(Image<Rgba32> image, string path, OutputFormat format, CancellationToken ct)
        {
            switch (format)
            {
                case OutputFormat.JPEG:
                    await image.SaveAsJpegAsync(path, new JpegEncoder { Quality = 90 }, ct);
                    break;

                case OutputFormat.BMP:
                    await image.SaveAsBmpAsync(path, new BmpEncoder(), ct);
                    break;

                case OutputFormat.TIFF:
                    await image.SaveAsTiffAsync(path, new TiffEncoder(), ct);
                    break;

                case OutputFormat.WebP:
                    await image.SaveAsWebpAsync(path, new WebpEncoder { Quality = 90 }, ct);
                    break;

                default:
                    await image.SaveAsPngAsync(path, new PngEncoder(), ct);
                    break;
            }
        }

        #endregion Output

        #region Temp Folder

        public bool InitializeTempFolder()
        {
            try
            {
                string tempPath = _settingsService.TempFolderPath;

                if (Directory.Exists(tempPath))
                {
                    ClearTempFolder();
                }

                Directory.CreateDirectory(tempPath);
                return true;
            }
            catch (Exception ex)
            {
                LogTempFolderInitFailed(ex);
                return false;
            }
        }

        public bool ClearTempFolder()
        {
            try
            {
                string tempPath = _settingsService.TempFolderPath;

                if (Directory.Exists(tempPath))
                {
                    Directory.Delete(tempPath, true);
                }
                return true;
            }
            catch (Exception ex)
            {
                LogTempFolderClearFailed(ex);
                return false;
            }
        }

        #endregion Temp Folder

        #region Logging

        [LoggerMessage(Level = LogLevel.Information, Message = "Image processing starting for {TotalCount} items (parallelism: {MaxParallelism})")]
        private partial void LogImageProcessingStarting(int totalCount, int maxParallelism);

        [LoggerMessage(Level = LogLevel.Information, Message = "Image processing completed, {SuccessCount}/{TotalCount} successful ({FailedCount} failed) in {ElapsedSeconds:F1}s")]
        private partial void LogImageProcessingCompleted(int successCount, int totalCount, int failedCount, double elapsedSeconds);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Image processing failed for '{SourcePath}'")]
        private partial void LogImageItemFailed(string sourcePath, Exception ex);

        [LoggerMessage(Level = LogLevel.Debug, Message = "Border removed, {OriginalWidth}x{OriginalHeight} cropped to {NewWidth}x{NewHeight}")]
        private partial void LogBorderRemoved(int originalWidth, int originalHeight, int newWidth, int newHeight);

        [LoggerMessage(Level = LogLevel.Error, Message = "Temp folder initialization failed")]
        private partial void LogTempFolderInitFailed(Exception ex);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Temp folder cleanup failed")]
        private partial void LogTempFolderClearFailed(Exception ex);

        #endregion Logging
    }
}