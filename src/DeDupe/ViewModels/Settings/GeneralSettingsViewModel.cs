using CommunityToolkit.Mvvm.ComponentModel;
using DeDupe.Enums;
using DeDupe.Services;
using System;
using System.Collections.Generic;

namespace DeDupe.ViewModels.Settings
{
    public partial class GeneralSettingsViewModel : SettingsPageViewModelBase
    {
        private readonly ISettingsService _settingsService;

        public GeneralSettingsViewModel(ISettingsService settingsService)
        {
            _settingsService = settingsService;

            Title = "General";
        }

        private void LoadSettings()
        {
            SelectedThemeIndex = (int)_settingsService.Theme;
            SelectedBackdropIndex = (int)_settingsService.Backdrop;
            SelectedAccentColorIndex = (int)_settingsService.AccentColor;
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
        }

        partial void OnSelectedBackdropIndexChanged(int value)
        {
            _settingsService.Backdrop = (AppBackdrop)value;
        }

        partial void OnSelectedAccentColorIndexChanged(int value)
        {
            _settingsService.AccentColor = (AppAccentColor)value;
        }

        #endregion Appearance

        #region Navigation

        public override void OnNavigatedTo()
        {
            base.OnNavigatedTo();

            LoadSettings();
        }

        #endregion Navigation
    }
}