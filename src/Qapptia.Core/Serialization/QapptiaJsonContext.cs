using System.Text.Json.Serialization;
using Qapptia.Core.Abstractions;
using Qapptia.Core.Configuration;
using Qapptia.Core.Ipc;

namespace Qapptia.Core.Serialization;

/// <summary>
/// Contexto estático de serialización JSON con Source Generators para System.Text.Json.
/// Elimina el uso de reflexión en tiempo de ejecución, habilitando soporte completo de Trimming (recorte) y AOT sin advertencias.
/// </summary>
[JsonSourceGenerationOptions(
    WriteIndented = false,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
[JsonSerializable(typeof(QapptiaConfig))]
[JsonSerializable(typeof(ReleaseInfoDto))]
[JsonSerializable(typeof(IpcMessage))]
[JsonSerializable(typeof(WakeUpRequest))]
[JsonSerializable(typeof(QuitRequest))]
[JsonSerializable(typeof(RefreshTrayIconRequest))]
[JsonSerializable(typeof(Ping))]
[JsonSerializable(typeof(Ack))]
[JsonSerializable(typeof(ErrorResponse))]
[JsonSerializable(typeof(Pong))]
[JsonSerializable(typeof(ThemeChangedNotification))]
[JsonSerializable(typeof(IpcMessageType))]
public partial class QapptiaJsonContext : JsonSerializerContext
{
}
