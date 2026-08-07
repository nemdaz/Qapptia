using System;
using Avalonia;
using Avalonia.Media;
using Qapptia.Editor.Core;

namespace Qapptia.Editor.Models;

public class LineShape : VectorShape
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
            ImageFilter = SkiaSharp.SKImageFilter.CreateDropShadow(0, 1, 2, 2, SkiaSharp.SKColors.Black.WithAlpha(60))
        };
        
        canvas.DrawLine((float)Start.X, (float)Start.Y, (float)End.X, (float)End.Y, paint);

        if (IsSelected)
        {
            HitTestEngine.DrawHandlesSkiaEnds(canvas, Start, End);
        }
    }

    public override HandleType HitTest(Point point)
    {
        if (IsSelected)
        {
            var handle = HitTestEngine.HitTestHandlesEnds(point, Start, End);
            if (handle != HandleType.None) return handle;
        }
        
        if (HitTestEngine.PointToLineDistance(point, Start, End, StrokeWidth + 5.0))
        {
            return HandleType.Body;
        }
        return HandleType.None;
    }
}
