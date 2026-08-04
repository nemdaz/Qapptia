using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Qapptia.Core.Configuration;

/// <summary>
/// Seccion: captura de pantalla y atajos.
/// </summary>
public sealed partial class QapptiaConfig
{
    // === Guardado ===

    [JsonPropertyName("save_path")]
    [Required]
    public string SavePath { get; set; } = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyPictures), "Qapptia");

    [JsonPropertyName("filename_format")]
    [Required]
    public string FilenameFormat { get; set; } = "Qapptia_YYYYMMDD_HHmmSS";

    [JsonPropertyName("subfolder_month")]
    public bool SubfolderMonth { get; set; } = true;

    [JsonPropertyName("subfolder_day")]
    public bool SubfolderDay { get; set; } = true;

    [JsonPropertyName("subfolder_hour")]
    public bool SubfolderHour { get; set; } = false;

    // === Cursor ===

    [JsonPropertyName("show_mouse")]
    public bool ShowMouse { get; set; } = true;

    [JsonPropertyName("highlight_mouse")]
    public bool HighlightMouse { get; set; } = false;

    // === Timer ===

    [JsonPropertyName("manual_timer")]
    [Range(0, 999)]
    public int ManualTimer { get; set; } = 0;

    // === Atajos (formato keyboard: ctrl+shift+q) ===

    [JsonPropertyName("shortcut_screen")]
    public string ShortcutScreen { get; set; } = "ctrl+shift+q";

    [JsonPropertyName("shortcut_area")]
    public string ShortcutArea { get; set; } = "ctrl+shift+a";

    [JsonPropertyName("shortcut_flow")]
    public string ShortcutFlow { get; set; } = "ctrl+shift+f";

    [JsonPropertyName("shortcut_flow_pause")]
    public string ShortcutFlowPause { get; set; } = "ctrl+shift";

    // === Copiado al portapapeles ===

    [JsonPropertyName("copy_to_clipboard_screen")]
    public bool CopyToClipboardScreen { get; set; } = false;

    [JsonPropertyName("copy_to_clipboard_area")]
    public bool CopyToClipboardArea { get; set; } = false;

    // === Scroll capture (omitido en esta migracion, conservado por compat) ===

    [JsonPropertyName("enable_scroll_capture")]
    public bool EnableScrollCapture { get; set; } = false;
}
