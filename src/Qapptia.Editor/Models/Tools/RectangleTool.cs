using System;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using Qapptia.Editor.Models;

namespace Qapptia.Editor.Tools;

/// <summary>
/// Herramienta vectorial para trazar rectángulos y cuadrados (con tecla Shift).
/// </summary>
public sealed class RectangleTool : VectorTool
{
    public override string Id => "Rectangle";
    public override string DisplayName => "Rectángulo";
    public override string IconData => IconCatalog.Rectangle;
    public override string? Shortcut => "R";

    public override VectorShape CreateShape(Point startPoint, Color color)
    {
        return new RectangleShape
        {
            Start = startPoint,
            End = startPoint,
            Color = color
        };
    }

    public override void UpdateDrawing(VectorShape shape, Point currentPoint, KeyModifiers modifiers)
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
