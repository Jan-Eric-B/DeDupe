using DeDupe.Enums;
using DeDupe.Models.Configuration;
using System;
using Windows.UI;

namespace DeDupe.Services
{
    /// <summary>
    /// Provides application settings backed by local storage.
    /// </summary>
    public interface ISettingsService
    {
        /// <summary>
        /// Reads setting from local storage by key, returning <paramref name="defaultValue"/>.
        /// </summary>
        T GetValue<T>(string key, T defaultValue);

        /// <summary>
        /// Writes setting from local storage by key.
        /// </summary>
        void SetValue<T>(string key, T value);

        #region Appearance

        AppTheme Theme { get; set; }

        AppBackdrop Backdrop { get; set; }

        AppAccentColor AccentColor { get; set; }

        event EventHandler<AppTheme>? ThemeChanged;

        event EventHandler<AppBackdrop>? BackdropChanged;

        event EventHandler<AppAccentColor>? AccentColorChanged;

        #endregion Appearance

        #region Language

        string Language { get; set; }

        #endregion Language

        #region Performance

        int ParallelProcessingCores { get; set; }

        bool EnableGpuAcceleration { get; set; }

        int InferenceBatchSize { get; set; }

        event EventHandler<int>? ParallelProcessingCoresChanged;

        event EventHandler<bool>? EnableGpuAccelerationChanged;

        event EventHandler<int>? InferenceBatchSizeChanged;

        #endregion Performance

        #region Resize

        bool EnableResizing { get; set; }

        uint ResizeSize { get; set; }

        ResizeMethod ResizeMethod { get; set; }

        Color PaddingColor { get; set; }

        InterpolationMethod DownsamplingMethod { get; set; }

        InterpolationMethod UpsamplingMethod { get; set; }

        bool Compand { get; set; }

        event EventHandler<bool>? EnableResizingChanged;

        event EventHandler<uint>? ResizeSizeChanged;

        event EventHandler<ResizeMethod>? ResizeMethodChanged;

        event EventHandler<Color>? PaddingColorChanged;

        event EventHandler<InterpolationMethod>? DownsamplingMethodChanged;

        event EventHandler<InterpolationMethod>? UpsamplingMethodChanged;

        event EventHandler<bool>? CompandChanged;

        #endregion Resize

        #region Border Detection

        bool EnableBorderDetection { get; set; }

        int BorderDetectionTolerance { get; set; }

        event EventHandler<bool>? EnableBorderDetectionChanged;

        event EventHandler<int>? BorderDetectionToleranceChanged;

        #endregion Border Detection

        #region Output

        OutputFormat OutputFormat { get; set; }

        uint Dpi { get; set; }

        ColorFormat ColorFormat { get; set; }

        event EventHandler<OutputFormat>? OutputFormatChanged;

        event EventHandler<uint>? DpiChanged;

        event EventHandler<ColorFormat>? ColorFormatChanged;

        #endregion Output

        #region Temp Folder

        bool UseCustomTempFolder { get; set; }

        string CustomTempFolderPath { get; set; }

        string TempFolderPath { get; }

        event EventHandler? TempFolderPathChanged;

        #endregion Temp Folder

        #region Model

        bool UseBundledModel { get; set; }

        string CustomModelFilePath { get; set; }

        event EventHandler<bool>? UseBundledModelChanged;

        event EventHandler<string>? CustomModelFilePathChanged;

        /// <summary>
        /// Raised when any setting that affects model loading or inference changes, including model selection, custom
        /// paths, and normalization values.
        /// </summary>
        event EventHandler? ModelConfigurationChanged;

        #endregion Model

        #region Inference

        TensorLayout TensorLayout { get; set; }

        event EventHandler<TensorLayout>? TensorLayoutChanged;

        #endregion Inference

        #region Normalization

        NormalizationSettings Normalization { get; set; }

        event EventHandler<NormalizationSettings>? NormalizationChanged;

        #endregion Normalization

        #region Similarity

        double SimilarityThreshold { get; set; }

        bool AutoAnalyzeSimilarity { get; set; }

        event EventHandler<double>? SimilarityThresholdChanged;

        event EventHandler<bool>? AutoAnalyzeSimilarityChanged;

        #endregion Similarity

        #region Log Folder

        string LogFolderPath { get; }

        #endregion Log Folder
    }
}