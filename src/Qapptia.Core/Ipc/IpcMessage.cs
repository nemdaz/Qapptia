using System.Text.Json.Serialization;

namespace Qapptia.Core.Ipc;

/// <summary>
/// Tipos de mensaje del protocolo IPC.
/// Sirve como discriminador para el polimorfismo de <see cref="IpcMessage"/>.
/// </summary>
[JsonConverter(typeof(IpcMessageJsonConverterFactory))]
public abstract class IpcMessage
{
    public abstract IpcMessageType Type { get; }
}

/// <summary>
/// Solicita a la instancia existsente que despierte/muestre su ventana.
/// Reemplaza la señal WAKE_UP del Python.
/// </summary>
public sealed class WakeUpRequest : IpcMessage
{
    public override IpcMessageType Type => IpcMessageType.WakeUp;
}

/// <summary>
/// Solicita cierre limpio de la instancia.
/// Reemplaza la señal QUIT del Python.
/// </summary>
public sealed class QuitRequest : IpcMessage
{
    public override IpcMessageType Type => IpcMessageType.Quit;
}

/// <summary>
/// Solicita refrescar el tray icon (p.ej. tras cambio de config).
/// Reemplaza la señal REFRESH_TRAY_ICON del Python.
/// </summary>
public sealed class RefreshTrayIconRequest : IpcMessage
{
    public override IpcMessageType Type => IpcMessageType.RefreshTrayIcon;
}

/// <summary>
/// Heartbeat para detectar que la instancia está viva. El servidor responde Pong.
/// </summary>
public sealed class Ping : IpcMessage
{
    public override IpcMessageType Type => IpcMessageType.Ping;
}

/// <summary>
/// Respuesta de confirmación exitosa ante un request. Echo del tipo request.
/// </summary>
public sealed class Ack : IpcMessage
{
    public override IpcMessageType Type => IpcMessageType.Ack;
    public IpcMessageType OriginalType { get; init; }
}

/// <summary>
/// Respuesta de error ante un request inválido o fallo de procesamiento.
/// </summary>
public sealed class ErrorResponse : IpcMessage
{
    public override IpcMessageType Type => IpcMessageType.Error;
    public string Reason { get; init; } = string.Empty;
}

/// <summary>
/// Respuesta a Ping. Incluye versión del protocolo y PID del servidor.
/// </summary>
public sealed class Pong : IpcMessage
{
    public override IpcMessageType Type => IpcMessageType.Pong;
    public int ProtocolVersion { get; init; } = IpcProtocol.Version;
    public int ServerPid { get; init; }
}

/// <summary>
/// Notifica un cambio de tema en tiempo real.
/// </summary>
public sealed class ThemeChangedNotification : IpcMessage
{
    public override IpcMessageType Type => IpcMessageType.ThemeChanged;
    public string Theme { get; init; } = global::Qapptia.Core.Theme.ThemeConstants.System;
}

/// <summary>
/// Enumera los tipos de mensaje del protocolo IPC de Qapptia.
/// </summary>
public enum IpcMessageType
{
    WakeUp = 1,
    Quit = 2,
    RefreshTrayIcon = 3,
    Ping = 4,
    Ack = 5,
    Error = 6,
    Pong = 7,
    ThemeChanged = 8,
}
