using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeDupe.Constants;
using DeDupe.Helpers;
using DeDupe.Models.Configuration;
using DeDupe.Services;
using DeDupe.Services.Model;
using System;
using System.Collections.Generic;
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
        private readonly IModelDownloadService _downloadService;

        public ModelSettingsViewModel(ISettingsService settingsService, IBundledModelService bundledModelService, IModelDownloadService downloadService)
        {
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _bundledModelService = bundledModelService ?? throw new ArgumentNullException(nameof(bundledModelService));
            _downloadService = downloadService ?? throw new ArgumentNullException(nameof(downloadService));

            Title = "Model Configuration";
        }

        private void LoadSettings()
        {
            UseBundledModel = _settingsService.UseBundledModel;
            SelectedBundledModelId = _settingsService.SelectedBundledModelId;
            CustomModelFilePath = _settingsService.CustomModelFilePath;
            Normalization = _settingsService.Normalization;
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

                double sizeMB = model.ExpectedFileSize / 1024.0 / 1024.0;
                return $"Not downloaded ({sizeMB:F0} MB)";
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
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error selecting model file: {ex.Message}");
            }
        }

        partial void OnUseBundledModelChanged(bool value)
        {
            _settingsService.UseBundledModel = value;
        }

        // Syncs normalization when bundled model is changed
        partial void OnSelectedBundledModelIdChanged(string value)
        {
            _settingsService.SelectedBundledModelId = value;

            if (UseBundledModel)
            {
                BundledModelInfo? model = _bundledModelService.GetModelInfo(value);
                if (model != null)
                {
                    Normalization = model.Normalization;
                }
            }

            OnPropertyChanged(nameof(SelectedModelAvailability));
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
        }

        [RelayCommand]
        private void ApplyModelNormalization()
        {
            if (SelectedBundledModel != null)
            {
                Normalization = SelectedBundledModel.Normalization;
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
    }
}