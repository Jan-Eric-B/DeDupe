using System;
using System.Linq;

namespace DeDupe.Constants
{
    /// <summary>
    /// Defines what extensions are supported
    /// </summary>
    public static class ModelFileExtensions
    {
        public static readonly string[] SupportedModelExtensions =
        [
            ".onnx"
        ];

        public static bool IsSupportedFile(string extension)
        {
            return SupportedModelExtensions.Contains(extension.ToLowerInvariant());
        }
    }
}