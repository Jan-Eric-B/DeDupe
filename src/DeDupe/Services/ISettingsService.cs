using DeDupe.Enums;
using System;
using Windows.UI;

namespace DeDupe.Services
{
    public interface ISettingsService
    {
        // General
        AppTheme Theme { get; set; }
        AppBackdrop Backdrop { get; set; }
        AppAccentColor AccentColor { get; set; }

        // Pre-Processing
        bool EnableResizing { get; set; }
        int ResizeSize { get; set; }
        ResizeMethod ResizeMethod { get; set; }
        Color PaddingColor { get; set; }
        InterpolationMethod DownsamplingMethod { get; set; }
        InterpolationMethod UpsamplingMethod { get; set; }
        bool EnableBorderDetection { get; set; }
        int BorderDetectionTolerance { get; set; }
        OutputFormat OutputFormat { get; set; }
        ColorFormat ColorFormat { get; set; }
        bool UseCustomTempFolder { get; set; }
        string CustomTempFolderPath { get; set; }
        string TempFolderPath { get; }

        // Events: General
        event EventHandler<AppTheme>? ThemeChanged;
        event EventHandler<AppBackdrop>? BackdropChanged;
        event EventHandler<AppAccentColor>? AccentColorChanged;

        // Events: Pre-Processing
        event EventHandler? TempFolderPathChanged;
        event EventHandler<bool>? EnableResizingChanged;
        event EventHandler<int>? ResizeSizeChanged;
        event EventHandler<ResizeMethod>? ResizeMethodChanged;
        event EventHandler<Color>? PaddingColorChanged;
        event EventHandler<InterpolationMethod>? DownsamplingMethodChanged;
        event EventHandler<InterpolationMethod>? UpsamplingMethodChanged;
        event EventHandler<bool>? EnableBorderDetectionChanged;
        event EventHandler<int>? BorderDetectionToleranceChanged;
        event EventHandler<OutputFormat>? OutputFormatChanged;
        event EventHandler<ColorFormat>? ColorFormatChanged;

        // Methods
        T GetValue<T>(string key, T defaultValue);
        void SetValue<T>(string key, T value);
    }
}