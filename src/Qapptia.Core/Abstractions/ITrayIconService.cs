namespace Qapptia.Core.Abstractions;

public enum TrayNotificationType
{
    Info,
    Warning,
    Error
}

public interface ITrayIconService : IDisposable
{
    /// <summary>
    /// Inicializa y muestra el icono en la bandeja del sistema con el menú especificado.
    /// </summary>
    void Initialize(TrayMenuDefinition menu, string iconPath);

    /// <summary>
    /// Muestra una notificación nativa del sistema en la bandeja de entrada o centro de notificaciones del SO.
    /// </summary>
    void ShowNotification(string title, string message, TrayNotificationType type = TrayNotificationType.Info, int timeoutMs = Constants.NotificationDurationMs);
}
