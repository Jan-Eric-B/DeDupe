using Microsoft.Extensions.Logging;
using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace DeDupe.Services;

/// <inheritdoc />
public partial class TaskbarProgressService(ILogger<TaskbarProgressService> logger) : ITaskbarProgressService
{
    private nint _hwnd;
    private readonly ILogger<TaskbarProgressService> _logger = logger;

    private static ITaskbarList3? _taskbar;
    private static bool _taskbarInitialized;

    /// <inheritdoc />
    public void SetWindowHandle(nint hwnd)
    {
        _hwnd = hwnd;
        LogTaskbarServiceInitialized(hwnd);
    }

    /// <inheritdoc />
    public void SetProgress(double value, double maximum)
    {
        if (_hwnd == 0) return;

        SetTaskbarState(TaskbarProgressState.Normal);
        SetTaskbarValue(value, maximum);
    }

    /// <inheritdoc />
    public void SetIndeterminate()
    {
        if (_hwnd == 0) return;

        SetTaskbarState(TaskbarProgressState.Indeterminate);
    }

    /// <inheritdoc />
    public void SetError()
    {
        if (_hwnd == 0) return;

        SetTaskbarState(TaskbarProgressState.Error);
    }

    /// <inheritdoc />
    public void SetPaused()
    {
        if (_hwnd == 0) return;

        SetTaskbarState(TaskbarProgressState.Paused);
    }

    /// <inheritdoc />
    public void Clear()
    {
        if (_hwnd == 0) return;

        SetTaskbarState(TaskbarProgressState.NoProgress);
    }

    #region Taskbar COM Interop

    private static readonly StrategyBasedComWrappers _comWrappers = new();
    private static readonly Guid CLSID_TaskbarList = new("56fdf344-fd6d-11d0-958a-006097c9a090");

    private static ITaskbarList3? GetTaskbar()
    {
        if (_taskbarInitialized) return _taskbar;
        _taskbarInitialized = true;
        try
        {
            Guid clsid = CLSID_TaskbarList;
            Guid iid = typeof(ITaskbarList3).GUID;
            int hr = CoCreateInstance(in clsid, 0, 1 /* CLSCTX_INPROC_SERVER */, in iid, out nint ptr);

            if (hr < 0 || ptr == 0) return null;

            object wrapper = _comWrappers.GetOrCreateObjectForComInstance(ptr, CreateObjectFlags.None);
            Marshal.Release(ptr);

            _taskbar = (ITaskbarList3)wrapper;
            _taskbar.HrInit();
        }
        catch
        {
            _taskbar = null;
        }
        return _taskbar;
    }

    private void SetTaskbarValue(double value, double maximum)
    {
        ITaskbarList3? tb = GetTaskbar();
        if (tb is null) return;
        try
        {
            ulong completed = (ulong)Math.Max(0, Math.Min(value, maximum));
            ulong total = (ulong)Math.Max(1, maximum);
            tb.SetProgressValue(_hwnd, completed, total);
        }
        catch { /* best-effort */ }
    }

    private void SetTaskbarState(TaskbarProgressState state)
    {
        ITaskbarList3? tb = GetTaskbar();
        if (tb is null) return;
        try
        {
            tb.SetProgressState(_hwnd, state);
        }
        catch { /* best-effort */ }
    }

    [LibraryImport("ole32.dll")]
    private static partial int CoCreateInstance(in Guid rclsid, nint pUnkOuter, uint dwClsContext, in Guid riid, out nint ppv);

    #endregion Taskbar COM Interop

    #region Logging

    [LoggerMessage(Level = LogLevel.Debug, Message = "Taskbar progress service initialized with HWND {Hwnd}")]
    private partial void LogTaskbarServiceInitialized(nint hwnd);

    #endregion Logging
}

/// <summary>
/// Taskbar button progress state flags used by <c>ITaskbarList3</c>.
/// </summary>
internal enum TaskbarProgressState
{
    NoProgress = 0x0,
    Indeterminate = 0x1,
    Normal = 0x2,
    Error = 0x4,
    Paused = 0x8
}

[GeneratedComInterface]
[Guid("ea1afb91-9e28-4b86-90e9-9e9f8a5eefaf")]
internal partial interface ITaskbarList3
{
    // ITaskbarList
    [PreserveSig] int HrInit();
    [PreserveSig] int AddTab(nint hwnd);
    [PreserveSig] int DeleteTab(nint hwnd);
    [PreserveSig] int ActivateTab(nint hwnd);
    [PreserveSig] int SetActiveAlt(nint hwnd);

    // ITaskbarList2
    [PreserveSig] int MarkFullscreenWindow(nint hwnd, [MarshalAs(UnmanagedType.Bool)] bool fFullscreen);

    // ITaskbarList3
    [PreserveSig] int SetProgressValue(nint hwnd, ulong ullCompleted, ulong ullTotal);
    [PreserveSig] int SetProgressState(nint hwnd, TaskbarProgressState tbpFlags);
}