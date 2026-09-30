namespace Qapptia.App.Config.Common;

/// <summary>
/// Constantes de texto e interfaz específicas de la aplicación de configuración.
/// </summary>
public static class Constants
{
    // Mensajes de estado de comprobación de actualizaciones
    public const string UpdateStatusPrompt = "Comprueba si existe una versión más reciente de Qapptia.";
    public const string UpdateStatusChecking = "Comprobando actualizaciones con el servidor...";
    public const string UpdateStatusAvailableFormat = "¡Nueva versión v{0} disponible!{1}";
    public const string UpdateStatusUpToDateFormat = "Tienes instalada la versión más reciente (v{0}).";
    public const string UpdateStatusConnectError = "No se pudo conectar con el servidor de actualizaciones.";
    public const string UpdateStatusServerError = "Ocurrió un error al contactar el servidor.";

    // Textos de botones de actualización
    public const string UpdateButtonCheck = "Comprobar actualización";
    public const string UpdateButtonDownload = "Descargar actualización";
    public const string UpdateButtonRetry = "Reintentar comprobación";

    // Mensajes de la barra de estado inferior (Footer)
    public const string FooterConfigSaved = "Configuración guardada exitosamente.";
    public const string FooterSaveErrorPrefix = "Error al guardar: ";

    // Diálogos del sistema
    public const string DialogSelectFolderTitle = "Selecciona carpeta de guardado";
}
