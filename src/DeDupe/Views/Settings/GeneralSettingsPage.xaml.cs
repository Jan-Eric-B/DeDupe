using DeDupe.ViewModels.Settings;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace DeDupe.Views.Settings
{
    public sealed partial class GeneralSettingsPage : Page
    {
        private GeneralSettingsViewModel ViewModel { get; }

        public GeneralSettingsPage()
        {
            InitializeComponent();
            ViewModel = App.Current.GetService<GeneralSettingsViewModel>();
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