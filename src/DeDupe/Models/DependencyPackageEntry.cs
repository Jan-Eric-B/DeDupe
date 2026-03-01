namespace DeDupe.Models
{
    public class DependencyPackageEntry
    {
        public string Id { get; set; }
        public string ResolvedVersion { get; set; }
        public string NuGetUrl => $"https://www.nuget.org/packages/{Id}/{ResolvedVersion}";
    }
}