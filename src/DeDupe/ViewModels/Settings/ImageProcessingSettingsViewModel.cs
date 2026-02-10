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
        private readonly ISettingsService _settingsService;

        public ImageProcessingSettingsViewModel(ISettingsService settingsService)
        {
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));

            Title = "Image Processing";
        }

        private void LoadSettings()
        {
            // Performance
            ParallelProcessingCores = _settingsService.ParallelProcessingCores;
            EnableGpuAcceleration = _settingsService.EnableGpuAcceleration;
            InferenceBatchSize = _settingsService.InferenceBatchSize;

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
        }

        #region Performance

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ParallelCoresDisplayText))]
        public partial int ParallelProcessingCores { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsBatchSizeEnabled))]
        public partial bool EnableGpuAcceleration { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(BatchSizeDisplayText))]
        public partial int InferenceBatchSize { get; set; }

        public int MaxParallelCores => Environment.ProcessorCount;

        public string ParallelCoresDisplayText => ParallelProcessingCores.ToString();

        public bool IsBatchSizeEnabled => true;

        public string BatchSizeDisplayText => InferenceBatchSize.ToString();

        public string GpuAccelerationDescription => EnableGpuAcceleration ? "GPU acceleration" : "CPU-only mode";

        partial void OnParallelProcessingCoresChanged(int value)
        {
            _settingsService.ParallelProcessingCores = value;
        }

        partial void OnEnableGpuAccelerationChanged(bool value)
        {
            _settingsService.EnableGpuAcceleration = value;
            OnPropertyChanged(nameof(GpuAccelerationDescription));
        }

        #endregion Performance

        #region Resize

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

        public bool IsResizeEnabled => EnableResizing;

        public bool IsPaddingColorVisible => ResizeMethod == ResizeMethod.Padding;

        public IEnumerable<InterpolationMethod> InterpolationMethods => Enum.GetValues<InterpolationMethod>();

        public IEnumerable<ResizeMethod> ResizeMethods => Enum.GetValues<ResizeMethod>();

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

        #endregion Resize

        #region Border Detection

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsBorderDetectionEnabled))]
        public partial bool EnableBorderDetection { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(BorderDetectionAggressivenessLabel))]
        public partial int BorderDetectionTolerance { get; set; }

        public bool IsBorderDetectionEnabled => EnableBorderDetection;

        public string BorderDetectionAggressivenessLabel => BorderDetectionTolerance switch
        {
            < 20 => "Very Conservative",
            < 50 => "Conservative",
            < 100 => "Balanced",
            < 150 => "Aggressive",
            _ => "Very Aggressive"
        };

        partial void OnEnableBorderDetectionChanged(bool value)
        {
            _settingsService.EnableBorderDetection = value;
        }

        partial void OnBorderDetectionToleranceChanged(int value)
        {
            _settingsService.BorderDetectionTolerance = value;
        }

        #endregion Border Detection

        #region Output

        [ObservableProperty]
        public partial OutputFormat OutputFormat { get; set; }

        [ObservableProperty]
        public partial uint Dpi { get; set; }

        [ObservableProperty]
        public partial ColorFormat ColorFormat { get; set; }

        public IEnumerable<OutputFormat> OutputFormats => Enum.GetValues<OutputFormat>();

        public IEnumerable<ColorFormat> ColorFormats => Enum.GetValues<ColorFormat>();

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

        #endregion Output

        #region Temp Folder

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsCustomTempFolderEnabled))]
        public partial bool UseCustomTempFolder { get; set; }

        [ObservableProperty]
        public partial string CustomTempFolderPath { get; set; } = string.Empty;

        public bool IsCustomTempFolderEnabled => UseCustomTempFolder;

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

        partial void OnUseCustomTempFolderChanged(bool value)
        {
            _settingsService.UseCustomTempFolder = value;
        }

        partial void OnCustomTempFolderPathChanged(string value)
        {
            _settingsService.CustomTempFolderPath = value;
        }

        #endregion Temp Folder

        #region Navigation

        public override void OnNavigatedTo()
        {
            base.OnNavigatedTo();
            LoadSettings();
        }

        #endregion Navigation
    }
}