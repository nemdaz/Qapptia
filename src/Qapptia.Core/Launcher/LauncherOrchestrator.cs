using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Qapptia.Core.Ipc;

namespace Qapptia.Core.Launcher;

/// <summary>
/// Orquestador del despacho de inicio para la suite de aplicaciones Qapptia.
/// </summary>
public static class LauncherOrchestrator
{
    /// <summary>
    /// Interpreta los argumentos de línea de comandos y determina el plan de despacho correspondiente.
    /// </summary>
    public static LaunchPlan ResolveLaunchPlan(string[]? args)
    {
        if (args == null || args.Length == 0)
        {
            return new LaunchPlan(
                LaunchTarget.Suite,
                AdditionalArguments: null,
                ShouldEnsureCapture: true,
                ShouldEnsureEditor: true,
                ShouldEnsureConfig: false);
        }

        bool hasCapture = false;
        bool hasEditor = false;
        bool hasConfig = false;
        var otherArgs = new List<string>();

        for (int i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (string.Equals(arg, Constants.ArgCapture, StringComparison.OrdinalIgnoreCase))
            {
                hasCapture = true;
            }
            else if (string.Equals(arg, Constants.ArgEditor, StringComparison.OrdinalIgnoreCase))
            {
                hasEditor = true;
            }
            else if (string.Equals(arg, Constants.ArgConfig, StringComparison.OrdinalIgnoreCase))
            {
                hasConfig = true;
            }
            else
            {
                otherArgs.Add(arg);
            }
        }

        string? childArgs = otherArgs.Count > 0 ? string.Join(" ", otherArgs.Select(FormatArgument)) : null;

        if (hasConfig)
        {
            return new LaunchPlan(LaunchTarget.ConfigOnly, childArgs, false, false, true);
        }

        if (hasCapture && !hasEditor)
        {
            return new LaunchPlan(LaunchTarget.CaptureOnly, childArgs, true, false, false);
        }

        if (hasEditor && !hasCapture)
        {
            return new LaunchPlan(LaunchTarget.EditorOnly, childArgs, false, true, false);
        }

        return new LaunchPlan(LaunchTarget.Suite, childArgs, true, true, false);
    }

    /// <summary>
    /// Resuelve la ruta absoluta al ejecutable hermano en el directorio base, tolerando variantes de plataforma.
    /// </summary>
    public static string ResolveExecutablePath(string baseDirectory, string executableName)
    {
        string resolvedName = executableName;
        if (!OperatingSystem.IsWindows() && resolvedName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            resolvedName = resolvedName[..^4];
        }

        string fullPath = Path.Combine(baseDirectory, resolvedName);
        if (File.Exists(fullPath))
        {
            return fullPath;
        }

        string alternativeName = resolvedName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? resolvedName[..^4]
            : resolvedName + ".exe";
        string altPath = Path.Combine(baseDirectory, alternativeName);
        if (File.Exists(altPath))
        {
            return altPath;
        }

        return fullPath;
    }

    /// <summary>
    /// Ejecuta el despacho del plan asegurando o despertando los procesos correspondientes.
    /// </summary>
    public static async Task DispatchPlanAsync(LaunchPlan plan, string baseDirectory, IProcessLauncher launcher)
    {
        if (plan.ShouldEnsureCapture)
        {
            if (!launcher.IsInstanceActive(IpcChannels.Capture))
            {
                string capturePath = ResolveExecutablePath(baseDirectory, Constants.CaptureExecutableName);
                launcher.StartProcess(capturePath, null, baseDirectory);
            }
        }

        if (plan.ShouldEnsureEditor)
        {
            if (!launcher.IsInstanceActive(IpcChannels.Editor))
            {
                string editorPath = ResolveExecutablePath(baseDirectory, Constants.EditorExecutableName);
                launcher.StartProcess(editorPath, plan.AdditionalArguments, baseDirectory);
            }
            else
            {
                await launcher.SendWakeUpAsync(IpcChannels.Editor).ConfigureAwait(false);
            }
        }

        if (plan.ShouldEnsureConfig)
        {
            if (!launcher.IsInstanceActive(IpcChannels.Config))
            {
                string configPath = ResolveExecutablePath(baseDirectory, Constants.ConfigExecutableName);
                launcher.StartProcess(configPath, plan.AdditionalArguments, baseDirectory);
            }
            else
            {
                await launcher.SendWakeUpAsync(IpcChannels.Config).ConfigureAwait(false);
            }
        }
    }

    private static string FormatArgument(string arg)
    {
        if (string.IsNullOrEmpty(arg)) return "\"\"";
        if (arg.Contains(' ') && !arg.StartsWith('"'))
        {
            return $"\"{arg}\"";
        }
        return arg;
    }
}
