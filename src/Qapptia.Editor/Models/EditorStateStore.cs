using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Serilog;

namespace Qapptia.Editor.Models;

public sealed class EditorStateStore
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

    public EditorStateStore(string basePath, string stateFileName, ILogger? logger = null)
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
            string path = GetStatePath();
            if (!File.Exists(path))
            {
                return new EditorState();
            }

            try
            {
                string json = File.ReadAllText(path);
                var state = JsonSerializer.Deserialize<EditorState>(json, s_jsonOptions);
                return state ?? new EditorState();
            }
            catch (Exception ex)
            {
                _logger?.Warning(ex, "Error reading {Path}. Returning default state.", path);
                return new EditorState();
            }
        }
    }

    public void Save(EditorState state)
    {
        lock (_gate)
        {
            string path = GetStatePath();
            try
            {
                state.Layout.ExpandedFolders.Sort();
                string json = JsonSerializer.Serialize(state, s_jsonOptions);
                File.WriteAllText(path, json);
            }
            catch (Exception ex)
            {
                _logger?.Error(ex, "Error saving to {Path}.", path);
            }
        }
    }
}
