using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeDupe.Enums;
using DeDupe.Helpers;
using DeDupe.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Windows.System;

namespace DeDupe.ViewModels.Settings
{
    public partial class GeneralSettingsViewModel : SettingsPageViewModelBase
    {
        private readonly ISettingsService _settingsService;
        private readonly ILogger<GeneralSettingsViewModel> _logger;

        public GeneralSettingsViewModel(ISettingsService settingsService, ILogger<GeneralSettingsViewModel> logger)
        {
            _settingsService = settingsService;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            Title = "General";
        }

        private void LoadSettings()
        {
            SelectedThemeIndex = (int)_settingsService.Theme;
            SelectedBackdropIndex = (int)_settingsService.Backdrop;
            SelectedAccentColorIndex = (int)_settingsService.AccentColor;

            string themeDescripion = EnumExtensions.GetDescription(_settingsService.Theme);
            string backdropDescription = EnumExtensions.GetDescription(_settingsService.Backdrop);
            string accentColorDescription = EnumExtensions.GetDescription(_settingsService.AccentColor);
            LogSettingsLoaded(themeDescripion, backdropDescription, accentColorDescription);
        }

        #region Appearance

        [ObservableProperty]
        public partial int SelectedThemeIndex { get; set; }

        [ObservableProperty]
        public partial int SelectedBackdropIndex { get; set; }

        [ObservableProperty]
        public partial int SelectedAccentColorIndex { get; set; }

        public IEnumerable<AppTheme> ThemeOptions => Enum.GetValues<AppTheme>();

        public IEnumerable<AppBackdrop> BackdropOptions => Enum.GetValues<AppBackdrop>();

        public IEnumerable<AppAccentColor> AccentColorOptions => Enum.GetValues<AppAccentColor>();

        partial void OnSelectedThemeIndexChanged(int value)
        {
            _settingsService.Theme = (AppTheme)value;

            LogSelectedThemeIndexChanged(EnumExtensions.GetDescription((AppTheme)value));
        }

        partial void OnSelectedBackdropIndexChanged(int value)
        {
            _settingsService.Backdrop = (AppBackdrop)value;

            LogSelectedBackdropIndexChanged(EnumExtensions.GetDescription((AppTheme)value));
        }

        partial void OnSelectedAccentColorIndexChanged(int value)
        {
            _settingsService.AccentColor = (AppAccentColor)value;

            LogSelectedAccentColorIndexChanged(EnumExtensions.GetDescription((AppTheme)value));
        }

        #endregion Appearance

        #region Log Folder

        [RelayCommand]
        private async Task OpenLogFolderAsync()
        {
            try
            {
                string path = _settingsService.LogFolderPath;
                if (string.IsNullOrEmpty(path))
                {
                    return;
                }

                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                    LogLogFolderCreated(path);
                }

                await Launcher.LaunchFolderPathAsync(path);
            }
            catch (Exception ex)
            {
                LogLogFolderOpenFailed(ex);
            }
        }

        #endregion Log Folder

        #region Navigation

        public override void OnNavigatedTo()
        {
            base.OnNavigatedTo();

            LoadSettings();
        }

        #endregion Navigation

        #region Logging

        [LoggerMessage(Level = LogLevel.Debug, Message = "General settings loaded: Theme={SelectedTheme}; Backdrop={SelectedBackdrop}; Accent color={SelectedAccentColor}")]
        private partial void LogSettingsLoaded(string selectedTheme, string selectedBackdrop, string selectedAccentColor);

        [LoggerMessage(Level = LogLevel.Information, Message = "Theme changed to {SelectedTheme}")]
        private partial void LogSelectedThemeIndexChanged(string selectedTheme);

        [LoggerMessage(Level = LogLevel.Information, Message = "Backdrop changed to {SelectedBackdrop}")]
        private partial void LogSelectedBackdropIndexChanged(string selectedBackdrop);

        [LoggerMessage(Level = LogLevel.Information, Message = "AccentColor changed to {SelectedAccentColor}")]
        private partial void LogSelectedAccentColorIndexChanged(string selectedAccentColor);

        [LoggerMessage(Level = LogLevel.Debug, Message = "log folder created at {FolderPath}")]
        private partial void LogLogFolderCreated(string folderPath);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Log folder open failed")]
        private partial void LogLogFolderOpenFailed(Exception ex);

        #endregion Logging
    }
}