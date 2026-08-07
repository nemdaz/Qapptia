using System;
using Avalonia;
using Avalonia.Media;

namespace Qapptia.Editor.Core;

public static class HitTestEngine
{
    public static void DrawHandlesSkia(SkiaSharp.SKCanvas canvas, Rect boundingBox)
    {
        using var paint = new SkiaSharp.SKPaint { Color = SkiaSharp.SKColors.Blue, Style = SkiaSharp.SKPaintStyle.Fill };
        float size = (float)Qapptia.Editor.Core.Constants.GripSize;
        float half = size / 2.0f;

        // Esquinas
        canvas.DrawRect(new SkiaSharp.SKRect((float)boundingBox.Left - half, (float)boundingBox.Top - half, (float)boundingBox.Left + half, (float)boundingBox.Top + half), paint);
        canvas.DrawRect(new SkiaSharp.SKRect((float)boundingBox.Right - half, (float)boundingBox.Top - half, (float)boundingBox.Right + half, (float)boundingBox.Top + half), paint);
        canvas.DrawRect(new SkiaSharp.SKRect((float)boundingBox.Left - half, (float)boundingBox.Bottom - half, (float)boundingBox.Left + half, (float)boundingBox.Bottom + half), paint);
        canvas.DrawRect(new SkiaSharp.SKRect((float)boundingBox.Right - half, (float)boundingBox.Bottom - half, (float)boundingBox.Right + half, (float)boundingBox.Bottom + half), paint);
    }

    public static void DrawHandlesSkia(SkiaSharp.SKCanvas canvas, Point start, Point end)
    {
        using var paint = new SkiaSharp.SKPaint { Color = SkiaSharp.SKColors.Blue, Style = SkiaSharp.SKPaintStyle.Fill };
        float size = (float)Qapptia.Editor.Core.Constants.GripSize;
        float half = size / 2.0f;

        // Extremos
        canvas.DrawRect(new SkiaSharp.SKRect((float)start.X - half, (float)start.Y - half, (float)start.X + half, (float)start.Y + half), paint);
        canvas.DrawRect(new SkiaSharp.SKRect((float)end.X - half, (float)end.Y - half, (float)end.X + half, (float)end.Y + half), paint);
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
