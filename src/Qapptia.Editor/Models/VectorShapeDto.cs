using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Qapptia.Editor.Models;

public class VectorShapeDto
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("coords")]
    public List<double> Coords { get; set; } = new();

    [JsonPropertyName("color")]
    public string Color { get; set; } = string.Empty;

    [JsonPropertyName("payload")]
    public Dictionary<string, object>? Payload { get; set; }
}
