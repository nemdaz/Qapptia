using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Qapptia.Capture;
using Qapptia.Core.Abstractions;
using Qapptia.Core.Configuration;
using Qapptia.Core.Ipc;
using Qapptia.Core.Logging;
using Qapptia.Core.Platform;
using Qapptia.Core.Services;
using Serilog;
using Serilog.Events;
using CaptureConstants = Qapptia.App.Capture.Common.Constants;
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
        var logDir = Qapptia.Core.Constants.DefaultLogDirectory;
#if DEBUG
        var logLevel = LogEventLevel.Debug;
#else
        var logLevel = LogEventLevel.Information;
#endif
        using var _log = LoggingBootstrap.ConfigureGlobal(logDir, logLevel, "capture");

        try
        {
            using var guard = new MutexSingleInstanceGuard(IpcChannels.Capture);
            if (!guard.Acquire())
            {
                Log.Warning("Otra instancia de App.Capture ya está en ejecución.");
                try
                {
                    QapptiaIpcClient.SendAsync(IpcChannels.Capture, new WakeUpRequest(), timeoutMs: 1000).GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    Log.Debug(ex, "No se pudo entregar WakeUpRequest a la instancia de captura existente.");
                }
                return;
            }

            Log.Information("Iniciando host de App.Capture en {OS}...", Environment.OSVersion);
            using var host = BuildHost(args);
            var appLogger = host.Services.GetRequiredService<Serilog.ILogger>();

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
            if (hostTask.IsFaulted) hostTask.GetAwaiter().GetResult();

            lifetime.Start(Array.Empty<string>());
            try
            {
                host.StopAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                appLogger.Error(ex, "Error deteniendo host");
            }
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Fallo fatal en App.Capture durante el ciclo de vida o arranque");
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    private static IHost BuildHost(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        builder.Logging.ClearProviders();
        builder.Services.AddSingleton<Serilog.ILogger>(Log.Logger);
        builder.Logging.AddSerilog(Log.Logger, dispose: false);

        var configPath = Qapptia.Core.Constants.DefaultConfigPath;
        builder.Services.AddSingleton<IConfigService>(_ => new JsonConfigService(configPath));

#if WINDOWS
        // NT 6.1 corresponde a Windows 7
        if (OperatingSystem.IsWindowsVersionAtLeast(6, 1)) builder.Services.AddWindowsPlatform();
#elif LINUX
        if (OperatingSystem.IsLinux()) builder.Services.AddLinuxPlatform();
        else if (OperatingSystem.IsMacOS())
            builder.Services.AddMacOSPlatform();
#endif

        builder.Services.AddSingleton<IFullscreenCaptureService, FullscreenCaptureService>();
        builder.Services.AddSingleton<IAreaCaptureService, AvaloniaAreaCaptureService>();

        builder.Services.AddSingleton<ICaptureActionHandler, CaptureWorker>();
        builder.Services.AddHostedService(sp => (CaptureWorker)sp.GetRequiredService<ICaptureActionHandler>());
        builder.Services.AddHostedService<IpcServerHostedService>();

        builder.Services.AddSingleton<IUpdateCheckService, HttpUpdateCheckService>();
        builder.Services.AddHostedService<UpdateCheckBackgroundService>();

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
            var logger = AppHost?.Services.GetService<Serilog.ILogger>();
            var captureHandler = AppHost?.Services.GetService<ICaptureActionHandler>();
            var trayService = AppHost?.Services.GetService<ITrayIconService>();
            var shellService = AppHost?.Services.GetService<IShellService>();

            var menuDef = new TrayMenuDefinition();

            var config = AppHost?.Services.GetService<IConfigService>();

            menuDef.Items.Add(new TrayMenuActionItem(CaptureConstants.TrayMenuCaptureScreen, () => captureHandler?.HandleFullscreenCaptureAsync(CancellationToken.None), shortcutTextProvider: () => config?.Current.ShortcutScreen));
            menuDef.Items.Add(new TrayMenuActionItem(CaptureConstants.TrayMenuCaptureArea, () => captureHandler?.HandleAreaCaptureAsync(CancellationToken.None), shortcutTextProvider: () => config?.Current.ShortcutArea));
            menuDef.Items.Add(new TrayMenuSeparatorItem());
            menuDef.Items.Add(new TrayMenuActionItem(CaptureConstants.TrayMenuEditor, () => LaunchApp(Qapptia.Core.Constants.EditorExecutableName, Qapptia.Core.Constants.ArgEditor)));
            menuDef.Items.Add(new TrayMenuActionItem(CaptureConstants.TrayMenuConfig, () => LaunchApp(Qapptia.Core.Constants.ConfigExecutableName, Qapptia.Core.Constants.ArgConfig)));
            menuDef.Items.Add(new TrayMenuSeparatorItem());
            menuDef.Items.Add(new TrayMenuActionItem(Qapptia.Core.Constants.SponsorDefaultActionTitle, () => shellService?.OpenUrl(Qapptia.Core.Constants.DefaultSponsorUrl), textProvider: () => SponsorVisualHelper.GetRandomCombination().Text));
            menuDef.Items.Add(new TrayMenuActionItem(CaptureConstants.TrayMenuAbout, () => LaunchApp(Qapptia.Core.Constants.ConfigExecutableName, Qapptia.Core.Constants.ArgAbout)));
            menuDef.Items.Add(new TrayMenuSeparatorItem());
            menuDef.Items.Add(new TrayMenuActionItem(CaptureConstants.TrayMenuRestart, () =>
            {
                var exePath = Process.GetCurrentProcess().MainModule?.FileName;
                if (!string.IsNullOrEmpty(exePath))
                {
                    Process.Start(new ProcessStartInfo(exePath, Qapptia.Core.Constants.ArgRestart) { UseShellExecute = true });
                }
                desktop.Shutdown();
            }));
            menuDef.Items.Add(new TrayMenuActionItem(CaptureConstants.TrayMenuExit, () => desktop.Shutdown()));

            var iconPath = Path.Combine(AppContext.BaseDirectory, Qapptia.Core.Constants.AssetsDirectoryName, Qapptia.Core.Constants.TrayIconFileName);
            trayService?.Initialize(menuDef, iconPath);

            var isRestart = desktop.Args?.Any(a => string.Equals(a, Qapptia.Core.Constants.ArgRestart, StringComparison.OrdinalIgnoreCase)) ?? false;
            var notificationTitle = Qapptia.Core.Constants.NotificationTitleCapture;
            var notificationMessage = isRestart
                ? Qapptia.Core.Constants.NotificationMessageCaptureRestarted
                : Qapptia.Core.Constants.NotificationMessageCaptureStarted;

            trayService?.ShowNotification(
                notificationTitle,
                notificationMessage,
                TrayNotificationType.Info,
                Qapptia.Core.Constants.NotificationDurationMs);

            logger?.Information("TrayIcon asignado a la aplicación de forma limpia.");
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
                Log.Error("No se encontró la aplicación: {ExePath}", exePath);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error al lanzar aplicación");
        }
    }
}
