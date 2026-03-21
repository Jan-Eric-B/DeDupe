namespace DeDupe.Services;

/// <summary>
/// Manages Windows taskbar button progress indicator.
/// </summary>
public interface ITaskbarProgressService
{
    /// <summary>
    /// Sets window handle to show progress.
    /// </summary>
    void SetWindowHandle(nint hwnd);

    /// <summary>
    /// Updates taskbar progress value (0 to <paramref name="maximum"/>).
    /// </summary>
    void SetProgress(double value, double maximum);

    /// <summary>
    /// Sets taskbar to indeterminate (spinning) state.
    /// </summary>
    void SetIndeterminate();

    /// <summary>
    /// Sets taskbar to error state at current progress value.
    /// </summary>
    void SetError();

    /// <summary>
    /// Sets taskbar to paused/warning state at current progress value.
    /// </summary>
    void SetPaused();

    /// <summary>
    /// Clears taskbar progress indicator entirely.
    /// </summary>
    void Clear();
}
