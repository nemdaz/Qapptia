using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Serilog;


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
        PropertyNamingPolicy = null,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly string _path;
    private readonly ILogger _logger;
    private readonly object _gate = new();
    private QapptiaConfig _current;

    public JsonConfigService(string path, ILogger? logger = null)
    {
        _path = path;
        _logger = logger ?? Serilog.Log.Logger;
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
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

                var json = JsonSerializer.Serialize(_current, s_jsonOptions);
                File.WriteAllText(_path, json);
                _logger.Information("Config guardado en {Path}", _path);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error guardando config en {Path}", _path);
                throw;
            }
        }
    }

    public void Reload()
    {
        lock (_gate)
        {
            _current = LoadOrNew(_path);
            _logger.Information("Config recargado desde {Path}", _path);
        }
    }

    private QapptiaConfig LoadOrNew(string path)
    {
        if (!File.Exists(path))
        {
            _logger.Information("config.json no existe en {Path}; usando defaults", path);
            return new QapptiaConfig();
        }

        try
        {
            var json = File.ReadAllText(path);
            var config = JsonSerializer.Deserialize<QapptiaConfig>(json, s_jsonOptions);
            if (config is null)
            {
                _logger.Warning("config.json vacio o invalido en {Path}; usando defaults", path);
                return new QapptiaConfig();
            }
            _logger.Information("Config cargado desde {Path}", path);
            return config;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error leyendo config.json desde {Path}; usando defaults", path);
            return new QapptiaConfig();
        }
    }
}

