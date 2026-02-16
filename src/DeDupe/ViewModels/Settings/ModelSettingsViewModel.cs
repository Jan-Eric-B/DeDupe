using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeDupe.Constants;
using DeDupe.Helpers;
using DeDupe.Models.Configuration;
using DeDupe.Services;
using DeDupe.Services.Model;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System;

namespace DeDupe.ViewModels.Settings
{
    public partial class ModelSettingsViewModel : SettingsPageViewModelBase
    {
        private readonly ISettingsService _settingsService;
        private readonly IBundledModelService _bundledModelService;
        private readonly IModelDownloadService _downloadService;
        private readonly ILogger<ModelSettingsViewModel> _logger;

        public ModelSettingsViewModel(ISettingsService settingsService, IBundledModelService bundledModelService, IModelDownloadService downloadService, ILogger<ModelSettingsViewModel> logger)
        {
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _bundledModelService = bundledModelService ?? throw new ArgumentNullException(nameof(bundledModelService));
            _downloadService = downloadService ?? throw new ArgumentNullException(nameof(downloadService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            Title = "Model Configuration";
        }

        private void LoadSettings()
        {
            UseBundledModel = _settingsService.UseBundledModel;
            SelectedBundledModelId = _settingsService.SelectedBundledModelId;
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
        [NotifyPropertyChangedFor(nameof(SelectedBundledModel))]
        [NotifyPropertyChangedFor(nameof(SelectedModelDescription))]
        [NotifyPropertyChangedFor(nameof(SelectedModelInputSize))]
        [NotifyPropertyChangedFor(nameof(ModelDisplayName))]
        [NotifyPropertyChangedFor(nameof(ActiveModelPath))]
        public partial string SelectedBundledModelId { get; set; } = BundledModelRegistry.DefaultModelId;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CustomDirectoryPath))]
        [NotifyPropertyChangedFor(nameof(CustomFileName))]
        [NotifyPropertyChangedFor(nameof(ModelDisplayName))]
        [NotifyPropertyChangedFor(nameof(ActiveModelPath))]
        public partial string CustomModelFilePath { get; set; } = string.Empty;

        public IReadOnlyList<BundledModelInfo> BundledModels => _bundledModelService.AvailableModels;

        public BundledModelInfo? SelectedBundledModel => _bundledModelService.GetModelInfo(SelectedBundledModelId);

        public string SelectedModelDescription => SelectedBundledModel?.Description ?? string.Empty;

        public int SelectedModelInputSize => SelectedBundledModel?.InputSize ?? 224;

        public string SelectedModelAvailability
        {
            get
            {
                BundledModelInfo? model = SelectedBundledModel;
                if (model is null)
                {
                    return string.Empty;
                }
                if (_downloadService.IsModelAvailable(model))
                {
                    return "Downloaded ✓";
                }
                return "Not downloaded";
            }
        }

        public bool UseCustomModel => !UseBundledModel;

        public bool IsCustomModelSectionEnabled => !UseBundledModel;
        public bool IsBundledModelSectionEnabled => UseBundledModel;

        public string ModelDisplayName => UseBundledModel
            ? SelectedBundledModel?.DisplayName ?? "Unknown Model"
            : !string.IsNullOrEmpty(CustomModelFilePath)
                ? Path.GetFileName(CustomModelFilePath)
                : "No model selected";

        public string ActiveModelPath => UseBundledModel
            ? _bundledModelService.GetModelPath(SelectedBundledModelId)
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
                BundledModelInfo? model = _bundledModelService.GetModelInfo(SelectedBundledModelId);
                if (model != null)
                {
                    Normalization = model.Normalization;
                    _settingsService.EnableResizing = true;
                    _settingsService.ResizeSize = (uint)model.InputSize;
                    _settingsService.ResizeMethod = model.RecommendedResizeMethod;
                }
            }

            LogModelSourceChanged(value ? "bundled" : "custom");
        }

        // Syncs normalization and image processing settings when bundled model is changed
        partial void OnSelectedBundledModelIdChanged(string value)
        {
            _settingsService.SelectedBundledModelId = value;

            if (UseBundledModel)
            {
                BundledModelInfo? model = _bundledModelService.GetModelInfo(value);
                if (model != null)
                {
                    // Sync normalization
                    Normalization = model.Normalization;

                    // Sync image processing settings
                    _settingsService.EnableResizing = true;
                    _settingsService.ResizeSize = (uint)model.InputSize;
                    _settingsService.ResizeMethod = model.RecommendedResizeMethod;

                    LogBundledModelSelected(model.DisplayName, value);
                }
            }

            OnPropertyChanged(nameof(SelectedModelAvailability));
        }

        partial void OnCustomModelFilePathChanged(string value)
        {
            _settingsService.CustomModelFilePath = value;
        }

        #endregion Model Selection

        #region Model Folder

        [RelayCommand]
        private async Task OpenModelFolderAsync()
        {
            try
            {
                string path = _settingsService.ModelFolderPath;
                if (string.IsNullOrEmpty(path))
                {
                    return;
                }

                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                    LogModelFolderCreated(path);
                }

                await Launcher.LaunchFolderPathAsync(path);
            }
            catch (Exception ex)
            {
                LogModelFolderOpenFailed(ex);
            }
        }

        #endregion Model Folder

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
        private void ApplyModelNormalization()
        {
            if (SelectedBundledModel != null)
            {
                Normalization = SelectedBundledModel.Normalization;
                LogNormalizationReset(SelectedBundledModel.DisplayName);
            }
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

        [LoggerMessage(Level = LogLevel.Information, Message = "Bundled model selected: {ModelName} ({ModelId})")]
        private partial void LogBundledModelSelected(string modelName, string modelId);

        [LoggerMessage(Level = LogLevel.Information, Message = "Custom model file selected: {FilePath}")]
        private partial void LogCustomModelFileSelected(string filePath);

        [LoggerMessage(Level = LogLevel.Information, Message = "Normalization reset to {Preset} defaults")]
        private partial void LogNormalizationReset(string preset);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Custom model file selection picker failed")]
        private partial void LogCustomModelFileSelectionFailed(Exception ex);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Model location open failed for {FilePath}")]
        private partial void LogModelLocationOpenFailed(string filePath, Exception ex);

        [LoggerMessage(Level = LogLevel.Debug, Message = "Model folder created at {FolderPath}")]
        private partial void LogModelFolderCreated(string folderPath);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Model folder open failed")]
        private partial void LogModelFolderOpenFailed(Exception ex);

        #endregion Logging
    }
}