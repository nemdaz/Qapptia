using System;
using Avalonia;
using Avalonia.Media;
using Qapptia.Editor.Core;

namespace Qapptia.Editor.Models;

public class ArrowShape : VectorShape
{
    public override void Render(DrawingContext context)
    {
        var pen = new Pen(new SolidColorBrush(Color), StrokeWidth, lineCap: PenLineCap.Round);
        context.DrawLine(pen, Start, End);
        
        DrawArrowHead(context, Color, StrokeWidth);

        if (IsSelected)
        {
            HitTestEngine.DrawHandles(context, Start, End);
        }
    }

    private void DrawArrowHead(DrawingContext context, Color color, double width)
    {
        double dx = End.X - Start.X;
        double dy = End.Y - Start.Y;
        double angle = Math.Atan2(dy, dx);
        
        double arrowSize = width * 3.0 + 5.0;

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(End, true);
            ctx.LineTo(new Point(
                End.X - arrowSize * Math.Cos(angle - Math.PI / 6),
                End.Y - arrowSize * Math.Sin(angle - Math.PI / 6)
            ));
            ctx.LineTo(new Point(
                End.X - arrowSize * Math.Cos(angle + Math.PI / 6),
                End.Y - arrowSize * Math.Sin(angle + Math.PI / 6)
            ));
            ctx.EndFigure(true);
        }

        context.DrawGeometry(new SolidColorBrush(color), null, geometry);
    }

    public override bool HitTest(Point point)
    {
        double tolerance = StrokeWidth + 5.0;
        return HitTestEngine.PointToLineDistance(point, Start, End, tolerance);
    }
}
