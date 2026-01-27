using CommunityToolkit.Mvvm.ComponentModel;
using DeDupe.Enums;
using DeDupe.Services;
using System;
using System.Collections.Generic;

namespace DeDupe.ViewModels.Settings
{
    public partial class GeneralSettingsViewModel : SettingsPageViewModelBase
    {
        #region Fields

        private readonly ISettingsService _settingsService;

        #endregion Fields

        #region Observable Properties

        [ObservableProperty]
        public partial int SelectedThemeIndex { get; set; }

        [ObservableProperty]
        public partial int SelectedBackdropIndex { get; set; }

        [ObservableProperty]
        public partial int SelectedAccentColorIndex { get; set; }

        #endregion Observable Properties

        #region Properties

        public IEnumerable<AppTheme> ThemeOptions => Enum.GetValues<AppTheme>();
        public IEnumerable<AppBackdrop> BackdropOptions => Enum.GetValues<AppBackdrop>();
        public IEnumerable<AppAccentColor> AccentColorOptions => Enum.GetValues<AppAccentColor>();

        #endregion Properties

        #region Constructor

        public GeneralSettingsViewModel(ISettingsService settingsService)
        {
            _settingsService = settingsService;
            Title = "General";

            // Load current values
            SelectedThemeIndex = (int)_settingsService.Theme;
            SelectedBackdropIndex = (int)_settingsService.Backdrop;
            SelectedAccentColorIndex = (int)_settingsService.AccentColor;
        }

        #endregion Constructor

        #region Methods
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

        public override void OnNavigatedTo()
        {
            base.OnNavigatedTo();

            // Refresh from settings
            SelectedThemeIndex = (int)_settingsService.Theme;
            SelectedBackdropIndex = (int)_settingsService.Backdrop;
            SelectedAccentColorIndex = (int)_settingsService.AccentColor;
        }

        #endregion Methods
    }
}