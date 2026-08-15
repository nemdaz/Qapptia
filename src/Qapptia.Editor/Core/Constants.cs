using Avalonia;
using Avalonia.Media;
using System.Collections.Generic;

namespace Qapptia.Editor.Core;

public static class Constants
{
    // Metrics
    public const double DefaultStrokeWidth = 4.0;
    public const double ArrowWingLen = 25.0;
    public const double GripSize = 6.0;
    public const double DrawMinDistance = 8.0;
    
    // Herramienta de texto (Anotaciones)
    public const string TextToolFontFamilyName = "Segoe UI";
    public static readonly SkiaSharp.SKTypeface TextToolTypeface = SkiaSharp.SKTypeface.FromFamilyName(TextToolFontFamilyName) ?? SkiaSharp.SKTypeface.Default;
    public static readonly Thickness TextToolPadding = new(4.0);
    public static readonly Thickness TextToolBorderThickness = new(1.0);
    public const double TextToolOffset = 5.0; // TextToolPadding (4) + TextToolBorderThickness (1)

    // Persistence
    public const string DrawingExtension = ".dibujo";

    // Shadows
    public static SkiaSharp.SKImageFilter CreateEditingShadow() => 
        SkiaSharp.SKImageFilter.CreateDropShadow(0, 1, 2, 2, SkiaSharp.SKColors.Black.WithAlpha(120));

    public static SkiaSharp.SKImageFilter CreateBurnedShadow() => 
        SkiaSharp.SKImageFilter.CreateDropShadow(0, 1, 2, 2, SkiaSharp.SKColors.Black.WithAlpha(40));

    // Palette Colors matches Legacy App
    public static readonly IReadOnlyList<Color> FavoriteColors = new[]
    {
        Color.Parse("#00FF00"), // Green
        Color.Parse("#FF0000"), // Red
        Color.Parse("#0078D7"), // Blue
        Color.Parse("#00B7C3"), // Cyan
        Color.Parse("#F7EB0C"), // Yellow
        Color.Parse("#FFA500"), // Orange
        Color.Parse("#FFFFFF"), // White
        Color.Parse("#000000")  // Black
    };

    // Convierte el color de Avalonia a SkiaSharp
    public static SkiaSharp.SKColor ToSKColor(this Color c)
    {
        return new SkiaSharp.SKColor(c.R, c.G, c.B, c.A);
    }

    // GetColorFamily was removed per user request for simpler persistence

    public static string GetColorName(Color c)
    {
        return $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";
    }

    public static Color ParseColorName(string name)
    {
        if (string.IsNullOrEmpty(name)) return FavoriteColors[1]; // Red fallback

        if (Color.TryParse(name, out var parsedHex))
        {
            return parsedHex;
        }
        
        return FavoriteColors[1]; // Default fallback to Red
    }
}
