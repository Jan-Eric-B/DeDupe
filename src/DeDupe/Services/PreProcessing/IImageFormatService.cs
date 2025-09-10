using DeDupe.Enums.PreProcessing;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace DeDupe.Services.PreProcessing
{
    public interface IImageFormatService
    {
        string GetFileExtension(OutputFormat outputFormat);

        Task<BitmapEncoder> CreateEncoderAsync(IRandomAccessStream stream, OutputFormat outputFormat);

        BitmapPixelFormat GetPixelFormat(BitDepth bitDepth);

        BitmapInterpolationMode GetInterpolationMode(InterpolationMethod method);
    }
}