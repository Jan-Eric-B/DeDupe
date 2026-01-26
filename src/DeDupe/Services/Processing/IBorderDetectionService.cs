using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace DeDupe.Services.Processing
{
    /// <summary>
    /// Interface for border detection services.
    /// </summary>
    public interface IBorderDetectionService
    {
        /// <summary>
        /// Detect borders.
        /// </summary>
        Rectangle DetectBorders(Image<Rgba32> image, int tolerance = 30);
    }
}