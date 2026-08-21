using System.Text.Json;
using System.Text.Json.Serialization;

namespace Qapptia.Core.Ipc;

/// <summary>
/// Constantes del protocolo wire: versión + longitud de cabecera.
/// Formato de frame: 4 bytes little-endian u32 (longitud payload) + payload UTF-8 JSON.
/// </summary>
public static class IpcProtocol
{
    public const int Version = 1;
    public const int MaxPayloadBytes = 64 * 1024;
    public const int LenFieldSize = 4;
}

/// <summary>
/// Channels (pipes) conocidos del modelo de 3 exes.
/// Cada exe escucha en su pipe de nombre fijo y envía a los otros dos pipes.
///
/// IMPORTANTE multi-OS: el nombre del pipe se pasa **sin prefijo** a NamedPipeServerStream.
/// En Windows el BCL agrega automáticamente `\.\pipe\`, en Unix usa sockets del filesystem.
/// Por eso NO se debe pre-fijar el nombre con `\.\pipe\` aquí.
/// </summary>
public static class IpcChannels
{
    public const string Capture = "qapptia.capture";
    public const string Editor = "qapptia.editor";
    public const string Config = "qapptia.config";

    /// <summary>
    /// Devuelve el nombre del pipe sin prefijo (multi-OS).
    /// NamedPipeServerStream/ClientStream acepta este nombre directo.
    /// </summary>
    public static string GetPipeName(string channel) => channel;
}

/// <summary>
/// Factoría de convertidores polimórficos para <see cref="IpcMessage"/> basada en el
/// discriminador <see cref="IpcMessage.Type"/>. Permite serializar/deserializar mensajes
/// de forma segura sin $type en el JSON (evita issues de seguridad de JsonSerializer polimórfico).
/// </summary>
public sealed class IpcMessageJsonConverterFactory : JsonConverterFactory
{
    private static readonly Dictionary<IpcMessageType, Type> _typeMap = new()
    {
        [IpcMessageType.WakeUp] = typeof(WakeUpRequest),
        [IpcMessageType.Quit] = typeof(QuitRequest),
        [IpcMessageType.RefreshTrayIcon] = typeof(RefreshTrayIconRequest),
        [IpcMessageType.Ping] = typeof(Ping),
        [IpcMessageType.Ack] = typeof(Ack),
        [IpcMessageType.Error] = typeof(ErrorResponse),
        [IpcMessageType.Pong] = typeof(Pong),
        [IpcMessageType.ThemeChanged] = typeof(ThemeChangedNotification),
    };

    public override bool CanConvert(Type typeToConvert) => typeToConvert == typeof(IpcMessage);

    public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        return new IpcMessageJsonConverter(_typeMap);
    }

    private sealed class IpcMessageJsonConverter : JsonConverter<IpcMessage>
    {
        private readonly Dictionary<IpcMessageType, Type> _typeMap;
        private readonly Dictionary<string, IpcMessageType> _nameMap;

        public IpcMessageJsonConverter(Dictionary<IpcMessageType, Type> typeMap)
        {
            _typeMap = typeMap;
            _nameMap = new(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in typeMap)
            {
                _nameMap[kv.Key.ToString()!] = kv.Key;
            }
        }

        public override IpcMessage? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
                throw new JsonException("Se esperaba StartObject para IpcMessage");

            using var doc = JsonDocument.ParseValue(ref reader);
            if (!doc.RootElement.TryGetProperty("type", out var typeEl) || typeEl.ValueKind != JsonValueKind.String)
                throw new JsonException("IpcMessage requiere propiedad 'type' string");

            var typeName = typeEl.GetString()!;
            if (!_nameMap.TryGetValue(typeName, out var typeEnum))
                throw new JsonException($"Tipo de IpcMessage desconocido: {typeName}");

            if (!_typeMap.TryGetValue(typeEnum, out var concrete))
                throw new JsonException($"No hay tipo concreto para IpcMessageType {typeEnum}");

            return (IpcMessage?)JsonSerializer.Deserialize(doc.RootElement, concrete, options);
        }

        public override void Write(Utf8JsonWriter writer, IpcMessage value, JsonSerializerOptions options)
        {
            JsonSerializer.Serialize(writer, value, value.GetType(), options);
        }
    }
}
