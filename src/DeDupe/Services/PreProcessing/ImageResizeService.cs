using DeDupe.Enums;
using System;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace DeDupe.Services.PreProcessing
{
    public class ImageResizeService(IImageFormatService imageFormatService) : IImageResizeService
    {
        private readonly IImageFormatService _imageFormatService = imageFormatService ?? throw new ArgumentNullException(nameof(imageFormatService));

        public async Task<byte[]> ResizeImageAsync(
            byte[] sourcePixels,
            uint sourceWidth,
            uint sourceHeight,
            uint targetWidth,
            uint targetHeight,
            ResizeMethod resizeMethod,
            InterpolationMethod upsamplingMethod,
            InterpolationMethod downsamplingMethod,
            byte[] paddingColor,
            ColorFormat bitDepth,
            double dpiX,
            double dpiY)
        {
            // Step 1 - Calculate intermediate dimensions
            uint intermediateWidth, intermediateHeight;
            uint cropX = 0, cropY = 0;
            uint offsetX = 0, offsetY = 0;
            bool needsCropping = false;
            bool needsPadding = false;

            switch (resizeMethod)
            {
                case ResizeMethod.Crop:
                    (uint scaledWidth, uint scaledHeight, uint cropOffsetX, uint cropOffsetY) = CalculateCropDimensions(sourceWidth, sourceHeight, targetWidth, targetHeight);
                    intermediateWidth = scaledWidth;
                    intermediateHeight = scaledHeight;
                    cropX = cropOffsetX;
                    cropY = cropOffsetY;
                    needsCropping = true;
                    break;

                case ResizeMethod.Padding:
                    (uint paddedWidth, uint paddedHeight, uint padOffsetX, uint padOffsetY) = CalculatePaddingDimensions(sourceWidth, sourceHeight, targetWidth, targetHeight);
                    intermediateWidth = paddedWidth;
                    intermediateHeight = paddedHeight;
                    offsetX = padOffsetX;
                    offsetY = padOffsetY;
                    needsPadding = true;
                    break;

                default:
                    intermediateWidth = targetWidth;
                    intermediateHeight = targetHeight;
                    break;
            }

            // Step 2 - Perform resize
            byte[] resizedPixels;
            bool isUpsampling = intermediateWidth > sourceWidth || intermediateHeight > sourceHeight;
            InterpolationMethod method = isUpsampling ? upsamplingMethod : downsamplingMethod;

            if (method == InterpolationMethod.Lanczos)
            {
                resizedPixels = ResizeLanczos(sourcePixels, sourceWidth, sourceHeight, intermediateWidth, intermediateHeight);
            }
            else
            {
                resizedPixels = await ResizeWindowsRuntime(sourcePixels, sourceWidth, sourceHeight, intermediateWidth, intermediateHeight, method, bitDepth, dpiX, dpiY);
            }

            // Step 3 - Apply cropping or padding
            if (needsCropping)
            {
                return CropPixels(resizedPixels, intermediateWidth, targetWidth, targetHeight, cropX, cropY);
            }
            else if (needsPadding)
            {
                return AddPadding(resizedPixels, intermediateWidth, intermediateHeight, targetWidth, targetHeight, offsetX, offsetY, paddingColor);
            }
            else
            {
                return resizedPixels;
            }
        }

        #region Dimension Calculations

        private static (uint scaledWidth, uint scaledHeight, uint cropX, uint cropY) CalculateCropDimensions(uint sourceWidth, uint sourceHeight, uint targetWidth, uint targetHeight)
        {
            double sourceAspect = (double)sourceWidth / sourceHeight;
            double targetAspect = (double)targetWidth / targetHeight;

            if (sourceAspect > targetAspect)
            {
                // Wider - crop sides
                double scale = (double)targetHeight / sourceHeight;
                uint scaledWidth = (uint)(sourceWidth * scale);
                uint cropX = (scaledWidth - targetWidth) / 2;
                return (scaledWidth, targetHeight, cropX, 0);
            }
            else
            {
                // Taller - crop top/bottom
                double scale = (double)targetWidth / sourceWidth;
                uint scaledHeight = (uint)(sourceHeight * scale);
                uint cropY = (scaledHeight - targetHeight) / 2;
                return (targetWidth, scaledHeight, 0, cropY);
            }
        }

        private static (uint scaledWidth, uint scaledHeight, uint offsetX, uint offsetY) CalculatePaddingDimensions(uint sourceWidth, uint sourceHeight, uint targetWidth, uint targetHeight)
        {
            double aspect = (double)sourceWidth / sourceHeight;
            uint scaledWidth, scaledHeight;

            if (aspect > 1) // Wider than tall
            {
                scaledWidth = targetWidth;
                scaledHeight = (uint)(targetWidth / aspect);
            }
            else
            {
                scaledHeight = targetHeight;
                scaledWidth = (uint)(targetHeight * aspect);
            }

            uint offsetX = (targetWidth - scaledWidth) / 2;
            uint offsetY = (targetHeight - scaledHeight) / 2;

            return (scaledWidth, scaledHeight, offsetX, offsetY);
        }

        #endregion Dimension Calculations

        #region Resizing Methods

        private async Task<byte[]> ResizeWindowsRuntime(
            byte[] sourcePixels,
            uint sourceWidth,
            uint sourceHeight,
            uint targetWidth,
            uint targetHeight,
            InterpolationMethod method,
            ColorFormat bitDepth,
            double dpiX,
            double dpiY)
        {
            try
            {
                using InMemoryRandomAccessStream memoryStream = new();

                BitmapEncoder tempEncoder = await _imageFormatService.CreateEncoderAsync(memoryStream, OutputFormat.PNG);
                tempEncoder.SetPixelData(
                    _imageFormatService.GetPixelFormat(bitDepth),
                    BitmapAlphaMode.Premultiplied,
                    sourceWidth, sourceHeight,
                    dpiX, dpiY,
                    sourcePixels);
                await tempEncoder.FlushAsync();

                memoryStream.Seek(0);

                BitmapDecoder decoder = await BitmapDecoder.CreateAsync(memoryStream);
                BitmapTransform transform = new()
                {
                    ScaledWidth = targetWidth,
                    ScaledHeight = targetHeight,
                    InterpolationMode = _imageFormatService.GetInterpolationMode(method)
                };

                PixelDataProvider pixelData = await decoder.GetPixelDataAsync(
                    _imageFormatService.GetPixelFormat(bitDepth),
                    BitmapAlphaMode.Premultiplied,
                    transform,
                    ExifOrientationMode.RespectExifOrientation,
                    ColorManagementMode.ColorManageToSRgb);

                return pixelData.DetachPixelData();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Windows Runtime resize failed: {ex.Message}");
                return [];
            }
        }

        private static byte[] ResizeLanczos(byte[] source, uint sourceWidth, uint sourceHeight, uint targetWidth, uint targetHeight)
        {
            byte[] target = new byte[targetWidth * targetHeight * 4];
            double xScale = (double)sourceWidth / targetWidth;
            double yScale = (double)sourceHeight / targetHeight;
            const int a = 3; // Lanczos3

            for (uint y = 0; y < targetHeight; y++)
            {
                for (uint x = 0; x < targetWidth; x++)
                {
                    double srcX = (x + 0.5) * xScale - 0.5;
                    double srcY = (y + 0.5) * yScale - 0.5;

                    double r = 0, g = 0, b = 0, alpha = 0;
                    double weightSum = 0;

                    int xMin = Math.Max(0, (int)Math.Floor(srcX) - a + 1);
                    int xMax = Math.Min((int)sourceWidth - 1, (int)Math.Floor(srcX) + a);
                    int yMin = Math.Max(0, (int)Math.Floor(srcY) - a + 1);
                    int yMax = Math.Min((int)sourceHeight - 1, (int)Math.Floor(srcY) + a);

                    for (int sy = yMin; sy <= yMax; sy++)
                    {
                        for (int sx = xMin; sx <= xMax; sx++)
                        {
                            double dx = srcX - sx;
                            double dy = srcY - sy;
                            double weight = LanczosKernel(dx, a) * LanczosKernel(dy, a);

                            if (weight != 0)
                            {
                                uint srcIndex = (uint)(sy * sourceWidth + sx) * 4;
                                if (srcIndex + 3 < source.Length)
                                {
                                    r += source[srcIndex] * weight;
                                    g += source[srcIndex + 1] * weight;
                                    b += source[srcIndex + 2] * weight;
                                    alpha += source[srcIndex + 3] * weight;
                                    weightSum += weight;
                                }
                            }
                        }
                    }

                    if (weightSum != 0)
                    {
                        uint targetIndex = (y * targetWidth + x) * 4;
                        target[targetIndex] = (byte)Math.Max(0, Math.Min(255, r / weightSum));
                        target[targetIndex + 1] = (byte)Math.Max(0, Math.Min(255, g / weightSum));
                        target[targetIndex + 2] = (byte)Math.Max(0, Math.Min(255, b / weightSum));
                        target[targetIndex + 3] = (byte)Math.Max(0, Math.Min(255, alpha / weightSum));
                    }
                }
            }

            return target;
        }

        private static double LanczosKernel(double x, int a)
        {
            if (x == 0) return 1.0;
            if (Math.Abs(x) >= a) return 0.0;

            double piX = Math.PI * x;
            return a * Math.Sin(piX) * Math.Sin(piX / a) / (piX * piX);
        }

        #endregion Resizing Methods

        private static byte[] CropPixels(byte[] source, uint sourceWidth, uint targetWidth, uint targetHeight, uint startX, uint startY)
        {
            byte[] target = new byte[targetWidth * targetHeight * 4];
            uint sourceStride = sourceWidth * 4;
            uint targetStride = targetWidth * 4;

            for (uint y = 0; y < targetHeight; y++)
            {
                for (uint x = 0; x < targetWidth; x++)
                {
                    uint sourceIndex = (startY + y) * sourceStride + (startX + x) * 4;
                    uint targetIndex = y * targetStride + x * 4;

                    if (sourceIndex + 3 < source.Length && targetIndex + 3 < target.Length)
                    {
                        target[targetIndex] = source[sourceIndex];
                        target[targetIndex + 1] = source[sourceIndex + 1];
                        target[targetIndex + 2] = source[sourceIndex + 2];
                        target[targetIndex + 3] = source[sourceIndex + 3];
                    }
                }
            }

            return target;
        }

        private static byte[] AddPadding(byte[] source, uint sourceWidth, uint sourceHeight, uint targetWidth, uint targetHeight, uint offsetX, uint offsetY, byte[] paddingColor)
        {
            byte[] target = new byte[targetWidth * targetHeight * 4];

            // Fill with padding color
            for (int i = 0; i < target.Length; i += 4)
            {
                target[i] = paddingColor[0];     // R
                target[i + 1] = paddingColor[1]; // G
                target[i + 2] = paddingColor[2]; // B
                target[i + 3] = paddingColor[3]; // A
            }

            // Copy source pixels
            uint sourceStride = sourceWidth * 4;
            uint targetStride = targetWidth * 4;

            for (uint y = 0; y < sourceHeight; y++)
            {
                for (uint x = 0; x < sourceWidth; x++)
                {
                    uint sourceIndex = y * sourceStride + x * 4;
                    uint targetIndex = (y + offsetY) * targetStride + (x + offsetX) * 4;

                    if (sourceIndex + 3 < source.Length && targetIndex + 3 < target.Length)
                    {
                        target[targetIndex] = source[sourceIndex];
                        target[targetIndex + 1] = source[sourceIndex + 1];
                        target[targetIndex + 2] = source[sourceIndex + 2];
                        target[targetIndex + 3] = source[sourceIndex + 3];
                    }
                }
            }

            return target;
        }
    }
}