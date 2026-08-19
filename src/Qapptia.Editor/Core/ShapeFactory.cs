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
    public static VectorShape? Create(ToolType tool, Point startPoint, Color color)
    {
        return tool switch
        {
            ToolType.Line => new LineShape { Start = startPoint, End = startPoint, Color = color },
            ToolType.Arrow => new ArrowShape { Start = startPoint, End = startPoint, Color = color },
            ToolType.Rectangle => new RectangleShape { Start = startPoint, End = startPoint, Color = color },
            ToolType.Ellipse => new EllipseShape { Start = startPoint, End = startPoint, Color = color },
            ToolType.Highlighter => new HighlighterShape { Start = startPoint, End = startPoint, Color = color },
            ToolType.Text => CreateAlignedTextInputShape(startPoint, color),
            _ => null
        };
    }

    private static TextShape CreateAlignedTextInputShape(Point clickPoint, Color color)
    {
        using var font = TextShape.CreateSKFont(24);
        font.GetFontMetrics(out var metrics);
        float caretHeight = Math.Max(metrics.Descent - metrics.Ascent, font.Spacing * 0.9f);

        // Alinea el punto de clic exactamente con el inicio horizontal y el centro vertical de la primera línea de texto
        double startX = Math.Max(0, clickPoint.X - Constants.TextToolOffset);
        double startY = Math.Max(0, clickPoint.Y - Constants.TextToolOffset - (caretHeight / 2.0));
        var alignedPoint = new Point(startX, startY);

        return new TextShape
        {
            Start = alignedPoint,
            End = alignedPoint,
            Color = color,
            TextSize = 24
        };
    }
}
