using System.Text.Json.Serialization;

namespace Qapptia.Core.Configuration;

/// <summary>
/// Configuracion raiz de Qapptia. Estructura flat compatible con config.json
/// del Python legacy. Se organiza internamente con partial class por categorias.
/// </span>
/// </summary>
public sealed partial class QapptiaConfig
{
    /// <summary>
    /// Version del esquema (para migraciones futuras). Se escribira solo si
    /// difiere del default.
    /// </summary>
    [JsonPropertyName("config_version")]
    public int ConfigVersion { get; set; } = 1;
}
