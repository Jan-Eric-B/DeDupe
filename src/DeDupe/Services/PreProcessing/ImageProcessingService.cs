using DeDupe.Constants;
using DeDupe.Enums.PreProcessing;
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
    public class ImageProcessingService
    {
        #region Fields

        private readonly IAppStateService _appStateService;
        private readonly IBorderDetectionService _borderDetectionService;
        private readonly IImageFormatService _imageFormatService;
        private readonly IImageResizeService _imageResizeService;

        #endregion Fields

        #region Properties

        // Resize
        public bool EnableResizing { get; set; } = ProcessingDefaults.EnableResizing;

        // Target size for resizing
        public uint TargetSize { get; set; } = ProcessingDefaults.TargetSize;

        // Resize method
        public ResizeMethod ResizeMethod { get; set; } = ProcessingDefaults.ResizeMethod;

        // Padding color (RGBA)
        public byte[] PaddingColor { get; set; } = ProcessingDefaults.PaddingColor;

        // Border Detection
        public bool EnableBorderDetection { get; set; } = ProcessingDefaults.EnableBorderDetection;

        // Tolerance for border detection (0-255)
        public int BorderDetectionTolerance { get; set; } = ProcessingDefaults.BorderDetectionTolerance;

        // Interpolation Methods
        public InterpolationMethod UpsamplingMethod { get; set; } = ProcessingDefaults.UpsamplingMethod;

        public InterpolationMethod DownsamplingMethod { get; set; } = ProcessingDefaults.DownsamplingMethod;

        // Output
        public OutputFormat OutputFormat { get; set; } = ProcessingDefaults.OutputFormat;

        public BitDepth BitDepth { get; set; } = ProcessingDefaults.BitDepth;
        public double DpiX { get; set; } = ProcessingDefaults.DpiX;
        public double DpiY { get; set; } = ProcessingDefaults.DpiY;

        #endregion Properties

        #region Initialization

        public ImageProcessingService(
            IAppStateService appStateService,
            IBorderDetectionService borderDetectionService,
            IImageFormatService imageFormatService,
            IImageResizeService imageResizeService)
        {
            _appStateService = appStateService ?? throw new ArgumentNullException(nameof(appStateService));
            _borderDetectionService = borderDetectionService ?? throw new ArgumentNullException(nameof(borderDetectionService));
            _imageFormatService = imageFormatService ?? throw new ArgumentNullException(nameof(imageFormatService));
            _imageResizeService = imageResizeService ?? throw new ArgumentNullException(nameof(imageResizeService));

            InitializeTempFolder();
        }

        public bool InitializeTempFolder()
        {
            try
            {
                string? tempPath = _appStateService.TempFolderPath;

                if (Directory.Exists(tempPath))
                {
                    ClearTempFolder();
                }

                Directory.CreateDirectory(tempPath);
                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        public bool ClearTempFolder()
        {
            try
            {
                string? tempPath = _appStateService.TempFolderPath;

                if (Directory.Exists(tempPath))
                {
                    Directory.Delete(tempPath, true);
                }
                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        #endregion Initialization

        #region Methods

        public async Task ProcessImagesAsync(IEnumerable<string> imagePaths)
        {
            // Clear processed images
            _appStateService.ClearProcessedImages();

            InitializeTempFolder();

            foreach (string imagePath in imagePaths)
            {
                try
                {
                    // Create ImageSource
                    StorageFile file = await StorageFile.GetFileFromPathAsync(imagePath);
                    using IRandomAccessStream stream = await file.OpenAsync(FileAccessMode.Read);
                    BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);

                    ImageSource imageSource = new(imagePath, (int)decoder.PixelWidth, (int)decoder.PixelHeight);

                    // Process image
                    ProcessedMedia? processedImage = await ProcessSingleImageAsync(imageSource);
                    if (processedImage != null)
                    {
                        _appStateService.AddProcessedImage(processedImage);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error processing {imagePath}: {ex.Message}");
                }
            }
        }

        private async Task<ProcessedMedia?> ProcessSingleImageAsync(ImageSource imageSource)
        {
            if (!imageSource.Exists() || _appStateService.TempFolderPath == null)
            {
                return null;
            }

            // Create unique filename
            string extension = _imageFormatService.GetFileExtension(OutputFormat);
            string processedFileName = $"{Guid.NewGuid()}{extension}";
            StorageFolder _tempFolder = await StorageFolder.GetFolderFromPathAsync(_appStateService.TempFolderPath);
            StorageFile outputFile = await _tempFolder.CreateFileAsync(processedFileName);

            try
            {
                // Load source file
                StorageFile sourceFile = await StorageFile.GetFileFromPathAsync(imageSource.FilePath);

                using IRandomAccessStream sourceStream = await sourceFile.OpenAsync(FileAccessMode.Read);

                // Read image
                BitmapDecoder decoder = await BitmapDecoder.CreateAsync(sourceStream);

                // Is image grayscale
                bool isGrayscale = decoder.BitmapPixelFormat == BitmapPixelFormat.Gray8 || decoder.BitmapPixelFormat == BitmapPixelFormat.Gray16;

                // Create resized image
                using (IRandomAccessStream outputStream = await outputFile.OpenAsync(FileAccessMode.ReadWrite))
                {
                    // Create encoder
                    BitmapEncoder encoder = await _imageFormatService.CreateEncoderAsync(outputStream, OutputFormat);

                    // Get pixel data from decoder (original size)
                    BitmapPixelFormat pixelFormat = _imageFormatService.GetPixelFormat(BitDepth);
                    BitmapAlphaMode alphaMode = BitmapAlphaMode.Premultiplied;

                    PixelDataProvider? pixelData = await decoder.GetPixelDataAsync(
                        pixelFormat,
                        alphaMode,
                        new BitmapTransform(),
                        ExifOrientationMode.RespectExifOrientation,
                        ColorManagementMode.ColorManageToSRgb);

                    byte[] pixels = pixelData.DetachPixelData();
                    uint width = decoder.PixelWidth;
                    uint height = decoder.PixelHeight;

                    // Step 1 - Convert grayscale to RGB
                    if (isGrayscale)
                    {
                        pixels = ConvertGrayscaleToRgba(pixels, width, height);
                    }

                    // Step 2 - Border/Letterbox Detection and Removal
                    if (EnableBorderDetection)
                    {
                        (byte[] borderRemovedPixels, uint newWidth, uint newHeight) = _borderDetectionService.RemoveBorders(pixels, width, height, BorderDetectionTolerance);

                        // If borders removed
                        if (newWidth != width || newHeight != height)
                        {
                            pixels = borderRemovedPixels;
                            width = newWidth;
                            height = newHeight;
                        }
                    }

                    // Step 3 - Resizing
                    if (EnableResizing)
                    {
                        uint outputWidth = TargetSize;
                        uint outputHeight = TargetSize;
                        byte[] resizedPixels;

                        resizedPixels = await _imageResizeService.ResizeImageAsync(pixels, width, height, TargetSize, TargetSize, ResizeMethod, UpsamplingMethod, DownsamplingMethod, PaddingColor, BitDepth, DpiX, DpiY);

                        encoder.SetPixelData(
                            pixelFormat,
                            alphaMode,
                            outputWidth,
                            outputHeight,
                            DpiX, DpiY,
                            resizedPixels);
                    }
                    else
                    {
                        // No resizing
                        encoder.SetPixelData(
                            pixelFormat,
                            alphaMode,
                            width, height,
                            DpiX, DpiY,
                            pixels);
                    }

                    await encoder.FlushAsync();
                }

                return new ProcessedMedia(
                    imageSource,
                    outputFile.Path);
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