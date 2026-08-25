using SkiaSharp;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Text;
using Serilog;
using Qapptia.Core.Abstractions;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Memory;

namespace Qapptia.Platform.Windows;

public sealed class WindowsClipboardService : IClipboardService
{
    private readonly ILogger _logger;

    public WindowsClipboardService(ILogger logger)
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
            
            // Renderizar a BGRA8888 porque CF_DIB espera orden de píxeles BGRA
            var info = new SKImageInfo(bitmap.Width, bitmap.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
            using var bgraBitmap = new SKBitmap(info);
            using (var canvas = new SKCanvas(bgraBitmap))
            {
                // Fondo blanco porque el portapapeles no siempre soporta bien la transparencia
                canvas.Clear(SKColors.White);
                canvas.DrawBitmap(bitmap, 0, 0);
            }
            
            byte[] pixelBytes = bgraBitmap.Bytes;
            int dibSize = 40 + pixelBytes.Length; // 40 bytes para BITMAPINFOHEADER

            nint hGlobalDib = PInvoke.GlobalAlloc(GLOBAL_ALLOC_FLAGS.GMEM_MOVEABLE | GLOBAL_ALLOC_FLAGS.GMEM_ZEROINIT, (nuint)dibSize);
            if (hGlobalDib == 0) throw new System.ComponentModel.Win32Exception(Marshal.GetLastPInvokeError(), "GlobalAlloc failed to allocate memory for clipboard DIB.");

            nint destDib = (nint)PInvoke.GlobalLock(new global::Windows.Win32.Foundation.HGLOBAL((void*)hGlobalDib));
            if (destDib != 0)
            {
                // Escribir BITMAPINFOHEADER (40 bytes)
                Marshal.WriteInt32(destDib, 0, 40); // biSize
                Marshal.WriteInt32(destDib, 4, info.Width); // biWidth
                Marshal.WriteInt32(destDib, 8, -info.Height); // biHeight (negativo significa de arriba hacia abajo)
                Marshal.WriteInt16(destDib, 12, 1); // biPlanes
                Marshal.WriteInt16(destDib, 14, 32); // biBitCount
                Marshal.WriteInt32(destDib, 16, 0); // biCompression (BI_RGB)
                Marshal.WriteInt32(destDib, 20, pixelBytes.Length); // biSizeImage
                Marshal.WriteInt32(destDib, 24, 2835); // biXPelsPerMeter (72 DPI)
                Marshal.WriteInt32(destDib, 28, 2835); // biYPelsPerMeter
                Marshal.WriteInt32(destDib, 32, 0); // biClrUsed
                Marshal.WriteInt32(destDib, 36, 0); // biClrImportant
                
                // Copiar los píxeles crudos BGRA
                Marshal.Copy(pixelBytes, 0, destDib + 40, pixelBytes.Length);
                
                PInvoke.GlobalUnlock(new global::Windows.Win32.Foundation.HGLOBAL((void*)hGlobalDib));
            }
            else
            {
                PInvoke.GlobalFree(new global::Windows.Win32.Foundation.HGLOBAL((void*)hGlobalDib));
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastPInvokeError(), "GlobalLock failed for DIB.");
            }

            // También guardar en formato PNG, requerido por aplicaciones modernas como Word, navegadores, etc.
            uint pngFormatId = PInvoke.RegisterClipboardFormat("PNG");
            nint hGlobalPng = PInvoke.GlobalAlloc(GLOBAL_ALLOC_FLAGS.GMEM_MOVEABLE | GLOBAL_ALLOC_FLAGS.GMEM_ZEROINIT, (nuint)pngBytes.Length);
            if (hGlobalPng != 0)
            {
                nint destPng = (nint)PInvoke.GlobalLock(new global::Windows.Win32.Foundation.HGLOBAL((void*)hGlobalPng));
                if (destPng != 0)
                {
                    Marshal.Copy(pngBytes, 0, destPng, pngBytes.Length);
                    PInvoke.GlobalUnlock(new global::Windows.Win32.Foundation.HGLOBAL((void*)hGlobalPng));
                }
            }

            if (PInvoke.OpenClipboard(default))
            {
                PInvoke.EmptyClipboard();
                
                // Set CF_DIB (8)
                PInvoke.SetClipboardData(8, new global::Windows.Win32.Foundation.HANDLE(hGlobalDib));
                
                // Set PNG
                if (hGlobalPng != 0 && pngFormatId != 0)
                {
                    PInvoke.SetClipboardData(pngFormatId, new global::Windows.Win32.Foundation.HANDLE(hGlobalPng));
                }
                
                PInvoke.CloseClipboard();
            }
            else
            {
                PInvoke.GlobalFree(new global::Windows.Win32.Foundation.HGLOBAL((void*)hGlobalDib));
                if (hGlobalPng != 0) PInvoke.GlobalFree(new global::Windows.Win32.Foundation.HGLOBAL((void*)hGlobalPng));
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastPInvokeError(), "OpenClipboard failed.");
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to set image to clipboard.");
            throw; // Rethrow so the caller knows the operation failed
        }

        return Task.CompletedTask;
    }

    public unsafe Task SetFileDropListAsync(string[] filePaths, CancellationToken ct = default)
    {
        if (filePaths == null || filePaths.Length == 0) return Task.CompletedTask;

        try
        {
            // 1. Preparar CF_HDROP
            int dropFilesSize = 20; // sizeof(DROPFILES)
            int stringsByteCount = 0;
            foreach (var path in filePaths)
            {
                stringsByteCount += (path.Length + 1) * 2;
            }
            stringsByteCount += 2; // Doble terminación nula

            int totalDropSize = dropFilesSize + stringsByteCount;
            nint hDrop = PInvoke.GlobalAlloc(GLOBAL_ALLOC_FLAGS.GMEM_MOVEABLE | GLOBAL_ALLOC_FLAGS.GMEM_ZEROINIT, (nuint)totalDropSize);
            if (hDrop == 0) throw new System.ComponentModel.Win32Exception(Marshal.GetLastPInvokeError(), "GlobalAlloc failed for CF_HDROP.");

            nint dropDest = (nint)PInvoke.GlobalLock(new global::Windows.Win32.Foundation.HGLOBAL((void*)hDrop));
            if (dropDest != 0)
            {
                Marshal.WriteInt32(dropDest, 0, 20); // pFiles (tamaño del encabezado)
                Marshal.WriteInt32(dropDest, 16, 1); // fWide (indica que usamos strings Unicode)

                nint strDest = dropDest + 20;
                foreach (var path in filePaths)
                {
                    var bytes = Encoding.Unicode.GetBytes(path + '\0');
                    Marshal.Copy(bytes, 0, strDest, bytes.Length);
                    strDest += bytes.Length;
                }
                Marshal.WriteInt16(strDest, 0); // Final con doble terminación nula

                PInvoke.GlobalUnlock(new global::Windows.Win32.Foundation.HGLOBAL((void*)hDrop));
            }
            else
            {
                PInvoke.GlobalFree(new global::Windows.Win32.Foundation.HGLOBAL((void*)hDrop));
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastPInvokeError(), "GlobalLock failed for CF_HDROP.");
            }

            // 2. Preparar CF_UNICODETEXT (respaldo para pegar en campos de texto, emulando comportamiento del legacy)
            string combinedPaths = string.Join(Environment.NewLine, filePaths);
            var textBytes = Encoding.Unicode.GetBytes(combinedPaths + '\0');
            nint hText = PInvoke.GlobalAlloc(GLOBAL_ALLOC_FLAGS.GMEM_MOVEABLE | GLOBAL_ALLOC_FLAGS.GMEM_ZEROINIT, (nuint)textBytes.Length);
            if (hText == 0) throw new System.ComponentModel.Win32Exception(Marshal.GetLastPInvokeError(), "GlobalAlloc failed for CF_UNICODETEXT.");

            nint textDest = (nint)PInvoke.GlobalLock(new global::Windows.Win32.Foundation.HGLOBAL((void*)hText));
            if (textDest != 0)
            {
                Marshal.Copy(textBytes, 0, textDest, textBytes.Length);
                PInvoke.GlobalUnlock(new global::Windows.Win32.Foundation.HGLOBAL((void*)hText));
            }
            else
            {
                PInvoke.GlobalFree(new global::Windows.Win32.Foundation.HGLOBAL((void*)hText));
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastPInvokeError(), "GlobalLock failed for CF_UNICODETEXT.");
            }

            // 3. Asignar al portapapeles
            if (PInvoke.OpenClipboard(default))
            {
                PInvoke.EmptyClipboard();
                PInvoke.SetClipboardData(15, new global::Windows.Win32.Foundation.HANDLE(hDrop)); // 15 = CF_HDROP
                PInvoke.SetClipboardData(13, new global::Windows.Win32.Foundation.HANDLE(hText)); // 13 = CF_UNICODETEXT
                PInvoke.CloseClipboard();
            }
            else
            {
                PInvoke.GlobalFree(new global::Windows.Win32.Foundation.HGLOBAL((void*)hDrop));
                PInvoke.GlobalFree(new global::Windows.Win32.Foundation.HGLOBAL((void*)hText));
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastPInvokeError(), "OpenClipboard failed.");
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to set file drop list to clipboard.");
            throw;
        }

        return Task.CompletedTask;
    }
}

