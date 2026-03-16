using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeDupe.Enums;
using DeDupe.Localization;
using DeDupe.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.UI;

namespace DeDupe.ViewModels.Settings
{
    public partial class ImageProcessingSettingsViewModel : SettingsPageViewModelBase
    {
        private readonly ISettingsService _settingsService;
        private readonly IDialogService _dialogService;
        private readonly ILocalizer _localizer;
        private readonly ILogger<ImageProcessingSettingsViewModel> _logger;

        public ImageProcessingSettingsViewModel(ISettingsService settingsService, IDialogService dialogService, ILocalizer localizer, ILogger<ImageProcessingSettingsViewModel> logger)
        {
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
            _localizer = localizer ?? throw new ArgumentNullException(nameof(localizer));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _localizer.LanguageChanged += OnLanguageChanged;

            Title = "Image Processing";
        }

        private void LoadSettings()
        {
            TensorLayoutOptions = _localizer.BuildLocalizedOptions<TensorLayout>();
            ResizeMethodOptions = _localizer.BuildLocalizedOptions<ResizeMethod>();
            InterpolationMethodOptions = _localizer.BuildLocalizedOptions<InterpolationMethod>();
            OutputFormatOptions = _localizer.BuildLocalizedOptions<OutputFormat>();
            ColorFormatOptions = _localizer.BuildLocalizedOptions<ColorFormat>();

            // Performance
            ParallelProcessingCores = _settingsService.ParallelProcessingCores;
            EnableGpuAcceleration = _settingsService.EnableGpuAcceleration;
            InferenceBatchSize = _settingsService.InferenceBatchSize;
            SelectedTensorLayoutIndex = (int)_settingsService.TensorLayout;

            // Resize
            EnableResizing = _settingsService.EnableResizing;
            ResizeSize = _settingsService.ResizeSize;
            SelectedResizeMethodIndex = (int)_settingsService.ResizeMethod;
            PaddingColor = _settingsService.PaddingColor;
            SelectedDownsamplingMethodIndex = (int)_settingsService.DownsamplingMethod;
            SelectedUpsamplingMethodIndex = (int)_settingsService.UpsamplingMethod;
            Compand = _settingsService.Compand;

            // Border Detection
            EnableBorderDetection = _settingsService.EnableBorderDetection;
            BorderDetectionTolerance = _settingsService.BorderDetectionTolerance;

            // Output
            SelectedOutputFormatIndex = (int)_settingsService.OutputFormat;
            Dpi = _settingsService.Dpi;
            SelectedColorFormatIndex = (int)_settingsService.ColorFormat;

            // Temp Folder
            UseCustomTempFolder = _settingsService.UseCustomTempFolder;
            CustomTempFolderPath = _settingsService.CustomTempFolderPath;

            LogSettingsLoaded();
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

        [ObservableProperty]
        public partial int SelectedTensorLayoutIndex { get; set; }

        [ObservableProperty]
        public partial IReadOnlyList<LocalizedEnumOption<TensorLayout>> TensorLayoutOptions { get; set; } = [];

        public int MaxParallelCores => Environment.ProcessorCount;

        public string ParallelCoresDisplayText => ParallelProcessingCores.ToString();

        public bool IsBatchSizeEnabled => true;

        public string BatchSizeDisplayText => InferenceBatchSize.ToString();

        public string GpuAccelerationDescription
        {
            get
            {
                return EnableGpuAcceleration
                    ? _localizer.GetLocalizedString("ImageProcessing_GpuAccelerationEnabled")
                    : _localizer.GetLocalizedString("ImageProcessing_CpuOnlyMode");
            }
        }

        partial void OnParallelProcessingCoresChanged(int value)
        {
            _settingsService.ParallelProcessingCores = value;
        }

        partial void OnEnableGpuAccelerationChanged(bool value)
        {
            _settingsService.EnableGpuAcceleration = value;
            OnPropertyChanged(nameof(GpuAccelerationDescription));

            LogGpuAccelerationChanged(value);
        }

        partial void OnInferenceBatchSizeChanged(int value)
        {
            _settingsService.InferenceBatchSize = value;
        }

        partial void OnSelectedTensorLayoutIndexChanged(int value)
        {
            if (value < 0)
            {
                return;
            }

            _settingsService.TensorLayout = (TensorLayout)value;
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
        public partial int SelectedResizeMethodIndex { get; set; }

        [ObservableProperty]
        public partial Color PaddingColor { get; set; }

        [ObservableProperty]
        public partial int SelectedDownsamplingMethodIndex { get; set; }

        [ObservableProperty]
        public partial int SelectedUpsamplingMethodIndex { get; set; }

        [ObservableProperty]
        public partial bool Compand { get; set; }

        [ObservableProperty]
        public partial IReadOnlyList<LocalizedEnumOption<ResizeMethod>> ResizeMethodOptions { get; set; } = [];

        [ObservableProperty]
        public partial IReadOnlyList<LocalizedEnumOption<InterpolationMethod>> InterpolationMethodOptions { get; set; } = [];

        public bool IsResizeEnabled => EnableResizing;

        public bool IsPaddingColorVisible => SelectedResizeMethodIndex >= 0 && (ResizeMethod)SelectedResizeMethodIndex == ResizeMethod.Padding;

        partial void OnEnableResizingChanged(bool value)
        {
            _settingsService.EnableResizing = value;
        }

        partial void OnResizeSizeChanged(uint value)
        {
            _settingsService.ResizeSize = value;
        }

        partial void OnSelectedResizeMethodIndexChanged(int value)
        {
            if (value < 0)
            {
                return;
            }

            _settingsService.ResizeMethod = (ResizeMethod)value;
        }

        partial void OnPaddingColorChanged(Color value)
        {
            _settingsService.PaddingColor = value;
        }

        partial void OnSelectedDownsamplingMethodIndexChanged(int value)
        {
            if (value < 0)
            {
                return;
            }

            _settingsService.DownsamplingMethod = (InterpolationMethod)value;
        }

        partial void OnSelectedUpsamplingMethodIndexChanged(int value)
        {
            if (value < 0)
            {
                return;
            }

            _settingsService.UpsamplingMethod = (InterpolationMethod)value;
        }

        partial void OnCompandChanged(bool value)
        {
            _settingsService.Compand = value;
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

        public string BorderDetectionAggressivenessLabel
        {
            get
            {
                return BorderDetectionTolerance switch
                {
                    < 20 => _localizer.GetLocalizedString("BorderDetection_VeryConservative"),
                    < 50 => _localizer.GetLocalizedString("BorderDetection_Conservative"),
                    < 100 => _localizer.GetLocalizedString("BorderDetection_Balanced"),
                    < 150 => _localizer.GetLocalizedString("BorderDetection_Aggressive"),
                    _ => _localizer.GetLocalizedString("BorderDetection_VeryAggressive")
                };
            }
        }

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
        public partial int SelectedOutputFormatIndex { get; set; }

        [ObservableProperty]
        public partial uint Dpi { get; set; }

        [ObservableProperty]
        public partial int SelectedColorFormatIndex { get; set; }

        [ObservableProperty]
        public partial IReadOnlyList<LocalizedEnumOption<OutputFormat>> OutputFormatOptions { get; set; } = [];

        [ObservableProperty]
        public partial IReadOnlyList<LocalizedEnumOption<ColorFormat>> ColorFormatOptions { get; set; } = [];

        partial void OnSelectedOutputFormatIndexChanged(int value)
        {
            if (value < 0)
            {
                return;
            }

            _settingsService.OutputFormat = (OutputFormat)value;
        }

        partial void OnDpiChanged(uint value)
        {
            _settingsService.Dpi = value;
        }

        partial void OnSelectedColorFormatIndexChanged(int value)
        {
            if (value < 0)
            {
                return;
            }

            _settingsService.ColorFormat = (ColorFormat)value;
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
                string? folderPath = await _dialogService.PickFolderAsync("Select Temp Folder");

                if (!string.IsNullOrEmpty(folderPath))
                {
                    CustomTempFolderPath = folderPath;
                    LogTempFolderSelected(folderPath);
                }
            }
            catch (Exception ex)
            {
                LogTempFolderBrowseFailed(ex);
            }
        }

        [RelayCommand]
        private async Task OpenTempFolderAsync()
        {
            try
            {
                string path = _settingsService.TempFolderPath;

                if (string.IsNullOrEmpty(path))
                {
                    return;
                }

                await _dialogService.OpenFolderInExplorerAsync(path);
            }
            catch (Exception ex)
            {
                LogTempFolderOpenFailed(ex);
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

        #region Language Changed

        private void OnLanguageChanged(object? sender, LanguageChangedEventArgs e)
        {
            // Rebuild all localized dropdown items
            int tensorIdx = SelectedTensorLayoutIndex;
            int resizeIdx = SelectedResizeMethodIndex;
            int downsampleIdx = SelectedDownsamplingMethodIndex;
            int upsampleIdx = SelectedUpsamplingMethodIndex;
            int outputIdx = SelectedOutputFormatIndex;
            int colorIdx = SelectedColorFormatIndex;

            TensorLayoutOptions = _localizer.BuildLocalizedOptions<TensorLayout>();
            ResizeMethodOptions = _localizer.BuildLocalizedOptions<ResizeMethod>();
            InterpolationMethodOptions = _localizer.BuildLocalizedOptions<InterpolationMethod>();
            OutputFormatOptions = _localizer.BuildLocalizedOptions<OutputFormat>();
            ColorFormatOptions = _localizer.BuildLocalizedOptions<ColorFormat>();

            SelectedTensorLayoutIndex = tensorIdx;
            SelectedResizeMethodIndex = resizeIdx;
            SelectedDownsamplingMethodIndex = downsampleIdx;
            SelectedUpsamplingMethodIndex = upsampleIdx;
            SelectedOutputFormatIndex = outputIdx;
            SelectedColorFormatIndex = colorIdx;

            // Refresh localized computed text
            OnPropertyChanged(nameof(GpuAccelerationDescription));
            OnPropertyChanged(nameof(BorderDetectionAggressivenessLabel));
        }

        #endregion Language Changed

        #region Navigation

        public override void OnNavigatedTo()
        {
            base.OnNavigatedTo();
            LoadSettings();
        }

        #endregion Navigation

        #region Logging

        [LoggerMessage(Level = LogLevel.Debug, Message = "Image processing settings loaded")]
        private partial void LogSettingsLoaded();

        [LoggerMessage(Level = LogLevel.Information, Message = "GPU acceleration changed to {Enabled}")]
        private partial void LogGpuAccelerationChanged(bool enabled);

        [LoggerMessage(Level = LogLevel.Debug, Message = "Custom temp folder selected: {FolderPath}")]
        private partial void LogTempFolderSelected(string folderPath);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Temp folder browse picker failed")]
        private partial void LogTempFolderBrowseFailed(Exception ex);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Temp folder open failed")]
        private partial void LogTempFolderOpenFailed(Exception ex);

        #endregion Logging
    }
}