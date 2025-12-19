using DeDupe.Enums;
using System;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace DeDupe.Services.PreProcessing
{
    public class ImageFormatService : IImageFormatService
    {
        public string GetFileExtension(OutputFormat outputFormat)
        {
            return outputFormat switch
            {
                OutputFormat.PNG => ".png",
                OutputFormat.BMP => ".bmp",
                OutputFormat.TIFF => ".tiff",
                OutputFormat.JPEG => ".jpg",
                _ => ".png",
            };
        }

        public async Task<BitmapEncoder> CreateEncoderAsync(IRandomAccessStream stream, OutputFormat outputFormat)
        {
            Guid encoderId = outputFormat switch
            {
                OutputFormat.PNG => BitmapEncoder.PngEncoderId,
                OutputFormat.BMP => BitmapEncoder.BmpEncoderId,
                OutputFormat.TIFF => BitmapEncoder.TiffEncoderId,
                OutputFormat.JPEG => BitmapEncoder.JpegEncoderId,
                _ => BitmapEncoder.PngEncoderId,
            };

            return await BitmapEncoder.CreateAsync(encoderId, stream);
        }

        public BitmapPixelFormat GetPixelFormat(ColorFormat bitDepth)
        {
            return bitDepth switch
            {
                ColorFormat.BGR8 => BitmapPixelFormat.Bgra8,
                ColorFormat.Gray8 => BitmapPixelFormat.Gray8,
                ColorFormat.RGB8 => BitmapPixelFormat.Rgba8,
                ColorFormat.RGB16 => BitmapPixelFormat.Rgba16,
                ColorFormat.BGR16 => BitmapPixelFormat.Bgra8,
                _ => BitmapPixelFormat.Rgba8,
            };
        }

        public BitmapInterpolationMode GetInterpolationMode(InterpolationMethod method)
        {
            return method switch
            {
                InterpolationMethod.NearestNeighbor => BitmapInterpolationMode.NearestNeighbor,
                InterpolationMethod.Bilinear => BitmapInterpolationMode.Linear,
                InterpolationMethod.Bicubic => BitmapInterpolationMode.Cubic,
                InterpolationMethod.Fant => BitmapInterpolationMode.Fant,
                InterpolationMethod.Lanczos => BitmapInterpolationMode.Linear, // Fallback for Lanczos
                _ => BitmapInterpolationMode.Linear,
            };
        }
    }
}