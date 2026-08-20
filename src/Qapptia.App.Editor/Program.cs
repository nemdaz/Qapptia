using Avalonia;
using System;

namespace Qapptia.App.Editor;

sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        var logDir = Qapptia.Core.AppConstants.DefaultLogDirectory;
        using var log = Qapptia.Core.Logging.LoggingBootstrap.ConfigureGlobal(logDir, Serilog.Events.LogEventLevel.Information, "editor");
        
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();
}
