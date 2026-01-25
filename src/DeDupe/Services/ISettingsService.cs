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

        uint ResizeSize { get; set; }
        ResizeMethod ResizeMethod { get; set; }
        Color PaddingColor { get; set; }
        InterpolationMethod DownsamplingMethod { get; set; }
        InterpolationMethod UpsamplingMethod { get; set; }
        bool EnableBorderDetection { get; set; }
        int BorderDetectionTolerance { get; set; }
        OutputFormat OutputFormat { get; set; }
        uint Dpi { get; set; }
        ColorFormat ColorFormat { get; set; }
        bool UseCustomTempFolder { get; set; }
        string CustomTempFolderPath { get; set; }
        string TempFolderPath { get; }

        // Model Configuration
        bool UseBundledModel { get; set; }

        string CustomModelFilePath { get; set; }
        double MeanR { get; set; }
        double MeanG { get; set; }
        double MeanB { get; set; }
        double StdR { get; set; }
        double StdG { get; set; }
        double StdB { get; set; }

        // Events
        event EventHandler<AppTheme>? ThemeChanged;

        event EventHandler<AppBackdrop>? BackdropChanged;

        event EventHandler<AppAccentColor>? AccentColorChanged;

        event EventHandler? TempFolderPathChanged;

        event EventHandler<bool>? EnableResizingChanged;

        event EventHandler<uint>? ResizeSizeChanged;

        event EventHandler<ResizeMethod>? ResizeMethodChanged;

        event EventHandler<Color>? PaddingColorChanged;

        event EventHandler<InterpolationMethod>? DownsamplingMethodChanged;

        event EventHandler<InterpolationMethod>? UpsamplingMethodChanged;

        event EventHandler<bool>? EnableBorderDetectionChanged;

        event EventHandler<int>? BorderDetectionToleranceChanged;

        event EventHandler<OutputFormat>? OutputFormatChanged;

        event EventHandler<uint>? DpiChanged;

        event EventHandler<ColorFormat>? ColorFormatChanged;

        event EventHandler<bool>? UseBundledModelChanged;

        event EventHandler<string>? CustomModelFilePathChanged;

        event EventHandler<double>? MeanRChanged;

        event EventHandler<double>? MeanGChanged;

        event EventHandler<double>? MeanBChanged;

        event EventHandler<double>? StdRChanged;

        event EventHandler<double>? StdGChanged;

        event EventHandler<double>? StdBChanged;

        event EventHandler? ModelConfigurationChanged;

        // Methods
        T GetValue<T>(string key, T defaultValue);

        void SetValue<T>(string key, T value);
    }
}