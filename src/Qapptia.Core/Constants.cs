using System;
using System.IO;

namespace Qapptia.Core;

public static class Constants
{
    public const string AppName = "Qapptia";
    public const string ConfigFileName = "config.json";
    public const string EditorStateFileName = "editor_state.json";
    public const string ShortcutCopyClipboard = "Ctrl+C";
    public const string ShortcutCopyFile = "Ctrl+F";
    public const string DrawingExtension = ".dibujo";
    public static readonly string[] SupportedImageExtensions = { ".png", ".jpg", ".jpeg" };
    public const string MetadataTagStart = "<QapptiaID>";
    public const string MetadataTagEnd = "</QapptiaID>";

    // Nombres de ejecutables de la suite
    public const string CaptureExecutableName = "Qapptia.App.Capture.exe";
    public const string EditorExecutableName = "Qapptia.App.Editor.exe";
    public const string ConfigExecutableName = "Qapptia.App.Config.exe";

    // Argumentos de línea de comandos
    public const string ArgEditor = "--editor";
    public const string ArgConfig = "--config";
    public const string ArgCapture = "--capture";

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
