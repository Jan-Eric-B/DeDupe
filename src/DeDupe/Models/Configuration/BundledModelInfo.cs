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
        public const string Developers = "Meta AI (converted from TorchScript to ONNX)";
        public const int InputSize = 320;
        public const ResizeMethod RecommendedResizeMethod = ResizeMethod.Stretch;

        public static NormalizationSettings Normalization => NormalizationSettings.ImageNet;
    }
}