using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Qapptia.Core.Ipc;
using Qapptia.Core.Platform;

namespace Qapptia.Core.Launcher;

/// <summary>
/// Implementación estándar de <see cref="IProcessLauncher"/> para el sistema operativo.
/// </summary>
public sealed class DefaultProcessLauncher : IProcessLauncher
{
    public bool IsInstanceActive(string channel)
    {
        return MutexSingleInstanceGuard.IsRunning(channel);
    }

    public bool StartProcess(string executablePath, string? arguments, string workingDirectory)
    {
        if (!File.Exists(executablePath))
        {
            return false;
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            Arguments = arguments ?? string.Empty,
            WorkingDirectory = workingDirectory,
            UseShellExecute = true,
        };

        using var proc = Process.Start(startInfo);
        return proc != null;
    }

    public async Task<bool> SendWakeUpAsync(string channel, int timeoutMs = 1000)
    {
        try
        {
            var response = await QapptiaIpcClient.SendAsync(channel, new WakeUpRequest(), timeoutMs).ConfigureAwait(false);
            return response is Ack;
        }
        catch
        {
            return false;
        }
    }
}
