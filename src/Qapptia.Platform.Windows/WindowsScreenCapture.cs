using System.Runtime.InteropServices;
using Qapptia.Core.Abstractions;
using Serilog;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Qapptia.Platform.Windows;

/// <summary>
/// Captura la pantalla completa (virtual screen multi-monitor) usando BitBlt+GetDIBits
/// via CsWin32. Devuelve bytes BGRA crudos (premultiplicados por alpha).
/// </summary>
public sealed class WindowsScreenCapture : IScreenCapture
{
    private readonly ILogger _logger;

    public WindowsScreenCapture(ILogger logger)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("WindowsScreenCapture requiere Windows.");
        _logger = logger;
    }

    public unsafe Task<ScreenCaptureResult> CaptureScreenAsync(bool captureAllScreens = false, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            int x, y, width, height;

            if (captureAllScreens)
            {
                x = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_XVIRTUALSCREEN);
                y = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_YVIRTUALSCREEN);
                width = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CXVIRTUALSCREEN);
                height = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CYVIRTUALSCREEN);
            }
            else
            {
                PInvoke.GetCursorPos(out var pt);
                var hMonitor = PInvoke.MonitorFromPoint(pt, MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST);
                var info = new MONITORINFO { cbSize = (uint)Marshal.SizeOf<MONITORINFO>() };
                if (PInvoke.GetMonitorInfo(hMonitor, ref info))
                {
                    x = info.rcMonitor.left;
                    y = info.rcMonitor.top;
                    width = info.rcMonitor.right - info.rcMonitor.left;
                    height = info.rcMonitor.bottom - info.rcMonitor.top;
                }
                else
                {
                    x = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_XVIRTUALSCREEN);
                    y = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_YVIRTUALSCREEN);
                    width = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CXVIRTUALSCREEN);
                    height = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CYVIRTUALSCREEN);
                }
            }

            if (width <= 0 || height <= 0) throw new InvalidOperationException("Virtual screen con dims inválidas");

            var hdcScreen = PInvoke.GetDC(HWND.Null);
            try
            {
                var hdcMem = PInvoke.CreateCompatibleDC(hdcScreen);
                var hbmp = PInvoke.CreateCompatibleBitmap(hdcScreen, width, height);
                var hbmpOld = PInvoke.SelectObject(hdcMem, new HGDIOBJ(hbmp.Value));

                try
                {
                    if (!PInvoke.BitBlt(hdcMem, 0, 0, width, height, hdcScreen, x, y, ROP_CODE.SRCCOPY))
                        throw new InvalidOperationException($"BitBlt failed: {Marshal.GetLastWin32Error()}");

                    var pixels = new byte[width * height * 4];
                    var bmiSize = Marshal.SizeOf<BITMAPINFOHEADER>();
                    var bmiPtr = Marshal.AllocHGlobal(bmiSize);
                    try
                    {
                        var header = new BITMAPINFOHEADER
                        {
                            biSize = (uint)bmiSize,
                            biWidth = width,
                            biHeight = -height, // top-down
                            biPlanes = 1,
                            biBitCount = 32,
                            biCompression = 0,
                        };
                        Marshal.StructureToPtr(header, bmiPtr, false);

                        fixed (byte* pPixels = pixels)
                        {
                            var rowsCopied = PInvoke.GetDIBits(hdcMem, hbmp, 0, (uint)height,
                                pPixels, (BITMAPINFO*)bmiPtr, DIB_USAGE.DIB_RGB_COLORS);
                            if (rowsCopied == 0)
                                throw new InvalidOperationException($"GetDIBits failed: {Marshal.GetLastWin32Error()}");
                        }
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(bmiPtr);
                    }

                    _logger.Debug("Captura {W}x{H} @ ({X},{Y})", width, height, x, y);
                    return new ScreenCaptureResult(pixels, width, height, x, y);
                }
                finally
                {
                    PInvoke.SelectObject(hdcMem, hbmpOld);
                    PInvoke.DeleteObject(new HGDIOBJ(hbmp.Value));
                    PInvoke.DeleteDC(hdcMem);
                }
            }
            finally
            {
                _ = PInvoke.ReleaseDC(HWND.Null, hdcScreen);
            }
        }, ct);
    }
}

