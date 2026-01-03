using DeDupe.ViewModels.Settings;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace DeDupe.Views.Settings
{
    public sealed partial class ModelSettingsPage : Page
    {
        public ModelSettingsViewModel ViewModel { get; }

        public ModelSettingsPage()
        {
            InitializeComponent();
            ViewModel = App.Current.GetService<ModelSettingsViewModel>();
            DataContext = ViewModel;
        }

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
    }
}