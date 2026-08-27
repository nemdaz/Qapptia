using System;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using Qapptia.Editor.Core;
using Qapptia.Editor.Models;
using SkiaSharp;

namespace Qapptia.Editor.Tools;

/// <summary>
/// Herramienta interactiva de tipo Widget para entrada y manipulación de texto en el lienzo.
/// </summary>
public class TextWidgetTool : Tool
{
    public override string Id => "Text";
    public override string DisplayName => "Texto";
    public override string IconData => IconCatalog.Text;
    public override string? Shortcut => "T";
    public override ToolType Type => ToolType.Widget;
    public override StandardCursorType DefaultCursor => StandardCursorType.Ibeam;
    public override Type TargetShapeType => typeof(TextShape);

    /// <summary>
    /// Crea y alinea una nueva figura de texto según las coordenadas de clic y métricas de fuente.
    /// </summary>
    public virtual TextShape CreateTextShape(Point clickPoint, Color color, float textSize, SKTypeface? typeface)
    {
        var textShape = new TextShape
        {
            Color = color,
            TextSize = textSize,
            Typeface = typeface ?? SKTypeface.Default
        };

        using var font = textShape.CreateSKFont();
        font.GetFontMetrics(out var metrics);
        float caretHeight = Math.Max(metrics.Descent - metrics.Ascent, font.Spacing * 0.9f);

        // Alinea el clic para que el ratón quede sobre el caret y evada el radio de colisión del nodo izquierdo
        double startX = Math.Max(0, clickPoint.X - Constants.TextToolOffset - 5);
        double startY = Math.Max(0, clickPoint.Y - Constants.TextToolOffset - (caretHeight / 2.0));
        var alignedPoint = new Point(startX, startY);

        textShape.Start = alignedPoint;
        textShape.End = new Point(startX + Constants.TextToolDefaultWidth, startY + 30);
        return textShape;
    }
}
