using System;

namespace DeDupe.Constants
{
    public static class SupportedFileExtensions
    {
        public static readonly string[] SupportedImageExtensions =
        [
            ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tiff", ".webp"
        ];

        public static readonly string[] SupportedVideoExtensions =
        [
            ".mp4", ".avi", ".mov", ".wmv", ".flv", ".webm", ".mkv", ".m4v"
        ];

        public static readonly string[] SupportedModelExtensions =
        [
            ".onnx"
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

        public static bool IsSupportedMediaFile(string extension)
        {
            return AllSupportedExtensions.Contains(extension.ToLowerInvariant());
        }

        public static bool IsSupportedModelFile(string extension)
        {
            return SupportedModelExtensions.Contains(extension.ToLowerInvariant());
        }
    }
}