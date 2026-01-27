using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeDupe.Enums;
using DeDupe.Helpers;
using DeDupe.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System;
using Windows.UI;

namespace DeDupe.ViewModels.Settings
{
    public partial class ImageProcessingSettingsViewModel : SettingsPageViewModelBase
    {
        #region Fields

        private readonly ISettingsService _settingsService;

        #endregion Fields

        #region Observable Properties

        // Resize Settings
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsResizeEnabled))]
        public partial bool EnableResizing { get; set; }

        [ObservableProperty]
        public partial uint ResizeSize { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsPaddingColorVisible))]
        public partial ResizeMethod ResizeMethod { get; set; }

        [ObservableProperty]
        public partial Color PaddingColor { get; set; }

        [ObservableProperty]
        public partial InterpolationMethod DownsamplingMethod { get; set; }

        [ObservableProperty]
        public partial InterpolationMethod UpsamplingMethod { get; set; }

        // Border Detection
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsBorderDetectionEnabled))]
        public partial bool EnableBorderDetection { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(BorderDetectionAggressivenessLabel))]
        public partial int BorderDetectionTolerance { get; set; }

        // Output Settings
        [ObservableProperty]
        public partial OutputFormat OutputFormat { get; set; }

        [ObservableProperty]
        public partial uint Dpi { get; set; }

        [ObservableProperty]
        public partial ColorFormat ColorFormat { get; set; }

        // Temp Folder Settings
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsCustomTempFolderEnabled))]
        public partial bool UseCustomTempFolder { get; set; }

        [ObservableProperty]
        public partial string CustomTempFolderPath { get; set; } = string.Empty;

        // Performance Settings
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ParallelCoresDisplayText))]
        public partial int ParallelProcessingCores { get; set; }

        // GPU Acceleration Settings
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsBatchSizeEnabled))]
        public partial bool EnableGpuAcceleration { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(BatchSizeDisplayText))]
        public partial int InferenceBatchSize { get; set; }

        #endregion Observable Properties

        #region Properties

        // Resize
        public bool IsResizeEnabled => EnableResizing;

        public bool IsPaddingColorVisible => ResizeMethod == ResizeMethod.Padding;

        // Border Detection
        public bool IsBorderDetectionEnabled => EnableBorderDetection;

        /// <summary>
        /// Human-readable label for aggressiveness level.
        /// </summary>
        public string BorderDetectionAggressivenessLabel => BorderDetectionTolerance switch
        {
            < 20 => "Very Conservative",
            < 50 => "Conservative",
            < 100 => "Balanced",
            < 150 => "Aggressive",
            _ => "Very Aggressive"
        };

        // Temp Folder
        public bool IsCustomTempFolderEnabled => UseCustomTempFolder;

        // Performance
        /// <summary>
        /// Maximum number of CPU cores available on this system.
        /// </summary>
        public int MaxParallelCores => Environment.ProcessorCount;

        /// <summary>
        /// Display text for parallel cores slider.
        /// </summary>
        public string ParallelCoresDisplayText => ParallelProcessingCores.ToString();

        // GPU Acceleration
        /// <summary>
        /// Batch size control is enabled.
        /// </summary>
        public bool IsBatchSizeEnabled => true;

        /// <summary>
        /// Display text for batch size slider.
        /// </summary>
        public string BatchSizeDisplayText => InferenceBatchSize.ToString();

        /// <summary>
        /// Human-readable description of GPU acceleration status.
        /// </summary>
        public string GpuAccelerationDescription => EnableGpuAcceleration
            ? "GPU acceleration"
            : "CPU-only mode";

        // ComboBox Enums
        public IEnumerable<InterpolationMethod> InterpolationMethods => Enum.GetValues<InterpolationMethod>();

        public IEnumerable<ResizeMethod> ResizeMethods => Enum.GetValues<ResizeMethod>();
        public IEnumerable<OutputFormat> OutputFormats => Enum.GetValues<OutputFormat>();
        public IEnumerable<ColorFormat> ColorFormats => Enum.GetValues<ColorFormat>();

        #endregion Properties

        #region Constructor

        public ImageProcessingSettingsViewModel(ISettingsService settingsService)
        {
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            Title = "Image Processing";

            LoadSettings();
        }

        #endregion Constructor

        #region Commands

        [RelayCommand]
        private async Task BrowseTempFolderAsync()
        {
            try
            {
                FolderPicker picker = new()
                {
                    SuggestedStartLocation = PickerLocationId.DocumentsLibrary
                };
                picker.FileTypeFilter.Add("*");
                picker.InitializeForCurrentWindow();

                StorageFolder folder = await picker.PickSingleFolderAsync();
                if (folder != null)
                {
                    CustomTempFolderPath = folder.Path;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error browsing for folder: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task OpenTempFolderAsync()
        {
            try
            {
                string path = _settingsService.TempFolderPath;
                if (!string.IsNullOrEmpty(path))
                {
                    if (!Directory.Exists(path))
                    {
                        Directory.CreateDirectory(path);
                    }
                    await Launcher.LaunchFolderPathAsync(path);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error opening temp folder: {ex.Message}");
            }
        }

        #endregion Commands

        #region Methods

        private void LoadSettings()
        {
            // Resize
            EnableResizing = _settingsService.EnableResizing;
            ResizeSize = _settingsService.ResizeSize;
            ResizeMethod = _settingsService.ResizeMethod;
            PaddingColor = _settingsService.PaddingColor;
            DownsamplingMethod = _settingsService.DownsamplingMethod;
            UpsamplingMethod = _settingsService.UpsamplingMethod;

            // Border Detection
            EnableBorderDetection = _settingsService.EnableBorderDetection;
            BorderDetectionTolerance = _settingsService.BorderDetectionTolerance;

            // Output
            OutputFormat = _settingsService.OutputFormat;
            Dpi = _settingsService.Dpi;
            ColorFormat = _settingsService.ColorFormat;

            // Temp Folder
            UseCustomTempFolder = _settingsService.UseCustomTempFolder;
            CustomTempFolderPath = _settingsService.CustomTempFolderPath;

            // Performance
            ParallelProcessingCores = _settingsService.ParallelProcessingCores;

            // GPU Acceleration
            EnableGpuAcceleration = _settingsService.EnableGpuAcceleration;
            InferenceBatchSize = _settingsService.InferenceBatchSize;
        }

        // Temp Folder
        partial void OnUseCustomTempFolderChanged(bool value)
        {
            _settingsService.UseCustomTempFolder = value;
        }

        partial void OnCustomTempFolderPathChanged(string value)
        {
            _settingsService.CustomTempFolderPath = value;
        }

        // Resize Settings
        partial void OnEnableResizingChanged(bool value)
        {
            _settingsService.EnableResizing = value;
        }

        partial void OnResizeSizeChanged(uint value)
        {
            _settingsService.ResizeSize = value;
        }

        partial void OnResizeMethodChanged(ResizeMethod value)
        {
            _settingsService.ResizeMethod = value;
        }

        partial void OnPaddingColorChanged(Color value)
        {
            _settingsService.PaddingColor = value;
        }
        partial void OnDownsamplingMethodChanged(InterpolationMethod value)
        {
            _settingsService.DownsamplingMethod = value;
        }
        partial void OnUpsamplingMethodChanged(InterpolationMethod value)
        {
            _settingsService.UpsamplingMethod = value;
        }

        // Border Detection
        partial void OnEnableBorderDetectionChanged(bool value)
        {
            _settingsService.EnableBorderDetection = value;
        }

        partial void OnBorderDetectionToleranceChanged(int value)
        {
            _settingsService.BorderDetectionTolerance = value;
        }

        // Output Settings
        partial void OnOutputFormatChanged(OutputFormat value)
        {
            _settingsService.OutputFormat = value;
        }

        partial void OnDpiChanged(uint value)
        {
            _settingsService.Dpi = value;
        }

        partial void OnColorFormatChanged(ColorFormat value)
        {
            _settingsService.ColorFormat = value;
        }

        // Performance Settings
        partial void OnParallelProcessingCoresChanged(int value)
        {
            _settingsService.ParallelProcessingCores = value;
        }

        // GPU Acceleration Settings
        partial void OnEnableGpuAccelerationChanged(bool value)
        {
            _settingsService.EnableGpuAcceleration = value;
            OnPropertyChanged(nameof(GpuAccelerationDescription));
        }

        public override void OnNavigatedTo()
        {
            base.OnNavigatedTo();
            LoadSettings();
        }

        #endregion Methods
    }
}