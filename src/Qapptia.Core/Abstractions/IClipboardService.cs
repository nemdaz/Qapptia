using System.Threading;
using System.Threading.Tasks;

namespace Qapptia.Core.Abstractions;

/// <summary>
/// Formatos soportados por el portapapeles del OS.
/// </summary>
public enum ClipboardFormat
{
    Text,
    Image,
    FileDrop,
}

/// <summary>
/// Servicio de portapapeles del OS para copiar screenshots/paths.
/// En Windows: <c>Windows.Graphics.Capture</c>; multiplataforma via Avalonia.
/// </summary>
public interface IClipboardService
{
    Task SetTextAsync(string text, CancellationToken ct = default);
    Task SetImageAsync(byte[] pngBytes, CancellationToken ct = default);
    Task SetRawImageAsync(byte[] bgraPixels, int width, int height, byte[]? pngBytes = null, CancellationToken ct = default)
        => pngBytes != null && pngBytes.Length > 0 ? SetImageAsync(pngBytes, ct) : Task.CompletedTask;
    Task SetFileDropListAsync(string[] filePaths, CancellationToken ct = default);
}
