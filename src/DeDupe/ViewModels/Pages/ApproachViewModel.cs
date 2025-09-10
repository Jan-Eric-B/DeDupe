using CommunityToolkit.Mvvm.Input;
using DeDupe.Enums.Approach;
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

        private ApproachType _selectedApproach = ApproachType.DeepLearning;

        private string _modelFilePath = string.Empty;

        private double _meanR = 0.485;
        private double _meanG = 0.456;
        private double _meanB = 0.406;
        private double _stdR = 0.229;
        private double _stdG = 0.224;
        private double _stdB = 0.225;

        #endregion Fields

        #region Properties

        public ApproachType SelectedApproach
        {
            get => _selectedApproach;
            set
            {
                if (SetProperty(ref _selectedApproach, value))
                {
                    // Notify all radio buttons
                    OnPropertyChanged(nameof(IsDeepLearningSelected));
                    OnPropertyChanged(nameof(IsPerceptualHashingSelected));
                    OnPropertyChanged(nameof(IsColorHistogramSelected));
                    OnPropertyChanged(nameof(IsSiftSurfSelected));
                    OnPropertyChanged(nameof(IsTemplateMatchingSelected));
                    OnPropertyChanged(nameof(IsSemanticSimilaritySelected));
                    OnPropertyChanged(nameof(IsOtherApproachSelected));
                }
            }
        }

        public string ModelFilePath
        {
            get => _modelFilePath;
            set
            {
                if (SetProperty(ref _modelFilePath, value))
                {
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
            get => _meanR;
            set => SetProperty(ref _meanR, value);
        }

        public double MeanG
        {
            get => _meanG;
            set => SetProperty(ref _meanG, value);
        }

        public double MeanB
        {
            get => _meanB;
            set => SetProperty(ref _meanB, value);
        }

        public double StdR
        {
            get => _stdR;
            set => SetProperty(ref _stdR, value);
        }

        public double StdG
        {
            get => _stdG;
            set => SetProperty(ref _stdG, value);
        }

        public double StdB
        {
            get => _stdB;
            set => SetProperty(ref _stdB, value);
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
            MeanR = 0.485;
            MeanG = 0.456;
            MeanB = 0.406;
            StdR = 0.229;
            StdG = 0.224;
            StdB = 0.225;
        }

        #endregion Commands

        #region Constructor

        public ApproachViewModel() : base(1)
        {
            Title = "Approach Selection";
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
                    return !string.IsNullOrEmpty(ModelFilePath) && File.Exists(ModelFilePath);
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

        #endregion Methods
    }
}