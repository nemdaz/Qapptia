using System;
using System.Collections.Generic;

namespace Qapptia.Core.Theme;

public static class ThemeConstants
{
    // Códigos internos persistidos en config.json
    public const string Dark = "dark";
    public const string Light = "light";
    public const string System = "system";

    // Nombres visuales para la interfaz de usuario
    public const string DisplayNameSystem = "Sistema";
    public const string DisplayNameDark = "Oscuro";
    public const string DisplayNameLight = "Claro";

    // Opciones disponibles para selectores de tema
    public static readonly IReadOnlyList<string> DisplayNames = new[]
    {
        DisplayNameSystem,
        DisplayNameDark,
        DisplayNameLight
    };

    // Convierte código interno a nombre para mostrar
    public static string ToDisplayName(string? theme)
    {
        var normalized = Normalize(theme);
        return normalized switch
        {
            Dark => DisplayNameDark,
            Light => DisplayNameLight,
            _ => DisplayNameSystem
        };
    }

    // Convierte nombre para mostrar a código interno
    public static string FromDisplayName(string? displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName)) return System;
        return displayName.Trim() switch
        {
            DisplayNameDark => Dark,
            DisplayNameLight => Light,
            _ => System
        };
    }

    // Normaliza el string de código de tema
    public static string Normalize(string? theme)
    {
        if (string.IsNullOrWhiteSpace(theme)) return System;
        var lower = theme.Trim().ToLowerInvariant();
        return lower switch
        {
            Dark => Dark,
            Light => Light,
            _ => System
        };
    }
}
