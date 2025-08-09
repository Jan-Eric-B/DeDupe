using DeDupe.ViewModels.Pages;
using Microsoft.UI.Xaml.Controls;

namespace DeDupe.Views.Pages
{
    public sealed partial class AnalysisPage : Page
    {
        public AnalysisViewModel ViewModel { get; }

        public AnalysisPage()
        {
            InitializeComponent();
            ViewModel = App.Current.GetService<AnalysisViewModel>();
            DataContext = ViewModel;
        }
    }
}