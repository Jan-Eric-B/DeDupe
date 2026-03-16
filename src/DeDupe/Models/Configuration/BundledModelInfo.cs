using DeDupe.Enums;

namespace DeDupe.Models.Configuration
{
    public static class BundledModelInfo
    {
        public const string DisplayName = "SSCD Disc Mixup";
        public const string FileName = "sscd_disc_mixup.onnx";
        public const string License = "MIT";
        public const string UrlName = "Facebook Research - SSCD Copy Detection";
        public const string Url = "https://github.com/facebookresearch/sscd-copy-detection";
        public const string Developers = "Meta AI (TorchScript -> ONNX)";
        public const int InputSize = 320;
        public const ResizeMethod RecommendedResizeMethod = ResizeMethod.Stretch;
        public const InterpolationMethod RecommendedDownsamplingMethod = InterpolationMethod.Bilinear;
        public const InterpolationMethod RecommendedUpsamplingMethod = InterpolationMethod.Bilinear;
        public const bool RecommendedCompanding = false;
        public const ColorFormat RecommendedColorFormat = ColorFormat.RGB8;
        public const TensorLayout RecommendedTensorLayout = TensorLayout.NCHW;
        public static NormalizationSettings Normalization => NormalizationSettings.ImageNet;
        public const OutputFormat RecommendedOutputFormat = OutputFormat.PNG;
        public const int OutputDimensions = 512;
    }
}