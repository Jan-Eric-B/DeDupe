using DeDupe.Enums;
using System;
using Windows.Storage;

namespace DeDupe.Services
{
    public class SettingsService : ISettingsService
    {
        #region Fields

        private readonly ApplicationDataContainer _localSettings;

        #endregion Fields

        #region Keys

        // General
        private const string ThemeKey = "AppTheme";

        private const string BackdropKey = "AppBackdrop";
        private const string AccentColorKey = "AppAccentColor";

        #endregion Keys

        #region Properties

        // General
        public AppTheme Theme
        {
            get => (AppTheme)GetValue(ThemeKey, (int)AppTheme.System);
            set
            {
                if (Theme != value)
                {
                    SetValue(ThemeKey, (int)value);
                    ThemeChanged?.Invoke(this, value);
                }
            }
        }

        public AppBackdrop Backdrop
        {
            get => (AppBackdrop)GetValue(BackdropKey, (int)AppBackdrop.Mica);
            set
            {
                if (Backdrop != value)
                {
                    SetValue(BackdropKey, (int)value);
                    BackdropChanged?.Invoke(this, value);
                }
            }
        }

        public AppAccentColor AccentColor
        {
            get => (AppAccentColor)GetValue(AccentColorKey, (int)AppAccentColor.System);
            set
            {
                if (AccentColor != value)
                {
                    SetValue(AccentColorKey, (int)value);
                    AccentColorChanged?.Invoke(this, value);
                }
            }
        }

        #endregion Properties

        #region Events

        // General
        public event EventHandler<AppTheme>? ThemeChanged;

        public event EventHandler<AppBackdrop>? BackdropChanged;

        public event EventHandler<AppAccentColor>? AccentColorChanged;

        #endregion Events

        #region Constructor

        public SettingsService()
        {
            _localSettings = ApplicationData.Current.LocalSettings;
        }

        #endregion Constructor

        #region Methods

        public T GetValue<T>(string key, T defaultValue)
        {
            try
            {
                if (_localSettings.Values.TryGetValue(key, out object? value) && value is T typedValue)
                {
                    return typedValue;
                }
            }
            catch (Exception ex)
            {
                // TODO Logging
                // Return default
            }

            return defaultValue;
        }

        public void SetValue<T>(string key, T value)
        {
            try
            {
                _localSettings.Values[key] = value;
            }
            catch (Exception ex)
            {
                // TODO Logging
            }
        }

        #endregion Methods
    }
}