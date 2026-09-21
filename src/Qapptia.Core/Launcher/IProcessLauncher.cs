using System.Threading.Tasks;

namespace Qapptia.Core.Launcher;

/// <summary>
/// Contrato abstracto para el ciclo de vida de procesos e interacción IPC del launcher.
/// </summary>
public interface IProcessLauncher
{
    /// <summary>
    /// Comprueba si ya existe una instancia activa asociada al canal especificado.
    /// </summary>
    bool IsInstanceActive(string channel);

    /// <summary>
    /// Inicia un proceso hijo de forma desacoplada.
    /// </summary>
    bool StartProcess(string executablePath, string? arguments, string workingDirectory);

    /// <summary>
    /// Envía una solicitud de reactivación/foco a la instancia existente.
    /// </summary>
    Task<bool> SendWakeUpAsync(string channel, int timeoutMs = 1000);
}
