// After
using DeDupe.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;

namespace DeDupe.Services.PreProcessing
{
    /// <summary>
    /// Service for preprocessing images before feature extraction.
    /// </summary>
    public class ImageProcessingService
    {
        #region Fields

        private readonly IAppStateService _appStateService;
        private readonly ISettingsService _settingsService;
        private readonly IBorderDetectionService _borderDetectionService;
        private readonly IImageFormatService _imageFormatService;
        private readonly IImageResizeService _imageResizeService;

        #endregion Fields

        #region Constructor

        public ImageProcessingService(
            IAppStateService appStateService,
            ISettingsService settingsService,
            IBorderDetectionService borderDetectionService,
            IImageFormatService imageFormatService,
            IImageResizeService imageResizeService)
        {
            _appStateService = appStateService ?? throw new ArgumentNullException(nameof(appStateService));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _borderDetectionService = borderDetectionService ?? throw new ArgumentNullException(nameof(borderDetectionService));
            _imageFormatService = imageFormatService ?? throw new ArgumentNullException(nameof(imageFormatService));
            _imageResizeService = imageResizeService ?? throw new ArgumentNullException(nameof(imageResizeService));

            InitializeTempFolder();
        }

        #endregion Constructor

        #region Initialization

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
                System.Diagnostics.Debug.WriteLine($"Failed to initialize temp folder: {ex.Message}");
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
                System.Diagnostics.Debug.WriteLine($"Failed to clear temp folder: {ex.Message}");
                return false;
            }
        }

        #endregion Initialization

        #region Methods

        /// <summary>
        /// Process all analysis items.
        /// </summary>
        public async Task ProcessItemsAsync(IEnumerable<AnalysisItem> items)
        {
            // Clear previous processing state
            _appStateService.ClearProcessedState();
            InitializeTempFolder();

            foreach (AnalysisItem item in items)
            {
                try
                {
                    string? processedPath = await ProcessSingleItemAsync(item);

                    if (!string.IsNullOrEmpty(processedPath))
                    {
                        item.SetProcessed(processedPath);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error processing {item.Source.FilePath}: {ex.Message}");
                }
            }

            _appStateService.NotifyProcessingComplete();
        }

        /// <summary>
        /// Process single analysis item.
        /// For images: processes original file.
        /// For video frames: processes extracted frame image.
        /// </summary>
        private async Task<string?> ProcessSingleItemAsync(AnalysisItem item)
        {
            if (item == null || string.IsNullOrEmpty(_settingsService.TempFolderPath))
            {
                return null;
            }

            // Determine source path
            // TODO handle video file
            string sourcePath = item.IsVideoFrame ? throw new NotImplementedException("Video frame processing not yet implemented") : item.Source.FilePath;

            // Create output file
            string extension = _imageFormatService.GetFileExtension(_settingsService.OutputFormat);
            string processedFileName = $"{Guid.NewGuid()}{extension}";
            StorageFolder tempFolder = await StorageFolder.GetFolderFromPathAsync(_settingsService.TempFolderPath);
            StorageFile outputFile = await tempFolder.CreateFileAsync(processedFileName);

            try
            {
                StorageFile sourceFile = await StorageFile.GetFileFromPathAsync(sourcePath);

                using IRandomAccessStream sourceStream = await sourceFile.OpenAsync(FileAccessMode.Read);
                BitmapDecoder decoder = await BitmapDecoder.CreateAsync(sourceStream);

                bool isGrayscale = decoder.BitmapPixelFormat == BitmapPixelFormat.Gray8 || decoder.BitmapPixelFormat == BitmapPixelFormat.Gray16;

                using (IRandomAccessStream outputStream = await outputFile.OpenAsync(FileAccessMode.ReadWrite))
                {
                    BitmapEncoder encoder = await _imageFormatService.CreateEncoderAsync(outputStream, _settingsService.OutputFormat);

                    BitmapPixelFormat pixelFormat = _imageFormatService.GetPixelFormat(_settingsService.ColorFormat);
                    BitmapAlphaMode alphaMode = BitmapAlphaMode.Premultiplied;

                    PixelDataProvider pixelData = await decoder.GetPixelDataAsync(pixelFormat, alphaMode, new BitmapTransform(), ExifOrientationMode.RespectExifOrientation, ColorManagementMode.ColorManageToSRgb);

                    byte[] pixels = pixelData.DetachPixelData();
                    uint width = decoder.PixelWidth;
                    uint height = decoder.PixelHeight;
                    uint dpi = _settingsService.Dpi;

                    // Step 1 - Convert grayscale to RGB
                    if (isGrayscale)
                    {
                        pixels = ConvertGrayscaleToRgba(pixels, width, height);
                    }

                    // Step 2 - Border/Letterbox Detection and Removal
                    if (_settingsService.EnableBorderDetection)
                    {
                        (byte[] borderRemovedPixels, uint newWidth, uint newHeight) = _borderDetectionService.RemoveBorders(pixels, width, height, _settingsService.BorderDetectionTolerance);

                        if (newWidth != width || newHeight != height)
                        {
                            pixels = borderRemovedPixels;
                            width = newWidth;
                            height = newHeight;
                        }
                    }

                    // Step 3 - Resizing
                    if (_settingsService.EnableResizing)
                    {
                        uint outputWidth = _settingsService.ResizeSize;
                        uint outputHeight = _settingsService.ResizeSize;

                        byte[] resizedPixels = await _imageResizeService.ResizeImageAsync(pixels, width, height, _settingsService.ResizeSize, _settingsService.ResizeSize, _settingsService.ResizeMethod, _settingsService.UpsamplingMethod, _settingsService.DownsamplingMethod, _settingsService.PaddingColor, _settingsService.ColorFormat, _settingsService.Dpi, _settingsService.Dpi);

                        encoder.SetPixelData(
                            pixelFormat,
                            alphaMode,
                            outputWidth,
                            outputHeight,
                            dpi, dpi,
                            resizedPixels);
                    }
                    else
                    {
                        // No resizing
                        encoder.SetPixelData(
                            pixelFormat,
                            alphaMode,
                            width, height,
                            dpi, dpi,
                            pixels);
                    }

                    await encoder.FlushAsync();
                }

                return outputFile.Path;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error processing image: {ex.Message}");
                return null;
            }
        }

        private static byte[] ConvertGrayscaleToRgba(byte[] grayscaleData, uint width, uint height)
        {
            byte[] rgbData = new byte[width * height * 4]; // RGBA

            int rgbIndex = 0;
            for (int i = 0; i < grayscaleData.Length; i++)
            {
                byte grayValue = grayscaleData[i];
                rgbData[rgbIndex++] = grayValue;  // R
                rgbData[rgbIndex++] = grayValue;  // G
                rgbData[rgbIndex++] = grayValue;  // B
                rgbData[rgbIndex++] = 255;        // A
            }

            return rgbData;
        }

        #endregion Methods
    }
}