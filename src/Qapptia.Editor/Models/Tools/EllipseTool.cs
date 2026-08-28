using System;
using Avalonia;
using Avalonia.Input;
using Qapptia.Editor.Models.Geometry;

namespace Qapptia.Editor.Tools;

/// <summary>
/// Herramienta vectorial para trazar elipses y círculos perfectos (con tecla Shift).
/// </summary>
public sealed class EllipseTool : VectorTool<EllipseGeometry>
{
    public override string Id => "Ellipse";
    public override string DisplayName => "Elipse";
    public override string IconData => IconCatalog.Ellipse;
    public override string? Shortcut => "E";

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
