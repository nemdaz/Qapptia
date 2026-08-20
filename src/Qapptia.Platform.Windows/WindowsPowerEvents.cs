using Serilog;
using Qapptia.Core.Abstractions;

namespace Qapptia.Platform.Windows;

/// <summary>
/// PowerEvents en Windows via <c>Microsoft.Win32.SystemEvents.PowerModeChanged</c> (BCL nativo).
///
/// IMPORTANTE: <c>SystemEvents</c> requiere un message pump (STA thread) en la app.
/// En App.Capture (background worker) se debe iniciar un thread STA dedicado que bombee mensajes
/// para que los eventos lleguen. Esto se orquesta en el host (Fase 2).
/// </summary>
public sealed class WindowsPowerEvents : IPowerEvents
{
    private readonly ILogger _logger;
    private bool _disposed;

    public WindowsPowerEvents(ILogger logger)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("WindowsPowerEvents requiere Windows.");
        _logger = logger;
        Microsoft.Win32.SystemEvents.PowerModeChanged += OnSystemPowerModeChanged;
    }

    public bool RequiresHotkeyReRegistrationAfterResume => true;
    public event EventHandler<PowerMode>? PowerModeChanged;

    private void OnSystemPowerModeChanged(object? sender, Microsoft.Win32.PowerModeChangedEventArgs e)
    {
        var mode = e.Mode switch
        {
            Microsoft.Win32.PowerModes.Suspend => PowerMode.Suspend,
            Microsoft.Win32.PowerModes.Resume => PowerMode.Resume,
            _ => PowerMode.StatusChange,
        };
        _logger.Information("Power event: {Mode}", mode);
        PowerModeChanged?.Invoke(this, mode);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Microsoft.Win32.SystemEvents.PowerModeChanged -= OnSystemPowerModeChanged;
    }
}

