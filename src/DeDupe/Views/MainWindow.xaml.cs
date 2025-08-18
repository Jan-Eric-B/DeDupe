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

        public MainWindow()
        {
            InitializeComponent();
            InitializeTheme();

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
        }

        private void NavigateToPage(int pageIndex)
        {
            Type pageType = pageIndex switch
            {
                0 => typeof(FileInputPage),
                1 => typeof(ApproachPage),
                2 => typeof(PreProcessingPage),
                3 => typeof(AnalysisPage),
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
            SetTitleBar(spTitle);
        }
    }
}