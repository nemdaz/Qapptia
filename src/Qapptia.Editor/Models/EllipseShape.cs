using System;
using Avalonia;
using Avalonia.Media;
using Qapptia.Editor.Core;

namespace Qapptia.Editor.Models;

public class EllipseShape : VectorShape
{
    public override void RenderSkia(SkiaSharp.SKCanvas canvas)
    {
        using var paint = new SkiaSharp.SKPaint
        {
            Color = Color.ToSKColor(),
            StrokeWidth = (float)StrokeWidth,
            IsAntialias = true,
            Style = SkiaSharp.SKPaintStyle.Stroke,
            ImageFilter = IsBurning ? Constants.CreateBurnedShadow() : Constants.CreateEditingShadow()
        };
        
        var rect = GetBoundingBox();
        
        float cx = (float)rect.Center.X;
        float cy = (float)rect.Center.Y;
        float rx = (float)(rect.Width / 2);
        float ry = (float)(rect.Height / 2);
        
        canvas.DrawOval(cx, cy, rx, ry, paint);

        if (IsSelected)
        {
            HitTestEngine.DrawHandlesSkiaCenters(canvas, rect);
        }
    }

    public override HandleType HitTest(Point point)
    {
        var rect = GetBoundingBox();
        if (IsSelected)
        {
            var handle = HitTestEngine.HitTestHandlesCenters(point, rect);
            if (handle != HandleType.None) return handle;
        }

        var center = rect.Center;
        double rx = rect.Width / 2;
        double ry = rect.Height / 2;
        
        if (rx <= 0 || ry <= 0) return HandleType.None;
        
        // Ecuación de la elipse: (x-cx)^2 / rx^2 + (y-cy)^2 / ry^2 = 1
        double dx = point.X - center.X;
        double dy = point.Y - center.Y;

        double value = (dx * dx) / (rx * rx) + (dy * dy) / (ry * ry);
        
        // Tolerancia para el hit test del borde
        double tolerance = (StrokeWidth + 5.0) / Math.Max(rx, ry);
        
        if (value >= (1.0 - tolerance) && value <= (1.0 + tolerance))
        {
            return HandleType.Body;
        }
        return HandleType.None;
    }
}
