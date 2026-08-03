namespace Qapptia.Core.Abstractions;

/// <summary>
/// Modos de energía relevantes: suspender/resumir y cambio de fuente (AC/battery).
/// </summary>
public enum PowerMode
{
    Suspend,
    Resume,
    StatusChange,
}

/// <summary>
/// Notifica eventos de energía del OS para re-registrar hotkeys tras suspender.
/// En Windows: <c>Microsoft.Win32.SystemEvents.PowerModeChanged</c>.
/// En Linux: DBus logind. En macOS: NSWorkspace.
/// Implementa <see cref="IDisposable"/> para liberar la suscripción al detener la app.
/// </summary>
public interface IPowerEvents : IDisposable
{
    event EventHandler<PowerMode> PowerModeChanged;
    bool RequiresHotkeyReRegistrationAfterResume { get; }
}
