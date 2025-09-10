using CommunityToolkit.Mvvm.Input;
using DeDupe.Constants;
using DeDupe.Enums.PreProcessing;
using DeDupe.Services;
using DeDupe.Services.PreProcessing;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Linq;
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

        private ResizeMethod _resizeMethod = ProcessingDefaults.ResizeMethod;

        private SolidColorBrush _paddingColor = ProcessingDefaults.PaddingColorBrush;

        private InterpolationMethod _downsamplingMethod = ProcessingDefaults.DownsamplingMethod;

        private InterpolationMethod _upsamplingMethod = ProcessingDefaults.UpsamplingMethod;

        private bool _enableBorderDetection = ProcessingDefaults.EnableBorderDetection;

        private int _borderDetectionTolerance = ProcessingDefaults.BorderDetectionTolerance;

        private OutputFormat _outputFormat = ProcessingDefaults.OutputFormat;

        private BitDepth _bitDepth = ProcessingDefaults.BitDepth;

        private int _processingProgress;

        private bool _hasProcessedImages;

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
                    OnPropertyChanged(nameof(IsResizeSettingsEnabled));
                    OnPropertyChanged(nameof(IsInterpolationMethodsEnabled));
                }
            }
        }

        public uint ResizeSize
        {
            get => _resizeSize;
            set => SetProperty(ref _resizeSize, value);
        }

        public bool IsResizeSettingsEnabled => EnableResizing;

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
        public SolidColorBrush PaddingColor
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
        public BitDepth BitDepth
        {
            get => _bitDepth;
            set => SetProperty(ref _bitDepth, value);
        }

        public int ProcessingProgress
        {
            get => _processingProgress;
            set => SetProperty(ref _processingProgress, value);
        }

        public bool HasProcessedImages
        {
            get => _hasProcessedImages;
            set
            {
                if (SetProperty(ref _hasProcessedImages, value))
                {
                    OnPropertyChanged(nameof(CanOpenTempFolder));
                    OpenTempFolderCommand.NotifyCanExecuteChanged();
                    UpdateCompletionStatus();
                }
            }
        }

        public bool CanStartProcessing => !IsBusy && _appStateService.FileCount >= 0;

        public bool CanOpenTempFolder => HasProcessedImages && !string.IsNullOrEmpty(_appStateService.TempFolderPath);

        // Available options for dropdowns
        public IEnumerable<InterpolationMethod> InterpolationMethods => Enum.GetValues<InterpolationMethod>();
        public IEnumerable<ResizeMethod> ResizeMethods => Enum.GetValues<ResizeMethod>();
        public IEnumerable<OutputFormat> OutputFormats => Enum.GetValues<OutputFormat>();
        public IEnumerable<BitDepth> BitDepths => Enum.GetValues<BitDepth>();

        #endregion Properties

        #region Constructor

        public PreProcessingViewModel(IAppStateService appStateService, IBorderDetectionService borderDetectionService, IImageFormatService imageFormatService, IImageResizeService imageResizeService) : base(2)
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
                await ConfigureImageProcessingServiceAsync();

                // Get all image paths from input
                IEnumerable<string>? imagePaths = GetAllImagePaths();

                if (!imagePaths.Any())
                {
                    Status = "No images to process";
                    return;
                }

                Status = $"Processing {imagePaths.Count()} images...";

                // Process images
                await _imageProcessingService.ProcessImagesAsync(imagePaths);

                if (_appStateService.ProcessedImageCount != 0)
                {
                    HasProcessedImages = true;
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

        private IEnumerable<string> GetAllImagePaths()
        {
            return _appStateService?.FilePaths ?? [];
        }

        private void UpdateStatus()
        {
            if (IsBusy)
            {
                Status = "Processing files...";
            }
            else if (_appStateService.ProcessedImageCount > 0)
            {
                Status = $"{_appStateService.ProcessedImageCount} image{(_appStateService.ProcessedImageCount == 1 ? "" : "s")} processed";
            }
            else
            {
                Status = "No images processed";
            }
        }

        private async Task ConfigureImageProcessingServiceAsync()
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

            // Convert padding color
            Color color = PaddingColor.Color;
            _imageProcessingService.PaddingColor = [color.R, color.G, color.B, color.A];
        }

        private void UpdateCompletionStatus()
        {
            IsComplete = HasProcessedImages;
        }

        #endregion Methods
    }
}