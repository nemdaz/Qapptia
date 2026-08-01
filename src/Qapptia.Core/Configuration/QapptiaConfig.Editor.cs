using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Qapptia.Core.Configuration;

/// <summary>
/// Seccion: preferencias persistentes del editor.
/// </summary>
public sealed partial class QapptiaConfig
{
    [JsonPropertyName("sidebar_width")]
    [Range(150, 800)]
    public int SidebarWidth { get; set; } = 250;

    [JsonPropertyName("active_favorite_color")]
    public string ActiveFavoriteColor { get; set; } = "green";

    [JsonPropertyName("last_selected_file")]
    public string? LastSelectedFile { get; set; }

    /// <summary>
    /// Mapa herramienta (line, arrow, rect, highlighter, text) -> nombre de color favorito.
    /// </summary>
    [JsonPropertyName("tool_favorite_colors")]
    public Dictionary<string, string> ToolFavoriteColors { get; set; } = new();

    /// <summary>
    /// Carpetas expandidas en el sidebar (rutas absolutas o relativas a SavePath).
    /// </summary>
    [JsonPropertyName("expanded_folders")]
    public List<string> ExpandedFolders { get; set; } = new();
}
