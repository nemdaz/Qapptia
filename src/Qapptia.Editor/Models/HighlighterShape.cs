using Avalonia;
using Avalonia.Media;
using Qapptia.Editor.Core;

namespace Qapptia.Editor.Models;

public class HighlighterShape : VectorShape
{
    public override void RenderSkia(SkiaSharp.SKCanvas canvas)
    {
        using var paint = new SkiaSharp.SKPaint
        {
            Color = Color.ToSKColor().WithAlpha(Constants.HighlighterAlpha),
            IsAntialias = true,
            Style = SkiaSharp.SKPaintStyle.Fill
        };
        
        var rect = GetBoundingBox();
        var skRect = new SkiaSharp.SKRect((float)rect.Left, (float)rect.Top, (float)rect.Right, (float)rect.Bottom);
        
        canvas.DrawRect(skRect, paint);

        if (IsSelected)
        {
            HitTestEngine.DrawHandlesSkiaCorners(canvas, rect);
        }
    }

    public override HandleType HitTest(Point point)
    {
        var rect = GetBoundingBox();
        if (IsSelected)
        {
            var handle = HitTestEngine.HitTestHandlesCorners(point, rect);
            if (handle != HandleType.None) return handle;
        }
        
        if (rect.Inflate(4).Contains(point))
        {
            return HandleType.Body;
        }
        return HandleType.None;
    }
}
