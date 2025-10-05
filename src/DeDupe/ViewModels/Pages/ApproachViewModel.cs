using CommunityToolkit.Mvvm.Input;
using DeDupe.Enums.Approach;
using DeDupe.Services;
using Microsoft.UI.Xaml;
using System;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace DeDupe.ViewModels.Pages
{
    public partial class ApproachViewModel : PageViewModelBase
    {
        #region Fields

        private readonly IAppStateService _appStateService;

        #endregion Fields

        #region Properties

        public ApproachType SelectedApproach
        {
            get => _appStateService.SelectedApproach;
            set
            {
                if (_appStateService.SelectedApproach != value)
                {
                    _appStateService.SelectedApproach = value;

                    // Notify all radio buttons
                    OnPropertyChanged(nameof(IsDeepLearningSelected));
                    OnPropertyChanged(nameof(IsPerceptualHashingSelected));
                    OnPropertyChanged(nameof(IsColorHistogramSelected));
                    OnPropertyChanged(nameof(IsSiftSurfSelected));
                    OnPropertyChanged(nameof(IsTemplateMatchingSelected));
                    OnPropertyChanged(nameof(IsSemanticSimilaritySelected));
                    OnPropertyChanged(nameof(IsOtherApproachSelected));

                    UpdateCompletionStatus();
                }
            }
        }

        public string ModelFilePath
        {
            get => _appStateService.ModelFilePath;
            set
            {
                if (_appStateService.ModelFilePath != value)
                {
                    _appStateService.ModelFilePath = value;
                    OnPropertyChanged();
                    UpdateCompletionStatus();
                }
            }
        }

        public bool IsDeepLearningSelected
        {
            get => SelectedApproach == ApproachType.DeepLearning;
            set { if (value) SelectedApproach = ApproachType.DeepLearning; }
        }

        public bool IsPerceptualHashingSelected
        {
            get => SelectedApproach == ApproachType.PerceptualHashing;
            set { if (value) SelectedApproach = ApproachType.PerceptualHashing; }
        }

        public bool IsColorHistogramSelected
        {
            get => SelectedApproach == ApproachType.ColorHistogram;
            set { if (value) SelectedApproach = ApproachType.ColorHistogram; }
        }

        public bool IsSiftSurfSelected
        {
            get => SelectedApproach == ApproachType.SiftSurf;
            set { if (value) SelectedApproach = ApproachType.SiftSurf; }
        }

        public bool IsTemplateMatchingSelected
        {
            get => SelectedApproach == ApproachType.TemplateMatching;
            set { if (value) SelectedApproach = ApproachType.TemplateMatching; }
        }

        public bool IsSemanticSimilaritySelected
        {
            get => SelectedApproach == ApproachType.SemanticSimilarity;
            set { if (value) SelectedApproach = ApproachType.SemanticSimilarity; }
        }

        public Visibility IsOtherApproachSelected => !IsDeepLearningSelected ? Visibility.Visible : Visibility.Collapsed;

        public double MeanR
        {
            get => _appStateService.MeanR;
            set
            {
                if (_appStateService.MeanR != value)
                {
                    _appStateService.MeanR = value;
                    OnPropertyChanged();
                }
            }
        }

        public double MeanG
        {
            get => _appStateService.MeanG;
            set
            {
                if (_appStateService.MeanG != value)
                {
                    _appStateService.MeanG = value;
                    OnPropertyChanged();
                }
            }
        }

        public double MeanB
        {
            get => _appStateService.MeanB;
            set
            {
                if (_appStateService.MeanB != value)
                {
                    _appStateService.MeanB = value;
                    OnPropertyChanged();
                }
            }
        }

        public double StdR
        {
            get => _appStateService.StdR;
            set
            {
                if (_appStateService.StdR != value)
                {
                    _appStateService.StdR = value;
                    OnPropertyChanged();
                }
            }
        }

        public double StdG
        {
            get => _appStateService.StdG;
            set
            {
                if (_appStateService.StdG != value)
                {
                    _appStateService.StdG = value;
                    OnPropertyChanged();
                }
            }
        }

        public double StdB
        {
            get => _appStateService.StdB;
            set
            {
                if (_appStateService.StdB != value)
                {
                    _appStateService.StdB = value;
                    OnPropertyChanged();
                }
            }
        }

        #endregion Properties

        #region Commands

        [RelayCommand]
        private async Task SelectModelFileAsync()
        {
            FileOpenPicker fileOpenPicker = new()
            {
                ViewMode = PickerViewMode.List,
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary
            };

            fileOpenPicker.FileTypeFilter.Add(".onnx");

            // Initialize file picker
            nint windowHandle = WindowNative.GetWindowHandle(App.Window);
            InitializeWithWindow.Initialize(fileOpenPicker, windowHandle);

            StorageFile? file = await fileOpenPicker.PickSingleFileAsync();
            if (file != null)
            {
                ModelFilePath = file.Path;
            }
        }

        [RelayCommand]
        private void ResetNormalization()
        {
            _appStateService.ResetNormalization();

            // Notify UI of changes
            OnPropertyChanged(nameof(MeanR));
            OnPropertyChanged(nameof(MeanG));
            OnPropertyChanged(nameof(MeanB));
            OnPropertyChanged(nameof(StdR));
            OnPropertyChanged(nameof(StdG));
            OnPropertyChanged(nameof(StdB));
        }

        #endregion Commands

        #region Constructor

        public ApproachViewModel(IAppStateService appStateService) : base(1)
        {
            _appStateService = appStateService ?? throw new ArgumentNullException(nameof(appStateService));

            Title = "Approach Selection";

            // Subscribe to app state changes
            _appStateService.ApproachSettingsChanged += OnApproachSettingsChanged;

            UpdateCompletionStatus();
        }

        #endregion Constructor

        #region Methods

        public override bool CanNavigateToNext
        {
            get
            {
                if (IsDeepLearningSelected)
                {
                    return !string.IsNullOrEmpty(_appStateService.ModelFilePath) && File.Exists(_appStateService.ModelFilePath);
                }
                else
                {
                    // TODO Implement other
                    return IsPerceptualHashingSelected || IsColorHistogramSelected || IsSiftSurfSelected || IsTemplateMatchingSelected || IsSemanticSimilaritySelected;
                }
            }
        }

        private void UpdateCompletionStatus()
        {
            IsComplete = CanNavigateToNext;
        }

        private void OnApproachSettingsChanged(object? sender, EventArgs e)
        {
            // Update UI when app state changes
            OnPropertyChanged(nameof(SelectedApproach));
            OnPropertyChanged(nameof(ModelFilePath));
            OnPropertyChanged(nameof(IsDeepLearningSelected));
            OnPropertyChanged(nameof(IsPerceptualHashingSelected));
            OnPropertyChanged(nameof(IsColorHistogramSelected));
            OnPropertyChanged(nameof(IsSiftSurfSelected));
            OnPropertyChanged(nameof(IsTemplateMatchingSelected));
            OnPropertyChanged(nameof(IsSemanticSimilaritySelected));
            OnPropertyChanged(nameof(IsOtherApproachSelected));
            OnPropertyChanged(nameof(MeanR));
            OnPropertyChanged(nameof(MeanG));
            OnPropertyChanged(nameof(MeanB));
            OnPropertyChanged(nameof(StdR));
            OnPropertyChanged(nameof(StdG));
            OnPropertyChanged(nameof(StdB));

            UpdateCompletionStatus();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _appStateService.ApproachSettingsChanged -= OnApproachSettingsChanged;
            }
            base.Dispose(disposing);
        }

        #endregion Methods
    }
}