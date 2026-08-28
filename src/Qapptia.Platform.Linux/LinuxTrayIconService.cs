using System;
using Qapptia.Core.Abstractions;
using Serilog;

namespace Qapptia.Platform.Linux;

public sealed class LinuxTrayIconService : ITrayIconService
{
    private readonly ILogger _logger;
    private bool _disposed;

    public LinuxTrayIconService(ILogger logger)
    {
        _logger = logger;
    }

    public void Initialize(TrayMenuDefinition menu, string iconPath)
    {
        _logger.Warning("LinuxTrayIconService: Implementación nativa pendiente. El icono de la bandeja no se mostrará.");
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
    }
}

