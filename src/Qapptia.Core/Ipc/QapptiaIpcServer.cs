using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

namespace Qapptia.Core.Ipc;

/// <summary>
/// Dispatcher asíncrono para un canal IPC. Acepta cada conexión entrante, lee un
/// frame <see cref="IpcMessage"/>, lo enruta al handler correspondiente según el tipo
/// y responde con otro <see cref="IpcMessage"/> (Ack, Error o Pong).
/// </summary>
public sealed class IpcMessageDispatcher
{
    private readonly Func<IpcMessage, CancellationToken, Task<IpcMessage>> _handler;
    private readonly ILogger _logger;

    public IpcMessageDispatcher(
        Func<IpcMessage, CancellationToken, Task<IpcMessage>> handler,
        ILogger logger)
    {
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        _logger = logger;
    }

    public async Task HandleConnectionAsync(Stream stream, CancellationToken ct)
    {
        try
        {
            var request = await IpcWire.ReadFrameAsync(stream, ct).ConfigureAwait(false);
            _logger.Debug("IPC req {Type}", request.Type);

            IpcMessage response;
            try
            {
                response = await _handler(request, ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                _logger.Error(ex, "Handler IPC lanzó para {Type}", request.Type);
                response = new ErrorResponse { Reason = ex.Message };
            }

            await IpcWire.WriteFrameAsync(stream, response, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { /* shutdown limpio */ }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            _logger.Warning(ex, "Conexión IPC fallida");
        }
    }
}

/// <summary>
/// Servidor de Named Pipes para un canal. Acepta conexiones entrantes en loop, cada
/// conexión en su propia <see cref="Task"/>. Implementa <see cref="IDisposable"/> para
/// liberar recursos al detener la app. Escribe el <see cref="IpcChannelState"/> (pid+token)
/// en disco al iniciar y lo borra al detenerse.
/// </summary>
public sealed class QapptiaIpcServer : IDisposable
{
    private readonly string _pipeName;
    private readonly IpcMessageDispatcher _dispatcher;
    private readonly ILogger _logger;
    private readonly CancellationTokenSource _cts = new();
    private readonly string _token;
    private readonly string _channel;
    private readonly List<Task> _acceptTasks = new();
    private readonly List<NamedPipeServerStream> _pendingServers = new();
    private const int MaxConcurrentInstances = 4;
    private readonly object _sync = new();

    public string Token => _token;

    public QapptiaIpcServer(
        string channel,
        string pipeName,
        IpcMessageDispatcher dispatcher,
        ILogger logger,
        string? token = null)
    {
        _channel = channel ?? throw new ArgumentNullException(nameof(channel));
        _pipeName = pipeName ?? throw new ArgumentNullException(nameof(pipeName));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _logger = logger;
        _token = token ?? Guid.NewGuid().ToString("N");
    }

    public Task StartAsync(CancellationToken ct = default)
    {
        var state = new IpcChannelState
        {
            Pid = Environment.ProcessId,
            Token = _token,
            PipeName = _pipeName,
        };
        IpcChannelState.Save(_channel, state);
        _logger.Information("IPC server escuchando en {Pipe} (token {Token}…)", _pipeName, _token[..8]);

        for (var i = 0; i < MaxConcurrentInstances; i++)
        {
            var server = CreateServer();
            lock (_sync) { _pendingServers.Add(server); }
            _acceptTasks.Add(AcceptOneAsync(server));
        }
        return Task.CompletedTask;
    }

    private NamedPipeServerStream CreateServer()
    {
        return new NamedPipeServerStream(
            _pipeName,
            PipeDirection.InOut,
            maxNumberOfServerInstances: MaxConcurrentInstances,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous);
    }

    private async Task AcceptOneAsync(NamedPipeServerStream server)
    {
        while (!_cts.IsCancellationRequested)
        {
            try
            {
                await server.WaitForConnectionAsync(_cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                try { server.Dispose(); } catch { }
                return;
            }
            catch (ObjectDisposedException)
            {
                return;
            }

            using (server)
            {
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
                try
                {
                    await _dispatcher.HandleConnectionAsync(server, linkedCts.Token).ConfigureAwait(false);
                }
                catch (Exception ex) when (!_cts.IsCancellationRequested)
                {
                    _logger.Warning(ex, "Error en conexión IPC");
                }
            }

            if (_cts.IsCancellationRequested) return;

            // Reemplazo: nuevo server para aceptar la siguiente conexión
            lock (_sync)
            {
                _pendingServers.Remove(server);
                if (_cts.IsCancellationRequested) return;
                var fresh = CreateServer();
                _pendingServers.Add(fresh);
                _acceptTasks.Add(AcceptOneAsync(fresh));
            }
            return;
        }
    }

    public async Task StopAsync(CancellationToken ct = default)
    {
        _cts.Cancel();
        NamedPipeServerStream[] servers;
        lock (_sync) { servers = _pendingServers.ToArray(); }
        foreach (var s in servers)
        {
            try { s.Dispose(); } catch { }
        }
        try
        {
            await Task.WhenAll(_acceptTasks).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { }
        catch (AggregateException) { }
        IpcChannelState.Delete(_channel, expectedToken: _token);
        _logger.Information("IPC server detenido en {Pipe}", _pipeName);
    }

    public void Dispose()
    {
        try { _cts.Cancel(); } catch { }
        NamedPipeServerStream[] servers;
        lock (_sync) { servers = _pendingServers.ToArray(); }
        foreach (var s in servers)
        {
            try { s.Dispose(); } catch { }
        }
        try { _cts.Dispose(); } catch { }
        IpcChannelState.Delete(_channel, expectedToken: _token);
    }
}
