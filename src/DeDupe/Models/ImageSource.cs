using DeDupe.Enums;

namespace DeDupe.Models
{
    /// <summary>
    /// Represents an image file
    /// </summary>
    public class ImageSource : MediaSource
    {
        /// <summary>
        /// Create new instance of ImageSource class
        /// </summary>
        /// <param name="filePath">The full path to image file</param>
        /// <param name="width">Width of image in pixels</param>
        /// <param name="height">Height of image in pixels</param>
        public ImageSource(string filePath, int width, int height) : base(filePath)
        {
            Type = MediaType.Image;
            Width = width;
            Height = height;
        }
    }
}