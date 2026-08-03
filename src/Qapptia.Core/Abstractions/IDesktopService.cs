namespace Qapptia.Core.Abstractions;

/// <summary>
/// Origen y tamaño del monitor en virtual-screen coords.
/// </summary>
public record struct MonitorInfo(
    int X,
    int Y,
    int Width,
    int Height,
    bool IsPrimary);

/// <summary>
/// Servicios misceláneos dependientes del escritorio: notificaciones nativas,
/// DPI scaling, info de monitores.
/// </summary>
public interface IDesktopService
{
    void ShowInfo(string title, string message);
    void ShowError(string title, string message);
    MonitorInfo GetMonitorAtCursor();
    (int X, int Y) GetCursorPosition();
    (int X, int Y) GetVirtualScreenOrigin();
    int GetVirtualScreenWidth();
    int GetVirtualScreenHeight();
    double GetDpiScalingAtCursor();
}
