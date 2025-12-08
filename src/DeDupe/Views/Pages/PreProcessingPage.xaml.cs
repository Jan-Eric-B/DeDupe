using DeDupe.ViewModels.Pages;
using Microsoft.UI.Xaml.Controls;

namespace DeDupe.Views.Pages
{
    public sealed partial class PreProcessingPage : Page
    {
        public PreProcessingViewModel ViewModel { get; }

        public PreProcessingPage()
        {
            this.InitializeComponent();
            ViewModel = App.Current.GetService<PreProcessingViewModel>();
            this.DataContext = ViewModel;
        }
    }
}