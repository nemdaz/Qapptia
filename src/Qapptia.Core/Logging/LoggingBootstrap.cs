using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Sinks.Async;

namespace Qapptia.Core.Logging;

/// <summary>
/// Bootstrap de Serilog. Configura sinks Consola + File rolling diario en
/// {exeDir}/logs/qapptia_{date}.log. En Release el nivel default es Information;
/// en Debug es Verbose. Se puede override conLogging:MinimumLevel en config.
/// </summary>
public static class LoggingBootstrap
{
    private const string ConsoleOutputTemplate =
        "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}";

    private const string FileOutputTemplate =
        "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}";

    public static Logger CreateLogger(
        string exeDirectory,
        LogEventLevel minimumLevel,
        string filePrefix = "qapptia")
    {
        var logPath = Path.Combine(exeDirectory, "logs", $"{filePrefix}_.log");
        Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);

        return new LoggerConfiguration()
            .MinimumLevel.Is(minimumLevel)
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("System", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("App", "Qapptia")
            .WriteTo.Async(a => a.Console(outputTemplate: ConsoleOutputTemplate, formatProvider: System.Globalization.CultureInfo.InvariantCulture))
            .WriteTo.Async(a => a.File(
                path: logPath,
                outputTemplate: FileOutputTemplate,
                formatProvider: System.Globalization.CultureInfo.InvariantCulture,
                rollingInterval: RollingInterval.Month,
                retainedFileCountLimit: 12, // Guarda logs de un año
                fileSizeLimitBytes: 50_000_000,
                rollOnFileSizeLimit: true,
                shared: false))
            .CreateLogger();
    }

    /// <summary>
    /// Configura el Logger global (Serilog.Log.Logger) y devuelve un IDisposable
    /// para cerrar y liberar al apagar la app.
    /// </summary>
    public static IDisposable ConfigureGlobal(
        string exeDirectory,
        LogEventLevel minimumLevel,
        string filePrefix = "qapptia")
    {
        var logger = CreateLogger(exeDirectory, minimumLevel, filePrefix);
        Log.Logger = logger;
        return logger;
    }
}
