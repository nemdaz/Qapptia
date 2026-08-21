using Avalonia;
using Avalonia.Styling;
using Qapptia.Core.Theme;

namespace Qapptia.UI.Components.Theme;

public static class ThemeManager
{
    // Obtiene el ThemeVariant de Avalonia correspondiente al string de tema
    public static ThemeVariant GetThemeVariant(string? theme)
    {
        var normalized = ThemeConstants.Normalize(theme);
        return normalized switch
        {
            ThemeConstants.Dark => ThemeVariant.Dark,
            ThemeConstants.Light => ThemeVariant.Light,
            _ => ThemeVariant.Default
        };
    }

    // Aplica el tema inmediatamente a la aplicación Avalonia activa
    public static void ApplyTheme(string? theme)
    {
        if (Application.Current is null) return;
        Application.Current.RequestedThemeVariant = GetThemeVariant(theme);
    }
}
