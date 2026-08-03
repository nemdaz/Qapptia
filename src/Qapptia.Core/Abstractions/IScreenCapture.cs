using System.Threading;
using System.Threading.Tasks;

namespace Qapptia.Core.Abstractions;

/// <summary>
/// Representa una captura de pantalla completa: imagen + dims + origen en virtual screen.
/// </summary>
public sealed record ScreenCaptureResult(
    byte[] BgraPixels,
    int Width,
    int Height,
    int OriginX,
    int OriginY);

/// <summary>
/// Captura la pantalla completa (virtual screen, multi-monitor) del OS anfitrión.
/// En Windows: <c>Graphics.CopyFromScreen</c> sobre el virtual screen rect.
/// </summary>
public interface IScreenCapture
{
    Task<ScreenCaptureResult> CaptureAllScreensAsync(CancellationToken ct = default);
}
