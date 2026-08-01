using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Qapptia.Core.Configuration;

/// <summary>
/// Implementacion basada en config.json junto al exe (portable).
/// Formato flat snake_case compatible con el Python legacy.
/// </summary>
public sealed class JsonConfigService : IConfigService
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = null
    };

    private readonly string _path;
    private readonly ILogger<JsonConfigService> _logger;
    private readonly object _gate = new();
    private QapptiaConfig _current;

    public JsonConfigService(string path, ILogger<JsonConfigService>? logger = null)
    {
        _path = path;
        _logger = logger ?? NullLogger<JsonConfigService>.Instance;
        _current = LoadOrNew(path);
    }

    public QapptiaConfig Current => _current;

    public void Save()
    {
        lock (_gate)
        {
            try
            {
                var dir = Path.GetDirectoryName(_path);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                var json = JsonSerializer.Serialize(_current, s_jsonOptions);
                File.WriteAllText(_path, json);
                _logger.LogInformation("Config guardado en {Path}", _path);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error guardando config en {Path}", _path);
                throw;
            }
        }
    }

    public void Reload()
    {
        lock (_gate)
        {
            _current = LoadOrNew(_path);
            _logger.LogInformation("Config recargado desde {Path}", _path);
        }
    }

    private QapptiaConfig LoadOrNew(string path)
    {
        if (!File.Exists(path))
        {
            _logger.LogInformation("config.json no existe en {Path}; usando defaults", path);
            return new QapptiaConfig();
        }

        try
        {
            var json = File.ReadAllText(path);
            var config = JsonSerializer.Deserialize<QapptiaConfig>(json, s_jsonOptions);
            if (config is null)
            {
                _logger.LogWarning("config.json vacio o invalido en {Path}; usando defaults", path);
                return new QapptiaConfig();
            }
            _logger.LogInformation("Config cargado desde {Path}", path);
            return config;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error leyendo config.json desde {Path}; usando defaults", path);
            return new QapptiaConfig();
        }
    }
}
