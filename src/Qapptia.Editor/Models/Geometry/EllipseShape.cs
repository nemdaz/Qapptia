using System;
using Avalonia;
using Avalonia.Media;
using Qapptia.Editor.Core;
using Qapptia.Editor.Services;

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
        
        double dx = point.X - center.X;
        double dy = point.Y - center.Y;

        // Distancia radial exacta al contorno de la elipse en píxeles
        double angle = Math.Atan2(dy, dx);
        double ellipseX = center.X + rx * Math.Cos(angle);
        double ellipseY = center.Y + ry * Math.Sin(angle);
        double distSq = (point.X - ellipseX) * (point.X - ellipseX) + (point.Y - ellipseY) * (point.Y - ellipseY);
        
        double tolerance = StrokeWidth + 8.0;
        if (distSq <= tolerance * tolerance)
        {
            return HandleType.Body;
        }
        return HandleType.None;
    }
}
