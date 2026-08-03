using System.Threading;
using System.Threading.Tasks;

namespace Qapptia.Core.Abstractions;

/// <summary>
/// Cursor visible con su hotspot Position relativo al virtual screen.
/// </summary>
public sealed record CursorImage(
    byte[] BgraPixels,
    int Width,
    int Height,
    int HotspotX,
    int HotspotY);

/// <summary>
/// Captura el cursor del mousetal como se está viendo en pantalla,
/// incluyendo resolución DPI-correcta y un-premultiplied alpha.
/// </summary>
public interface ICursorCapture
{
    Task<CursorImage?> CaptureCursorAsync(CancellationToken ct = default);
}
