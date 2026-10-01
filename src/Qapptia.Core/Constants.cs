using System;
using System.IO;
using SkiaSharp;

namespace Qapptia.Core;

public static class Constants
{
    public const string AppName = "Qapptia";
    public const string AppVersion = "2.0.0-beta";
    public const string AppDescription = "Herramienta de captura de pantalla, recortes y anotaciones. Mantiene un enfoque en la agilidad para documentar evidencias en el proceso de Pruebas de Software (QA) y practicidad para propósitos generales.";
    public const string AboutSupportHeader = "Apoyar el proyecto";
    public const string AboutSupportDescription = "Este es un proyecto libre e independiente. Tu apoyo impulsa la creación de nuevas características y su desarrollo continuo.";
    public const string AboutUpdatesHeader = "Actualizaciones de software";
    public const string ConfigFileName = "config.json";
    public const string EditorStateFileName = "editor_state.json";
    public const string ShortcutCopyClipboard = "Ctrl+C";
    public const string ShortcutCopyFile = "Ctrl+F";
    public const string DrawingExtension = ".dibujo";
    public static readonly string[] SupportedImageExtensions = { ".png", ".jpg", ".jpeg" };

    // Propiedades de serialización JSON en archivos de estado (.dibujo)
    public const string MetadataPropertyMediaId = "Qapptia.mediaId";
    public const string MetadataPropertyMediaType = "Qapptia.mediaType";

    // Constantes de namespaces y cabeceras XMP (ISO 16684-1)
    public const string XmpNamespaceMediaManagement = "http://ns.adobe.com/xap/1.0/mm/";
    public const string XmpNamespaceDublinCore = "http://purl.org/dc/elements/1.1/";
    public const string XmpNamespaceAdobeBasic = "http://ns.adobe.com/xap/1.0/";
    public const string PngChunkXmpKeyword = "XML:com.adobe.xmp";
    public const string JpegXmpHeader = "http://ns.adobe.com/xap/1.0/\0";

    // Constantes de persistencia y buffers de lectura rápida
    public const string JsonFileExtension = ".json";
    public const string JsonSearchPattern = "*.json";
    public const int JsonHeaderBufferSize = 512;

    // Tipos MIME estándar (IANA / HTTP Content-Type)
    public const string MediaTypePng = "image/png";
    public const string MediaTypeJpeg = "image/jpeg";
    public const string DefaultMediaType = MediaTypePng;

    // Formatos de fecha para subcarpetas de almacenamiento de capturas
    public const string SubfolderMonthFormat = "yyyy-MM";
    public const string SubfolderDayFormat = "yyyy-MM-dd";
    public const string SubfolderHourFormat = "HH'h'";

    // Formato por defecto para nombres de archivo de captura
    public const string DefaultFilenameFormat = "Qapptia_YYYYMMDD_HHmmSS";

    // Color del halo de resaltado del cursor (amarillo vibrante perceptible a primera vista)
    public static readonly SKColor CursorHaloColor = new(255, 235, 59, 160);

    public static string ResolveMediaType(string filePath)
    {
        string ext = Path.GetExtension(filePath).ToLowerInvariant();
        return ext switch
        {
            ".png" => MediaTypePng,
            ".jpg" or ".jpeg" => MediaTypeJpeg,
            _ => MediaTypePng
        };
    }

    // Nombres de aplicaciones de la suite
    public const string LauncherAppName = "Qapptia";
    public const string CaptureAppName = "Qapptia Capture";
    public const string EditorAppName = "Qapptia Editor";
    public const string ConfigAppName = "Qapptia Config";

    // Nombres de ejecutables de la suite
    public const string LauncherExecutableName = "Qapptia.exe";
    public const string CaptureExecutableName = "Qapptia.App.Capture.exe";
    public const string EditorExecutableName = "Qapptia.App.Editor.exe";
    public const string ConfigExecutableName = "Qapptia.App.Config.exe";

    // Argumentos de línea de comandos
    public const string ArgEditor = "--editor";
    public const string ArgConfig = "--config";
    public const string ArgCapture = "--capture";
    public const string ArgRestart = "--restart";
    public const string ArgAbout = "--about";

    // URLs de servicios web, descargas y apoyo al proyecto
    public const string DefaultSponsorUrl = "https://localhost:8080/sponsor";
    public const string DefaultDownloadUrl = "https://localhost:8080/download";
    public const string DefaultVersionApiUrl = "https://localhost:8080/api/version";
    public const int DefaultHttpTimeoutMs = 5000;
    public const int StartupUpdateCheckDelayMs = 3000;
    public const int UpdateCheckCooldownMs = 2000;
    public static readonly TimeSpan PeriodicUpdateCheckInterval = TimeSpan.FromHours(24);

    // Textos de apoyo y contribución compartidos (numerados para estabilidad ante cambios de textos)
    public const string SponsorText1 = "Invítame un café";
    public const string SponsorText2 = "Apoyar proyecto";
    public const string SponsorText3 = "Contribuir";
    public const string SponsorText4 = "Impulsar desarrollo";
    public const string SponsorDefaultActionTitle = "Apoyar";

    // Claves de iconos vectoriales de apoyo (numeradas para estabilidad)
    public const string SponsorIcon1 = "IconSponsorCoffeeHeart";
    public const string SponsorIcon2 = "IconSponsorRamenHeart";
    public const string SponsorIcon3 = "IconSponsorCoffeeCup";
    public const string SponsorIcon4 = "IconSponsorHeartBadge";

    // Identificadores del set de estilos de apoyo (definidos en MaterialTheme.axaml)
    public const string SponsorStyleA = "SponsorStyleA";
    public const string SponsorStyleB = "SponsorStyleB";
    public const string SponsorStyleC = "SponsorStyleC";
    public const string SponsorStyleD = "SponsorStyleD";

    // Notificaciones del sistema
    public const int NotificationDurationMs = 5000;
    public const string NotificationTitleCapture = CaptureAppName;
    public const string NotificationTitleEditor = EditorAppName;
    public const string NotificationTitleConfig = ConfigAppName;
    public const string NotificationMessageCaptureStarted = "El capturador está activo en segundo plano.";
    public const string NotificationMessageCaptureRestarted = "El capturador se ha reiniciado correctamente.";
    public const string NotificationTitleUpdateAvailable = "Actualización disponible";
    public const string NotificationMessageUpdateAvailableFormat = "Qapptia v{0} está disponible para descargar.";

    // Nombres de recursos y carpetas de assets
    public const string AssetsDirectoryName = "Assets";
    public const string TrayIconFileName = "tray_icon.ico";
    public const string AppIconFileName = "app_icon.ico";

#if DEBUG
    public static string DefaultConfigPath => Path.Combine(AppContext.BaseDirectory, ConfigFileName);

    public static string DefaultLogDirectory => AppContext.BaseDirectory;
#else
    public static string DefaultConfigPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        AppName, 
        ConfigFileName);

    public static string DefaultLogDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        AppName);
#endif

    public static string DefaultSavePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
        AppName);
}
