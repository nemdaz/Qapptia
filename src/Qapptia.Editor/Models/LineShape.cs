using System;
using Avalonia;
using Avalonia.Media;
using Qapptia.Editor.Core;

namespace Qapptia.Editor.Models;

public class LineShape : VectorShape
{
    public override void Render(DrawingContext context)
    {
        var pen = new Pen(new SolidColorBrush(Color), StrokeWidth, lineCap: PenLineCap.Round);
        context.DrawLine(pen, Start, End);

        if (IsSelected)
        {
            HitTestEngine.DrawHandles(context, Start, End);
        }
    }

    public override bool HitTest(Point point)
    {
        double tolerance = StrokeWidth + 5.0;
        return HitTestEngine.PointToLineDistance(point, Start, End, tolerance);
    }
}
