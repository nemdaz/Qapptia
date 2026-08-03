using System;
using Microsoft.Extensions.Logging;
using Qapptia.Core.Abstractions;

namespace Qapptia.Platform.Linux;

public sealed class LinuxTrayIconService : ITrayIconService
{
    private readonly ILogger<LinuxTrayIconService> _logger;
    private bool _disposed;

    public LinuxTrayIconService(ILogger<LinuxTrayIconService> logger)
    {
        _logger = logger;
    }

    public void Initialize(TrayMenuDefinition menu, string iconPath)
    {
        _logger.LogWarning("LinuxTrayIconService: Implementación nativa pendiente. El icono de la bandeja no se mostrará.");
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
    }
}
