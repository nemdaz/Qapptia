namespace Qapptia.Core.Abstractions;

/// <summary>
/// Guard de single-instance por nombre. <c>Acquire</c> intenta tomar el recurso,
/// devuelve false si otra instancia ya lo tiene. <c>Dispose</c> libera.
/// En Windows: <c>Mutex Global\Qapptia.*</c>; Linux: lock file en <c>~/.local/share</c>.
/// </summary>
public interface ISingleInstanceGuard : IDisposable
{
    string Key { get; }
    bool Acquire();
    bool IsHeld { get; }
    void Release();
    int? CurrentOwnerPid { get; }
}
