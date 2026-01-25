using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeDupe.Enums;
using DeDupe.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System;
using Windows.UI;

namespace DeDupe.ViewModels.Settings
{
    public partial class PreProcessingSettingsViewModel : SettingsPageViewModelBase
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
        public partial bool EnableBorderDetection { get; set; }

        [ObservableProperty]
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

        #endregion Observable Properties

        #region Properties

        // Resize
        public bool IsResizeEnabled => EnableResizing;

        public bool IsPaddingColorVisible => ResizeMethod == ResizeMethod.Padding;

        // Temp Folder
        public bool IsCustomTempFolderEnabled => UseCustomTempFolder;

        // ComboBox Enums
        public IEnumerable<InterpolationMethod> InterpolationMethods => Enum.GetValues<InterpolationMethod>();

        public IEnumerable<ResizeMethod> ResizeMethods => Enum.GetValues<ResizeMethod>();
        public IEnumerable<OutputFormat> OutputFormats => Enum.GetValues<OutputFormat>();
        public IEnumerable<ColorFormat> ColorFormats => Enum.GetValues<ColorFormat>();

        #endregion Properties

        #region Constructor

        public PreProcessingSettingsViewModel(ISettingsService settingsService)
        {
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            Title = "Pre-Processing";

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

                nint hwnd = GetActiveWindow();
                WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

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

        public override void OnNavigatedTo()
        {
            base.OnNavigatedTo();
            LoadSettings();
        }

        public override void OnNavigatedFrom()
        {
            base.OnNavigatedFrom();
        }

        #endregion Methods

        [DllImport("user32.dll")]
        private static extern nint GetActiveWindow();
    }
}