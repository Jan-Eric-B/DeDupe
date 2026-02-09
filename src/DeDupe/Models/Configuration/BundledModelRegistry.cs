using System.Collections.Generic;
using System.Linq;

namespace DeDupe.Models.Configuration;

/// <summary>
/// Registry of all bundled ONNX models.
/// </summary>
public static class BundledModelRegistry
{
    private const string ReleaseBaseUrl = "https://github.com/Jan-Eric-B/DeDupe/releases/download/v1.0.0-models";

    /// <summary>
    /// DINOv2 ViT-B/14
    /// </summary>
    public static BundledModelInfo DinoV2VitB14 { get; } = new(
        Id: "dinov2-vitb14",
        DisplayName: "DINOv2 ViT-B/14",
        FileName: "dinov2_vitb14.onnx",
        Description: "Best visual similarity. Finds duplicates even with edits or different angles.",
        Normalization: NormalizationSettings.ImageNet,
        InputSize: 224,
        DownloadUrl: $"{ReleaseBaseUrl}/dinov2_vitb14.onnx",
        ExpectedFileSize: 347_148_037);

    /// <summary>
    /// ResNet50-v2
    /// </summary>
    public static BundledModelInfo ResNet50 { get; } = new(
        Id: "resnet50-v2",
        DisplayName: "ResNet50-v2",
        FileName: "resnet50-v2-7.onnx",
        Description: "Fastest option. Good for exact or near-exact duplicates.",
        Normalization: NormalizationSettings.ImageNet,
        InputSize: 224,
        DownloadUrl: $"{ReleaseBaseUrl}/resnet50-v2-7.onnx",
        ExpectedFileSize: 102_442_452);

    /// <summary>
    /// CLIP ViT-B/32
    /// </summary>
    public static BundledModelInfo ClipVitB32 { get; } = new(
        Id: "clip-vit-b32",
        DisplayName: "CLIP ViT-B/32",
        FileName: "clip-vit-b32-image.onnx",
        Description: "Groups by content (e.g., all beach photos). Less strict on visual match.",
        Normalization: NormalizationSettings.Clip,
        InputSize: 224,
        DownloadUrl: $"{ReleaseBaseUrl}/clip-vit-b32-image.onnx",
        ExpectedFileSize: 976_517);

    /// <summary>
    /// All bundled models.
    /// </summary>
    public static IReadOnlyList<BundledModelInfo> All { get; } =
    [
        ResNet50,
        DinoV2VitB14,
        ClipVitB32
    ];

    /// <summary>
    /// Default model ID.
    /// </summary>
    public static string DefaultModelId => ResNet50.Id;

    /// <summary>
    /// Get model by ID.
    /// </summary>
    public static BundledModelInfo? GetById(string? id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        return All.FirstOrDefault(m => m.Id == id);
    }

    /// <summary>
    /// Check if model exists.
    /// </summary>
    public static bool Exists(string? id) => GetById(id) != null;
}