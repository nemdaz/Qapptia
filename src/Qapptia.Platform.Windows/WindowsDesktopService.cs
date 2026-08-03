using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Qapptia.Core.Abstractions;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Qapptia.Platform.Windows;

/// <summary>
/// Desktop services en Windows: MessageBox, info de monitor, DPI.
/// Usa CsWin32 para MessageBox, MonitorFromPoint, GetMonitorInfoW, GetSystemMetrics.
/// </summary>
public sealed class WindowsDesktopService : IDesktopService
{
    private readonly ILogger<WindowsDesktopService> _logger;

    public WindowsDesktopService(ILogger<WindowsDesktopService> logger)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("WindowsDesktopService requiere Windows.");
        _logger = logger;
    }

    public void ShowInfo(string title, string message)
    {
        PInvoke.MessageBox(HWND.Null, message, title,
            MESSAGEBOX_STYLE.MB_OK | MESSAGEBOX_STYLE.MB_ICONINFORMATION);
    }

    public void ShowError(string title, string message)
    {
        PInvoke.AllowSetForegroundWindow(unchecked((uint)-1));
        PInvoke.MessageBox(HWND.Null, message, title,
            MESSAGEBOX_STYLE.MB_OK | MESSAGEBOX_STYLE.MB_ICONERROR |
            MESSAGEBOX_STYLE.MB_TOPMOST | MESSAGEBOX_STYLE.MB_SETFOREGROUND);
    }

    public MonitorInfo GetMonitorAtCursor()
    {
        PInvoke.GetCursorPos(out var pt);
        var hmon = PInvoke.MonitorFromPoint(pt, MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST);

        var mi = new MONITORINFO { cbSize = (uint)Marshal.SizeOf<MONITORINFO>() };
        PInvoke.GetMonitorInfo(hmon, ref mi);
        return new MonitorInfo(
            mi.rcMonitor.left, mi.rcMonitor.top,
            mi.rcMonitor.right - mi.rcMonitor.left,
            mi.rcMonitor.bottom - mi.rcMonitor.top,
            (mi.dwFlags & 1u) != 0);
    }

    public (int X, int Y) GetVirtualScreenOrigin()
        => (PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_XVIRTUALSCREEN),
            PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_YVIRTUALSCREEN));

    public int GetVirtualScreenWidth()
        => PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CXVIRTUALSCREEN);

    public int GetVirtualScreenHeight()
        => PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CYVIRTUALSCREEN);

    public double GetDpiScalingAtCursor()
    {
        var hdc = PInvoke.GetDC(HWND.Null);
        try
        {
            var logicalWidth = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CXSCREEN);
            var physicalWidth = PInvoke.GetDeviceCaps(hdc, GET_DEVICE_CAPS_INDEX.HORZRES);
            return logicalWidth > 0 && physicalWidth > 0
                ? (double)physicalWidth / logicalWidth
                : 1.0;
        }
        finally
        {
            PInvoke.ReleaseDC(HWND.Null, hdc);
        }
    }
}
