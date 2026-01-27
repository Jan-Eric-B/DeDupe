using DeDupe.Services;
using DeDupe.ViewModels;
using DeDupe.Views.Pages;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using System;
using System.ComponentModel;
using Windows.Foundation;

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
            grdTitleBar.Loaded += (s, e) => SetRegionsForCustomTitleBar();
            grdTitleBar.SizeChanged += (s, e) => SetRegionsForCustomTitleBar();

            // Cleanup on close
            this.Closed += OnWindowClosed;

            ViewModel = App.Current.GetService<MainWindowViewModel>();
            grdRoot.DataContext = ViewModel;
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;

            // Navigate to ConfigurationPage initially
            frMain.Navigate(typeof(ConfigurationPage));
        }

        private void SetRegionsForCustomTitleBar()
        {
            double scaleAdjustment = grdTitleBar.XamlRoot.RasterizationScale;

            // Set padding for caption buttons
            RightPaddingColumn.Width = new GridLength(AppWindow.TitleBar.RightInset / scaleAdjustment);
            LeftPaddingColumn.Width = new GridLength(AppWindow.TitleBar.LeftInset / scaleAdjustment);

            // Make the button interactive
            GeneralTransform? transform = btnSettings.TransformToVisual(null);
            Rect bounds = transform.TransformBounds(new Rect(0, 0, btnSettings.ActualWidth, btnSettings.ActualHeight));

            Windows.Graphics.RectInt32 buttonRect = new(
                (int)Math.Round(bounds.X * scaleAdjustment),
                (int)Math.Round(bounds.Y * scaleAdjustment),
                (int)Math.Round(bounds.Width * scaleAdjustment),
                (int)Math.Round(bounds.Height * scaleAdjustment));

            InputNonClientPointerSource? nonClientInputSrc = InputNonClientPointerSource.GetForWindowId(AppWindow.Id);
            nonClientInputSrc.SetRegionRects(NonClientRegionKind.Passthrough, new[] { buttonRect });
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