using DeDupe.Enums;
using Microsoft.UI.Xaml;

namespace DeDupe.Services
{
    /// <summary>
    /// Provides methods for managing application themes, accent colors, and visual effects across registered windows.
    /// </summary>
    public interface IThemeService
    {
        /// <summary>
        /// Subscribes to settings change events and applies initial accent color.
        /// </summary>
        void Initialize();

        /// <summary>
        /// Registers a window and applies the current theme and backdrop to it.
        /// </summary>
        void RegisterWindow(Window window, FrameworkElement rootElement);

        /// <summary>
        /// Unregisters specified window.
        /// </summary>
        void UnregisterWindow(Window window);

        /// <summary>
        /// Applies current theme to the element and updates the window's title bar button colors to match.
        /// </summary>
        void ApplyTheme(Window window, FrameworkElement element);

        /// <summary>
        /// Applies visual backdrop effect to specified window.
        /// </summary>
        void ApplyBackdrop(Window window);

        /// <summary>
        /// Applies specified accent color, or restores system accent colors when set to
        /// <see cref="AppAccentColor.Application"/>. Triggers UI refresh on all registered windows.
        /// </summary>
        void ApplyAccentColor(AppAccentColor accentColor);
    }
}