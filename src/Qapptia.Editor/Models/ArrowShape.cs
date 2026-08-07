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
        
        DrawArrowHead(context, pen, StrokeWidth);

        if (IsSelected)
        {
            HitTestEngine.DrawHandles(context, Start, End);
        }
    }

    private void DrawArrowHead(DrawingContext context, Pen pen, double width)
    {
        double dx = End.X - Start.X;
        double dy = End.Y - Start.Y;
        
        double arrowWingLen = Qapptia.Editor.Core.Constants.ArrowWingLen;
        
        // No dibujar si la flecha es demasiado corta
        if (Math.Sqrt(dx * dx + dy * dy) < Math.Max(arrowWingLen * 0.35, width * 2))
        {
            return;
        }

        double angle = Math.Atan2(dy, dx);
        
        // Ala 1
        var w1 = new Point(
            End.X - arrowWingLen * Math.Cos(angle - Math.PI / 6),
            End.Y - arrowWingLen * Math.Sin(angle - Math.PI / 6)
        );
        
        // Ala 2
        var w2 = new Point(
            End.X - arrowWingLen * Math.Cos(angle + Math.PI / 6),
            End.Y - arrowWingLen * Math.Sin(angle + Math.PI / 6)
        );

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(w1, false);
            ctx.LineTo(End);
            ctx.LineTo(w2);
            ctx.EndFigure(false);
        }

        var arrowPen = new Pen(pen.Brush, pen.Thickness, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        context.DrawGeometry(null, arrowPen, geometry);
    }

    public override bool HitTest(Point point)
    {
        double tolerance = StrokeWidth + 5.0;
        return HitTestEngine.PointToLineDistance(point, Start, End, tolerance);
    }
}
