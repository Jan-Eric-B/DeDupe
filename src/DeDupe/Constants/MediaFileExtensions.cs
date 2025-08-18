using System;
using System.Linq;

namespace DeDupe.Constants
{
    /// <summary>
    /// Defines what extensions are supported
    /// </summary>
    public static class MediaFileExtensions
    {
        public static readonly string[] SupportedImageExtensions =
        [
            ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tiff", ".webp"
        ];

        public static readonly string[] SupportedVideoExtensions =
        [
            ".mp4", ".avi", ".mov", ".wmv", ".flv", ".webm", ".mkv", ".m4v"
        ];

        public static readonly string[] AllSupportedExtensions = [.. SupportedImageExtensions, .. SupportedVideoExtensions];

        public static bool IsImageFile(string extension)
        {
            return SupportedImageExtensions.Contains(extension.ToLowerInvariant());
        }

        public static bool IsVideoFile(string extension)
        {
            return SupportedVideoExtensions.Contains(extension.ToLowerInvariant());
        }

        public static bool IsSupportedFile(string extension)
        {
            return AllSupportedExtensions.Contains(extension.ToLowerInvariant());
        }
    }
}