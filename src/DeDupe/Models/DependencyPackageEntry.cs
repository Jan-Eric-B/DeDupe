namespace DeDupe.Models
{
    public class DependencyPackageEntry
    {
        public required string Id { get; set; }
        public required string ResolvedVersion { get; set; }
        public string? Url { get; set; }
        public bool IsNuGetPackage => Url is null;
        public bool IsNotNuGetPackage => Url is not null;
        public string PackageUrl => Url ?? $"https://www.nuget.org/packages/{Id}/{ResolvedVersion}";
    }
}