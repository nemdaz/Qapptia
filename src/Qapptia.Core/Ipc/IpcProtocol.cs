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
    public override bool CanConvert(Type typeToConvert) => typeToConvert == typeof(IpcMessage);

    public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        return new IpcMessageJsonConverter();
    }

    private sealed class IpcMessageJsonConverter : JsonConverter<IpcMessage>
    {
        private static readonly Dictionary<string, IpcMessageType> s_nameMap = InitializeNameMap();
        private static readonly Qapptia.Core.Serialization.QapptiaJsonContext s_context = Qapptia.Core.Serialization.QapptiaJsonContext.Default;

        private static Dictionary<string, IpcMessageType> InitializeNameMap()
        {
            var map = new Dictionary<string, IpcMessageType>(StringComparer.OrdinalIgnoreCase);
            var snakeCase = JsonNamingPolicy.SnakeCaseLower;
            foreach (IpcMessageType type in Enum.GetValues<IpcMessageType>())
            {
                var name = type.ToString();
                map[name] = type;
                map[snakeCase.ConvertName(name)] = type;
            }
            return map;
        }

        public override IpcMessage? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
                throw new JsonException("Se esperaba StartObject para IpcMessage");

            using var doc = JsonDocument.ParseValue(ref reader);
            if (!doc.RootElement.TryGetProperty("type", out var typeEl) || typeEl.ValueKind != JsonValueKind.String)
                throw new JsonException("IpcMessage requiere propiedad 'type' string");

            var typeName = typeEl.GetString()!;
            if (!s_nameMap.TryGetValue(typeName, out var typeEnum))
                throw new JsonException($"Tipo de IpcMessage desconocido: {typeName}");

            return typeEnum switch
            {
                IpcMessageType.WakeUp => doc.RootElement.Deserialize(s_context.WakeUpRequest),
                IpcMessageType.Quit => doc.RootElement.Deserialize(s_context.QuitRequest),
                IpcMessageType.RefreshTrayIcon => doc.RootElement.Deserialize(s_context.RefreshTrayIconRequest),
                IpcMessageType.Ping => doc.RootElement.Deserialize(s_context.Ping),
                IpcMessageType.Ack => doc.RootElement.Deserialize(s_context.Ack),
                IpcMessageType.Error => doc.RootElement.Deserialize(s_context.ErrorResponse),
                IpcMessageType.Pong => doc.RootElement.Deserialize(s_context.Pong),
                IpcMessageType.ThemeChanged => doc.RootElement.Deserialize(s_context.ThemeChangedNotification),
                _ => throw new JsonException($"No hay tipo concreto para IpcMessageType {typeEnum}")
            };
        }

        public override void Write(Utf8JsonWriter writer, IpcMessage value, JsonSerializerOptions options)
        {
            switch (value)
            {
                case WakeUpRequest req:
                    JsonSerializer.Serialize(writer, req, s_context.WakeUpRequest);
                    break;
                case QuitRequest req:
                    JsonSerializer.Serialize(writer, req, s_context.QuitRequest);
                    break;
                case RefreshTrayIconRequest req:
                    JsonSerializer.Serialize(writer, req, s_context.RefreshTrayIconRequest);
                    break;
                case Ping req:
                    JsonSerializer.Serialize(writer, req, s_context.Ping);
                    break;
                case Ack req:
                    JsonSerializer.Serialize(writer, req, s_context.Ack);
                    break;
                case ErrorResponse req:
                    JsonSerializer.Serialize(writer, req, s_context.ErrorResponse);
                    break;
                case Pong req:
                    JsonSerializer.Serialize(writer, req, s_context.Pong);
                    break;
                case ThemeChangedNotification req:
                    JsonSerializer.Serialize(writer, req, s_context.ThemeChangedNotification);
                    break;
                default:
                    throw new JsonException($"Tipo de IpcMessage no soportado: {value?.GetType().FullName}");
            }
        }
    }
}
