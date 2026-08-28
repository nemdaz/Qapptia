using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Qapptia.Core.Ipc;

/// <summary>
/// Estado de un canal IPC escrito a disco por el servidor para que clientes
/// encuentren la instancia y se autentiquen. Reemplaza al state file del Python
/// ( puerto+token )" — aquí solo guardamos pid+token porque el pipe está nominalcido.
/// Path: <c>{temp}/qapptia_ipc/{channel}.json</c>.
/// </summary>
public sealed class IpcChannelState
{
    public int Pid { get; init; }
    public string Token { get; init; } = string.Empty;
    public string PipeName { get; init; } = string.Empty;

    private static readonly JsonSerializerOptions s_jsonOpts = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static string GetStateDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "qapptia_ipc");
        Directory.CreateDirectory(dir);
        return dir;
    }

    public static string GetStateFilePath(string channel)
    {
        return Path.Combine(GetStateDir(), $"{channel}.json");
    }

    public static IpcChannelState? Load(string channel)
    {
        var path = GetStateFilePath(channel);
        try
        {
            if (!File.Exists(path)) return null;

            using var stream = File.OpenRead(path);
            return JsonSerializer.Deserialize<IpcChannelState>(stream, s_jsonOpts);
        }
        catch (IOException) { return null; }
        catch (JsonException) { Delete(channel); return null; }
    }

    public static void Save(string channel, IpcChannelState state)
    {
        var path = GetStateFilePath(channel);
        var tmpPath = path + ".tmp";
        using (var stream = File.Create(tmpPath))
        {
            JsonSerializer.Serialize(stream, state, s_jsonOpts);
        }
        File.Move(tmpPath, path, overwrite: true);
    }

    public static void Delete(string channel, string? expectedToken = null)
    {
        var path = GetStateFilePath(channel);
        if (expectedToken is not null)
        {
            var state = Load(channel);
            if (state is null || state.Token != expectedToken) return;
        }

        try
        { if (File.Exists(path)) File.Delete(path); }
        catch (IOException) { /* best effort */ }
    }
}
