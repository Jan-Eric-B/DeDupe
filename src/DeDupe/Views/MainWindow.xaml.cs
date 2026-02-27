using DeDupe.Services;
using DeDupe.ViewModels;
using DeDupe.Views.Pages;
using Microsoft.Extensions.Logging;
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

        private readonly WindowSizeService _windowSizeService;
        private readonly IThemeService _themeService;
        private readonly ILogger<MainWindow> _logger;

        public MainWindow()
        {
            InitializeComponent();

            _logger = App.Current.GetService<ILogger<MainWindow>>();

            // Set minimum window size
            nint hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            _windowSizeService = new WindowSizeService(hWnd, 800, 600, App.Current.GetService<ILogger<WindowSizeService>>());

            // Theme
            _themeService = App.Current.GetService<IThemeService>();
            _themeService.RegisterWindow(this, grdRoot);
            ExtendsContentIntoTitleBar = true;
            grdTitleBar.Loaded += (s, e) => SetRegionsForCustomTitleBar();
            grdTitleBar.SizeChanged += (s, e) => SetRegionsForCustomTitleBar();

            // Cleanup on close
            Closed += OnWindowClosed;

            // ViewModel
            ViewModel = App.Current.GetService<MainWindowViewModel>();
            grdRoot.DataContext = ViewModel;
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;

            // Navigate to ConfigurationPage
            frMain.Navigate(typeof(ConfigurationPage));

            LogMainWindowInitialized();
        }

        private void SetRegionsForCustomTitleBar()
        {
            double scaleAdjustment = grdTitleBar.XamlRoot.RasterizationScale;

            // Set padding for caption buttons
            RightPaddingColumn.Width = new GridLength(AppWindow.TitleBar.RightInset / scaleAdjustment);
            LeftPaddingColumn.Width = new GridLength(AppWindow.TitleBar.LeftInset / scaleAdjustment);

            // Make button interactive
            GeneralTransform? transform = btnSettings.TransformToVisual(null);
            Rect bounds = transform.TransformBounds(new Rect(0, 0, btnSettings.ActualWidth, btnSettings.ActualHeight));

            Windows.Graphics.RectInt32 buttonRect = new(
                (int)Math.Round(bounds.X * scaleAdjustment),
                (int)Math.Round(bounds.Y * scaleAdjustment),
                (int)Math.Round(bounds.Width * scaleAdjustment),
                (int)Math.Round(bounds.Height * scaleAdjustment));

            InputNonClientPointerSource? nonClientInputSrc = InputNonClientPointerSource.GetForWindowId(AppWindow.Id);
            nonClientInputSrc.SetRegionRects(NonClientRegionKind.Passthrough, [buttonRect]);
        }

        private async void OnWindowClosed(object sender, WindowEventArgs args)
        {
            LogMainWindowClosed();

            _themeService.UnregisterWindow(this);
            _windowSizeService?.Dispose();

            await App.Current.Shutdown();
        }

        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainWindowViewModel.IsInManagementMode) && ViewModel.IsInManagementMode)
            {
                frManagement.Navigate(typeof(ManagementPage));
                LogNavigatedToManagementPage();
            }
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            App.Current.OpenSettingsWindow();
        }

        #region Logging

        [LoggerMessage(Level = LogLevel.Information, Message = "Main window initialized")]
        private partial void LogMainWindowInitialized();

        [LoggerMessage(Level = LogLevel.Information, Message = "Main window closed")]
        private partial void LogMainWindowClosed();

        [LoggerMessage(Level = LogLevel.Debug, Message = "Navigated to management page")]
        private partial void LogNavigatedToManagementPage();

        #endregion Logging
    }
}