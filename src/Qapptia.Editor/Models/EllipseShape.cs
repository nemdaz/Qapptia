using System;
using Avalonia;
using Avalonia.Media;
using Qapptia.Editor.Core;

namespace Qapptia.Editor.Models;

public class EllipseShape : VectorShape
{
    public override void Render(DrawingContext context)
    {
        var pen = new Pen(new SolidColorBrush(Color), StrokeWidth);
        var rect = GetBoundingBox();
        var center = rect.Center;
        double radiusX = rect.Width / 2;
        double radiusY = rect.Height / 2;
        
        context.DrawEllipse(null, pen, center, radiusX, radiusY);

        if (IsSelected)
        {
            HitTestEngine.DrawHandles(context, rect);
        }
    }

    public override bool HitTest(Point point)
    {
        var rect = GetBoundingBox();
        var center = rect.Center;
        double rx = rect.Width / 2;
        double ry = rect.Height / 2;
        
        if (rx <= 0 || ry <= 0) return false;

        // Ecuación de la elipse: (x-cx)^2 / rx^2 + (y-cy)^2 / ry^2 = 1
        double dx = point.X - center.X;
        double dy = point.Y - center.Y;

        double value = (dx * dx) / (rx * rx) + (dy * dy) / (ry * ry);
        
        // Tolerancia para el hit test del borde
        double tolerance = (StrokeWidth + 5.0) / Math.Max(rx, ry);
        
        return value >= (1.0 - tolerance) && value <= (1.0 + tolerance);
    }
}
