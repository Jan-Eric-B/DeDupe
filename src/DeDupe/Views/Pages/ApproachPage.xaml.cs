using DeDupe.ViewModels.Pages;
using Microsoft.UI.Xaml.Controls;

namespace DeDupe.Views.Pages
{
    public sealed partial class ApproachPage : Page
    {
        public ApproachViewModel ViewModel { get; }

        public ApproachPage()
        {
            InitializeComponent();
            ViewModel = App.Current.GetService<ApproachViewModel>();
            DataContext = ViewModel;
        }
    }
}