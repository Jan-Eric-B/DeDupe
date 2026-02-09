using DeDupe.Enums;
using DeDupe.Models.Configuration;
using System;
using Windows.UI;

namespace DeDupe.Services
{
    public interface ISettingsService
    {
        #region General

        AppTheme Theme { get; set; }
        AppBackdrop Backdrop { get; set; }
        AppAccentColor AccentColor { get; set; }

        #endregion General

        #region Processing

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
        int ParallelProcessingCores { get; set; }

        #endregion Processing

        #region Model Configuration

        bool UseBundledModel { get; set; }
        string SelectedBundledModelId { get; set; }
        string CustomModelFilePath { get; set; }
        NormalizationSettings Normalization { get; set; }

        #endregion Model Configuration

        #region Feature Extraction

        bool EnableGpuAcceleration { get; set; }
        int InferenceBatchSize { get; set; }

        #endregion Feature Extraction

        #region Events

        // General
        event EventHandler<AppTheme>? ThemeChanged;

        event EventHandler<AppBackdrop>? BackdropChanged;

        event EventHandler<AppAccentColor>? AccentColorChanged;

        // Processing
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

        event EventHandler<int>? ParallelProcessingCoresChanged;

        // Model Configuration
        event EventHandler<bool>? UseBundledModelChanged;

        event EventHandler<string>? SelectedBundledModelIdChanged;

        event EventHandler<string>? CustomModelFilePathChanged;

        event EventHandler<NormalizationSettings>? NormalizationChanged;

        event EventHandler? ModelConfigurationChanged;

        // Feature Extraction Performance
        event EventHandler<bool>? EnableGpuAccelerationChanged;

        event EventHandler<int>? InferenceBatchSizeChanged;

        #endregion Events

        #region Methods

        T GetValue<T>(string key, T defaultValue);

        void SetValue<T>(string key, T value);

        #endregion Methods
    }
}