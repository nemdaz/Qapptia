using System;
using Avalonia;
using Avalonia.Media;
using Qapptia.Editor.Models;

namespace Qapptia.Editor.Core;

/// <summary>
/// Fábrica centralizada para la instanciación de figuras vectoriales según la herramienta activa.
/// </summary>
public static class ShapeFactory
{
    public static VectorShape? Create(ToolType tool, Point startPoint, Color color, float textSize = 24f)
    {
        return tool switch
        {
            ToolType.Line => new LineShape { Start = startPoint, End = startPoint, Color = color },
            ToolType.Arrow => new ArrowShape { Start = startPoint, End = startPoint, Color = color },
            ToolType.Rectangle => new RectangleShape { Start = startPoint, End = startPoint, Color = color },
            ToolType.Ellipse => new EllipseShape { Start = startPoint, End = startPoint, Color = color },
            ToolType.Highlighter => new HighlighterShape { Start = startPoint, End = startPoint, Color = color },
            ToolType.Text => CreateAlignedTextInputShape(startPoint, color, textSize),
            _ => null
        };
    }

    private static TextShape CreateAlignedTextInputShape(Point clickPoint, Color color, float textSize)
    {
        using var font = TextShape.CreateSKFont(textSize);
        font.GetFontMetrics(out var metrics);
        float caretHeight = Math.Max(metrics.Descent - metrics.Ascent, font.Spacing * 0.9f);

        // Alinea el clic para que el ratón quede sobre el caret y evada el radio de colisión del nodo izquierdo.
        double startX = Math.Max(0, clickPoint.X - Constants.TextToolOffset - 5);
        double startY = Math.Max(0, clickPoint.Y - Constants.TextToolOffset - (caretHeight / 2.0));
        var alignedPoint = new Point(startX, startY);

        return new TextShape
        {
            Start = alignedPoint,
            End = new Point(startX + Constants.TextToolDefaultWidth, startY + 30),
            Color = color,
            TextSize = textSize
        };
    }
}
