using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Qapptia.Core.Ipc;

namespace Qapptia.Capture;

public sealed class IpcServerHostedService : IHostedService, IDisposable
{
    private readonly QapptiaIpcServer _server;
    private readonly ILogger<IpcServerHostedService> _logger;

    public IpcServerHostedService(
        ILoggerFactory loggerFactory,
        ILogger<IpcServerHostedService> logger,
        ICaptureActionHandler handler)
    {
        _logger = logger;

        var dispatcherLogger = loggerFactory.CreateLogger<IpcMessageDispatcher>();
        var serverLogger = loggerFactory.CreateLogger<QapptiaIpcServer>();

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
        _logger.LogInformation("Iniciando IPC server (capture)");
        return _server.StartAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Deteniendo IPC server (capture)");
        return _server.StopAsync(cancellationToken);
    }

    public void Dispose()
    {
        _server.Dispose();
    }
}