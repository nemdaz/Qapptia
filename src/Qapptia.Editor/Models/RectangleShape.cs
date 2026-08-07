using Avalonia;
using Avalonia.Media;
using Qapptia.Editor.Core;

namespace Qapptia.Editor.Models;

public class RectangleShape : VectorShape
{
    public override void Render(DrawingContext context)
    {
        var pen = new Pen(new SolidColorBrush(Color), StrokeWidth, lineJoin: PenLineJoin.Round);
        var rect = GetBoundingBox();
        context.DrawRectangle(null, pen, rect);

        if (IsSelected)
        {
            HitTestEngine.DrawHandles(context, rect);
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
