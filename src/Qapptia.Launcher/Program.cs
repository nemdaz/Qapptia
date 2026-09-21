using System;
using Qapptia.Core.Launcher;

namespace Qapptia.Launcher;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        var baseDir = AppContext.BaseDirectory;
        var plan = LauncherOrchestrator.ResolveLaunchPlan(args);
        var launcher = new DefaultProcessLauncher();

        LauncherOrchestrator.DispatchPlanAsync(plan, baseDir, launcher).GetAwaiter().GetResult();
    }
}
