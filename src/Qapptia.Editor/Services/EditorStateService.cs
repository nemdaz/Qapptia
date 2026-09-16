using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Qapptia.Editor.Models;
using Serilog;

namespace Qapptia.Editor.Services;

/// <summary>
/// Servicio de persistencia y serialización del estado de sesión y preferencias del editor en formato JSON.
/// </summary>
public sealed class EditorStateService : IEditorStateService
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = null,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly string _basePath;
    private readonly string _stateFileName;
    private readonly ILogger? _logger;
    private readonly object _gate = new();

    // Fuente única de verdad en memoria: evita relecturas síncronas de disco en el hilo de UI
    private EditorState? _cachedState;

    // Escritor diferido coalescido: el último snapshot solicitado siempre prevalece en disco
    private readonly object _asyncSaveGate = new();
    private string? _pendingJson;
    private bool _asyncSaveActive;

    public EditorStateService(string basePath, string stateFileName, ILogger? logger = null)
    {
        _basePath = basePath;
        _stateFileName = stateFileName;
        _logger = logger ?? Serilog.Log.Logger;
    }

    private string GetStatePath()
    {
        if (!Directory.Exists(_basePath))
        {
            Directory.CreateDirectory(_basePath);
        }
        return Path.Combine(_basePath, _stateFileName);
    }

    public EditorState Load()
    {
        lock (_gate)
        {
            if (_cachedState != null)
            {
                return _cachedState;
            }

            string path = GetStatePath();
            if (!File.Exists(path))
            {
                _cachedState = new EditorState();
                return _cachedState;
            }

            try
            {
                string json = File.ReadAllText(path);
                _cachedState = JsonSerializer.Deserialize<EditorState>(json, s_jsonOptions) ?? new EditorState();
                return _cachedState;
            }
            catch (Exception ex)
            {
                _logger?.Warning(ex, "Error al leer {Path}. Devolviendo estado por defecto.", path);
                _cachedState = new EditorState();
                return _cachedState;
            }
        }
    }

    public void Save(EditorState state)
    {
        lock (_gate)
        {
            _cachedState = state;
            string path = GetStatePath();
            try
            {
                state.Layout.ExpandedFolders.Sort();
                string json = JsonSerializer.Serialize(state, s_jsonOptions);
                File.WriteAllText(path, json);
            }
            catch (Exception ex)
            {
                _logger?.Error(ex, "Error al guardar en {Path}.", path);
            }
        }
    }

    public void SaveDeferred(EditorState state)
    {
        string json;
        lock (_gate)
        {
            _cachedState = state;
            // Snapshot consistente congelado en el hilo llamante; solo la escritura es asíncrona
            state.Layout.ExpandedFolders.Sort();
            json = JsonSerializer.Serialize(state, s_jsonOptions);
        }

        lock (_asyncSaveGate)
        {
            _pendingJson = json;
            if (_asyncSaveActive)
            {
                return; // El escritor en curso recoge el snapshot más reciente
            }
            _asyncSaveActive = true;
        }

        _ = Task.Run(async () =>
        {
            while (true)
            {
                string? payload;
                lock (_asyncSaveGate)
                {
                    payload = _pendingJson;
                    _pendingJson = null;
                    if (payload == null)
                    {
                        _asyncSaveActive = false;
                        return;
                    }
                }

                try
                {
                    await File.WriteAllTextAsync(GetStatePath(), payload).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger?.Error(ex, "Error al guardar el estado en segundo plano.");
                }
            }
        });
    }
}
