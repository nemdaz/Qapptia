using System;
using System.Globalization;

namespace Qapptia.Core.Extensions;

public static class StringExtensions
{
    /// <summary>
    /// Formatea una cadena de atajo (ej. "ctrl+shift+p") usando el formateador nativo de C# (TitleCase) para que se vea como "Ctrl+Shift+P".
    /// </summary>
    public static string ToShortcutTitleCase(this string shortcut)
    {
        if (string.IsNullOrWhiteSpace(shortcut)) 
            return string.Empty;
        
        var parts = shortcut.Split('+');
        var textInfo = CultureInfo.InvariantCulture.TextInfo;

        for (int i = 0; i < parts.Length; i++)
        {
            var p = parts[i].Trim();
            if (p.Length > 0)
            {
                // Uso del formateador nativo de C# para TitleCase
                parts[i] = textInfo.ToTitleCase(p.ToLowerInvariant());
            }
        }
        return string.Join("+", parts);
    }
}
