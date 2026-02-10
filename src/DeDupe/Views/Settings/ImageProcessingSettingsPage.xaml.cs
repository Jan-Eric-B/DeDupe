using DeDupe.ViewModels.Settings;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace DeDupe.Views.Settings
{
    public sealed partial class ImageProcessingSettingsPage : Page
    {
        private ImageProcessingSettingsViewModel ViewModel { get; }

        public ImageProcessingSettingsPage()
        {
            InitializeComponent();
            ViewModel = App.Current.GetService<ImageProcessingSettingsViewModel>();
            DataContext = ViewModel;
        }

        #region Navigation

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            ViewModel.OnNavigatedTo();
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            ViewModel.OnNavigatedFrom();
        }

        #endregion Navigation
    }
}