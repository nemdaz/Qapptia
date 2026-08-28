using System.Collections.Generic;
using SkiaSharp;

namespace Qapptia.Editor.Core;

public interface IFontProvider
{
    /// <summary>
    /// Obtiene una fuente gestionando su carga y caché.
    /// </summary>
    SKTypeface GetTypeface(string fontName, string fallbackFontFamily = "Segoe UI", bool forceReload = false);

    /// <summary>
    /// Obtiene los nombres de las fuentes actualmente cacheadas.
    /// </summary>
    IReadOnlyList<string> GetLoadedFonts();
}
