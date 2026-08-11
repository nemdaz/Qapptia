using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Qapptia.Editor.Models;

public sealed class EditorState
{
    [JsonPropertyName("sidebar_width")]
    public int SidebarWidth { get; set; } = 250;

    [JsonPropertyName("active_fav_color")]
    public string ActiveFavoriteColor { get; set; } = "#00FF00";

    [JsonPropertyName("last_selected_file")]
    public string? LastSelectedFile { get; set; }

    [JsonPropertyName("tool_fav_colors")]
    public SortedDictionary<string, string> ToolFavoriteColors { get; set; } = new();

    [JsonPropertyName("expanded_folders")]
    public List<string> ExpandedFolders { get; set; } = new();
}
