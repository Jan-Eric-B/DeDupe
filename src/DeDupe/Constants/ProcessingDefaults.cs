using DeDupe.Enums;

namespace DeDupe.Constants
{
    public static class ProcessingDefaults
    {
        #region Resize

        public const bool EnableResizing = true;
        public const int TargetSize = 224;
        public const ResizeMethod DefaultResizeMethod = ResizeMethod.Padding;

        #endregion Resize

        #region Padding Color

        public static readonly byte[] PaddingColorRgba = [255, 255, 255, 255];

        public const byte PaddingColorR = 255;
        public const byte PaddingColorG = 255;
        public const byte PaddingColorB = 255;
        public const byte PaddingColorA = 255;

        #endregion Padding Color

        #region Interpolation

        public const InterpolationMethod DefaultUpsamplingMethod = InterpolationMethod.Lanczos;
        public const InterpolationMethod DefaultDownsamplingMethod = InterpolationMethod.Lanczos;

        #endregion Interpolation

        #region Border Detection

        public const bool EnableBorderDetection = true;
        public const int BorderDetectionTolerance = 80;

        #endregion Border Detection

        #region Output

        public const OutputFormat DefaultOutputFormat = OutputFormat.PNG;
        public const ColorFormat DefaultColorFormat = ColorFormat.RGB8;

        public const double DpiX = 96.0;
        public const double DpiY = 96.0;

        #endregion Output
    }
}