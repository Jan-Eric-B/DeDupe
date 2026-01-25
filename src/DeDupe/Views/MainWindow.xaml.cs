using DeDupe.Services;
using DeDupe.ViewModels;
using DeDupe.Views.Pages;
using Microsoft.UI.Xaml;
using System.ComponentModel;

namespace DeDupe
{
    public sealed partial class MainWindow : Window
    {
        private MainWindowViewModel ViewModel { get; }

        private int _previousStepIndex = 0;

        private readonly WindowsSizeService _windowsSizeService;
        private readonly IThemeService _themeService;

        public MainWindow()
        {
            InitializeComponent();

            // Set minimum window size
            nint hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            _windowsSizeService = new WindowsSizeService(hwnd, 800, 600);

            // Theme
            _themeService = App.Current.GetService<IThemeService>();
            _themeService.RegisterWindow(this, grdRoot);
            ExtendsContentIntoTitleBar = true;
            SetTitleBar(grdTitleBar);

            // Cleanup on close
            this.Closed += OnWindowClosed;

            ViewModel = App.Current.GetService<MainWindowViewModel>();
            grdRoot.DataContext = ViewModel;
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;

            // Navigate to ConfigurationPage initially
            frMain.Navigate(typeof(ConfigurationPage));
        }

        private void OnWindowClosed(object sender, WindowEventArgs args)
        {
            _themeService.UnregisterWindow(this);
            _windowsSizeService?.Dispose();

            // Close entire application
            App.Current.Shutdown();
        }

        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainWindowViewModel.IsInManagementMode))
            {
                if (ViewModel.IsInManagementMode)
                {
                    // Navigate to ManagementPage when entering management mode
                    frManagement.Navigate(typeof(ManagementPage));
                }
                // When returning to configuration mode, ConfigurationPage is already loaded in frMain
            }
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            App.Current.OpenSettingsWindow();
        }
    }
}