namespace DeDupe.Models.Configuration;

public sealed record BundledModelInfo(string Id, string DisplayName, string FileName, string Description, NormalizationSettings Normalization, int InputSize = 224)
{
    public string RelativePath => $@"Resources\Models\{FileName}";
}