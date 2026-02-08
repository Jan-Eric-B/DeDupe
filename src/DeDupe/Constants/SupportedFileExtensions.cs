using System;
using System.Collections.Frozen;
using System.Linq;

namespace DeDupe.Constants
{
    public static class SupportedFileExtensions
    {
        public static readonly FrozenSet<string> SupportedImageExtensions = FrozenSet.ToFrozenSet(
            [".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tiff", ".tif", ".webp"],
            StringComparer.OrdinalIgnoreCase
        );

        public static readonly FrozenSet<string> SupportedVideoExtensions = FrozenSet.ToFrozenSet(
            [".mp4", ".avi", ".mov", ".wmv", ".flv", ".webm", ".mkv", ".m4v"],
            StringComparer.OrdinalIgnoreCase
        );

        public static readonly FrozenSet<string> SupportedModelExtensions = FrozenSet.ToFrozenSet(
            [".onnx"],
            StringComparer.OrdinalIgnoreCase
        );

        public static readonly FrozenSet<string> AllSupportedExtensions = FrozenSet.ToFrozenSet(SupportedImageExtensions.Concat(SupportedVideoExtensions), StringComparer.OrdinalIgnoreCase);

        public static bool IsImageFile(string extension)
        {
            return SupportedImageExtensions.Contains(extension);
        }

        public static bool IsVideoFile(string extension)
        {
            return SupportedVideoExtensions.Contains(extension);
        }

        public static bool IsSupportedMediaFile(string extension)
        {
            return AllSupportedExtensions.Contains(extension);
        }

        public static bool IsSupportedModelFile(string extension)
        {
            return SupportedModelExtensions.Contains(extension);
        }
    }
}