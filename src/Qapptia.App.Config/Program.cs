using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using System;
using System.Threading.Tasks;
using Qapptia.Core.Ipc;
using Qapptia.Core.Logging;
using Qapptia.Core.Platform;
using Qapptia.UI.Components.Theme;
using Serilog;
using Serilog.Events;

namespace Qapptia.App.Config;

sealed class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        var logDir = Qapptia.Core.AppConstants.DefaultLogDirectory;
        using var _log = LoggingBootstrap.ConfigureGlobal(logDir, LogEventLevel.Information, "config");
        Log.Information("Qapptia Config App iniciada");

        using var guard = new MutexSingleInstanceGuard(IpcChannels.Config);
        if (!guard.Acquire())
        {
            Log.Warning("Instancia de Config ya existente, intentando despertar...");
            try
            {
                QapptiaIpcClient.SendAsync(IpcChannels.Config, new WakeUpRequest(), timeoutMs: 1000).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "No se pudo despertar la otra instancia de Config");
            }
            return;
        }

        var dispatcher = new IpcMessageDispatcher(
            (msg, ct) =>
            {
                switch (msg)
                {
                    case WakeUpRequest:
                        Dispatcher.UIThread.Post(() =>
                        {
                            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop &&
                                desktop.MainWindow != null)
                            {
                                desktop.MainWindow.Show();
                                desktop.MainWindow.WindowState = Avalonia.Controls.WindowState.Normal;
                                desktop.MainWindow.Activate();
                                desktop.MainWindow.Topmost = true;
                                desktop.MainWindow.Topmost = false;
                            }
                        });
                        return Task.FromResult<IpcMessage>(new Ack { OriginalType = msg.Type });

                    case ThemeChangedNotification themeMsg:
                        Dispatcher.UIThread.Post(() =>
                        {
                            ThemeManager.ApplyTheme(themeMsg.Theme);
                        });
                        return Task.FromResult<IpcMessage>(new Ack { OriginalType = msg.Type });

                    default:
                        return Task.FromResult<IpcMessage>(new Ack { OriginalType = msg.Type });
                }
            },
            Log.Logger.ForContext<IpcMessageDispatcher>());

        using var ipcServer = new QapptiaIpcServer(
            IpcChannels.Config,
            IpcChannels.GetPipeName(IpcChannels.Config),
            dispatcher,
            Log.Logger.ForContext<QapptiaIpcServer>());

        _ = ipcServer.StartAsync();

        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            try { ipcServer.StopAsync().GetAwaiter().GetResult(); } catch { }
        }
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();
}
