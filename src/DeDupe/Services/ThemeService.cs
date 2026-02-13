using DeDupe.Constants;
using DeDupe.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using System.Collections.Generic;
using Windows.UI;
using WinRT.Interop;

namespace DeDupe.Services
{
    /// <inheritdoc/>
    public partial class ThemeService(ISettingsService settingsService, ILogger<ThemeService> logger) : IThemeService
    {
        private readonly ISettingsService _settingsService = settingsService;
        private readonly ILogger<ThemeService> _logger = logger;
        private readonly Dictionary<Window, FrameworkElement> _registeredWindows = [];

        /// <inheritdoc/>
        public void Initialize()
        {
            _settingsService.ThemeChanged += OnThemeChanged;
            _settingsService.BackdropChanged += OnBackdropChanged;
            _settingsService.AccentColorChanged += OnAccentColorChanged;

            ApplyAccentColor(_settingsService.AccentColor);

            LogThemeServiceInitialized();
        }

        /// <inheritdoc/>
        public void RegisterWindow(Window window, FrameworkElement rootElement)
        {
            if (!_registeredWindows.ContainsKey(window))
            {
                _registeredWindows[window] = rootElement;

                ApplyTheme(window, rootElement);
                ApplyBackdrop(window);

                LogWindowRegistered(_registeredWindows.Count);
            }
        }

        /// <inheritdoc/>
        public void UnregisterWindow(Window window)
        {
            _registeredWindows.Remove(window);

            LogWindowUnregistered(_registeredWindows.Count);
        }

        #region Theme

        /// <inheritdoc/>
        public void ApplyTheme(Window window, FrameworkElement element)
        {
            element.RequestedTheme = _settingsService.Theme switch
            {
                AppTheme.Light => ElementTheme.Light,
                AppTheme.Dark => ElementTheme.Dark,
                _ => ElementTheme.Default
            };

            // Get AppWindow
            nint hWnd = WindowNative.GetWindowHandle(window);
            WindowId windowId = Win32Interop.GetWindowIdFromWindow(hWnd);
            AppWindow appWindow = AppWindow.GetFromWindowId(windowId);

            // Get theme
            bool isDark = element.ActualTheme == ElementTheme.Dark;

            // Set caption button colors
            if (AppWindowTitleBar.IsCustomizationSupported())
            {
                AppWindowTitleBar titleBar = appWindow.TitleBar;

                if (isDark)
                {
                    titleBar.ButtonForegroundColor = Colors.White;
                    titleBar.ButtonHoverForegroundColor = Colors.White;
                    titleBar.ButtonHoverBackgroundColor = Color.FromArgb(30, 255, 255, 255);
                    titleBar.ButtonPressedForegroundColor = Colors.White;
                    titleBar.ButtonPressedBackgroundColor = Color.FromArgb(20, 255, 255, 255);
                }
                else
                {
                    titleBar.ButtonForegroundColor = Colors.Black;
                    titleBar.ButtonHoverForegroundColor = Colors.Black;
                    titleBar.ButtonHoverBackgroundColor = Color.FromArgb(30, 0, 0, 0);
                    titleBar.ButtonPressedForegroundColor = Colors.Black;
                    titleBar.ButtonPressedBackgroundColor = Color.FromArgb(20, 0, 0, 0);
                }

                titleBar.ButtonBackgroundColor = Colors.Transparent;
                titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
            }

            LogThemeApplied(_settingsService.Theme.ToString(), isDark);
        }

        private void OnThemeChanged(object? sender, AppTheme theme)
        {
            foreach ((Window? window, FrameworkElement? rootElement) in _registeredWindows)
            {
                ApplyTheme(window, rootElement);
            }
        }

        #endregion Theme

        #region Backdrop

        /// <inheritdoc/>
        public void ApplyBackdrop(Window window)
        {
            window.SystemBackdrop = _settingsService.Backdrop switch
            {
                AppBackdrop.Mica => new MicaBackdrop { Kind = MicaKind.Base },
                AppBackdrop.MicaAlt => new MicaBackdrop { Kind = MicaKind.BaseAlt },
                AppBackdrop.Acrylic => new DesktopAcrylicBackdrop(),
                AppBackdrop.None => null,
                _ => null
            };

            LogBackdropApplied(_settingsService.Backdrop.ToString());
        }

        private void OnBackdropChanged(object? sender, AppBackdrop backdrop)
        {
            foreach (Window window in _registeredWindows.Keys)
            {
                ApplyBackdrop(window);
            }
        }

        #endregion Backdrop

        #region Accent Color

        private bool _isCustomAccentApplied = false;

        /// <inheritdoc/>
        public void ApplyAccentColor(AppAccentColor accentColor)
        {
            ResourceDictionary resources = Application.Current.Resources;

            if (accentColor == AppAccentColor.Application)
            {
                AccentPalette palette = AccentPalette.Purple;

                // Apply accent palette
                resources["SystemAccentColor"] = palette.Base;
                resources["SystemAccentColorLight1"] = palette.Light1;
                resources["SystemAccentColorLight2"] = palette.Light2;
                resources["SystemAccentColorLight3"] = palette.Light3;
                resources["SystemAccentColorDark1"] = palette.Dark1;
                resources["SystemAccentColorDark2"] = palette.Dark2;
                resources["SystemAccentColorDark3"] = palette.Dark3;

                _isCustomAccentApplied = true;
            }
            else if (_isCustomAccentApplied)
            {
                // Restore system accent colors
                RemoveAccentColorOverrides(resources);
                _isCustomAccentApplied = false;
            }

            // Force UI refresh
            RefreshAllWindows();

            LogAccentColorApplied(accentColor.ToString());
        }

        private void RefreshAllWindows()
        {
            foreach ((_, FrameworkElement? rootElement) in _registeredWindows)
            {
                // Force resource refresh
                ElementTheme currentTheme = rootElement.RequestedTheme;
                rootElement.RequestedTheme = currentTheme == ElementTheme.Light ? ElementTheme.Dark : ElementTheme.Light;
                rootElement.RequestedTheme = currentTheme;
            }
        }

        private static void RemoveAccentColorOverrides(ResourceDictionary resources)
        {
            string[] accentKeys =
            [
                "SystemAccentColor",
                "SystemAccentColorLight1",
                "SystemAccentColorLight2",
                "SystemAccentColorLight3",
                "SystemAccentColorDark1",
                "SystemAccentColorDark2",
                "SystemAccentColorDark3"
            ];

            foreach (string key in accentKeys)
            {
                if (resources.ContainsKey(key))
                {
                    resources.Remove(key);
                }
            }
        }

        private void OnAccentColorChanged(object? sender, AppAccentColor accentColor)
        {
            ApplyAccentColor(accentColor);
        }

        #endregion Accent Color

        #region Logging

        [LoggerMessage(Level = LogLevel.Information, Message = "Theme service initialized")]
        private partial void LogThemeServiceInitialized();

        [LoggerMessage(Level = LogLevel.Debug, Message = "Window registered, {WindowCount} window(s) tracked")]
        private partial void LogWindowRegistered(int windowCount);

        [LoggerMessage(Level = LogLevel.Debug, Message = "Window unregistered, {WindowCount} window(s) tracked")]
        private partial void LogWindowUnregistered(int windowCount);

        [LoggerMessage(Level = LogLevel.Debug, Message = "Theme applied: {ThemeName}, resolved as dark: {IsDark}")]
        private partial void LogThemeApplied(string themeName, bool isDark);

        [LoggerMessage(Level = LogLevel.Debug, Message = "Backdrop applied: {BackdropType}")]
        private partial void LogBackdropApplied(string backdropType);

        [LoggerMessage(Level = LogLevel.Debug, Message = "Accent color applied: {AccentColorType}")]
        private partial void LogAccentColorApplied(string accentColorType);

        #endregion Logging
    }
}