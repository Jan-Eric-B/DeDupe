using DeDupe.Enums;
using Microsoft.UI.Xaml;

namespace DeDupe.Services
{
    public interface IThemeService
    {
        void Initialize();

        void RegisterWindow(Window window, FrameworkElement rootElement);

        void UnregisterWindow(Window window);

        void ApplyTheme(Window window, FrameworkElement element);

        void ApplyBackdrop(Window window);

        void ApplyAccentColor(AppAccentColor accentColor);
    }
}