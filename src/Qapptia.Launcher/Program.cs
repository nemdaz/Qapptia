using System;
using System.IO;
using Qapptia.Core;
using Qapptia.Core.Launcher;

namespace Qapptia.Launcher;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        try
        {
            var baseDir = AppContext.BaseDirectory;
            var plan = LauncherOrchestrator.ResolveLaunchPlan(args);
            var launcher = new DefaultProcessLauncher();

            LauncherOrchestrator.DispatchPlanAsync(plan, baseDir, launcher).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            try
            {
                var logDir = Constants.DefaultLogDirectory;
                Directory.CreateDirectory(logDir);
                File.AppendAllText(Path.Combine(logDir, "launcher_error.log"), $"[{DateTime.UtcNow:O}] {ex}\n");
            }
            catch
            {
                // Failsafe silencioso si el entorno restringe el almacenamiento local
            }
        }
    }
}
