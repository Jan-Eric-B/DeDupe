using CommunityToolkit.Mvvm.Input;
using DeDupe.Constants;
using DeDupe.Enums;
using DeDupe.Models;
using DeDupe.Services;
using DeDupe.Services.PreProcessing;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.System;
using Windows.UI;

namespace DeDupe.ViewModels.Pages
{
    public partial class PreProcessingViewModel : PageViewModelBase
    {
        #region Fields

        private readonly IAppStateService _appStateService;
        private readonly ImageProcessingService _imageProcessingService;

        private bool _enableResizing = ProcessingDefaults.EnableResizing;
        private uint _resizeSize = ProcessingDefaults.TargetSize;
        private ResizeMethod _resizeMethod = ProcessingDefaults.DefaultResizeMethod;
        private Color _paddingColor = Color.FromArgb(ProcessingDefaults.PaddingColorA, ProcessingDefaults.PaddingColorR, ProcessingDefaults.PaddingColorG, ProcessingDefaults.PaddingColorB);
        private InterpolationMethod _downsamplingMethod = ProcessingDefaults.DefaultDownsamplingMethod;
        private InterpolationMethod _upsamplingMethod = ProcessingDefaults.DefaultUpsamplingMethod;
        private bool _enableBorderDetection = ProcessingDefaults.EnableBorderDetection;
        private int _borderDetectionTolerance = ProcessingDefaults.BorderDetectionTolerance;
        private OutputFormat _outputFormat = ProcessingDefaults.DefaultOutputFormat;
        private ColorFormat _bitDepth = ProcessingDefaults.DefaultColorFormat;
        private int _processingProgress;
        private bool _hasProcessedItems;

        #endregion Fields

        #region Properties

        // Resize Settings
        public bool EnableResizing
        {
            get => _enableResizing;
            set
            {
                if (SetProperty(ref _enableResizing, value))
                {
                    OnPropertyChanged(nameof(IsResizeEnabled));
                    OnPropertyChanged(nameof(IsInterpolationMethodsEnabled));
                }
            }
        }

        public uint ResizeSize
        {
            get => _resizeSize;
            set => SetProperty(ref _resizeSize, value);
        }

        public bool IsResizeEnabled => EnableResizing;

        // Resize Method
        public ResizeMethod ResizeMethod
        {
            get => _resizeMethod;
            set
            {
                if (SetProperty(ref _resizeMethod, value))
                {
                    OnPropertyChanged(nameof(IsPaddingColorVisible));
                }
            }
        }

        public bool IsPaddingColorVisible => ResizeMethod == ResizeMethod.Padding;

        // Padding Color
        public Color PaddingColor
        {
            get => _paddingColor;
            set => SetProperty(ref _paddingColor, value);
        }

        // Interpolation Methods
        public InterpolationMethod DownsamplingMethod
        {
            get => _downsamplingMethod;
            set => SetProperty(ref _downsamplingMethod, value);
        }

        public InterpolationMethod UpsamplingMethod
        {
            get => _upsamplingMethod;
            set => SetProperty(ref _upsamplingMethod, value);
        }

        public bool IsInterpolationMethodsEnabled => EnableResizing;

        // Border Detection
        public bool EnableBorderDetection
        {
            get => _enableBorderDetection;
            set => SetProperty(ref _enableBorderDetection, value);
        }

        // Border Detection Tolerance
        public int BorderDetectionTolerance
        {
            get => _borderDetectionTolerance;
            set => SetProperty(ref _borderDetectionTolerance, value);
        }

        // Output Format
        public OutputFormat OutputFormat
        {
            get => _outputFormat;
            set => SetProperty(ref _outputFormat, value);
        }

        // Bit Depth
        public ColorFormat BitDepth
        {
            get => _bitDepth;
            set => SetProperty(ref _bitDepth, value);
        }

        public int ProcessingProgress
        {
            get => _processingProgress;
            set => SetProperty(ref _processingProgress, value);
        }

        public bool HasProcessedItems
        {
            get => _hasProcessedItems;
            set
            {
                if (SetProperty(ref _hasProcessedItems, value))
                {
                    OnPropertyChanged(nameof(CanOpenTempFolder));
                    OpenTempFolderCommand.NotifyCanExecuteChanged();
                    UpdateCompletionStatus();
                }
            }
        }

        public bool CanStartProcessing => !IsBusy && _appStateService.AnalysisItemCount > 0;

        public bool CanOpenTempFolder => HasProcessedItems && !string.IsNullOrEmpty(_appStateService.TempFolderPath);

        // Available options for dropdowns
        public IEnumerable<InterpolationMethod> InterpolationMethods => Enum.GetValues<InterpolationMethod>();

        public IEnumerable<ResizeMethod> ResizeMethods => Enum.GetValues<ResizeMethod>();
        public IEnumerable<OutputFormat> OutputFormats => Enum.GetValues<OutputFormat>();
        public IEnumerable<ColorFormat> BitDepths => Enum.GetValues<ColorFormat>();

        #endregion Properties

        #region Constructor

        public PreProcessingViewModel(IAppStateService appStateService, IBorderDetectionService borderDetectionService, IImageFormatService imageFormatService, IImageResizeService imageResizeService) : base(1)
        {
            _appStateService = appStateService ?? throw new ArgumentNullException(nameof(appStateService));
            _imageProcessingService = new ImageProcessingService(
                _appStateService,
                borderDetectionService,
                imageFormatService,
                imageResizeService);

            Title = "Pre-Processing";
        }

        #endregion Constructor

        #region Commands

        [RelayCommand]
        private async Task OpenTempFolderAsync()
        {
            try
            {
                if (!string.IsNullOrEmpty(_appStateService.TempFolderPath) && System.IO.Directory.Exists(_appStateService.TempFolderPath))
                {
                    await Launcher.LaunchFolderPathAsync(_appStateService.TempFolderPath);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error opening temp folder: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task ProcessImagesAsync()
        {
            try
            {
                IsBusy = true;
                UpdateStatus();
                ProcessingProgress = 0;

                // Configure image processing service
                ConfigureImageProcessingService();

                // Get analysis items to process
                IReadOnlyCollection<AnalysisItem> analysisItems = _appStateService.AnalysisItems;

                if (analysisItems.Count == 0)
                {
                    Status = "No items to process";
                    return;
                }

                Status = $"Processing {analysisItems.Count} items...";

                // Process items
                await _imageProcessingService.ProcessItemsAsync(analysisItems);

                if (_appStateService.ProcessedItemCount > 0)
                {
                    HasProcessedItems = true;
                }
            }
            catch (Exception ex)
            {
                Status = $"Error during processing: {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"Preprocessing error: {ex}");
            }
            finally
            {
                IsBusy = false;
                ProcessingProgress = 0;
                UpdateStatus();
            }
        }

        #endregion Commands

        #region Methods

        private void UpdateStatus()
        {
            if (IsBusy)
            {
                Status = "Processing files...";
            }
            else if (_appStateService.ProcessedItemCount > 0)
            {
                Status = $"{_appStateService.ProcessedItemCount} item{(_appStateService.ProcessedItemCount == 1 ? "" : "s")} processed";
            }
            else
            {
                Status = "No items processed";
            }
        }

        private void ConfigureImageProcessingService()
        {
            _imageProcessingService.EnableBorderDetection = EnableBorderDetection;
            _imageProcessingService.BorderDetectionTolerance = BorderDetectionTolerance;
            _imageProcessingService.EnableResizing = EnableResizing;
            _imageProcessingService.TargetSize = ResizeSize;
            _imageProcessingService.ResizeMethod = ResizeMethod;
            _imageProcessingService.UpsamplingMethod = UpsamplingMethod;
            _imageProcessingService.DownsamplingMethod = DownsamplingMethod;
            _imageProcessingService.OutputFormat = OutputFormat;
            _imageProcessingService.BitDepth = BitDepth;
            _imageProcessingService.PaddingColor = [PaddingColor.R, PaddingColor.G, PaddingColor.B, PaddingColor.A];
        }

        private void UpdateCompletionStatus()
        {
            IsComplete = HasProcessedItems;
        }

        #endregion Methods
    }
}