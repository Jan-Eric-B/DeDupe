using DeDupe.Enums;
using System.Threading.Tasks;

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
            byte[] paddingColor,
            ColorFormat bitDepth,
            double dpiX,
            double dpiY);
    }
}