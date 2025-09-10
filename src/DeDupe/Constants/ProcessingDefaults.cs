using DeDupe.Enums.PreProcessing;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace DeDupe.Constants
{
    public static class ProcessingDefaults
    {
        #region Resize Settings

        public const bool EnableResizing = true;
        public const uint TargetSize = 224;
        public const ResizeMethod ResizeMethod = Enums.PreProcessing.ResizeMethod.Padding;

        #endregion Resize Settings

        #region Interpolation Methods

        public const InterpolationMethod UpsamplingMethod = InterpolationMethod.Lanczos;
        public const InterpolationMethod DownsamplingMethod = InterpolationMethod.Lanczos;

        #endregion Interpolation Methods

        #region Border Detection

        public const bool EnableBorderDetection = true;
        public const int BorderDetectionTolerance = 80;

        #endregion Border Detection

        #region Output Settings

        public const OutputFormat OutputFormat = Enums.PreProcessing.OutputFormat.PNG;
        public const BitDepth BitDepth = Enums.PreProcessing.BitDepth.RGB8;
        public const double DpiX = 96.0;
        public const double DpiY = 96.0;

        #endregion Output Settings

        #region Padding Color

        public static readonly byte[] PaddingColor = [255, 255, 255, 255];
        public static readonly Color PaddingColorUI = Colors.White;
        public static SolidColorBrush PaddingColorBrush => new(PaddingColorUI);

        #endregion Padding Color
    }
}