using System;
using System.Collections.Generic;
using Avalonia;
using Qapptia.Editor.Core;
using Qapptia.Editor.Services;

namespace Qapptia.Editor.Models.Geometry;

/// <summary>
/// Geometría vectorial para trazo continuo a mano alzada compuesto por una secuencia de puntos.
/// </summary>
public class FreehandLineGeometry : VectorGeometry
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

        var bbox = BoundingBox;

        if (IsSelected)
        {
            var handle = HitTestEngine.HitTestHandlesCorners(point, bbox, zoom);
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

        if (IsSelected && bbox.Inflate(4.0).Contains(point))
        {
            return HandleType.Body;
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

    public override void DragHandle(HandleType handle, double dx, double dy, ref HandleType activeHandle)
    {
        if (handle == HandleType.Body)
        {
            Move(dx, dy);
            return;
        }

        if (Points.Count < 2) return;

        var oldBbox = BoundingBox;
        if (oldBbox.Width < 0.001 || oldBbox.Height < 0.001) return;

        double minX = oldBbox.Left;
        double maxX = oldBbox.Right;
        double minY = oldBbox.Top;
        double maxY = oldBbox.Bottom;

        bool flipX = false;
        bool flipY = false;

        if (handle == HandleType.TopLeft)
        {
            minX += dx; minY += dy;
            if (minX > maxX) flipX = true;
            if (minY > maxY) flipY = true;
        }
        else if (handle == HandleType.TopRight)
        {
            maxX += dx; minY += dy;
            if (maxX < minX) flipX = true;
            if (minY > maxY) flipY = true;
        }
        else if (handle == HandleType.BottomLeft)
        {
            minX += dx; maxY += dy;
            if (minX > maxX) flipX = true;
            if (maxY < minY) flipY = true;
        }
        else if (handle == HandleType.BottomRight)
        {
            maxX += dx; maxY += dy;
            if (maxX < minX) flipX = true;
            if (maxY < minY) flipY = true;
        }

        double newMinX = Math.Min(minX, maxX);
        double newMaxX = Math.Max(minX, maxX);
        double newMinY = Math.Min(minY, maxY);
        double newMaxY = Math.Max(minY, maxY);

        double newWidth = Math.Max(1.0, newMaxX - newMinX);
        double newHeight = Math.Max(1.0, newMaxY - newMinY);

        for (int i = 0; i < Points.Count; i++)
        {
            var p = Points[i];
            double u = (p.X - oldBbox.Left) / oldBbox.Width;
            double v = (p.Y - oldBbox.Top) / oldBbox.Height;

            if (flipX) u = 1.0 - u;
            if (flipY) v = 1.0 - v;

            Points[i] = new Point(newMinX + u * newWidth, newMinY + v * newHeight);
        }

        if (Points.Count > 0)
        {
            Start = Points[0];
            End = Points[^1];
        }

        if (flipX)
        {
            if (activeHandle == HandleType.TopLeft) activeHandle = HandleType.TopRight;
            else if (activeHandle == HandleType.TopRight) activeHandle = HandleType.TopLeft;
            else if (activeHandle == HandleType.BottomLeft) activeHandle = HandleType.BottomRight;
            else if (activeHandle == HandleType.BottomRight) activeHandle = HandleType.BottomLeft;
        }

        if (flipY)
        {
            if (activeHandle == HandleType.TopLeft) activeHandle = HandleType.BottomLeft;
            else if (activeHandle == HandleType.BottomLeft) activeHandle = HandleType.TopLeft;
            else if (activeHandle == HandleType.TopRight) activeHandle = HandleType.BottomRight;
            else if (activeHandle == HandleType.BottomRight) activeHandle = HandleType.TopRight;
        }
    }
}
