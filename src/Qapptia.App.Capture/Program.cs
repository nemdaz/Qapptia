using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Qapptia.Capture;
using Qapptia.Core.Configuration;
using Qapptia.Core.Ipc;
using Qapptia.Core.Logging;
using Qapptia.Core.Platform;
using Qapptia.Core.Abstractions;
using Qapptia.UI.Shared;
using Serilog;
using Serilog.Events;
using System.Diagnostics;
using Avalonia.Platform;
using Avalonia.Themes.Fluent;
#if WINDOWS
using Qapptia.Platform.Windows;
#elif LINUX
using Qapptia.Platform.Linux;
using Qapptia.Platform.MacOS;
#endif

namespace Qapptia.App.Capture;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        using var guard = new MutexSingleInstanceGuard(IpcChannels.Capture);
        if (!guard.Acquire())
        {
            Console.Error.WriteLine("Otra instancia de App.Capture ya está corriendo.");
            return;
        }

        var exeDir = AppContext.BaseDirectory;
        using var _log = LoggingBootstrap.ConfigureGlobal(exeDir, LogEventLevel.Information, "capture");

        using var host = BuildHost(args);
        var appLogger = host.Services.GetRequiredService<ILogger<AppLoggerMarker>>();

        var lifetime = new ClassicDesktopStyleApplicationLifetime
        {
            Args = args,
            ShutdownMode = ShutdownMode.OnExplicitShutdown,
        };
        AppBuilder.Configure<HeadlessCaptureApp>()
            .UsePlatformDetect()
            .AfterSetup(b => 
            {
                if (b.Instance is HeadlessCaptureApp app)
                {
                    app.AppHost = host;
                }
            })
            .SetupWithLifetime(lifetime);

        var hostTask = Task.Run(() => host.StartAsync());
        if (hostTask.IsFaulted)
            hostTask.GetAwaiter().GetResult();

        try
        {
            lifetime.Start(Array.Empty<string>());
            try { host.StopAsync().GetAwaiter().GetResult(); }
            catch (Exception ex) { appLogger.LogHostStopError(ex); }
        }
        catch (Exception ex)
        {
            appLogger.LogAppFatalError(ex);
            throw;
        }
    }

    private static IHost BuildHost(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        builder.Logging.ClearProviders();
        builder.Logging.AddSerilog(Log.Logger, dispose: false);

        var configPath = Path.Combine(AppContext.BaseDirectory, "config.json");
        builder.Services.AddSingleton<IConfigService>(_ => new JsonConfigService(configPath));

#if WINDOWS
        if (OperatingSystem.IsWindows())
            builder.Services.AddWindowsPlatform();
#elif LINUX
        if (OperatingSystem.IsLinux())
            builder.Services.AddLinuxPlatform();
        else if (OperatingSystem.IsMacOS())
            builder.Services.AddMacOSPlatform();
#endif

        builder.Services.AddSingleton<IFullscreenCaptureService, FullscreenCaptureService>();
        builder.Services.AddSingleton<IAreaCaptureService, AvaloniaAreaCaptureService>();

        builder.Services.AddSingleton<ICaptureActionHandler, CaptureWorker>();
        builder.Services.AddHostedService(sp => (CaptureWorker)sp.GetRequiredService<ICaptureActionHandler>());
        builder.Services.AddHostedService<IpcServerHostedService>();

        return builder.Build();
    }
}

internal sealed class AppLoggerMarker { }

internal sealed class HeadlessCaptureApp : Application
{
    public IHost AppHost { get; set; } = null!;

    public override void Initialize()
    {
        // No cargamos FluentTheme porque esta app es headless (solo usa TrayIcon nativo)
        // Esto reduce drásticamente el tiempo de inicio y la memoria.
    }

    public override void OnFrameworkInitializationCompleted()
    {
        base.OnFrameworkInitializationCompleted();
        
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var logger = AppHost?.Services.GetService<ILogger<HeadlessCaptureApp>>();
            var captureHandler = AppHost?.Services.GetService<ICaptureActionHandler>();
            var trayService = AppHost?.Services.GetService<ITrayIconService>();
            
            var menuDef = new TrayMenuDefinition();
            
            menuDef.Items.Add(new TrayMenuActionItem("Capturar pantalla", () => captureHandler?.HandleFullscreenCaptureAsync(CancellationToken.None)));
            menuDef.Items.Add(new TrayMenuActionItem("Capturar área", () => captureHandler?.HandleAreaCaptureAsync(CancellationToken.None)));
            menuDef.Items.Add(new TrayMenuSeparatorItem());
            menuDef.Items.Add(new TrayMenuActionItem("Editor", () => LaunchApp("Qapptia.App.Editor.exe", "--editor")));
            menuDef.Items.Add(new TrayMenuActionItem("Configuración", () => LaunchApp("Qapptia.App.Config.exe", "--config")));
            menuDef.Items.Add(new TrayMenuSeparatorItem());
            menuDef.Items.Add(new TrayMenuActionItem("Reiniciar", () => 
            {
                var exePath = Process.GetCurrentProcess().MainModule?.FileName;
                if (!string.IsNullOrEmpty(exePath))
                {
                    Process.Start(new ProcessStartInfo(exePath) { UseShellExecute = true });
                }
                desktop.Shutdown();
            }));
            menuDef.Items.Add(new TrayMenuActionItem("Salir", () => desktop.Shutdown()));

            var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "app_icon.ico");
            trayService?.Initialize(menuDef, iconPath);
            
            logger?.LogTrayIconAssigned();
        }
    }

    private static void LaunchApp(string exeName, string arguments)
    {
        try
        {
            var basePath = AppContext.BaseDirectory;
            var exePath = Path.Combine(basePath, exeName);
            if (File.Exists(exePath))
            {
                Process.Start(new ProcessStartInfo(exePath, arguments) { UseShellExecute = true });
            }
            else
            {
                Console.WriteLine($"No se encontró la aplicación: {exePath}");
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Error al lanzar aplicación: " + ex.Message);
        }
    }
}

internal static partial class CaptureLogMessages
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "Error deteniendo host")]
    public static partial void LogHostStopError(this Microsoft.Extensions.Logging.ILogger logger, Exception ex);

    [LoggerMessage(EventId = 2, Level = LogLevel.Error, Message = "Fatal en App.Capture")]
    public static partial void LogAppFatalError(this Microsoft.Extensions.Logging.ILogger logger, Exception ex);

    [LoggerMessage(EventId = 3, Level = LogLevel.Information, Message = "TrayIcon asignado a la aplicación de forma limpia.")]
    public static partial void LogTrayIconAssigned(this Microsoft.Extensions.Logging.ILogger logger);
}
