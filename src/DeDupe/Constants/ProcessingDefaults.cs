using DeDupe.Enums;

// Constants/ProcessingDefaults.cs - Service layer only
namespace DeDupe.Constants
{
    /// <summary>
    /// Default values for image processing operations.
    /// </summary>
    public static class ProcessingDefaults
    {
        #region Resize Settings

        public const bool EnableResizing = true;
        public const uint TargetSize = 224;
        public const ResizeMethod DefaultResizeMethod = ResizeMethod.Padding;

        #endregion Resize Settings

        #region Interpolation Methods

        public const InterpolationMethod DefaultUpsamplingMethod = InterpolationMethod.Lanczos;
        public const InterpolationMethod DefaultDownsamplingMethod = InterpolationMethod.Lanczos;

        #endregion Interpolation Methods

        #region Border Detection

        public const bool EnableBorderDetection = true;
        public const int BorderDetectionTolerance = 80;

        #endregion Border Detection

        #region Output Settings

        public const OutputFormat DefaultOutputFormat = OutputFormat.PNG;
        public const ColorFormat DefaultColorFormat = ColorFormat.RGB8;
        public const double DpiX = 96.0;
        public const double DpiY = 96.0;

        #endregion Output Settings

        #region Padding Color

        /// <summary>
        /// Default padding color as RGBA byte array.
        /// </summary>
        public static readonly byte[] PaddingColorRgba = [255, 255, 255, 255];

        /// <summary>
        /// Default padding color components.
        /// </summary>
        public const byte PaddingColorR = 255;

        public const byte PaddingColorG = 255;
        public const byte PaddingColorB = 255;
        public const byte PaddingColorA = 255;

        #endregion Padding Color
    }
}