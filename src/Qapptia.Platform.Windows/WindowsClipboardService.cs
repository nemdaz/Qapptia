using SkiaSharp;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Qapptia.Core.Abstractions;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Memory;

namespace Qapptia.Platform.Windows;

public sealed class WindowsClipboardService : IClipboardService
{
    private readonly ILogger<WindowsClipboardService> _logger;

    public WindowsClipboardService(ILogger<WindowsClipboardService> logger)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("WindowsClipboardService requiere Windows.");
        _logger = logger;
    }

    public Task SetTextAsync(string text, CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }

    public unsafe Task SetImageAsync(byte[] pngBytes, CancellationToken ct = default)
    {
        try
        {
            using var bitmap = SKBitmap.Decode(pngBytes) ?? throw new InvalidOperationException("SKBitmap.Decode failed to decode the PNG bytes.");
            
            // Render to BGRA8888 because CF_DIB expects BGRA pixel order
            var info = new SKImageInfo(bitmap.Width, bitmap.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
            using var bgraBitmap = new SKBitmap(info);
            using (var canvas = new SKCanvas(bgraBitmap))
            {
                // Fill with white background since clipboard doesn't always support transparency well
                canvas.Clear(SKColors.White);
                canvas.DrawBitmap(bitmap, 0, 0);
            }
            
            byte[] pixelBytes = bgraBitmap.Bytes;
            int dibSize = 40 + pixelBytes.Length; // 40 bytes for BITMAPINFOHEADER

            nint hGlobal = PInvoke.GlobalAlloc(GLOBAL_ALLOC_FLAGS.GMEM_MOVEABLE | GLOBAL_ALLOC_FLAGS.GMEM_ZEROINIT, (nuint)dibSize);
            if (hGlobal == 0) throw new System.ComponentModel.Win32Exception(Marshal.GetLastPInvokeError(), "GlobalAlloc failed to allocate memory for clipboard.");

            nint dest = (nint)PInvoke.GlobalLock(new global::Windows.Win32.Foundation.HGLOBAL((void*)hGlobal));
            if (dest != 0)
            {
                // Write BITMAPINFOHEADER (40 bytes)
                Marshal.WriteInt32(dest, 0, 40); // biSize
                Marshal.WriteInt32(dest, 4, info.Width); // biWidth
                Marshal.WriteInt32(dest, 8, -info.Height); // biHeight (negative means top-down, so pixels map directly)
                Marshal.WriteInt16(dest, 12, 1); // biPlanes
                Marshal.WriteInt16(dest, 14, 32); // biBitCount
                Marshal.WriteInt32(dest, 16, 0); // biCompression (BI_RGB)
                Marshal.WriteInt32(dest, 20, pixelBytes.Length); // biSizeImage
                Marshal.WriteInt32(dest, 24, 2835); // biXPelsPerMeter (72 DPI)
                Marshal.WriteInt32(dest, 28, 2835); // biYPelsPerMeter
                Marshal.WriteInt32(dest, 32, 0); // biClrUsed
                Marshal.WriteInt32(dest, 36, 0); // biClrImportant
                
                // Copy the raw BGRA pixels
                Marshal.Copy(pixelBytes, 0, dest + 40, pixelBytes.Length);
                
                PInvoke.GlobalUnlock(new global::Windows.Win32.Foundation.HGLOBAL((void*)hGlobal));
            }
            else
            {
                PInvoke.GlobalFree(new global::Windows.Win32.Foundation.HGLOBAL((void*)hGlobal));
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastPInvokeError(), "GlobalLock failed.");
            }

            if (PInvoke.OpenClipboard(default))
            {
                PInvoke.EmptyClipboard();
                PInvoke.SetClipboardData(8, new global::Windows.Win32.Foundation.HANDLE(hGlobal)); // 8 is CF_DIB
                PInvoke.CloseClipboard();
            }
            else
            {
                PInvoke.GlobalFree(new global::Windows.Win32.Foundation.HGLOBAL((void*)hGlobal));
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastPInvokeError(), "OpenClipboard failed.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set image to clipboard.");
            throw; // Rethrow so the caller knows the operation failed
        }

        return Task.CompletedTask;
    }

    public Task SetFileDropListAsync(string[] filePaths, CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }
}
