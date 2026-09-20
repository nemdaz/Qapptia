using System;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using Qapptia.Editor.Models.Geometry;

namespace Qapptia.Editor.Tools;

/// <summary>
/// Herramienta de borrador inteligente (estilo ShareX) que muestrea el color de fondo circundante
/// y genera una cobertura rectangular no destructiva que disimula texto, elementos o trazos.
/// </summary>
public sealed class SmartEraserTool : VectorTool<SmartEraserGeometry>
{
    public override string Id => "SmartEraser";
    public override string DisplayName => "Borrador inteligente";
    public override string? Shortcut => "W";

    public override Color ResolveInitialColor(Color activePaletteColor, Point startPoint, Func<Point, Color>? sampleCanvasColor = null)
    {
        return sampleCanvasColor?.Invoke(startPoint) ?? activePaletteColor;
    }

    public override void UpdateDrawing(VectorGeometry shape, Point currentPoint, KeyModifiers modifiers)
    {
        if (modifiers.HasFlag(KeyModifiers.Shift))
        {
            double dx = currentPoint.X - shape.Start.X;
            double dy = currentPoint.Y - shape.Start.Y;
            double size = Math.Max(Math.Abs(dx), Math.Abs(dy));

            double signX = dx >= 0 ? 1 : -1;
            double signY = dy >= 0 ? 1 : -1;

            shape.End = new Point(shape.Start.X + size * signX, shape.Start.Y + size * signY);
        }
        else
        {
            shape.End = currentPoint;
        }
    }
}
