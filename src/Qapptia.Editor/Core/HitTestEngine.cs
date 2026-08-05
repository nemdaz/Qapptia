using System;
using Avalonia;
using Avalonia.Media;

namespace Qapptia.Editor.Core;

public static class HitTestEngine
{
    public static void DrawHandles(DrawingContext context, Rect boundingBox)
    {
        var brush = new SolidColorBrush(Colors.Blue);
        double size = 8.0;
        double half = size / 2.0;

        // Esquinas
        context.DrawRectangle(brush, null, new Rect(boundingBox.Left - half, boundingBox.Top - half, size, size));
        context.DrawRectangle(brush, null, new Rect(boundingBox.Right - half, boundingBox.Top - half, size, size));
        context.DrawRectangle(brush, null, new Rect(boundingBox.Left - half, boundingBox.Bottom - half, size, size));
        context.DrawRectangle(brush, null, new Rect(boundingBox.Right - half, boundingBox.Bottom - half, size, size));
    }

    public static void DrawHandles(DrawingContext context, Point start, Point end)
    {
        var brush = new SolidColorBrush(Colors.Blue);
        double size = 8.0;
        double half = size / 2.0;

        context.DrawRectangle(brush, null, new Rect(start.X - half, start.Y - half, size, size));
        context.DrawRectangle(brush, null, new Rect(end.X - half, end.Y - half, size, size));
    }

    public static bool PointToLineDistance(Point pt, Point lineStart, Point lineEnd, double tolerance)
    {
        double l2 = DistanceSquared(lineStart, lineEnd);
        if (l2 == 0) return DistanceSquared(pt, lineStart) <= tolerance * tolerance;
        
        double t = Math.Max(0, Math.Min(1, DotProduct(pt, lineStart, lineEnd) / l2));
        var projection = new Point(
            lineStart.X + t * (lineEnd.X - lineStart.X),
            lineStart.Y + t * (lineEnd.Y - lineStart.Y)
        );
        
        return DistanceSquared(pt, projection) <= tolerance * tolerance;
    }

    private static double DistanceSquared(Point v, Point w)
    {
        double dx = v.X - w.X;
        double dy = v.Y - w.Y;
        return dx * dx + dy * dy;
    }

    private static double DotProduct(Point pt, Point lineStart, Point lineEnd)
    {
        return (pt.X - lineStart.X) * (lineEnd.X - lineStart.X) + (pt.Y - lineStart.Y) * (lineEnd.Y - lineStart.Y);
    }
}
