using System.Diagnostics;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Qapptia.Core.Abstractions;
using Qapptia.Core.Capture;
using Serilog;

namespace Qapptia.Platform.Linux;

public sealed class LinuxScreenCapture : IScreenCapture
{
    public LinuxScreenCapture() { }
    public Task<ScreenCaptureResult> CaptureScreenAsync(bool captureAllScreens = false, CancellationToken ct = default)
        => throw Throw();
    private static PlatformNotSupportedException Throw()
        => new("IScreenCapture en Linux se implementa en Fase 3 (X11/XGetImage o XDamage).");
}

public sealed class LinuxCursorCapture : ICursorCapture
{
    public Task<CursorImage?> CaptureCursorAsync(CancellationToken ct = default)
        => throw new PlatformNotSupportedException("ICursorCapture en Linux se implementa en Fase 3 (XFixesGetCursorImage).");
}

public sealed class LinuxHotkeyRegistrar : IHotkeyRegistrar
{
    public IHotkeyHandle Register(HotkeyModifiers modifiers, uint virtualKey, Action callback)
        => throw new PlatformNotSupportedException("IHotkeyRegistrar en Linux se implementa en Fase 3 (XGrabKey).");
}

public sealed class LinuxPowerEvents : IPowerEvents
{
    public event EventHandler<PowerMode>? PowerModeChanged { add { } remove { } }
    public bool RequiresHotkeyReRegistrationAfterResume => true;
    public void Dispose() { }
}

public sealed class LinuxDesktopService : IDesktopService
{
    public void ShowInfo(string title, string message)
        => throw new PlatformNotSupportedException("IDesktopService en Linux se implementa en Fase 3.");
    public void ShowError(string title, string message) => throw new PlatformNotSupportedException("LinuxDesktopService: Fase 3.");
    public MonitorInfo GetMonitorAtCursor() => throw new PlatformNotSupportedException("LinuxDesktopService: Fase 3.");
    public (int X, int Y) GetCursorPosition() => throw new PlatformNotSupportedException("LinuxDesktopService: Fase 3.");
    public (int X, int Y) GetVirtualScreenOrigin() => throw new PlatformNotSupportedException("LinuxDesktopService: Fase 3.");
    public int GetVirtualScreenWidth() => throw new PlatformNotSupportedException("LinuxDesktopService: Fase 3.");
    public int GetVirtualScreenHeight() => throw new PlatformNotSupportedException("LinuxDesktopService: Fase 3.");
    public double GetDpiScalingAtCursor() => throw new PlatformNotSupportedException("LinuxDesktopService: Fase 3.");
}

public sealed class LinuxShutterSoundService : IShutterSoundService
{
    public Task PlayAsync(CancellationToken ct = default)
        => throw new PlatformNotSupportedException("IShutterSoundService en Linux se implementa en Fase 3 (ALSA/PulseAudio).");
}


public sealed class LinuxClipboardService : IClipboardService
{
    public Task SetTextAsync(string text, CancellationToken ct = default)
        => throw new PlatformNotSupportedException("IClipboardService en Linux se implementa en Fase 3 (xclip / DBus).");
    public Task SetImageAsync(byte[] pngBytes, CancellationToken ct = default) => throw new PlatformNotSupportedException("LinuxClipboardService: Fase 3.");
    public Task SetFileDropListAsync(string[] filePaths, CancellationToken ct = default) => throw new PlatformNotSupportedException("LinuxClipboardService: Fase 3.");
}

public sealed class LinuxShellService : IShellService
{
    private readonly ILogger? _logger;

    public LinuxShellService(ILogger? logger = null)
    {
        _logger = logger?.ForContext<LinuxShellService>();
    }

    public bool OpenFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return false;
        var fullPath = Path.GetFullPath(filePath);
        if (!File.Exists(fullPath)) return false;

        try
        {
            Process.Start(new ProcessStartInfo("xdg-open", $"\"{fullPath}\"") { UseShellExecute = false });
            return true;
        }
        catch (Exception ex)
        {
            _logger?.Error(ex, "Error al abrir archivo en Linux: {FilePath}", fullPath);
            return false;
        }
    }

    public bool ShowInFolder(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return false;
        var fullPath = Path.GetFullPath(filePath);

        try
        {
            var dir = File.Exists(fullPath) ? Path.GetDirectoryName(fullPath) : fullPath;
            if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
            {
                Process.Start(new ProcessStartInfo("xdg-open", $"\"{dir}\"") { UseShellExecute = false });
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger?.Error(ex, "Error al mostrar en carpeta en Linux: {FilePath}", fullPath);
            return false;
        }
    }

    public bool OpenUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
             !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
        {
            _logger?.Warning("Esquema no permitido en Linux: {Url}", url);
            return false;
        }

        try
        {
            Process.Start(new ProcessStartInfo("xdg-open", $"\"{uri.AbsoluteUri}\"") { UseShellExecute = false });
            return true;
        }
        catch (Exception ex)
        {
            _logger?.Error(ex, "Error al abrir URL en Linux: {Url}", url);
            return false;
        }
    }
}

public sealed class LinuxAutoStartService : IAutoStartService
{
    private static readonly string DesktopFileName = $"{Qapptia.Core.Constants.LauncherAppName.ToLowerInvariant()}.desktop";
    private readonly ILogger? _logger;

    public LinuxAutoStartService(ILogger? logger = null)
    {
        _logger = logger?.ForContext<LinuxAutoStartService>();
    }

    public bool IsSupported => OperatingSystem.IsLinux();

    private static string GetAutostartFilePath()
    {
        string? xdgConfig = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        string configDir = !string.IsNullOrWhiteSpace(xdgConfig)
            ? xdgConfig
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
        return Path.Combine(configDir, "autostart", DesktopFileName);
    }

    public bool IsAutoStartEnabled()
    {
        if (!OperatingSystem.IsLinux()) return false;

        try
        {
            string path = GetAutostartFilePath();
            if (!File.Exists(path)) return false;

            var lines = File.ReadAllLines(path);
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("Hidden=", StringComparison.OrdinalIgnoreCase) &&
                    bool.TryParse(trimmed.AsSpan(7), out var hidden) && hidden)
                {
                    return false;
                }
                if (trimmed.StartsWith("X-GNOME-Autostart-enabled=", StringComparison.OrdinalIgnoreCase) &&
                    bool.TryParse(trimmed.AsSpan(26), out var gnomeEnabled) && !gnomeEnabled)
                {
                    return false;
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger?.Warning(ex, "Error al consultar estado de autostart en Linux");
            return false;
        }
    }

    public bool SetAutoStartEnabled(bool enabled)
    {
        if (!OperatingSystem.IsLinux()) return false;

        try
        {
            string desktopFilePath = GetAutostartFilePath();
            string autostartDir = Path.GetDirectoryName(desktopFilePath)!;

            if (enabled)
            {
                if (!Directory.Exists(autostartDir))
                {
                    Directory.CreateDirectory(autostartDir);
                }

                string launcherPath = Qapptia.Core.Launcher.LauncherOrchestrator.ResolveLauncherPath().Trim('"');
                string content = $"""
[Desktop Entry]
Type=Application
Version=1.0
Name={Qapptia.Core.Constants.LauncherAppName}
Comment=Herramienta de captura y edicion de pantalla
Exec="{launcherPath}" {Qapptia.Core.Constants.ArgCapture}
Icon={Qapptia.Core.Constants.LauncherAppName.ToLowerInvariant()}
Terminal=false
Categories=Utility;Graphics;
StartupNotify=false
X-GNOME-Autostart-enabled=true

""";
                File.WriteAllText(desktopFilePath, content);
                _logger?.Information("Autostart de Linux registrado en {Path}", desktopFilePath);
            }
            else
            {
                if (File.Exists(desktopFilePath))
                {
                    File.Delete(desktopFilePath);
                    _logger?.Information("Autostart de Linux eliminado de {Path}", desktopFilePath);
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger?.Error(ex, "Error al configurar autostart en Linux (enabled={Enabled})", enabled);
            return false;
        }
    }
}

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddLinuxPlatform(this IServiceCollection services)
    {
        if (!OperatingSystem.IsLinux()) throw new PlatformNotSupportedException("AddLinuxPlatform requiere Linux.");
        services.TryAddSingleton<IScreenCapture, LinuxScreenCapture>();
        services.TryAddSingleton<ICursorCapture, LinuxCursorCapture>();
        services.TryAddSingleton<IHotkeyRegistrar, LinuxHotkeyRegistrar>();
        services.TryAddSingleton<IPowerEvents, LinuxPowerEvents>();
        services.TryAddSingleton<IDesktopService, LinuxDesktopService>();
        services.TryAddSingleton<IShutterSoundService, LinuxShutterSoundService>();
        services.TryAddSingleton<IClipboardService, LinuxClipboardService>();
        services.TryAddSingleton<ITrayIconService, LinuxTrayIconService>();
        services.TryAddSingleton<IShellService, LinuxShellService>();
        services.TryAddSingleton<IAutoStartService, LinuxAutoStartService>();
        return services;
    }
}
