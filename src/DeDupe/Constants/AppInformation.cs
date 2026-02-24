using System.Reflection;

namespace DeDupe.Constants
{
    public static class AppInformation
    {
        public const string AppName = "DeDupe";
        public const string ModelReleaseTag = "v1.0.0-models";
        public const string GitHubRepo = $"https://github.com/Jan-Eric-B/{AppName}";
        public const string License = $"{GitHubRepo}/blob/main/LICENSE";
        public const string Privacy = $"{GitHubRepo}/blob/main/PRIVACY.md";

        public static readonly string Version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0";

        public static readonly string UserAgent = $"{AppName}/{Version} ({GitHubRepo})";

        public const string DependenciesJsonUri = $"ms-appx:///Assets/dependencies.json";
    }
}