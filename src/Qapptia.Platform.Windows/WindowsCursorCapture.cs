using Qapptia.Core.Capture;
using System.Runtime.InteropServices;
using Serilog;
using Qapptia.Core.Abstractions;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Qapptia.Platform.Windows;

/// <summary>
/// Captura el cursor actual via <c>GetCursorInfo</c> + <c>GetIconInfo</c> + <c>DrawIconEx</c> +
/// <c>GetDIBits</c>. Realiza un-premultiply de alpha para composición correcta.
/// </summary>
public sealed class WindowsCursorCapture : ICursorCapture
{
    private readonly ILogger _logger;

    public WindowsCursorCapture(ILogger logger)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("WindowsCursorCapture requiere Windows.");
        _logger = logger;
    }

    public unsafe Task<CursorImage?> CaptureCursorAsync(CancellationToken ct = default)
    {
        return Task.Run<CursorImage?>(() =>
        {
            var ci = new CURSORINFO { cbSize = (uint)Marshal.SizeOf<CURSORINFO>() };
            if (!PInvoke.GetCursorInfo(ref ci))
                return null;

            if ((ci.flags & CURSORINFO_FLAGS.CURSOR_SHOWING) == 0)
                return null;

            var ii = new ICONINFO();
            using var iconHandle = new DestroyIconSafeHandle(ci.hCursor.Value, ownsHandle: false);
            if (!PInvoke.GetIconInfo(iconHandle, out ii))
                return null;

            try
            {
                var cw = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CXCURSOR);
                var ch = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CYCURSOR);
                if (cw <= 0) cw = 32;
                if (ch <= 0) ch = 32;

                var hdcScreen = PInvoke.GetDC(HWND.Null);
                try
                {
                    var hdcMem = PInvoke.CreateCompatibleDC(hdcScreen);
                    var hbmp = PInvoke.CreateCompatibleBitmap(hdcScreen, cw, ch);
                    var hbmpOld = PInvoke.SelectObject(hdcMem, new HGDIOBJ(hbmp.Value));

                    try
                    {
                        // DrawIconEx overload directo (no SafeHandle) requiere unsafe.
                        if (!PInvoke.DrawIconEx(hdcMem, 0, 0, ci.hCursor, cw, ch, 0,
                            HBRUSH.Null, DI_FLAGS.DI_NORMAL))
                        {
                            _logger.Warning("DrawIconEx failed");
                            return null;
                        }

                        var pixels = new byte[cw * ch * 4];
                        var bmiSize = Marshal.SizeOf<BITMAPINFOHEADER>();
                        var bmiPtr = Marshal.AllocHGlobal(bmiSize);
                        try
                        {
                            var header = new BITMAPINFOHEADER
                            {
                                biSize = (uint)bmiSize,
                                biWidth = cw,
                                biHeight = -ch,
                                biPlanes = 1,
                                biBitCount = 32,
                                biCompression = 0,
                            };
                            Marshal.StructureToPtr(header, bmiPtr, false);

                            fixed (byte* pPixels = pixels)
                            {
                                var rowsCopied = PInvoke.GetDIBits(hdcMem, hbmp, 0, (uint)ch,
                                    pPixels, (BITMAPINFO*)bmiPtr, DIB_USAGE.DIB_RGB_COLORS);
                                if (rowsCopied == 0) return null;
                            }
                        }
                        finally
                        {
                            Marshal.FreeHGlobal(bmiPtr);
                        }

                        UnPremultiplyAlpha(pixels);
                        if (!HasVisiblePixels(pixels)) return null;

                        return new CursorImage(pixels, cw, ch, (int)ii.xHotspot, (int)ii.yHotspot);
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
            }
            finally
            {
                if (ii.hbmMask.Value != IntPtr.Zero) PInvoke.DeleteObject(new HGDIOBJ(ii.hbmMask.Value));
                if (ii.hbmColor.Value != IntPtr.Zero) PInvoke.DeleteObject(new HGDIOBJ(ii.hbmColor.Value));
            }
        }, ct);
    }

    private static void UnPremultiplyAlpha(byte[] bgra)
    {
        for (var i = 0; i < bgra.Length; i += 4)
        {
            var b = bgra[i];
            var g = bgra[i + 1];
            var r = bgra[i + 2];
            var a = bgra[i + 3];

            if (a == 0) { bgra[i] = 0; bgra[i + 1] = 0; bgra[i + 2] = 0; continue; }
            if (a < 255)
            {
                bgra[i] = (byte)Math.Min(255, b * 255 / a);
                bgra[i + 1] = (byte)Math.Min(255, g * 255 / a);
                bgra[i + 2] = (byte)Math.Min(255, r * 255 / a);
            }
        }
    }

    private static bool HasVisiblePixels(byte[] bgra)
    {
        for (var i = 3; i < bgra.Length; i += 4)
        {
            if (bgra[i] != 0) return true;
        }
        return false;
    }
}

