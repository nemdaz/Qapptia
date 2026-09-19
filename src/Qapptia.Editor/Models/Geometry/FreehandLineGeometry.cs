using System;
using System.Collections.Generic;
using Avalonia;
using Qapptia.Editor.Core;
using Qapptia.Editor.Services;

namespace Qapptia.Editor.Models.Geometry;

/// <summary>
/// Geometría vectorial para trazo continuo a mano alzada compuesto por una secuencia de puntos.
/// </summary>
public class FreehandLineGeometry : VectorGeometry, IContinuableShape
{
    public List<Point> Points { get; set; } = new();

    public void AddPoint(Point point)
    {
        if (Points.Count == 0)
        {
            Start = point;
            End = point;
            Points.Add(point);
            return;
        }

        var last = Points[^1];
        double dx = point.X - last.X;
        double dy = point.Y - last.Y;
        // Filtrar puntos redundantes a menos de 2px de distancia
        if (dx * dx + dy * dy >= 4)
        {
            Points.Add(point);
            End = point;
        }
    }

    public void PrependPoint(Point point)
    {
        if (Points.Count == 0)
        {
            Start = point;
            End = point;
            Points.Add(point);
            return;
        }

        var first = Points[0];
        double dx = point.X - first.X;
        double dy = point.Y - first.Y;
        // Filtrar puntos redundantes a menos de 2px de distancia
        if (dx * dx + dy * dy >= 4)
        {
            Points.Insert(0, point);
            Start = point;
        }
    }

    protected override Rect GetBoundingBox()
    {
        if (Points.Count == 0) return new Rect(Start, End);

        double minX = double.MaxValue, maxX = double.MinValue;
        double minY = double.MaxValue, maxY = double.MinValue;

        for (int i = 0; i < Points.Count; i++)
        {
            var p = Points[i];
            if (p.X < minX) minX = p.X;
            if (p.X > maxX) maxX = p.X;
            if (p.Y < minY) minY = p.Y;
            if (p.Y > maxY) maxY = p.Y;
        }

        return new Rect(minX, minY, Math.Max(1.0, maxX - minX), Math.Max(1.0, maxY - minY));
    }

    public override HandleType HitTest(Point point, float zoom = 1.0f)
    {
        if (Points.Count < 2) return HandleType.None;

        if (IsSelected)
        {
            var handle = HitTestEngine.HitTestHandlesEnds(point, Start, End, zoom);
            if (handle != HandleType.None) return handle;
        }

        double tolerance = StrokeWidth + 8.0;
        for (int i = 0; i < Points.Count - 1; i++)
        {
            if (HitTestEngine.PointToLineDistance(point, Points[i], Points[i + 1], tolerance))
            {
                return HandleType.Body;
            }
        }

        return HandleType.None;
    }

    public override void Move(double dx, double dy)
    {
        base.Move(dx, dy);
        for (int i = 0; i < Points.Count; i++)
        {
            Points[i] = new Point(Points[i].X + dx, Points[i].Y + dy);
        }
    }

    public bool TryStartContinuation(HandleType handle)
    {
        if (handle == HandleType.Start)
        {
            Points.Reverse();
            if (Points.Count > 0)
            {
                Start = Points[0];
                End = Points[^1];
            }
            return true;
        }

        if (handle == HandleType.End)
        {
            return true;
        }

        return false;
    }

    public void ContinueDrawing(Point point)
    {
        AddPoint(point);
    }

    public bool ShouldCommitContinuation() => Points.Count >= 2;

    public override void DragHandle(HandleType handle, double dx, double dy, ref HandleType activeHandle)
    {
        if (handle == HandleType.Body)
        {
            Move(dx, dy);
        }
    }
}
