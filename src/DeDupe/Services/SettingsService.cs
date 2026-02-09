using DeDupe.Enums;
using DeDupe.Models.Configuration;
using System;
using System.Diagnostics;
using System.IO;
using Windows.Storage;
using Windows.UI;

namespace DeDupe.Services
{
    public class SettingsService : ISettingsService
    {
        #region Fields

        private readonly ApplicationDataContainer _localSettings;
        private readonly string _defaultTempFolderPath;

        #endregion Fields

        #region Keys

        // General
        private const string ThemeKey = "AppTheme";

        private const string BackdropKey = "AppBackdrop";
        private const string AccentColorKey = "AppAccentColor";

        // Processing
        private const string EnableResizingKey = "EnableResizing";

        private const string ResizeSizeKey = "ResizeSize";
        private const string ResizeMethodKey = "ResizeMethod";
        private const string PaddingColorKey = "PaddingColor";
        private const string DownsamplingMethodKey = "DownsamplingMethod";
        private const string UpsamplingMethodKey = "UpsamplingMethod";
        private const string EnableBorderDetectionKey = "EnableBorderDetection";
        private const string BorderDetectionToleranceKey = "BorderDetectionTolerance";
        private const string OutputFormatKey = "OutputFormat";
        private const string DpiKey = "Dpi";
        private const string ColorFormatKey = "ColorFormat";
        private const string UseCustomTempFolderKey = "UseCustomTempFolder";
        private const string CustomTempFolderPathKey = "CustomTempFolderPath";
        private const string ParallelProcessingCoresKey = "ParallelProcessingCores";

        // Model Configuration
        private const string UseBundledModelKey = "UseBundledModel";

        private const string SelectedBundledModelIdKey = "SelectedBundledModelId";
        private const string CustomModelFilePathKey = "CustomModelFilePath";
        private const string MeanRKey = "NormalizationMeanR";
        private const string MeanGKey = "NormalizationMeanG";
        private const string MeanBKey = "NormalizationMeanB";
        private const string StdRKey = "NormalizationStdR";
        private const string StdGKey = "NormalizationStdG";
        private const string StdBKey = "NormalizationStdB";

        // Feature Extraction Performance
        private const string EnableGpuAccelerationKey = "EnableGpuAcceleration";

        private const string InferenceBatchSizeKey = "InferenceBatchSize";

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

        // Processing
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
            get => GetValue(ResizeSizeKey, 224u);
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
            get => (ResizeMethod)GetValue(ResizeMethodKey, (int)ResizeMethod.Padding);
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
            get => (ColorFormat)GetValue(ColorFormatKey, ColorFormat.RGB8);
            set
            {
                if (ColorFormat != value)
                {
                    SetValue(ColorFormatKey, (int)value);
                    ColorFormatChanged?.Invoke(this, value);
                }
            }
        }

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

        public string TempFolderPath => UseCustomTempFolder && !string.IsNullOrEmpty(CustomTempFolderPath)
            ? CustomTempFolderPath
            : _defaultTempFolderPath;

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

        // Model Configuration
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

        public string SelectedBundledModelId
        {
            get => GetValue(SelectedBundledModelIdKey, BundledModelRegistry.DefaultModelId);
            set
            {
                string newValue = string.IsNullOrEmpty(value) ? BundledModelRegistry.DefaultModelId : value;
                if (SelectedBundledModelId != newValue)
                {
                    SetValue(SelectedBundledModelIdKey, newValue);
                    SelectedBundledModelIdChanged?.Invoke(this, newValue);
                    if (UseBundledModel)
                    {
                        ModelConfigurationChanged?.Invoke(this, EventArgs.Empty);
                    }
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

        // Feature Extraction Performance
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

        #endregion Properties

        #region Events

        // General
        public event EventHandler<AppTheme>? ThemeChanged;

        public event EventHandler<AppBackdrop>? BackdropChanged;

        public event EventHandler<AppAccentColor>? AccentColorChanged;

        // Processing
        public event EventHandler<bool>? EnableResizingChanged;

        public event EventHandler<uint>? ResizeSizeChanged;

        public event EventHandler<ResizeMethod>? ResizeMethodChanged;

        public event EventHandler<Color>? PaddingColorChanged;

        public event EventHandler<InterpolationMethod>? DownsamplingMethodChanged;

        public event EventHandler<InterpolationMethod>? UpsamplingMethodChanged;

        public event EventHandler<bool>? EnableBorderDetectionChanged;

        public event EventHandler<int>? BorderDetectionToleranceChanged;

        public event EventHandler<OutputFormat>? OutputFormatChanged;

        public event EventHandler<uint>? DpiChanged;

        public event EventHandler<ColorFormat>? ColorFormatChanged;

        public event EventHandler? TempFolderPathChanged;

        public event EventHandler<int>? ParallelProcessingCoresChanged;

        // Model Configuration
        public event EventHandler<bool>? UseBundledModelChanged;

        public event EventHandler<string>? SelectedBundledModelIdChanged;

        public event EventHandler<string>? CustomModelFilePathChanged;

        public event EventHandler<NormalizationSettings>? NormalizationChanged;

        public event EventHandler? ModelConfigurationChanged;

        // Feature Extraction Performance
        public event EventHandler<bool>? EnableGpuAccelerationChanged;

        public event EventHandler<int>? InferenceBatchSizeChanged;

        #endregion Events

        #region Constructor

        public SettingsService()
        {
            _localSettings = ApplicationData.Current.LocalSettings;
            _defaultTempFolderPath = Path.Combine(ApplicationData.Current.TemporaryFolder.Path, "ProcessedImages");
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
                Debug.WriteLine($"Error reading setting '{key}': {ex.Message}");
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
                Debug.WriteLine($"Error saving setting '{key}': {ex.Message}");
            }
        }

        private static uint ColorToUInt(Color color)
        {
            return ((uint)color.A << 24) | ((uint)color.R << 16) | ((uint)color.G << 8) | color.B;
        }

        private static Color UIntToColor(uint value)
        {
            return Color.FromArgb(
                (byte)(value >> 24),
                (byte)(value >> 16),
                (byte)(value >> 8),
                (byte)value);
        }

        #endregion Methods
    }
}