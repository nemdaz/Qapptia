using System;
using Avalonia;
using Avalonia.Media;
using Qapptia.Editor.Core;
using Qapptia.Editor.Services;

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
            ImageFilter = IsBurning ? Constants.CreateBurnedShadow() : Constants.CreateEditingShadow()
        };

        canvas.DrawLine((float)Start.X, (float)Start.Y, (float)End.X, (float)End.Y, paint);
        
        DrawArrowHead(canvas, paint, (float)StrokeWidth);

        if (IsSelected)
        {
            HitTestEngine.DrawHandlesSkiaEnds(canvas, Start, End);
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

    public override HandleType HitTest(Point point)
    {
        if (IsSelected)
        {
            var handle = HitTestEngine.HitTestHandlesEnds(point, Start, End);
            if (handle != HandleType.None) return handle;
        }

        double tolerance = StrokeWidth + 8.0;
        
        // Verifica si toca el cuerpo de la flecha
        if (HitTestEngine.PointToLineDistance(point, Start, End, tolerance))
            return HandleType.Body;
            
        // Verifica si toca las alas de la flecha
        double dx = Start.X - End.X;
        double dy = Start.Y - End.Y;
        double angle = Math.Atan2(dy, dx);
        
        double wing1Angle = angle - Math.PI / 6;
        Point wing1 = new Point(
            End.X + Qapptia.Editor.Core.Constants.ArrowWingLen * Math.Cos(wing1Angle),
            End.Y + Qapptia.Editor.Core.Constants.ArrowWingLen * Math.Sin(wing1Angle)
        );
        
        if (HitTestEngine.PointToLineDistance(point, End, wing1, tolerance))
            return HandleType.Body;
            
        double wing2Angle = angle + Math.PI / 6;
        Point wing2 = new Point(
            End.X + Qapptia.Editor.Core.Constants.ArrowWingLen * Math.Cos(wing2Angle),
            End.Y + Qapptia.Editor.Core.Constants.ArrowWingLen * Math.Sin(wing2Angle)
        );
        
        if (HitTestEngine.PointToLineDistance(point, End, wing2, tolerance))
            return HandleType.Body;
            
        return HandleType.None;
    }
}
