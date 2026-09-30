using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
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
        var logDir = Qapptia.Core.Constants.DefaultLogDirectory;
        using var _log = LoggingBootstrap.ConfigureGlobal(logDir, LogEventLevel.Information, "config");
        Log.Information("Qapptia Config App iniciada");

        using var guard = new MutexSingleInstanceGuard(IpcChannels.Config);
        if (!guard.Acquire())
        {
            Log.Warning("Instancia de Config ya existente, intentando despertar...");
            try
            {
                var isAbout = args.Any(a => string.Equals(a, Qapptia.Core.Constants.ArgAbout, StringComparison.OrdinalIgnoreCase));
                var request = new WakeUpRequest { Argument = isAbout ? Qapptia.Core.Constants.ArgAbout : null };
                QapptiaIpcClient.SendAsync(IpcChannels.Config, request, timeoutMs: 1000).GetAwaiter().GetResult();
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
                    case WakeUpRequest wakeMsg:
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

                                if (string.Equals(wakeMsg.Argument, Qapptia.Core.Constants.ArgAbout, StringComparison.OrdinalIgnoreCase) &&
                                    desktop.MainWindow.DataContext is ViewModels.ConfigViewModel vm)
                                {
                                    vm.SelectedTabIndex = 3;
                                }
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
