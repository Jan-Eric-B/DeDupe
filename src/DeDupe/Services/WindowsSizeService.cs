using System;
using System.Runtime.InteropServices;

namespace DeDupe.Services
{
    /// <summary>
    /// Service to set minimum window size using SetWindowSubclass API
    /// </summary>
    public partial class WindowsSizeService : IDisposable
    {
        // Windows message sent when the OS queries window size constraints
        private const int WM_GETMINMAXINFO = 0x0024;

        // Window handle - ID for window
        private readonly IntPtr _hwnd;

        // Minimum pixel size (before DPI scaling)
        private readonly int _minWidth;

        private readonly int _minHeight;

        // ID for subclass
        private readonly uint _subclassId;

        // Delegate instance
        private readonly SUBCLASSPROC _subclassProcDelegate;

        public WindowsSizeService(IntPtr hwnd, int minWidth, int minHeight, uint subclassId = 1)
        {
            _hwnd = hwnd;
            _hwnd = hwnd;
            _minWidth = minWidth;
            _minHeight = minHeight;
            _subclassId = subclassId;
            _subclassProcDelegate = SubclassProc;

            // Install subclass using API
            if (!SetWindowSubclass(_hwnd, _subclassProcDelegate, _subclassId, IntPtr.Zero))
            {
                throw new InvalidOperationException("Failed to set window subclass");
            }
        }

        /// <summary>
        /// Callback invoked by Windows for every window message
        /// </summary>
        private IntPtr SubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, IntPtr uIdSubclass, IntPtr dwRefData)
        {
            // Only intercept message that queries size constraints
            if (uMsg == WM_GETMINMAXINFO)
            {
                // Get current DPI for this window
                // - 100% scaling = 96 DPI (Default)
                // - 150% scaling = 144 DPI
                // - 200% scaling = 192 DPI
                uint dpi = GetDpiForWindow(hWnd);
                float scalingFactor = dpi / 96f;

                // Read MINMAXINFO structure from unmanaged memory
                MINMAXINFO minMaxInfo = Marshal.PtrToStructure<MINMAXINFO>(lParam);

                // Set minimum size with DPI to be consistent across different displays
                minMaxInfo.ptMinTrackSize.x = (int)(_minWidth * scalingFactor);
                minMaxInfo.ptMinTrackSize.y = (int)(_minHeight * scalingFactor);

                // Write modified structure back to unmanaged memory
                Marshal.StructureToPtr(minMaxInfo, lParam, true);

                // Return 0 to indicate message handled
                return IntPtr.Zero;
            }

            // Pass to default subclass handler
            return DefSubclassProc(hWnd, uMsg, wParam, lParam);
        }

        #region P/Invoke Declarations

        /// <summary>
        /// Delegate signature for subclass callback function
        /// - StdCall convention matches Windows API expectations
        /// - Called by Windows for every message sent to window
        /// </summary>
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate IntPtr SUBCLASSPROC(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, IntPtr uIdSubclass, IntPtr dwRefData);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MINMAXINFO
        {
            public POINT ptReserved;        // Reserved
            public POINT ptMaxSize;         // Maximized window size
            public POINT ptMaxPosition;     // Position of maximized window
            public POINT ptMinTrackSize;    // Min size
            public POINT ptMaxTrackSize;    // Max size
        }

        /// <summary>
        /// Window subclass callback
        /// </summary>
        [DllImport("comctl32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetWindowSubclass(IntPtr hWnd, SUBCLASSPROC pfnSubclass, uint uIdSubclass, IntPtr dwRefData);

        /// <summary>
        /// Default subclass procedure
        /// </summary>
        [DllImport("comctl32.dll", SetLastError = true)]
        private static extern IntPtr DefSubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

        /// <summary>
        /// Gets the DPI for specific window
        /// </summary>
        [DllImport("user32.dll")]
        private static extern uint GetDpiForWindow(IntPtr hWnd);

        #endregion P/Invoke Declarations

        #region IDisposable

        private bool _disposed;

        public void Dispose()
        {
            if (!_disposed)
            {
                RemoveWindowSubclass(_hwnd, _subclassProcDelegate, _subclassId);
                _disposed = true;
            }
            GC.SuppressFinalize(this);
        }

        [DllImport("comctl32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool RemoveWindowSubclass(IntPtr hWnd, SUBCLASSPROC pfnSubclass, uint uIdSubclass);

        #endregion IDisposable
    }
}