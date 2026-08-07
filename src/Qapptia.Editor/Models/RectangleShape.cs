using Avalonia;
using Avalonia.Media;
using Qapptia.Editor.Core;

namespace Qapptia.Editor.Models;

public class RectangleShape : VectorShape
{
    public override void RenderSkia(SkiaSharp.SKCanvas canvas)
    {
        using var paint = new SkiaSharp.SKPaint
        {
            Color = Color.ToSKColor(),
            StrokeWidth = (float)StrokeWidth,
            IsAntialias = true,
            Style = SkiaSharp.SKPaintStyle.Stroke,
            StrokeJoin = SkiaSharp.SKStrokeJoin.Round,
            ImageFilter = SkiaSharp.SKImageFilter.CreateDropShadow(0, 1, 2, 2, SkiaSharp.SKColors.Black.WithAlpha(60))
        };
        
        var rect = GetBoundingBox();
        var skRect = new SkiaSharp.SKRect((float)rect.Left, (float)rect.Top, (float)rect.Right, (float)rect.Bottom);
        
        canvas.DrawRect(skRect, paint);

        if (IsSelected)
        {
            HitTestEngine.DrawHandlesSkia(canvas, rect);
        }
    }

    public override bool HitTest(Point point)
    {
        var rect = GetBoundingBox();
        double tolerance = StrokeWidth + 5.0;
        
        var outerRect = rect.Inflate(tolerance);
        var innerRect = rect.Inflate(-tolerance);
        
        return outerRect.Contains(point) && !innerRect.Contains(point);
    }
}
