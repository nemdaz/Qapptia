using Avalonia;
using System;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls.ApplicationLifetimes;
using Qapptia.Core.Ipc;
using Qapptia.Core.Platform;

namespace Qapptia.App.Config;

sealed class Program
{
    private const string PipeName = "Qapptia_Config_IPC";

    [STAThread]
    public static void Main(string[] args)
    {
        using var guard = new MutexSingleInstanceGuard("Config");
        
        if (!guard.Acquire())
        {
            // Ya hay una instancia, enviar petición para despertar
            Task.Run(async () =>
            {
                using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut);
                try
                {
                    await client.ConnectAsync(1000);
                    await IpcWire.WriteFrameAsync(client, new WakeUpRequest());
                }
                catch { /* Ignorar si no logra conectar */ }
            }).Wait(1500);
            return;
        }

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
                    if (msg is WakeUpRequest)
                    {
                        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
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
                    }
                }
                catch { /* Ignorar errores de lectura */ }
            }
            catch (OperationCanceledException) { break; }
            catch { await Task.Delay(1000, ct); }
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
