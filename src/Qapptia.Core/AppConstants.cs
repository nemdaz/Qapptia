using System;
using System.IO;

namespace Qapptia.Core;

public static class AppConstants
{
    public const string AppName = "Qapptia";
    public const string ConfigFileName = "config.json";
    public const string EditorStateFileName = "editor_state.json";
    public const string ShortcutCopyClipboard = "Ctrl+C";
    public const string ShortcutCopyFile = "Ctrl+F";
    public const string DrawingExtension = ".dibujo";
    public const string MetadataTagStart = "<QapptiaID>";
    public const string MetadataTagEnd = "</QapptiaID>";

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
