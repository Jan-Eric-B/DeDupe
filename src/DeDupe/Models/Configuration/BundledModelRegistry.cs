using DeDupe.Constants;
using DeDupe.Enums;
using System.Collections.Generic;
using System.Linq;

namespace DeDupe.Models.Configuration
{
    /// <summary>
    /// Registry of all bundled ONNX models.
    /// </summary>
    public static class BundledModelRegistry
    {
        /// <summary>
        /// DINOv2 ViT-B/14
        /// </summary>
        public static BundledModelInfo DinoV2VitB14 { get; } = new(
            Id: "dinov2-vitb14",
            DisplayName: "DINOv2 ViT-B/14",
            FileName: "dinov2_vitb14.onnx",
            Description: "Best visual similarity. Finds duplicates even with edits or different angles.",
            License: "Apache 2.0",
            Url: "https://github.com/facebookresearch/dinov2",
            Developers: "Meta AI (converted from TorchScript to ONNX)",
            Normalization: NormalizationSettings.ImageNet,
            InputSize: 224,
            DownloadUrl: $"{AppInformation.GitHubRepo}/releases/download/{AppInformation.ModelReleaseTag}/dinov2_vitb14.onnx",
            ExpectedSha256: "425f9a4ace446639cd1c86304b2ff90aee95b03551b3fb95dbb3a1db307f38a2",
            RecommendedResizeMethod: ResizeMethod.Padding);

        /// <summary>
        /// ResNet50
        /// </summary>
        public static BundledModelInfo ResNet50 { get; } = new(
            Id: "resnet50-features",
            DisplayName: "ResNet50",
            FileName: "resnet50-features.onnx",
            Description: "Fastest option. Good for exact or near-exact duplicates.",
            License: "BSD 3-Clause",
            Url: "https://github.com/pytorch/vision",
            Developers: "Kaiming He et al.; torchvision IMAGENET1K_V2 weights (converted to ONNX)",
            Normalization: NormalizationSettings.ImageNet,
            InputSize: 224,
            DownloadUrl: $"{AppInformation.GitHubRepo}/releases/download/{AppInformation.ModelReleaseTag}/resnet50-features.onnx",
            ExpectedSha256: "e889e7fda55b9c453502a93d8962a3eebf71a3389d03eed7243474fcf202856f",
            RecommendedResizeMethod: ResizeMethod.Padding);

        /// <summary>
        /// CLIP ViT-B/32
        /// </summary>
        public static BundledModelInfo ClipVitB32 { get; } = new(
            Id: "clip-vit-b32",
            DisplayName: "CLIP ViT-B/32",
            FileName: "clip-vit-b32-image.onnx",
            Description: "Groups by content (e.g., all beach photos). Less strict on visual match.",
            License: "MIT",
            Url: "https://github.com/openai/CLIP",
            Developers: "OpenAI (converted from TorchScript to ONNX)",
            Normalization: NormalizationSettings.Clip,
            InputSize: 224,
            DownloadUrl: $"{AppInformation.GitHubRepo}/releases/download/{AppInformation.ModelReleaseTag}/clip-vit-b32-image.onnx",
            ExpectedSha256: "2be71b05268ee2dc4288fce6f138b7790ab683bc56f558a332db2d0fb88aaca3",
            RecommendedResizeMethod: ResizeMethod.Padding);

        /// <summary>
        /// SSCD Disc Mixup (ResNet50 backbone)
        /// </summary>
        public static BundledModelInfo SscdDiscMixup { get; } = new(
            Id: "sscd-disc-mixup",
            DisplayName: "SSCD Disc Mixup",
            FileName: "sscd_disc_mixup.onnx",
            Description: "Purpose-built for copy detection. Best at finding duplicates with heavy edits, crops, or overlays.",
            License: "MIT",
            Url: "https://github.com/facebookresearch/sscd-copy-detection",
            Developers: "Meta AI (converted from TorchScript to ONNX)",
            Normalization: NormalizationSettings.ImageNet,
            InputSize: 320,
            DownloadUrl: $"{AppInformation.GitHubRepo}/releases/download/{AppInformation.ModelReleaseTag}/sscd_disc_mixup.onnx",
            ExpectedSha256: "1542492e86910dd7fd0b45ce4cb230831f935a322ef41034be66acb6fe843176",
            RecommendedResizeMethod: ResizeMethod.Stretch);

        /// <summary>
        /// SSCD Disc Large (ResNeXt101 backbone)
        /// </summary>
        public static BundledModelInfo SscdDiscLarge { get; } = new(
            Id: "sscd-disc-large",
            DisplayName: "SSCD Disc Large",
            FileName: "sscd_disc_large.onnx",
            Description: "Highest accuracy copy detection (ResNeXt101). Slower but more precise than Disc Mixup.",
            License: "MIT",
            Url: "https://github.com/facebookresearch/sscd-copy-detection",
            Developers: "Meta AI (converted from TorchScript to ONNX)",
            Normalization: NormalizationSettings.ImageNet,
            InputSize: 320,
            DownloadUrl: $"{AppInformation.GitHubRepo}/releases/download/{AppInformation.ModelReleaseTag}/sscd_disc_large.onnx",
            ExpectedSha256: "b466f25bf4e2cd08984e6a2c9e860d40d3613d3cbf8ea7595881eb20173287bd",
            RecommendedResizeMethod: ResizeMethod.Stretch);

        public static IReadOnlyList<BundledModelInfo> All { get; } =
        [
            ResNet50,
            DinoV2VitB14,
            ClipVitB32,
            SscdDiscMixup,
            SscdDiscLarge
        ];

        public static string DefaultModelId => SscdDiscMixup.Id;

        public static BundledModelInfo? GetById(string? id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            return All.FirstOrDefault(m => m.Id == id);
        }

        public static bool Exists(string? id) => GetById(id) != null;
    }
}