using DeDupe.Enums;
using System;

namespace DeDupe.Services
{
    public interface ISettingsService
    {
        // General
        AppTheme Theme { get; set; }

        AppBackdrop Backdrop { get; set; }
        AppAccentColor AccentColor { get; set; }

        // Events
        event EventHandler<AppTheme>? ThemeChanged;

        event EventHandler<AppBackdrop>? BackdropChanged;

        event EventHandler<AppAccentColor>? AccentColorChanged;

        // Methods
        T GetValue<T>(string key, T defaultValue);

        void SetValue<T>(string key, T value);
    }
}