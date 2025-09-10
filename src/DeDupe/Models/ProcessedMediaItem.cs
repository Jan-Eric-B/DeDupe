namespace DeDupe.Models
{
    public class ProcessedMedia(IMediaItem originalItem, string processedImagePath)
    {
        /// <summary>
        /// Original media item
        /// </summary>
        public IMediaItem OriginalItem { get; } = originalItem;

        /// <summary>
        /// Path to the processed image
        /// </summary>
        public string ProcessedImagePath { get; } = processedImagePath;
    }
}