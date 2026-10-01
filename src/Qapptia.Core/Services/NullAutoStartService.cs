using Qapptia.Core.Abstractions;

namespace Qapptia.Core.Services;

/// <summary>
/// Implementación neutra (Null Object Pattern) de IAutoStartService
/// para entornos de prueba o plataformas sin soporte de autostart.
/// </summary>
public sealed class NullAutoStartService : IAutoStartService
{
    public static readonly NullAutoStartService Instance = new();

    public bool IsSupported => false;
    public bool IsAutoStartEnabled() => false;
    public bool SetAutoStartEnabled(bool enabled) => false;
}
