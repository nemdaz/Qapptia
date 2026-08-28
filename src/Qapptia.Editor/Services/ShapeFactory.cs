using System;
using Avalonia;
using Avalonia.Media;
using Qapptia.Editor.Models;
using Qapptia.Editor.Models.Geometry;
using Qapptia.Editor.Tools;

namespace Qapptia.Editor.Services;

/// <summary>
/// Fábrica centralizada para la instanciación de figuras vectoriales delegando en las clases de herramientas correspondientes.
/// </summary>
public static class ShapeFactory
{
    public static readonly LineTool Line = new();
    public static readonly ArrowTool Arrow = new();
    public static readonly RectangleTool Rectangle = new();
    public static readonly EllipseTool Ellipse = new();
    public static readonly HighlighterTool Highlighter = new();
    public static readonly TextWidgetTool Text = new();
    public static readonly CropTool Crop = new();
    public static readonly RotateTool Rotate = new(() => { });

    public static VectorGeometry? Create(Tool tool, Point startPoint, Color color, float textSize = 24f, SkiaSharp.SKTypeface? typeface = null)
    {
        ArgumentNullException.ThrowIfNull(tool);

        if (tool is VectorTool vectorTool)
        {
            return vectorTool.CreateShape(startPoint, color);
        }

        if (tool is TextWidgetTool textTool)
        {
            return textTool.CreateTextShape(startPoint, color, textSize, typeface);
        }

        return null;
    }

    public static VectorGeometry? Create(string toolId, Point startPoint, Color color, float textSize = 24f, SkiaSharp.SKTypeface? typeface = null)
    {
        return toolId?.ToLowerInvariant() switch
        {
            "line" => Line.CreateShape(startPoint, color),
            "arrow" => Arrow.CreateShape(startPoint, color),
            "rectangle" => Rectangle.CreateShape(startPoint, color),
            "ellipse" => Ellipse.CreateShape(startPoint, color),
            "highlighter" => Highlighter.CreateShape(startPoint, color),
            "text" => Text.CreateTextShape(startPoint, color, textSize, typeface),
            _ => null
        };
    }
}
