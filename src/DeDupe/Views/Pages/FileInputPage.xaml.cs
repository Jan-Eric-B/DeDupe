using DeDupe.ViewModels.Pages;
using Microsoft.UI.Xaml.Controls;

namespace DeDupe.Views.Pages
{
    public sealed partial class FileInputPage : Page
    {
        public FileInputViewModel ViewModel { get; }

        public FileInputPage()
        {
            InitializeComponent();
            ViewModel = App.Current.GetService<FileInputViewModel>();
            DataContext = ViewModel;
        }
    }
}