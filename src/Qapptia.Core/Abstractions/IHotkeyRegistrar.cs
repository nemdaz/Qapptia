using System;

namespace Qapptia.Core.Abstractions;

/// <summary>
/// Modificadores de tecla para un atajo global.
/// </summary>
[Flags]
public enum HotkeyModifiers
{
    None = 0,
    Alt = 1,
    Control = 2,
    Shift = 4,
    Win = 8,
}

/// <summary>
/// Representación de un atajo global registrado. Dispose lo elimina.
/// </summary>
public interface IHotkeyHandle : IDisposable
{
    HotkeyModifiers Modifiers { get; }
    uint VirtualKey { get; }
    bool IsRegistered { get; }
}

/// <summary>
/// Registra atajos de teclado globales (system-wide hotkeys). En Windows usa
/// <c>RegisterHotKey</c> + Message-only window en lugar de hooks low-level.
/// El callback se invoca en el thread de UI/bucle de mensajes.
/// </summary>
public interface IHotkeyRegistrar
{
    IHotkeyHandle Register(HotkeyModifiers modifiers, uint virtualKey, Action callback);
}
