namespace DeDupe.Models.Configuration
{
    public sealed record BundledModelInfo(string Id, string DisplayName, string FileName, string Description, NormalizationSettings Normalization, int InputSize = 224, string? DownloadUrl = null, long ExpectedFileSize = 0, string? ExpectedSha256 = null)
    {
        /// <summary>
        /// Whether this model requires downloading.
        /// </summary>
        public bool RequiresDownload => DownloadUrl is not null;
    }
}