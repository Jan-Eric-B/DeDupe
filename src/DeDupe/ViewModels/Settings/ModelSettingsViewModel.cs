using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeDupe.Services;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace DeDupe.ViewModels.Settings
{
    public partial class ModelSettingsViewModel : SettingsPageViewModelBase
    {
        #region Fields

        private readonly ISettingsService _settingsService;
        private readonly IBundledModelService _bundledModelService;

        #endregion Fields

        #region Observable Properties

        // Model Selection
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(UseCustomModel))]
        [NotifyPropertyChangedFor(nameof(IsCustomModelSectionEnabled))]
        [NotifyPropertyChangedFor(nameof(ModelDisplayName))]
        [NotifyPropertyChangedFor(nameof(DirectoryPath))]
        [NotifyPropertyChangedFor(nameof(FileName))]
        public partial bool UseBundledModel { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CustomDirectoryPath))]
        [NotifyPropertyChangedFor(nameof(CustomFileName))]
        [NotifyPropertyChangedFor(nameof(ModelDisplayName))]
        public partial string CustomModelFilePath { get; set; } = string.Empty;

        // Normalization Settings
        [ObservableProperty]
        public partial double MeanR { get; set; }

        [ObservableProperty]
        public partial double MeanG { get; set; }

        [ObservableProperty]
        public partial double MeanB { get; set; }

        [ObservableProperty]
        public partial double StdR { get; set; }

        [ObservableProperty]
        public partial double StdG { get; set; }

        [ObservableProperty]
        public partial double StdB { get; set; }

        #endregion Observable Properties

        #region Properties

        // Model Selection
        public bool UseCustomModel => !UseBundledModel;

        public bool IsCustomModelSectionEnabled => !UseBundledModel;
        public string BundledModelName => _bundledModelService.BundledModelName;

        public string ModelDisplayName
        {
            get
            {
                if (UseBundledModel)
                {
                    return BundledModelName;
                }
                return !string.IsNullOrEmpty(CustomModelFilePath)
                    ? Path.GetFileName(CustomModelFilePath)
                    : "No model selected";
            }
        }

        public string ModelFilePath
        {
            get
            {
                if (UseBundledModel)
                {
                    return _bundledModelService.BundledModelPath;
                }
                return CustomModelFilePath;
            }
        }

        // Display Properties
        public string DirectoryPath
        {
            get
            {
                string path = ModelFilePath;
                return !string.IsNullOrEmpty(path)
                    ? Path.GetDirectoryName(path) + Path.DirectorySeparatorChar
                    : string.Empty;
            }
        }

        public string FileName
        {
            get
            {
                if (UseBundledModel)
                {
                    return string.IsNullOrWhiteSpace(BundledModelName) ? BundledModelName : "Bundled model not found";
                }
                return !string.IsNullOrEmpty(CustomModelFilePath)
                    ? Path.GetFileName(CustomModelFilePath)
                    : "Select a model file...";
            }
        }

        public string CustomDirectoryPath
        {
            get
            {
                return !string.IsNullOrEmpty(CustomModelFilePath)
                    ? Path.GetDirectoryName(CustomModelFilePath) + Path.DirectorySeparatorChar
                    : string.Empty;
            }
        }

        public string CustomFileName
        {
            get
            {
                return !string.IsNullOrEmpty(CustomModelFilePath)
                    ? Path.GetFileName(CustomModelFilePath)
                    : "Select a model file...";
            }
        }

        #endregion Properties

        #region Constructor

        public ModelSettingsViewModel(ISettingsService settingsService, IBundledModelService bundledModelService)
        {
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _bundledModelService = bundledModelService ?? throw new ArgumentNullException(nameof(bundledModelService));

            Title = "Model Configuration";

            LoadSettings();
        }

        #endregion Constructor

        #region Commands

        [RelayCommand]
        private async Task SelectCustomModelFileAsync()
        {
            try
            {
                FileOpenPicker fileOpenPicker = new()
                {
                    ViewMode = PickerViewMode.List,
                    SuggestedStartLocation = PickerLocationId.DocumentsLibrary
                };

                fileOpenPicker.FileTypeFilter.Add(".onnx");

                nint hwnd = GetActiveWindow();
                WinRT.Interop.InitializeWithWindow.Initialize(fileOpenPicker, hwnd);

                StorageFile? file = await fileOpenPicker.PickSingleFileAsync();
                if (file != null)
                {
                    CustomModelFilePath = file.Path;
                    UseBundledModel = false;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error selecting model file: {ex.Message}");
            }
        }

        [RelayCommand]
        private void ResetNormalization()
        {
            MeanR = 0.485;
            MeanG = 0.456;
            MeanB = 0.406;
            StdR = 0.229;
            StdG = 0.224;
            StdB = 0.225;
        }

        [RelayCommand]
        private void OpenModelLocation()
        {
            string pathToOpen = UseBundledModel ? ModelFilePath : CustomModelFilePath;

            if (!string.IsNullOrEmpty(pathToOpen) && File.Exists(pathToOpen))
            {
                try
                {
                    Process.Start("explorer.exe", $"/select,\"{pathToOpen}\"");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error opening folder: {ex.Message}");
                }
            }
        }

        #endregion Commands

        #region Methods

        private void LoadSettings()
        {
            // Model Selection
            UseBundledModel = _settingsService.UseBundledModel;
            CustomModelFilePath = _settingsService.CustomModelFilePath;

            // Normalization
            MeanR = _settingsService.MeanR;
            MeanG = _settingsService.MeanG;
            MeanB = _settingsService.MeanB;
            StdR = _settingsService.StdR;
            StdG = _settingsService.StdG;
            StdB = _settingsService.StdB;
        }

        // Model Selection Change Handlers
        partial void OnUseBundledModelChanged(bool value)
        {
            _settingsService.UseBundledModel = value;
        }

        partial void OnCustomModelFilePathChanged(string value)
        {
            _settingsService.CustomModelFilePath = value;
        }

        // Normalization Change Handlers
        partial void OnMeanRChanged(double value)
        {
            _settingsService.MeanR = value;
        }

        partial void OnMeanGChanged(double value)
        {
            _settingsService.MeanG = value;
        }

        partial void OnMeanBChanged(double value)
        {
            _settingsService.MeanB = value;
        }

        partial void OnStdRChanged(double value)
        {
            _settingsService.StdR = value;
        }

        partial void OnStdGChanged(double value)
        {
            _settingsService.StdG = value;
        }

        partial void OnStdBChanged(double value)
        {
            _settingsService.StdB = value;
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