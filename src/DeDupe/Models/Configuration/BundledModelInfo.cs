using DeDupe.Enums;

namespace DeDupe.Models.Configuration
{
    public sealed record BundledModelInfo(
         string Id,
         string DisplayName,
         string FileName,
         string Description,
         string License,
         string Url,
         string Developers,
         NormalizationSettings Normalization,
         int InputSize = 224,
         string? DownloadUrl = null,
         long ExpectedFileSize = 0,
         string? ExpectedSha256 = null,
         ResizeMethod RecommendedResizeMethod = ResizeMethod.Padding)
    {
        public bool RequiresDownload => DownloadUrl is not null;
    }
}