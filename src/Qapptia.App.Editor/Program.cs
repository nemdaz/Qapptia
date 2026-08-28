using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Qapptia.Core.Configuration;
using Qapptia.Core.Ipc;
using Qapptia.Core.Platform;
using Qapptia.UI.Components.Theme;
using Serilog;

namespace Qapptia.App.Editor;

sealed class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        var logDir = Qapptia.Core.Constants.DefaultLogDirectory;
#if DEBUG
        var logLevel = Serilog.Events.LogEventLevel.Debug;
#else
        var logLevel = Serilog.Events.LogEventLevel.Information;
#endif
        using var log = Qapptia.Core.Logging.LoggingBootstrap.ConfigureGlobal(logDir, logLevel, "editor");

        using var guard = new MutexSingleInstanceGuard(IpcChannels.Editor);
        if (!guard.Acquire())
        {
            Log.Warning("Instancia de Editor ya existente, intentando despertar...");
            try
            {
                QapptiaIpcClient.SendAsync(IpcChannels.Editor, new WakeUpRequest(), timeoutMs: 1000).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "No se pudo despertar la otra instancia de Editor");
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
                        Log.Information("Editor recibió cambio de tema vía IPC: {Theme}", themeMsg.Theme);
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
            IpcChannels.Editor,
            IpcChannels.GetPipeName(IpcChannels.Editor),
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
