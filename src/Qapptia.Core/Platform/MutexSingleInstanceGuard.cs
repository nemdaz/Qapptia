using System.Threading;
using Qapptia.Core.Abstractions;

namespace Qapptia.Core.Platform;

/// <summary>
/// Single-instance guard cross-platform basado en <see cref="Mutex"/> named.
///
/// Comportamiento por SO:
/// - Windows: named mutex via kernel (Global\Qapptia.{key}). Sobrevive a crashes del proceso dueño.
/// - Linux/macOS: .NET emula con file lock en /tmp/. Los file locks (flock) se liberan automáticamente
///   al terminar el proceso (incluso en SIGKILL), por lo que la emulación es correcta.
///
/// El nombre se normaliza siempre a "Global\Qapptia.{key}" para consistencia.
/// </summary>
public sealed class MutexSingleInstanceGuard : ISingleInstanceGuard
{
    public string Key { get; }
    public bool IsHeld => _mutex != null;
    public int? CurrentOwnerPid => IsHeld ? Environment.ProcessId : null;

    private const string MutexPrefix = @"Global\Qapptia.";
    private Mutex? _mutex;
    private readonly string _mutexName;

    public MutexSingleInstanceGuard(string key)
    {
        Key = key ?? throw new ArgumentNullException(nameof(key));
        _mutexName = $"{MutexPrefix}{key}";
    }

    /// <summary>
    /// Comprueba de forma no bloqueante si ya existe una instancia activa asociada a la clave especificada.
    /// </summary>
    public static bool IsRunning(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return false;

        try
        {
            if (Mutex.TryOpenExisting($"{MutexPrefix}{key}", out var mutex))
            {
                mutex.Dispose();
                return true;
            }
        }
        catch
        {
            // Retorna false si la plataforma no soporta named mutexes o no existe la instancia.
        }

        return false;
    }

    public bool Acquire()
    {
        if (_mutex != null) return true;

        try
        {
            _mutex = new Mutex(initiallyOwned: false, name: _mutexName, createdNew: out var createdNew);
            try
            {
                // Espera 2s para permitir reinicio limpio mientras la instancia anterior cierra.
                if (!_mutex.WaitOne(TimeSpan.FromSeconds(2)))
                {
                    _mutex.Dispose();
                    _mutex = null;
                    return false;
                }
                return true;
            }
            catch (AbandonedMutexException)
            {
                // Wait tuvo éxito tras un crash o cierre abrupto de la instancia dueña anterior.
                return true;
            }
        }
        catch (UnauthorizedAccessException)
        {
            _mutex = null;
            return false;
        }
        catch (WaitHandleCannotBeOpenedException)
        {
            _mutex = null;
            return false;
        }
    }

    public void Release()
    {
        if (_mutex == null) return;
        try
        {
            if (_mutex.SafeWaitHandle.DangerousGetHandle() != IntPtr.Zero) _mutex.ReleaseMutex();
        }
        catch (ApplicationException)
        {
            // El thread actual no posee el mutex; ignorar.
        }
        finally
        {
            _mutex.Dispose();
            _mutex = null;
        }
    }

    public void Dispose() => Release();
}
