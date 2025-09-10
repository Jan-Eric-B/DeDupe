using DeDupe.Enums;

namespace DeDupe.Models
{
    /// <summary>
    /// Interface for all processable media items
    /// </summary>
    public interface IMediaItem
    {
        /// <summary>
        /// Path to actual file representing this item
        /// </summary>
        string FilePath { get; }

        /// <summary>
        /// Width of item in pixels
        /// </summary>
        int Width { get; }

        /// <summary>
        /// Height of item in pixels
        /// </summary>
        int Height { get; }

        /// <summary>
        /// Media type (Image, Video, VideoFrame)
        /// </summary>
        MediaType Type { get; }

        /// <summary>
        /// Name of the item
        /// </summary>
        string GetDisplayName();
    }
}