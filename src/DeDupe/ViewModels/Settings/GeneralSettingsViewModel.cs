using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeDupe.Enums;
using DeDupe.Localization;
using DeDupe.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DeDupe.ViewModels.Settings
{
    public partial class GeneralSettingsViewModel : SettingsPageViewModelBase
    {
        private readonly ISettingsService _settingsService;
        private readonly IDialogService _dialogService;
        private readonly ILogger<GeneralSettingsViewModel> _logger;

        public GeneralSettingsViewModel(ISettingsService settingsService, IDialogService dialogService, ILocalizer localizer, ILogger<GeneralSettingsViewModel> logger) : base(localizer)
        {
            _settingsService = settingsService;
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            Localizer.LanguageChanged += OnLanguageChanged;

            Title = L("GeneralSettings_PageTitle");
        }

        private void LoadSettings()
        {
            ThemeOptions = Localizer.BuildLocalizedOptions<AppTheme>();
            BackdropOptions = Localizer.BuildLocalizedOptions<AppBackdrop>();
            AccentColorOptions = Localizer.BuildLocalizedOptions<AppAccentColor>();

            SelectedThemeIndex = (int)_settingsService.Theme;
            SelectedBackdropIndex = (int)_settingsService.Backdrop;
            SelectedAccentColorIndex = (int)_settingsService.AccentColor;

            AvailableLanguages = [.. Localizer.GetAvailableLanguages().Select(code => new LanguageItem(code, $"Language_{code}"))];

            SelectedLanguage = AvailableLanguages.FirstOrDefault(x => x.Language == Localizer.GetCurrentLanguage())!;

            LogSettingsLoaded(_settingsService.Theme.ToString(), _settingsService.Backdrop.ToString(), _settingsService.AccentColor.ToString());
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

            LogSelectedThemeIndexChanged(value.ToString());
        }

        partial void OnSelectedBackdropIndexChanged(int value)
        {
            if (value < 0)
            {
                return;
            }

            _settingsService.Backdrop = (AppBackdrop)value;

            LogSelectedBackdropIndexChanged(value.ToString());
        }

        partial void OnSelectedAccentColorIndexChanged(int value)
        {
            if (value < 0)
            {
                return;
            }

            _settingsService.AccentColor = (AppAccentColor)value;

            LogSelectedAccentColorIndexChanged(value.ToString());
        }

        #endregion Appearance

        #region Localization

        [ObservableProperty]
        public partial LanguageItem SelectedLanguage { get; set; } = null!;

        public IReadOnlyList<LanguageItem> AvailableLanguages { get; private set; } = [];

        partial void OnSelectedLanguageChanged(LanguageItem value)
        {
            if (value is null)
            {
                return;
            }

            if (Localizer.GetCurrentLanguage() != value.Language)
            {
                Localizer.SetLanguage(value.Language);
                _settingsService.Language = value.Language;
            }
        }

        private void OnLanguageChanged(object? sender, LanguageChangedEventArgs e)
        {
            Title = L("GeneralSettings_PageTitle");

            // Get current selection
            int themeIdx = SelectedThemeIndex;
            int backdropIdx = SelectedBackdropIndex;
            int accentIdx = SelectedAccentColorIndex;

            // Rebuild dropdown items
            ThemeOptions = Localizer.BuildLocalizedOptions<AppTheme>();
            BackdropOptions = Localizer.BuildLocalizedOptions<AppBackdrop>();
            AccentColorOptions = Localizer.BuildLocalizedOptions<AppAccentColor>();

            // Restore selections
            SelectedThemeIndex = themeIdx;
            SelectedBackdropIndex = backdropIdx;
            SelectedAccentColorIndex = accentIdx;
            SelectedLanguage = AvailableLanguages.FirstOrDefault(x => x.Language == e.NewLanguage)!;

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