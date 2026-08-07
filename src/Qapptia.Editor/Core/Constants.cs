using Avalonia.Media;

namespace Qapptia.Editor.Core;

public static class Constants
{
    // Metrics
    public const double DefaultStrokeWidth = 4.0;
    public const double ArrowWingLen = 25.0;
    public const double GripSize = 6.0;
    public const double DrawMinDistance = 8.0;

    // Palette Colors matches Legacy App
    public static readonly Color ColorGreen = Color.Parse("#00FF00");
    public static readonly Color ColorRed = Color.Parse("#FF0000");
    public static readonly Color ColorBlue = Color.Parse("#0078D7");
    public static readonly Color ColorCyan = Color.Parse("#00B7C3");
    public static readonly Color ColorYellow = Color.Parse("#F7EB0C");
    public static readonly Color ColorOrange = Color.Parse("#FFA500");
    public static readonly Color ColorWhite = Color.Parse("#FFFFFF");
    public static readonly Color ColorBlack = Color.Parse("#000000");

    // Convierte el color de Avalonia a SkiaSharp
    public static SkiaSharp.SKColor ToSKColor(this Color c)
    {
        return new SkiaSharp.SKColor(c.R, c.G, c.B, c.A);
    }
}
