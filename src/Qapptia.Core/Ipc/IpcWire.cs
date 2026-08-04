using System.Buffers.Binary;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Qapptia.Core.Ipc;

/// <summary>
/// Codifica y decodifica frames del protocolo wire IPC:
/// 4 bytes little-endian u32 con la longitud del payload (en bytes) seguidos del
/// payload UTF-8 JSON serializado como <see cref="IpcMessage"/> polimórfico.
/// </summary>
public static class IpcWire
{
    private static readonly JsonSerializerOptions s_jsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        Converters =
        {
            new IpcMessageJsonConverterFactory(),
            new System.Text.Json.Serialization.JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower),
        },
    };

    public static byte[] Encode(IpcMessage message)
    {
        var payloadJson = JsonSerializer.SerializeToUtf8Bytes<IpcMessage>(message, s_jsonOpts);
        if (payloadJson.Length > IpcProtocol.MaxPayloadBytes)
        {
            throw new InvalidOperationException(
                $"Payload IPC {payloadJson.Length}B supera el máximo {IpcProtocol.MaxPayloadBytes}B");
        }

        var frame = new byte[IpcProtocol.LenFieldSize + payloadJson.Length];
        BinaryPrimitives.WriteUInt32LittleEndian(frame.AsSpan(0, 4), (uint)payloadJson.Length);
        payloadJson.CopyTo(frame, IpcProtocol.LenFieldSize);
        return frame;
    }

    public static async Task<IpcMessage> ReadFrameAsync(Stream stream, CancellationToken ct = default)
    {
        var lenBuffer = new byte[IpcProtocol.LenFieldSize];
        await ReadExactAsync(stream, lenBuffer, IpcProtocol.LenFieldSize, ct).ConfigureAwait(false);
        var payloadLen = BinaryPrimitives.ReadUInt32LittleEndian(lenBuffer);

        if (payloadLen == 0 || payloadLen > IpcProtocol.MaxPayloadBytes)
            throw new InvalidDataException($"Longitud de payload IPC inválida: {payloadLen}");

        var payloadBuffer = new byte[payloadLen];
        await ReadExactAsync(stream, payloadBuffer, (int)payloadLen, ct).ConfigureAwait(false);

        var msg = JsonSerializer.Deserialize<IpcMessage>(payloadBuffer, s_jsonOpts);
        return msg ?? throw new InvalidDataException("IpcMessage deserializado a null");
    }

    public static async Task WriteFrameAsync(Stream stream, IpcMessage message, CancellationToken ct = default)
    {
        var frame = Encode(message);
        await stream.WriteAsync(frame, ct).ConfigureAwait(false);
        await stream.FlushAsync(ct).ConfigureAwait(false);
    }

    private static async Task ReadExactAsync(Stream stream, byte[] buffer, int count, CancellationToken ct)
    {
        var totalRead = 0;
        while (totalRead < count)
        {
            var read = await stream.ReadAsync(
                buffer.AsMemory(totalRead, count - totalRead), ct).ConfigureAwait(false);
            if (read == 0)
                throw new EndOfStreamException("El stream IPC se cerró antes de leer el frame completo");
            totalRead += read;
        }
    }
}
