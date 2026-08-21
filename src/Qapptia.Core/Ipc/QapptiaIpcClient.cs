using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

namespace Qapptia.Core.Ipc;

/// <summary>
/// Cliente de Named Pipes unificado para comunicación interproceso.
/// Lee el estado persistido del canal o utiliza el nombre de pipe por convención,
/// envía un mensaje de protocolo y espera la respuesta de forma asíncrona.
/// </summary>
public static class QapptiaIpcClient
{
    public static async Task<IpcMessage?> SendAsync(
        string channel,
        IpcMessage request,
        int timeoutMs = 1000,
        CancellationToken ct = default)
    {
        var state = IpcChannelState.Load(channel);
        var pipeName = state?.PipeName ?? IpcChannels.GetPipeName(channel);

        return await SendToPipeAsync(pipeName, request, timeoutMs, ct).ConfigureAwait(false);
    }

    public static async Task<IpcMessage?> SendToPipeAsync(
        string pipeName,
        IpcMessage request,
        int timeoutMs = 1000,
        CancellationToken ct = default)
    {
        using var client = new NamedPipeClientStream(
            ".",
            pipeName,
            PipeDirection.InOut,
            PipeOptions.Asynchronous);

        try
        {
            using var timeoutCts = new CancellationTokenSource(timeoutMs);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);
            await client.ConnectAsync(linkedCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { return null; }
        catch (TimeoutException) { return null; }
        catch (IOException) { return null; }
        catch (Exception) { return null; }

        try
        {
            await IpcWire.WriteFrameAsync(client, request, ct).ConfigureAwait(false);
            var response = await IpcWire.ReadFrameAsync(client, ct).ConfigureAwait(false);
            return response;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
