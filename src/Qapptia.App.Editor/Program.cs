using Avalonia;
using Avalonia.Threading;
using System;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using Qapptia.Core.Configuration;
using Qapptia.Core.Ipc;
using Qapptia.UI.Components.Theme;
using Serilog;

namespace Qapptia.App.Editor;

sealed class Program
{
    private static readonly string PipeName = IpcChannels.GetPipeName(IpcChannels.Editor);

    [STAThread]
    public static void Main(string[] args)
    {
        var logDir = Qapptia.Core.AppConstants.DefaultLogDirectory;
#if DEBUG
        var logLevel = Serilog.Events.LogEventLevel.Debug;
#else
        var logLevel = Serilog.Events.LogEventLevel.Information;
#endif
        using var log = Qapptia.Core.Logging.LoggingBootstrap.ConfigureGlobal(logDir, logLevel, "editor");
        
        var configPath = Qapptia.Core.AppConstants.DefaultConfigPath;
        var configService = new JsonConfigService(configPath, Log.Logger);
        
        // Aplicar tema configurado al iniciar
        ThemeManager.ApplyTheme(configService.Current.Theme);

        var cts = new CancellationTokenSource();
        var ipcThread = new Thread(() => RunIpcServer(cts.Token)) { IsBackground = true };
        ipcThread.Start();

        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            cts.Cancel();
        }
    }

    private static async void RunIpcServer(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var server = new NamedPipeServerStream(PipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                await server.WaitForConnectionAsync(ct);

                try
                {
                    var msg = await IpcWire.ReadFrameAsync(server, ct);
                    if (msg is ThemeChangedNotification themeMsg)
                    {
                        Dispatcher.UIThread.Post(() =>
                        {
                            ThemeManager.ApplyTheme(themeMsg.Theme);
                        });
                    }
                }
                catch (Exception ex) { Log.Warning(ex, "Error al leer mensaje IPC en Editor"); }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                Log.Error(ex, "Fallo inesperado en IPC server Editor");
                await Task.Delay(1000, ct);
            }
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
