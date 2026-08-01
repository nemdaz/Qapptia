namespace Qapptia.Core.Configuration;

/// <summary>
/// Servicio de configuracion central. Lee/escribe config.json junto al exe.
/// El archivo es un objeto flat (compatible con el Python legacy).
/// </summary>
public interface IConfigService
{
    /// <summary>
    /// Configuracion actual en memoria.
    /// </summary>
    QapptiaConfig Current { get; }

    /// <summary>
    /// Persiste los cambios a config.json.
    /// </summary>
    void Save();

    /// <summary>
    /// Recarga config.json desde disco (descarta cambios en memoria).
    /// </summary>
    void Reload();
}
