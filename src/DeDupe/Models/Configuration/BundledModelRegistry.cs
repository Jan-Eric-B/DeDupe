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
            ExpectedFileSize: 347_148_037,
            RecommendedResizeMethod: ResizeMethod.Padding);

        /// <summary>
        /// ResNet50-v2
        /// </summary>
        public static BundledModelInfo ResNet50 { get; } = new(
            Id: "resnet50-v2",
            DisplayName: "ResNet50-v2-7",
            FileName: "resnet50-v2-7.onnx",
            Description: "Fastest option. Good for exact or near-exact duplicates.",
            License: "Apache 2.0",
            Url: "https://huggingface.co/onnxmodelzoo/resnet50-v2-7",
            Developers: "Microsoft Research (via ONNX Model Zoo); Initially by: Kaiming He, Xiangyu Zhang, Shaoqing Ren, and Jian Sun.",
            Normalization: NormalizationSettings.ImageNet,
            InputSize: 224,
            DownloadUrl: $"{AppInformation.GitHubRepo}/releases/download/{AppInformation.ModelReleaseTag}/resnet50-v2-7.onnx",
            ExpectedFileSize: 102_442_452,
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
            ExpectedFileSize: 976_517,
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
            ExpectedFileSize: 98_158_429,
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
            ExpectedFileSize: 176_700_233,
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