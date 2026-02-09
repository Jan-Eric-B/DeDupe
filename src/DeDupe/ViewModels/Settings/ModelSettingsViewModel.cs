using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeDupe.Constants;
using DeDupe.Helpers;
using DeDupe.Models.Configuration;
using DeDupe.Services;
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
        #region Fields

        private readonly ISettingsService _settingsService;
        private readonly IBundledModelService _bundledModelService;

        #endregion Fields

        #region Properties

        // Model Selection
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

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(MeanR))]
        [NotifyPropertyChangedFor(nameof(MeanG))]
        [NotifyPropertyChangedFor(nameof(MeanB))]
        [NotifyPropertyChangedFor(nameof(StdR))]
        [NotifyPropertyChangedFor(nameof(StdG))]
        [NotifyPropertyChangedFor(nameof(StdB))]
        public partial NormalizationSettings Normalization { get; set; } = NormalizationSettings.Default;

        public IReadOnlyList<BundledModelInfo> BundledModels => _bundledModelService.AvailableModels;

        public BundledModelInfo? SelectedBundledModel => _bundledModelService.GetModelInfo(SelectedBundledModelId);

        public string SelectedModelDescription => SelectedBundledModel?.Description ?? string.Empty;

        public int SelectedModelInputSize => SelectedBundledModel?.InputSize ?? 224;

        // Mode toggles
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

        #endregion Commands

        #region Methods

        private void LoadSettings()
        {
            UseBundledModel = _settingsService.UseBundledModel;
            SelectedBundledModelId = _settingsService.SelectedBundledModelId;
            CustomModelFilePath = _settingsService.CustomModelFilePath;
            Normalization = _settingsService.Normalization;
        }

        private void UpdateNormalization(double meanR, double meanG, double meanB, double stdR, double stdG, double stdB)
        {
            Normalization = new NormalizationSettings(meanR, meanG, meanB, stdR, stdG, stdB);
        }

        partial void OnUseBundledModelChanged(bool value)
        {
            _settingsService.UseBundledModel = value;
        }

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
        }

        partial void OnCustomModelFilePathChanged(string value)
        {
            _settingsService.CustomModelFilePath = value;
        }

        partial void OnNormalizationChanged(NormalizationSettings value)
        {
            _settingsService.Normalization = value;
        }

        public override void OnNavigatedTo()
        {
            base.OnNavigatedTo();
            LoadSettings();
        }

        #endregion Methods
    }
}