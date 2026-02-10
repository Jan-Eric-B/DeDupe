using DeDupe.Services;
using DeDupe.ViewModels;
using DeDupe.Views.Settings;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using System.Runtime.InteropServices;

namespace DeDupe.Views
{
    public sealed partial class SettingsWindow : Window
    {
        private SettingsWindowViewModel ViewModel { get; }

        private readonly WindowSizeService _windowsSizeService;
        private readonly IThemeService _themeService;

        public SettingsWindow()
        {
            InitializeComponent();

            // Set window size and minimum size
            nint hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            _windowsSizeService = new WindowSizeService(hWnd, 1000, 700);
            SetWindowSize(hWnd, 1000, 700);

            // Theme
            _themeService = App.Current.GetService<IThemeService>();
            _themeService.RegisterWindow(this, grdRoot);
            ExtendsContentIntoTitleBar = true;
            SetTitleBar(spTitle);

            // Cleanup on close
            Closed += OnWindowClosed;

            // ViewModel
            ViewModel = App.Current.GetService<SettingsWindowViewModel>();
            grdRoot.DataContext = ViewModel;

            nvSettings.SelectedItem = nvSettings.MenuItems[0];
        }

        private void OnWindowClosed(object sender, WindowEventArgs args)
        {
            _themeService.UnregisterWindow(this);
            _windowsSizeService?.Dispose();
        }

        private void NavigationView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.SelectedItem is NavigationViewItem selectedItem)
            {
                string? tag = selectedItem.Tag?.ToString();
                Type? pageType = tag switch
                {
                    "General" => typeof(GeneralSettingsPage),
                    "ImageProcessing" => typeof(ImageProcessingSettingsPage),
                    "ModelConfiguration" => typeof(ModelSettingsPage),
                    _ => null
                };

                if (pageType is not null)
                {
                    frSettings.Navigate(pageType, null, new EntranceNavigationTransitionInfo());
                }
            }
        }

        private static void SetWindowSize(IntPtr hWnd, int width, int height)
        {
            WindowId windowId = Win32Interop.GetWindowIdFromWindow(hWnd);
            AppWindow appWindow = AppWindow.GetFromWindowId(windowId);

            // DPI scaling adjustment
            uint dpi = GetDpiForWindow(hWnd);
            float scalingFactor = dpi / 96f;

            appWindow.Resize(new Windows.Graphics.SizeInt32((int)(width * scalingFactor), (int)(height * scalingFactor)));
        }

        [DllImport("user32.dll")]
        private static extern uint GetDpiForWindow(nint hWnd);
    }
}