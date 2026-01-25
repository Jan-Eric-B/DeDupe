using DeDupe.Enums;
using System.Threading.Tasks;
using Windows.UI;

namespace DeDupe.Services.PreProcessing
{
    public interface IImageResizeService
    {
        Task<byte[]> ResizeImageAsync(
            byte[] sourcePixels,
            uint sourceWidth,
            uint sourceHeight,
            uint targetWidth,
            uint targetHeight,
            ResizeMethod resizeMethod,
            InterpolationMethod upsamplingMethod,
            InterpolationMethod downsamplingMethod,
            Color paddingColor,
            ColorFormat bitDepth,
            uint dpiX,
            uint dpiY);
    }
}