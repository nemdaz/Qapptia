using Microsoft.Extensions.Hosting;
using Serilog;
using Qapptia.Core.Ipc;

namespace Qapptia.Capture;

public sealed class IpcServerHostedService : IHostedService, IDisposable
{
    private readonly QapptiaIpcServer _server;
    private readonly ILogger _logger;

    public IpcServerHostedService(
        ILogger logger,
        ICaptureActionHandler handler)
    {
        _logger = logger.ForContext<IpcServerHostedService>();

        var dispatcherLogger = logger.ForContext<IpcMessageDispatcher>();
        var serverLogger = logger.ForContext<QapptiaIpcServer>();

        var dispatcher = new IpcMessageDispatcher(
            async (msg, ct) =>
            {
                switch (msg)
                {
                    case WakeUpRequest:
                        await handler.HandleWakeUpAsync(ct);
                        return new Ack { OriginalType = msg.Type };
                    case QuitRequest:
                        await handler.HandleQuitAsync(ct);
                        return new Ack { OriginalType = msg.Type };
                    case RefreshTrayIconRequest:
                        await handler.HandleRefreshTrayAsync(ct);
                        return new Ack { OriginalType = msg.Type };
                    case Ping:
                        return new Pong { ServerPid = Environment.ProcessId };
                    default:
                        return new ErrorResponse { Reason = $"Unhandled type: {msg.Type}" };
                }
            },
            dispatcherLogger);

        _server = new QapptiaIpcServer(
            IpcChannels.Capture,
            IpcChannels.GetPipeName(IpcChannels.Capture),
            dispatcher,
            serverLogger);
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.Information("Iniciando IPC server (capture)");
        return _server.StartAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.Information("Deteniendo IPC server (capture)");
        return _server.StopAsync(cancellationToken);
    }

    public void Dispose()
    {
        _server.Dispose();
    }
}
