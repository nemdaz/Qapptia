using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Qapptia.Editor.Models;

/// <summary>
/// Modelo de estado persistible del lienzo (recorte, rotación y anotaciones vectoriales por imagen).
/// </summary>
public sealed class CanvasState
{
    /// <summary>
    /// Identificador único inmutable del medio asociado.
    /// </summary>
    [JsonPropertyName(Qapptia.Core.Constants.MetadataPropertyMediaId)]
    [JsonPropertyOrder(-2)]
    public string? MediaId { get; set; }

    /// <summary>
    /// Tipo MIME estándar (IANA / HTTP Content-Type) del medio asociado.
    /// </summary>
    [JsonPropertyName(Qapptia.Core.Constants.MetadataPropertyMediaType)]
    [JsonPropertyOrder(-1)]
    public string MediaType { get; set; } = Qapptia.Core.Constants.DefaultMediaType;

    /// <summary>
    /// Coordenadas del recorte acumulado [x, y, width, height] o null si no se ha recortado.
    /// </summary>
    [JsonPropertyName("crop")]
    public List<double>? Crop { get; set; }

    /// <summary>
    /// Ángulo acumulado de rotación en grados.
    /// </summary>
    [JsonPropertyName("rotation")]
    public int Rotation { get; set; } = 0;

    /// <summary>
    /// Lista de figuras vectoriales dibujadas sobre el lienzo.
    /// </summary>
    [JsonPropertyName("shapes")]
    public List<VectorShapeDto> Shapes { get; set; } = new();

    /// <summary>
    /// Determina si el lienzo tiene alguna modificación sobre la imagen base (para decidir si guardar o limpiar el archivo de persistencia).
    /// </summary>
    [JsonIgnore]
    public bool HasModifications => (Crop != null && Crop.Count >= 4) || (Rotation % 360 != 0) || (Shapes.Count > 0);
}
