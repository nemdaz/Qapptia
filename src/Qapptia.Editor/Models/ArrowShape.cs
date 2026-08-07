using System;
using Avalonia;
using Avalonia.Media;
using Qapptia.Editor.Core;

namespace Qapptia.Editor.Models;

public class ArrowShape : VectorShape
{
    public override void RenderSkia(SkiaSharp.SKCanvas canvas)
    {
        using var paint = new SkiaSharp.SKPaint
        {
            Color = Color.ToSKColor(),
            StrokeWidth = (float)StrokeWidth,
            IsAntialias = true,
            Style = SkiaSharp.SKPaintStyle.Stroke,
            StrokeCap = SkiaSharp.SKStrokeCap.Round,
            StrokeJoin = SkiaSharp.SKStrokeJoin.Round,
            ImageFilter = SkiaSharp.SKImageFilter.CreateDropShadow(0, 1, 2, 2, SkiaSharp.SKColors.Black.WithAlpha(60))
        };

        canvas.DrawLine((float)Start.X, (float)Start.Y, (float)End.X, (float)End.Y, paint);
        
        DrawArrowHead(canvas, paint, (float)StrokeWidth);

        if (IsSelected)
        {
            HitTestEngine.DrawHandlesSkia(canvas, Start, End);
        }
    }

    private void DrawArrowHead(SkiaSharp.SKCanvas canvas, SkiaSharp.SKPaint paint, float width)
    {
        double dx = End.X - Start.X;
        double dy = End.Y - Start.Y;
        
        double arrowWingLen = Qapptia.Editor.Core.Constants.ArrowWingLen;
        
        // No dibujar si la flecha es muy corta
        if (Math.Sqrt(dx * dx + dy * dy) < Math.Max(arrowWingLen * 0.35, width * 2))
        {
            return;
        }

        double angle = Math.Atan2(dy, dx);
        
        // Ala 1
        var w1 = new SkiaSharp.SKPoint(
            (float)(End.X - arrowWingLen * Math.Cos(angle - Math.PI / 6)),
            (float)(End.Y - arrowWingLen * Math.Sin(angle - Math.PI / 6))
        );
        
        // Ala 2
        var w2 = new SkiaSharp.SKPoint(
            (float)(End.X - arrowWingLen * Math.Cos(angle + Math.PI / 6)),
            (float)(End.Y - arrowWingLen * Math.Sin(angle + Math.PI / 6))
        );

        using var path = new SkiaSharp.SKPath();
        path.MoveTo(w1);
        path.LineTo((float)End.X, (float)End.Y);
        path.LineTo(w2);

        canvas.DrawPath(path, paint);
    }

    public override bool HitTest(Point point)
    {
        double tolerance = StrokeWidth + 5.0;
        return HitTestEngine.PointToLineDistance(point, Start, End, tolerance);
    }
}
