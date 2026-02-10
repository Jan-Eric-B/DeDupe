using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace DeDupe.Services.Processing
{
    /// <summary>
    /// Detects uniform-color borders around images. Each edge is analyzed independently.
    /// </summary>
    public interface IBorderDetectionService
    {
        /// <summary>
        /// Analyzes image edges and returns the content rectangle excluding detected borders.
        /// </summary>
        /// <param name="tolerance">
        /// Per-channel color tolerance (0–255) when comparing pixels to the detected border color. Higher values allow
        /// more color variation.
        /// </param>
        /// <returns>A <see cref="Rectangle"/> representing the content area with borders excluded.</returns>
        Rectangle DetectBorders(Image<Rgba32> image, int tolerance = 30);
    }
}