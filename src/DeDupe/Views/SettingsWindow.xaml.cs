using DeDupe.Services;
using DeDupe.ViewModels;
using DeDupe.Views.Settings;
using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using System;

namespace DeDupe.Views
{
    public sealed partial class SettingsWindow : Window
    {
        private SettingsWindowViewModel ViewModel { get; }

        private readonly WindowsSizeService _windowsSizeService;

        public SettingsWindow()
        {
            InitializeComponent();
            InitializeTheme();

            // Set minimum window size
            nint hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            _windowsSizeService = new WindowsSizeService(hwnd, 600, 450);
            this.Closed += (s, e) => _windowsSizeService?.Dispose();

            SetWindowSize(hwnd, 700, 500);

            ViewModel = App.Current.GetService<SettingsWindowViewModel>();
            grdRoot.DataContext = ViewModel;

            nvSettings.SelectedItem = nvSettings.MenuItems[0];
        }

        private void NavigationView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.SelectedItem is NavigationViewItem selectedItem)
            {
                string? tag = selectedItem.Tag?.ToString();
                Type? pageType = tag switch
                {
                    "General" => typeof(GeneralSettingsPage),
                    "PreProcessing" => typeof(PreProcessingSettingsPage),
                    "ModelConfiguration" => typeof(ModelSettingsPage),
                    _ => null
                };

                if (pageType is not null)
                {
                    frSettings.Navigate(pageType, null, new EntranceNavigationTransitionInfo());
                }
            }
        }

        private void InitializeTheme()
        {
            SystemBackdrop = new MicaBackdrop() { Kind = MicaKind.Base };
            ExtendsContentIntoTitleBar = true;
            SetTitleBar(spTitle);
        }

        private static void SetWindowSize(nint hwnd, int width, int height)
        {
            WindowId windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
            AppWindow appWindow = AppWindow.GetFromWindowId(windowId);

            // DPI scaling
            uint dpi = GetDpiForWindow(hwnd);
            float scalingFactor = dpi / 96f;

            appWindow.Resize(new Windows.Graphics.SizeInt32((int)(width * scalingFactor), (int)(height * scalingFactor)));
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern uint GetDpiForWindow(nint hwnd);
    }
}