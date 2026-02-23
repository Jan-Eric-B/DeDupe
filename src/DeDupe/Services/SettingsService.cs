using DeDupe.Enums;
using DeDupe.Models.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using Windows.Storage;
using Windows.UI;

namespace DeDupe.Services
{
    /// <inheritdoc/>
    public partial class SettingsService(ILogger<SettingsService> logger) : ISettingsService
    {
        private readonly ApplicationDataContainer _localSettings = ApplicationData.Current.LocalSettings;
        private readonly string _defaultTempFolderPath = Path.Combine(ApplicationData.Current.TemporaryFolder.Path, "ProcessedImages");
        private readonly string _defaultLogFolderPath = Path.Combine(ApplicationData.Current.LocalCacheFolder.Path, "Logs");
        private readonly ILogger<SettingsService> _logger = logger;

        /// <inheritdoc/>
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
                LogSettingReadFailed(key, ex);
            }

            return defaultValue;
        }

        /// <inheritdoc/>
        public void SetValue<T>(string key, T value)
        {
            try
            {
                _localSettings.Values[key] = value;
            }
            catch (Exception ex)
            {
                LogSettingWriteFailed(key, ex);
            }
        }

        #region Appearance

        private const string ThemeKey = "AppTheme";

        private const string BackdropKey = "AppBackdrop";

        private const string AccentColorKey = "AppAccentColor";

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
            get => (AppAccentColor)GetValue(AccentColorKey, (int)AppAccentColor.Application);
            set
            {
                if (AccentColor != value)
                {
                    SetValue(AccentColorKey, (int)value);
                    AccentColorChanged?.Invoke(this, value);
                }
            }
        }

        public event EventHandler<AppTheme>? ThemeChanged;

        public event EventHandler<AppBackdrop>? BackdropChanged;

        public event EventHandler<AppAccentColor>? AccentColorChanged;

        #endregion Appearance

        #region Performance

        private const string ParallelProcessingCoresKey = "ParallelProcessingCores";

        private const string EnableGpuAccelerationKey = "EnableGpuAcceleration";

        private const string InferenceBatchSizeKey = "InferenceBatchSize";

        public int ParallelProcessingCores
        {
            get => GetValue(ParallelProcessingCoresKey, Environment.ProcessorCount);
            set
            {
                int clamped = Math.Clamp(value, 1, Environment.ProcessorCount);
                if (ParallelProcessingCores != clamped)
                {
                    SetValue(ParallelProcessingCoresKey, clamped);
                    ParallelProcessingCoresChanged?.Invoke(this, clamped);
                }
            }
        }

        public bool EnableGpuAcceleration
        {
            get => GetValue(EnableGpuAccelerationKey, true);
            set
            {
                if (EnableGpuAcceleration != value)
                {
                    SetValue(EnableGpuAccelerationKey, value);
                    EnableGpuAccelerationChanged?.Invoke(this, value);
                }
            }
        }

        public int InferenceBatchSize
        {
            get => GetValue(InferenceBatchSizeKey, 16);
            set
            {
                int clamped = Math.Clamp(value, 1, 64);
                if (InferenceBatchSize != clamped)
                {
                    SetValue(InferenceBatchSizeKey, clamped);
                    InferenceBatchSizeChanged?.Invoke(this, clamped);
                }
            }
        }

        public event EventHandler<int>? ParallelProcessingCoresChanged;

        public event EventHandler<bool>? EnableGpuAccelerationChanged;

        public event EventHandler<int>? InferenceBatchSizeChanged;

        #endregion Performance

        #region Resize

        private const string EnableResizingKey = "EnableResizing";

        private const string ResizeSizeKey = "ResizeSize";

        private const string ResizeMethodKey = "ResizeMethod";

        private const string PaddingColorKey = "PaddingColor";

        private const string DownsamplingMethodKey = "DownsamplingMethod";

        private const string UpsamplingMethodKey = "UpsamplingMethod";

        public bool EnableResizing
        {
            get => GetValue(EnableResizingKey, true);
            set
            {
                if (EnableResizing != value)
                {
                    SetValue(EnableResizingKey, value);
                    EnableResizingChanged?.Invoke(this, value);
                }
            }
        }

        public uint ResizeSize
        {
            get => GetValue(ResizeSizeKey, 320u);
            set
            {
                if (ResizeSize != value)
                {
                    SetValue(ResizeSizeKey, value);
                    ResizeSizeChanged?.Invoke(this, value);
                }
            }
        }

        public ResizeMethod ResizeMethod
        {
            get => (ResizeMethod)GetValue(ResizeMethodKey, (int)ResizeMethod.Stretch);
            set
            {
                if (ResizeMethod != value)
                {
                    SetValue(ResizeMethodKey, (int)value);
                    ResizeMethodChanged?.Invoke(this, value);
                }
            }
        }

        public Color PaddingColor
        {
            get => UIntToColor(GetValue<uint>(PaddingColorKey, 0xFFFFFFFF));
            set
            {
                if (PaddingColor != value)
                {
                    SetValue(PaddingColorKey, ColorToUInt(value));
                    PaddingColorChanged?.Invoke(this, value);
                }
            }
        }

        private static Color UIntToColor(uint value)
        {
            return Color.FromArgb(
                (byte)(value >> 24),
                (byte)(value >> 16),
                (byte)(value >> 8),
                (byte)value);
        }

        private static uint ColorToUInt(Color color)
        {
            return ((uint)color.A << 24) | ((uint)color.R << 16) | ((uint)color.G << 8) | color.B;
        }

        public InterpolationMethod DownsamplingMethod
        {
            get => (InterpolationMethod)GetValue(DownsamplingMethodKey, (int)InterpolationMethod.Lanczos);
            set
            {
                if (DownsamplingMethod != value)
                {
                    SetValue(DownsamplingMethodKey, (int)value);
                    DownsamplingMethodChanged?.Invoke(this, value);
                }
            }
        }

        public InterpolationMethod UpsamplingMethod
        {
            get => (InterpolationMethod)GetValue(UpsamplingMethodKey, (int)InterpolationMethod.Lanczos);
            set
            {
                if (UpsamplingMethod != value)
                {
                    SetValue(UpsamplingMethodKey, (int)value);
                    UpsamplingMethodChanged?.Invoke(this, value);
                }
            }
        }

        public event EventHandler<bool>? EnableResizingChanged;

        public event EventHandler<uint>? ResizeSizeChanged;

        public event EventHandler<ResizeMethod>? ResizeMethodChanged;

        public event EventHandler<Color>? PaddingColorChanged;

        public event EventHandler<InterpolationMethod>? DownsamplingMethodChanged;

        public event EventHandler<InterpolationMethod>? UpsamplingMethodChanged;

        #endregion Resize

        #region Border Detection

        private const string EnableBorderDetectionKey = "EnableBorderDetection";

        private const string BorderDetectionToleranceKey = "BorderDetectionTolerance";

        public bool EnableBorderDetection
        {
            get => GetValue(EnableBorderDetectionKey, true);
            set
            {
                if (EnableBorderDetection != value)
                {
                    SetValue(EnableBorderDetectionKey, value);
                    EnableBorderDetectionChanged?.Invoke(this, value);
                }
            }
        }

        public int BorderDetectionTolerance
        {
            get => GetValue(BorderDetectionToleranceKey, 80);
            set
            {
                if (BorderDetectionTolerance != value)
                {
                    SetValue(BorderDetectionToleranceKey, value);
                    BorderDetectionToleranceChanged?.Invoke(this, value);
                }
            }
        }

        public event EventHandler<bool>? EnableBorderDetectionChanged;

        public event EventHandler<int>? BorderDetectionToleranceChanged;

        #endregion Border Detection

        #region Output

        private const string OutputFormatKey = "OutputFormat";

        private const string DpiKey = "Dpi";

        private const string ColorFormatKey = "ColorFormat";

        public OutputFormat OutputFormat
        {
            get => (OutputFormat)GetValue(OutputFormatKey, (int)OutputFormat.JPEG);
            set
            {
                if (OutputFormat != value)
                {
                    SetValue(OutputFormatKey, (int)value);
                    OutputFormatChanged?.Invoke(this, value);
                }
            }
        }

        public uint Dpi
        {
            get => GetValue(DpiKey, 96u);
            set
            {
                if (Dpi != value)
                {
                    SetValue(DpiKey, value);
                    DpiChanged?.Invoke(this, value);
                }
            }
        }

        public ColorFormat ColorFormat
        {
            get => GetValue(ColorFormatKey, ColorFormat.RGB8);
            set
            {
                if (ColorFormat != value)
                {
                    SetValue(ColorFormatKey, (int)value);
                    ColorFormatChanged?.Invoke(this, value);
                }
            }
        }

        public event EventHandler<OutputFormat>? OutputFormatChanged;

        public event EventHandler<uint>? DpiChanged;

        public event EventHandler<ColorFormat>? ColorFormatChanged;

        #endregion Output

        #region Temp Folder

        private const string UseCustomTempFolderKey = "UseCustomTempFolder";

        private const string CustomTempFolderPathKey = "CustomTempFolderPath";

        public bool UseCustomTempFolder
        {
            get => GetValue(UseCustomTempFolderKey, false);
            set
            {
                if (UseCustomTempFolder != value)
                {
                    SetValue(UseCustomTempFolderKey, value);
                    TempFolderPathChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public string CustomTempFolderPath
        {
            get => GetValue(CustomTempFolderPathKey, string.Empty);
            set
            {
                if (CustomTempFolderPath != value)
                {
                    SetValue(CustomTempFolderPathKey, value ?? string.Empty);
                    if (UseCustomTempFolder)
                    {
                        TempFolderPathChanged?.Invoke(this, EventArgs.Empty);
                    }
                }
            }
        }

        public string TempFolderPath => UseCustomTempFolder && !string.IsNullOrEmpty(CustomTempFolderPath) ? CustomTempFolderPath : _defaultTempFolderPath;

        public event EventHandler? TempFolderPathChanged;

        #endregion Temp Folder

        #region Model

        private const string UseBundledModelKey = "UseBundledModel";

        private const string CustomModelFilePathKey = "CustomModelFilePath";

        public bool UseBundledModel
        {
            get => GetValue(UseBundledModelKey, true);
            set
            {
                if (UseBundledModel != value)
                {
                    SetValue(UseBundledModelKey, value);
                    UseBundledModelChanged?.Invoke(this, value);
                    ModelConfigurationChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public string CustomModelFilePath
        {
            get => GetValue(CustomModelFilePathKey, string.Empty);
            set
            {
                if (CustomModelFilePath != value)
                {
                    SetValue(CustomModelFilePathKey, value ?? string.Empty);
                    CustomModelFilePathChanged?.Invoke(this, value ?? string.Empty);
                    if (!UseBundledModel)
                    {
                        ModelConfigurationChanged?.Invoke(this, EventArgs.Empty);
                    }
                }
            }
        }

        public event EventHandler<bool>? UseBundledModelChanged;

        public event EventHandler<string>? CustomModelFilePathChanged;

        /// <inheritdoc/>
        public event EventHandler? ModelConfigurationChanged;

        #endregion Model

        #region Normalization

        private const string MeanRKey = "NormalizationMeanR";

        private const string MeanGKey = "NormalizationMeanG";

        private const string MeanBKey = "NormalizationMeanB";

        private const string StdRKey = "NormalizationStdR";

        private const string StdGKey = "NormalizationStdG";

        private const string StdBKey = "NormalizationStdB";

        public NormalizationSettings Normalization
        {
            get => new(
                GetValue(MeanRKey, NormalizationSettings.Default.MeanR),
                GetValue(MeanGKey, NormalizationSettings.Default.MeanG),
                GetValue(MeanBKey, NormalizationSettings.Default.MeanB),
                GetValue(StdRKey, NormalizationSettings.Default.StdR),
                GetValue(StdGKey, NormalizationSettings.Default.StdG),
                GetValue(StdBKey, NormalizationSettings.Default.StdB));
            set
            {
                NormalizationSettings current = Normalization;
                if (current != value)
                {
                    SetValue(MeanRKey, value.MeanR);
                    SetValue(MeanGKey, value.MeanG);
                    SetValue(MeanBKey, value.MeanB);
                    SetValue(StdRKey, value.StdR);
                    SetValue(StdGKey, value.StdG);
                    SetValue(StdBKey, value.StdB);

                    NormalizationChanged?.Invoke(this, value);
                    ModelConfigurationChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public event EventHandler<NormalizationSettings>? NormalizationChanged;

        #endregion Normalization

        #region Similarity

        private const string SimilarityThresholdKey = "SimilarityThreshold";

        private const string AutoAnalyzeSimilarityKey = "AutoAnalyzeSimilarity";

        public double SimilarityThreshold
        {
            get => GetValue(SimilarityThresholdKey, 0.75);
            set
            {
                double clamped = Math.Clamp(value, 0.3, 1.0);
                if (Math.Abs(SimilarityThreshold - clamped) > 0.001)
                {
                    SetValue(SimilarityThresholdKey, clamped);
                    SimilarityThresholdChanged?.Invoke(this, clamped);
                }
            }
        }

        public bool AutoAnalyzeSimilarity
        {
            get => GetValue(AutoAnalyzeSimilarityKey, true);
            set
            {
                if (AutoAnalyzeSimilarity != value)
                {
                    SetValue(AutoAnalyzeSimilarityKey, value);
                    AutoAnalyzeSimilarityChanged?.Invoke(this, value);
                }
            }
        }

        public event EventHandler<double>? SimilarityThresholdChanged;

        public event EventHandler<bool>? AutoAnalyzeSimilarityChanged;

        #endregion Similarity

        #region Log Folder

        public string LogFolderPath => _defaultLogFolderPath;

        #endregion Log Folder

        #region Logging

        [LoggerMessage(Level = LogLevel.Warning, Message = "Setting read failed for key {SettingKey}, using default value")]
        private partial void LogSettingReadFailed(string settingKey, Exception ex);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Setting write failed for key {SettingKey}")]
        private partial void LogSettingWriteFailed(string settingKey, Exception ex);

        #endregion Logging
    }
}