using System;
using Avalonia;
using Qapptia.Editor.Core;
using Qapptia.Editor.Services;

namespace Qapptia.Editor.Models.Geometry;

/// <summary>
/// Geometría de flecha direccional de 2 nodos con punta (back, sin renderizado).
/// </summary>
public class ArrowGeometry : VectorGeometry
{
    public override HandleType HitTest(Point point)
    {
        if (IsSelected)
        {
            var handle = HitTestEngine.HitTestHandlesEnds(point, Start, End);
            if (handle != HandleType.None) return handle;
        }

        double tolerance = StrokeWidth + 8.0;

        // Verifica si toca el cuerpo de la flecha
        if (HitTestEngine.PointToLineDistance(point, Start, End, tolerance)) return HandleType.Body;

        var head = GetArrowHeadPoints();
        if (head != null)
        {
            if (HitTestEngine.PointToLineDistance(point, End, head.Value.w1, tolerance)) return HandleType.Body;

            if (HitTestEngine.PointToLineDistance(point, End, head.Value.w2, tolerance)) return HandleType.Body;
        }

        return HandleType.None;
    }

    public (Point w1, Point w2)? GetArrowHeadPoints()
    {
        double dx = End.X - Start.X;
        double dy = End.Y - Start.Y;

        double arrowWingLen = Constants.ArrowWingLen;

        // No dibujar punta si la flecha es muy corta
        if (Math.Sqrt(dx * dx + dy * dy) < Math.Max(arrowWingLen * 0.35, StrokeWidth * 2))
        {
            return null;
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

        return (w1, w2);
    }

}
