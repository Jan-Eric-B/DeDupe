using System.Runtime.InteropServices;
using WinRT.Interop;

namespace DeDupe.Helpers
{
    public static class PickerExtensions
    {
        public static void InitializeForCurrentWindow(this object picker)
        {
            nint hwnd = App.Window is not null ? WindowNative.GetWindowHandle(App.Window) : GetActiveWindow();
            InitializeWithWindow.Initialize(picker, hwnd);
        }

        [DllImport("user32.dll")]
        private static extern nint GetActiveWindow();
    }
}