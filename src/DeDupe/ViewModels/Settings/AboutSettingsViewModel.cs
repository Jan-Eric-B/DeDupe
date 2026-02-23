using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeDupe.Constants;
using DeDupe.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;

namespace DeDupe.ViewModels.Settings
{
    public partial class AboutSettingsViewModel : SettingsPageViewModelBase
    {
        private readonly ILogger<AboutSettingsViewModel> _logger;

        public AboutSettingsViewModel(ILogger<AboutSettingsViewModel> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            Title = "About";
        }

        [ObservableProperty]
        public partial string AppName { get; set; } = "DeDupe";

        [ObservableProperty]
        public partial string Publisher { get; set; } = "Jan-Eric-B";

        [ObservableProperty]
        public partial string AppVersion { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string DotNetVersion { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string ModelReleaseTag { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string CommitId { get; set; } = string.Empty;

        [ObservableProperty]
        public partial List<DependencyPackageEntry> Dependencies { get; set; } = [];

        private static readonly JsonSerializerOptions jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private async Task LoadInformation()
        {
            try
            {
                Package? package = Package.Current;
                PackageVersion version = package.Id.Version;

                AppName = package.DisplayName;
                Publisher = package.PublisherDisplayName;
                AppVersion = $"{version.Major}.{version.Minor}.{version.Build}.{version.Revision}";

                ModelReleaseTag = AppInformation.ModelReleaseTag;

                DotNetVersion = RuntimeInformation.FrameworkDescription;

                string? infoVersion = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
                CommitId = infoVersion?.Split('+').LastOrDefault() ?? "Unknown";

                Dependencies = await GetDependencies();

                LogInformationLoaded(AppName, Publisher, AppVersion, DotNetVersion, CommitId);
            }
            catch (InvalidOperationException ex)
            {
                AppVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "Unknown";

                LogInformationFailed(AppName, Publisher, AppVersion, DotNetVersion, CommitId, ex);
            }
        }

        private async Task<List<DependencyPackageEntry>> GetDependencies()
        {
            try
            {
                StorageFile file = await StorageFile.GetFileFromApplicationUriAsync(new Uri(AppInformation.DependenciesJsonUri));
                string json = await FileIO.ReadTextAsync(file);

                List<DependencyPackageEntry>? dependencies = JsonSerializer.Deserialize<List<DependencyPackageEntry>>(json, jsonOptions);

                return dependencies ?? [];
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse dependencies.json");
                return [];
            }
        }

        [RelayCommand]
        private void CopyToClipboard()
        {
            StringBuilder builder = new();
            builder.AppendLine($"App: {AppName}");
            builder.AppendLine($"Publisher: {Publisher}");
            builder.AppendLine($"Version: {AppVersion}");
            builder.AppendLine($"Commit: {CommitId}");
            builder.AppendLine($".NET Version: {DotNetVersion}");
            builder.AppendLine($"Model Release Version: {ModelReleaseTag}");

            DataPackage dataPackage = new();
            dataPackage.SetText(builder.ToString());
            Clipboard.SetContent(dataPackage);
        }

        #region Navigation

        public override async void OnNavigatedTo()
        {
            base.OnNavigatedTo();

            await LoadInformation();
        }

        #endregion Navigation

        #region Logging

        [LoggerMessage(Level = LogLevel.Debug, Message = "About information loaded: App name={AppName}; Publisher={Publisher}; App version={AppVersion}; .NET version={dotNetVersion}; Latest commit={latestCommit}")]
        private partial void LogInformationLoaded(string appName, string publisher, string appVersion, string dotNetVersion, string latestCommit);

        [LoggerMessage(Level = LogLevel.Error, Message = "About information loading failed: App name={AppName}; Publisher={Publisher}; App version={AppVersion}; .NET version={dotNetVersion}; Latest commit={latestCommit}")]
        private partial void LogInformationFailed(string appName, string publisher, string appVersion, string dotNetVersion, string latestCommit, Exception ex);

        #endregion Logging
    }
}