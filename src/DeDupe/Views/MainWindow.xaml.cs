using DeDupe.Services;
using DeDupe.ViewModels;
using DeDupe.Views.Pages;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using System;
using System.ComponentModel;

namespace DeDupe
{
    public sealed partial class MainWindow : Window
    {
        private MainWindowViewModel ViewModel { get; }

        private int _previousStepIndex = 0;

        private readonly WindowsSizeService _windowsSizeService;

        public MainWindow()
        {
            InitializeComponent();
            InitializeTheme();

            // Set minimum window size
            nint hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            _windowsSizeService = new WindowsSizeService(hwnd, 800, 600);
            this.Closed += (s, e) => _windowsSizeService?.Dispose();

            ViewModel = App.Current.GetService<MainWindowViewModel>();
            grdRoot.DataContext = ViewModel;
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;
            NavigateToPage(ViewModel.SelectedStepIndex);
        }

        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainWindowViewModel.SelectedStepIndex))
            {
                NavigateToPage(ViewModel.SelectedStepIndex);
            }
            else if (e.PropertyName == nameof(MainWindowViewModel.IsInManagementMode) && ViewModel.IsInManagementMode)
            {
                // Navigate to management page when entering management mode
                frManagement.Navigate(typeof(ManagementPage));
            }
        }

        private void StepperControl_StepClicked(object sender, int stepNumber)
        {
            ViewModel.NavigateToStepCommand.Execute(stepNumber);
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            App.Current.OpenSettingsWindow();
        }

        private void NavigateToPage(int pageIndex)
        {
            Type pageType = pageIndex switch
            {
                0 => typeof(FileInputPage),
                1 => typeof(PreProcessingPage),
                2 => typeof(ModelConfigurationPage),
                _ => typeof(FileInputPage)
            };

            bool isForward = pageIndex > _previousStepIndex;
            Microsoft.UI.Xaml.Media.Animation.SlideNavigationTransitionInfo? slideTransition = new()
            {
                Effect = isForward
                    ? Microsoft.UI.Xaml.Media.Animation.SlideNavigationTransitionEffect.FromRight
                    : Microsoft.UI.Xaml.Media.Animation.SlideNavigationTransitionEffect.FromLeft
            };

            frMain.Navigate(pageType, null, slideTransition);
            _previousStepIndex = pageIndex;
        }

        private void InitializeTheme()
        {
            SystemBackdrop = new MicaBackdrop() { Kind = MicaKind.Base };
            ExtendsContentIntoTitleBar = true;
            SetTitleBar(grdTitleBar);
        }
    }
}