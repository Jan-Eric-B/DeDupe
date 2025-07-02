using DeDupe.ViewModels;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace DeDupe
{
    public sealed partial class MainWindow : Window
    {
        private MainWindowViewModel ViewModel { get; }

        public MainWindow()
        {
            InitializeComponent();
            InitializeTheme();

            ViewModel = App.Current.GetService<MainWindowViewModel>();
        }

        private void InitializeTheme()
        {
            SystemBackdrop = new MicaBackdrop() { Kind = MicaKind.Base };
            ExtendsContentIntoTitleBar = true;
            SetTitleBar(spTitle);
        }
    }
}