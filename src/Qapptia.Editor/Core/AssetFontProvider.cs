using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Serilog;
using SkiaSharp;

namespace Qapptia.Editor.Core;

public class AssetFontProvider : IFontProvider
{
    private readonly ConcurrentDictionary<string, SKTypeface> _fontCache = new();
    private readonly ILogger _logger;

    public AssetFontProvider(ILogger logger)
    {
        _logger = logger;
    }

    public SKTypeface GetTypeface(string fontName, string fallbackFontFamily = "Segoe UI", bool forceReload = false)
    {
        if (!forceReload && _fontCache.TryGetValue(fontName, out var cachedTypeface))
        {
            return cachedTypeface;
        }

        var assembly = typeof(AssetFontProvider).Assembly;
        var resourceName = $"Qapptia.Editor.Assets.Fonts.{fontName}";

        try
        {
            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream != null)
            {
                // Convertir a SKData asegura compatibilidad nativa con flujos no administrados.
                using var skData = SKData.Create(stream);
                var typeface = SKTypeface.FromData(skData);

                if (typeface != null)
                {
                    _fontCache[fontName] = typeface;
                    _logger.Information("Fuente embebida cargada exitosamente: '{FontName}'", fontName);
                    return typeface;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error al cargar la fuente embebida '{FontName}'", fontName);
        }

        _logger.Warning("No se pudo cargar la fuente embebida '{FontName}'. Usando fallback: '{FallbackFontFamily}'", fontName, fallbackFontFamily);

        var fallbackTypeface = SKTypeface.FromFamilyName(fallbackFontFamily) ?? SKTypeface.Default;
        _fontCache[fontName] = fallbackTypeface;
        return fallbackTypeface;
    }

    public IReadOnlyList<string> GetLoadedFonts()
    {
        return _fontCache.Keys.ToList();
    }
}
