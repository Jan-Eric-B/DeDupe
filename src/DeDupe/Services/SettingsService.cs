using DeDupe.Enums;
using System;
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

        // Pre-Processing
        private const string EnableResizingKey = "EnableResizing";
        private const string ResizeSizeKey = "ResizeSize";
        private const string ResizeMethodKey = "ResizeMethod";
        private const string PaddingColorKey = "PaddingColor";
        private const string DownsamplingMethodKey = "DownsamplingMethod";
        private const string UpsamplingMethodKey = "UpsamplingMethod";

        private const string EnableBorderDetectionKey = "EnableBorderDetection";
        private const string BorderDetectionToleranceKey = "BorderDetectionTolerance";

        private const string OutputFormatKey = "OutputFormat";
        private const string ColorFormatKey = "ColorFormat";

        private const string UseCustomTempFolderKey = "UseCustomTempFolder";
        private const string CustomTempFolderPathKey = "CustomTempFolderPath";

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

        // Pre-Processing
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

        public int ResizeSize
        {
            get => GetValue(ResizeSizeKey, 224);
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
            get => (ResizeMethod)GetValue(ResizeMethodKey, 0);
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
            get => UIntToColor(GetValue<uint>(PaddingColorKey, 0xFF000000));
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
            get => (InterpolationMethod)GetValue(DownsamplingMethodKey, 0);
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
            get => (InterpolationMethod)GetValue(UpsamplingMethodKey, 0);
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
            get => (OutputFormat)GetValue(OutputFormatKey, 1);
            set
            {
                if (OutputFormat != value)
                {
                    SetValue(OutputFormatKey, (int)value);
                    OutputFormatChanged?.Invoke(this, value);
                }
            }
        }

        public ColorFormat ColorFormat
        {
            get => (ColorFormat)GetValue(ColorFormatKey, 0);
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
            get => GetValue(CustomTempFolderPathKey, _defaultTempFolderPath);
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

        #endregion Properties

        #region Events

        // General
        public event EventHandler<AppTheme>? ThemeChanged;
        public event EventHandler<AppBackdrop>? BackdropChanged;
        public event EventHandler<AppAccentColor>? AccentColorChanged;

        // Pre-Processing
        public event EventHandler<bool>? EnableResizingChanged;
        public event EventHandler<int>? ResizeSizeChanged;
        public event EventHandler<ResizeMethod>? ResizeMethodChanged;
        public event EventHandler<Color>? PaddingColorChanged;
        public event EventHandler<InterpolationMethod>? DownsamplingMethodChanged;
        public event EventHandler<InterpolationMethod>? UpsamplingMethodChanged;
        public event EventHandler<bool>? EnableBorderDetectionChanged;
        public event EventHandler<int>? BorderDetectionToleranceChanged;
        public event EventHandler<OutputFormat>? OutputFormatChanged;
        public event EventHandler<ColorFormat>? ColorFormatChanged;
        public event EventHandler? TempFolderPathChanged;

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
                // TODO Logging
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