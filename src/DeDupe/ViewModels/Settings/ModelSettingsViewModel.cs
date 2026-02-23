using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeDupe.Constants;
using DeDupe.Helpers;
using DeDupe.Models.Configuration;
using DeDupe.Services;
using DeDupe.Services.Model;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace DeDupe.ViewModels.Settings
{
    public partial class ModelSettingsViewModel : SettingsPageViewModelBase
    {
        private readonly ISettingsService _settingsService;
        private readonly IBundledModelService _bundledModelService;
        private readonly ILogger<ModelSettingsViewModel> _logger;

        public ModelSettingsViewModel(ISettingsService settingsService, IBundledModelService bundledModelService, ILogger<ModelSettingsViewModel> logger)
        {
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _bundledModelService = bundledModelService ?? throw new ArgumentNullException(nameof(bundledModelService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            Title = "Model Configuration";
        }

        private void LoadSettings()
        {
            UseBundledModel = _settingsService.UseBundledModel;
            CustomModelFilePath = _settingsService.CustomModelFilePath;
            Normalization = _settingsService.Normalization;

            LogSettingsLoaded(ModelDisplayName, UseBundledModel ? "bundled" : "custom");
        }

        #region Model Selection

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(UseCustomModel))]
        [NotifyPropertyChangedFor(nameof(IsCustomModelSectionEnabled))]
        [NotifyPropertyChangedFor(nameof(IsBundledModelSectionEnabled))]
        [NotifyPropertyChangedFor(nameof(ModelDisplayName))]
        [NotifyPropertyChangedFor(nameof(ActiveModelPath))]
        public partial bool UseBundledModel { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CustomDirectoryPath))]
        [NotifyPropertyChangedFor(nameof(CustomFileName))]
        [NotifyPropertyChangedFor(nameof(ModelDisplayName))]
        [NotifyPropertyChangedFor(nameof(ActiveModelPath))]
        public partial string CustomModelFilePath { get; set; } = string.Empty;

        public string BundledModelDisplayName => BundledModelInfo.DisplayName;

        public string BundledModelLicense => BundledModelInfo.License;

        public string BundledModelDevelopers => BundledModelInfo.Developers;

        public int BundledModelInputSize => BundledModelInfo.InputSize;

        public bool IsBundledModelAvailable => _bundledModelService.IsModelAvailable();

        public bool UseCustomModel => !UseBundledModel;

        public bool IsCustomModelSectionEnabled => !UseBundledModel;
        public bool IsBundledModelSectionEnabled => UseBundledModel;

        public string ModelDisplayName => UseBundledModel
            ? BundledModelInfo.DisplayName
            : !string.IsNullOrEmpty(CustomModelFilePath)
                ? Path.GetFileName(CustomModelFilePath)
                : "No model selected";

        public string ActiveModelPath => UseBundledModel
            ? _bundledModelService.GetModelPath()
            : CustomModelFilePath;

        public string CustomDirectoryPath => !string.IsNullOrEmpty(CustomModelFilePath)
           ? Path.GetDirectoryName(CustomModelFilePath) + Path.DirectorySeparatorChar
           : string.Empty;

        public string CustomFileName => !string.IsNullOrEmpty(CustomModelFilePath)
            ? Path.GetFileName(CustomModelFilePath)
            : "Select a model file...";

        [RelayCommand]
        private void OpenModelLocation()
        {
            string pathToOpen = ActiveModelPath;

            if (string.IsNullOrEmpty(pathToOpen) || !File.Exists(pathToOpen))
            {
                return;
            }

            try
            {
                Process.Start("explorer.exe", $"/select,\"{pathToOpen}\"");
            }
            catch (Exception ex)
            {
                LogModelLocationOpenFailed(pathToOpen, ex);
            }
        }

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

                foreach (string ext in SupportedFileExtensions.SupportedModelExtensions)
                {
                    fileOpenPicker.FileTypeFilter.Add(ext);
                }
                fileOpenPicker.InitializeForCurrentWindow();

                StorageFile? file = await fileOpenPicker.PickSingleFileAsync();
                if (file != null)
                {
                    CustomModelFilePath = file.Path;
                    UseBundledModel = false;

                    LogCustomModelFileSelected(file.Path);
                }
            }
            catch (Exception ex)
            {
                LogCustomModelFileSelectionFailed(ex);
            }
        }

        partial void OnUseBundledModelChanged(bool value)
        {
            _settingsService.UseBundledModel = value;

            if (value)
            {
                // Sync normalization and image processing settings to bundled model defaults
                Normalization = BundledModelInfo.Normalization;
                _settingsService.EnableResizing = true;
                _settingsService.ResizeSize = (uint)BundledModelInfo.InputSize;
                _settingsService.ResizeMethod = BundledModelInfo.RecommendedResizeMethod;
            }

            LogModelSourceChanged(value ? "bundled" : "custom");
        }

        partial void OnCustomModelFilePathChanged(string value)
        {
            _settingsService.CustomModelFilePath = value;
        }

        #endregion Model Selection

        #region Normalization

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(MeanR))]
        [NotifyPropertyChangedFor(nameof(MeanG))]
        [NotifyPropertyChangedFor(nameof(MeanB))]
        [NotifyPropertyChangedFor(nameof(StdR))]
        [NotifyPropertyChangedFor(nameof(StdG))]
        [NotifyPropertyChangedFor(nameof(StdB))]
        public partial NormalizationSettings Normalization { get; set; } = NormalizationSettings.Default;

        public double MeanR
        {
            get => Normalization.MeanR;
            set => UpdateNormalization(value, Normalization.MeanG, Normalization.MeanB, Normalization.StdR, Normalization.StdG, Normalization.StdB);
        }

        public double MeanG
        {
            get => Normalization.MeanG;
            set => UpdateNormalization(Normalization.MeanR, value, Normalization.MeanB, Normalization.StdR, Normalization.StdG, Normalization.StdB);
        }

        public double MeanB
        {
            get => Normalization.MeanB;
            set => UpdateNormalization(Normalization.MeanR, Normalization.MeanG, value, Normalization.StdR, Normalization.StdG, Normalization.StdB);
        }

        public double StdR
        {
            get => Normalization.StdR;
            set => UpdateNormalization(Normalization.MeanR, Normalization.MeanG, Normalization.MeanB, value, Normalization.StdG, Normalization.StdB);
        }

        public double StdG
        {
            get => Normalization.StdG;
            set => UpdateNormalization(Normalization.MeanR, Normalization.MeanG, Normalization.MeanB, Normalization.StdR, value, Normalization.StdB);
        }

        public double StdB
        {
            get => Normalization.StdB;
            set => UpdateNormalization(Normalization.MeanR, Normalization.MeanG, Normalization.MeanB, Normalization.StdR, Normalization.StdG, value);
        }

        [RelayCommand]
        private void ResetNormalization()
        {
            Normalization = NormalizationSettings.ImageNet;
            LogNormalizationReset("ImageNet");
        }

        [RelayCommand]
        private void ApplyBundledModelNormalization()
        {
            Normalization = BundledModelInfo.Normalization;
            LogNormalizationReset(BundledModelInfo.DisplayName);
        }

        private void UpdateNormalization(double meanR, double meanG, double meanB, double stdR, double stdG, double stdB)
        {
            Normalization = new NormalizationSettings(meanR, meanG, meanB, stdR, stdG, stdB);
        }

        partial void OnNormalizationChanged(NormalizationSettings value)
        {
            _settingsService.Normalization = value;
        }

        #endregion Normalization

        #region Navigation

        public override void OnNavigatedTo()
        {
            base.OnNavigatedTo();
            LoadSettings();
        }

        #endregion Navigation

        #region Logging

        [LoggerMessage(Level = LogLevel.Debug, Message = "Model settings loaded, active model: {ModelName} ({ModelSource})")]
        private partial void LogSettingsLoaded(string modelName, string modelSource);

        [LoggerMessage(Level = LogLevel.Information, Message = "Model source changed to {ModelSource}")]
        private partial void LogModelSourceChanged(string modelSource);

        [LoggerMessage(Level = LogLevel.Information, Message = "Custom model file selected: {FilePath}")]
        private partial void LogCustomModelFileSelected(string filePath);

        [LoggerMessage(Level = LogLevel.Information, Message = "Normalization reset to {Preset} defaults")]
        private partial void LogNormalizationReset(string preset);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Custom model file selection picker failed")]
        private partial void LogCustomModelFileSelectionFailed(Exception ex);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Model location open failed for {FilePath}")]
        private partial void LogModelLocationOpenFailed(string filePath, Exception ex);

        #endregion Logging
    }
}