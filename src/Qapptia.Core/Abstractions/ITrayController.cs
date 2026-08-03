using System.Threading;
using System.Threading.Tasks;

namespace Qapptia.Core.Abstractions;

/// <summary>
/// Item visible del tray menu. Dispose lo elimina del menú.
/// </summary>
public interface ITrayMenuItem : IDisposable
{
    string Label { get; }
    bool IsVisible { get; set; }
    bool IsEnabled { get; set; }
    System.Action? OnClick { get; set; }
}

/// <summary>
/// Controla el tray icon, su menú contextual y notificaciones.
/// En Windows: <c>H.NotifyIcon.TaskbarIcon</c>; Linux: appindicator; macOS: status item.
/// </summary>
public interface ITrayController : IDisposable
{
    void SetIcon(byte[] pngBytes, string tooltip);
    ITrayMenuItem AddMenuItem(string label, System.Action? onClick = null, bool visible = true);
    void AddMenuSeparator();
    void ShowContextMenu();
    Task ShowNotificationAsync(string title, string body, CancellationToken ct = default);
    void RefreshIcon();
}
