using DeDupe.Enums.PreProcessing;
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
            BitDepth bitDepth,
            double dpiX,
            double dpiY);
    }
}