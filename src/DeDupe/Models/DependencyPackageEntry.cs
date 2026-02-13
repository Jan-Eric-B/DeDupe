namespace DeDupe.Models
{
    public class DependencyPackageEntry
    {
        public required string Id { get; set; }
        public required string ResolvedVersion { get; set; }
        public string NuGetUrl => $"https://www.nuget.org/packages/{Id}/{ResolvedVersion}";
    }
}