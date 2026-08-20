using System;
using Serilog;
using Qapptia.Core.Abstractions;

namespace Qapptia.Platform.MacOS;

public sealed class MacOSTrayIconService : ITrayIconService
{
    private readonly ILogger _logger;
    private bool _disposed;

    public MacOSTrayIconService(ILogger logger)
    {
        _logger = logger;
    }

    public void Initialize(TrayMenuDefinition menu, string iconPath)
    {
        _logger.Warning("MacOSTrayIconService: Implementación nativa pendiente. El icono de la bandeja no se mostrará.");
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
    }
}

