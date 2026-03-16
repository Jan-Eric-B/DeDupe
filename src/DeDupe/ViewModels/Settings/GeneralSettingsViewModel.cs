using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeDupe.Enums;
using DeDupe.Helpers;
using DeDupe.Localization;
using DeDupe.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DeDupe.ViewModels.Settings
{
    public partial class GeneralSettingsViewModel : SettingsPageViewModelBase
    {
        private readonly ISettingsService _settingsService;
        private readonly IDialogService _dialogService;
        private readonly ILocalizer _localizer;
        private readonly ILogger<GeneralSettingsViewModel> _logger;

        public GeneralSettingsViewModel(ISettingsService settingsService, IDialogService dialogService, ILocalizer localizer, ILogger<GeneralSettingsViewModel> logger)
        {
            _settingsService = settingsService;
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
            _localizer = localizer ?? throw new ArgumentNullException(nameof(localizer));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _localizer.LanguageChanged += OnLanguageChanged;

            Title = "General";
        }

        private void LoadSettings()
        {
            ThemeOptions = _localizer.BuildLocalizedOptions<AppTheme>();
            BackdropOptions = _localizer.BuildLocalizedOptions<AppBackdrop>();
            AccentColorOptions = _localizer.BuildLocalizedOptions<AppAccentColor>();

            SelectedThemeIndex = (int)_settingsService.Theme;
            SelectedBackdropIndex = (int)_settingsService.Backdrop;
            SelectedAccentColorIndex = (int)_settingsService.AccentColor;

            AvailableLanguages = [.. _localizer.GetAvailableLanguages()];
            SelectedLanguage = _localizer.GetCurrentLanguage();

            string themeDescription = EnumExtensions.GetDescription(_settingsService.Theme);
            string backdropDescription = EnumExtensions.GetDescription(_settingsService.Backdrop);
            string accentColorDescription = EnumExtensions.GetDescription(_settingsService.AccentColor);
            LogSettingsLoaded(themeDescription, backdropDescription, accentColorDescription);
        }

        #region Appearance

        [ObservableProperty]
        public partial int SelectedThemeIndex { get; set; }

        [ObservableProperty]
        public partial int SelectedBackdropIndex { get; set; }

        [ObservableProperty]
        public partial int SelectedAccentColorIndex { get; set; }

        [ObservableProperty]
        public partial IReadOnlyList<LocalizedEnumOption<AppTheme>> ThemeOptions { get; set; } = [];

        [ObservableProperty]
        public partial IReadOnlyList<LocalizedEnumOption<AppBackdrop>> BackdropOptions { get; set; } = [];

        [ObservableProperty]
        public partial IReadOnlyList<LocalizedEnumOption<AppAccentColor>> AccentColorOptions { get; set; } = [];

        partial void OnSelectedThemeIndexChanged(int value)
        {
            if (value < 0)
            {
                return;
            }

            _settingsService.Theme = (AppTheme)value;

            LogSelectedThemeIndexChanged(EnumExtensions.GetDescription((AppTheme)value));
        }

        partial void OnSelectedBackdropIndexChanged(int value)
        {
            if (value < 0)
            {
                return;
            }

            _settingsService.Backdrop = (AppBackdrop)value;

            LogSelectedBackdropIndexChanged(EnumExtensions.GetDescription((AppBackdrop)value));
        }

        partial void OnSelectedAccentColorIndexChanged(int value)
        {
            if (value < 0)
            {
                return;
            }

            _settingsService.AccentColor = (AppAccentColor)value;

            LogSelectedAccentColorIndexChanged(EnumExtensions.GetDescription((AppAccentColor)value));
        }

        #endregion Appearance

        #region Localization

        [ObservableProperty]
        public partial string SelectedLanguage { get; set; } = string.Empty;

        public IReadOnlyList<string> AvailableLanguages { get; private set; } = [];

        partial void OnSelectedLanguageChanged(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return;
            }

            if (_localizer.GetCurrentLanguage() != value)
            {
                _localizer.SetLanguage(value);
                _settingsService.Language = value;
            }
        }

        private void OnLanguageChanged(object? sender, LanguageChangedEventArgs e)
        {
            // Get current selection
            int themeIdx = SelectedThemeIndex;
            int backdropIdx = SelectedBackdropIndex;
            int accentIdx = SelectedAccentColorIndex;

            // Rebuild dropdown items
            ThemeOptions = _localizer.BuildLocalizedOptions<AppTheme>();
            BackdropOptions = _localizer.BuildLocalizedOptions<AppBackdrop>();
            AccentColorOptions = _localizer.BuildLocalizedOptions<AppAccentColor>();

            // Restore selections
            SelectedThemeIndex = themeIdx;
            SelectedBackdropIndex = backdropIdx;
            SelectedAccentColorIndex = accentIdx;

            LogLanguageChanged(e.NewLanguage);
        }

        #endregion Localization

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

                await _dialogService.OpenFolderInExplorerAsync(path);
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

        [LoggerMessage(Level = LogLevel.Information, Message = "Language changed to {Language}")]
        private partial void LogLanguageChanged(string language);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Log folder open failed")]
        private partial void LogLogFolderOpenFailed(Exception ex);

        #endregion Logging
    }
}