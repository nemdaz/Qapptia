using System;
using Avalonia;
using Avalonia.Media;
using SkiaSharp;
using Qapptia.Editor.Models;

namespace Qapptia.Editor.Core;

public static class HitTestEngine
{
    public static void DrawHandle(SKCanvas canvas, Point center)
    {
        float size = (float)Constants.GripSize * 1.5f * 1.3f; // 30% larger
        float radius = size / 2.0f;

        using var shadowPaint = new SKPaint
        {
            Color = SKColors.Black.WithAlpha(80),
            Style = SKPaintStyle.Fill,
            IsAntialias = true,
            MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 2f)
        };
        
        using var paint = new SKPaint
        {
            Color = SKColors.White,
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };

        // Draw shadow slightly offset
        canvas.DrawCircle((float)center.X, (float)center.Y + 1f, radius, shadowPaint);
        // Draw white circle
        canvas.DrawCircle((float)center.X, (float)center.Y, radius, paint);
    }

    public static void DrawHandlesSkiaEnds(SKCanvas canvas, Point start, Point end)
    {
        DrawHandle(canvas, start);
        DrawHandle(canvas, end);
    }

    public static void DrawHandlesSkiaCorners(SKCanvas canvas, Rect boundingBox)
    {
        DrawHandle(canvas, boundingBox.TopLeft);
        DrawHandle(canvas, boundingBox.TopRight);
        DrawHandle(canvas, boundingBox.BottomLeft);
        DrawHandle(canvas, boundingBox.BottomRight);
    }

    public static void DrawHandlesSkiaCenters(SKCanvas canvas, Rect boundingBox)
    {
        DrawHandle(canvas, new Point(boundingBox.Center.X, boundingBox.Top));
        DrawHandle(canvas, new Point(boundingBox.Center.X, boundingBox.Bottom));
        DrawHandle(canvas, new Point(boundingBox.Left, boundingBox.Center.Y));
        DrawHandle(canvas, new Point(boundingBox.Right, boundingBox.Center.Y));
    }

    public static void DrawHandlesSkiaSides(SKCanvas canvas, Rect boundingBox)
    {
        DrawHandle(canvas, new Point(boundingBox.Left, boundingBox.Center.Y));
        DrawHandle(canvas, new Point(boundingBox.Right, boundingBox.Center.Y));
    }

    public static bool HitTestHandle(Point pt, Point center)
    {
        float size = (float)Constants.GripSize * 2.0f * 1.3f; // 30% larger hit area
        var rect = new Rect(center.X - size / 2, center.Y - size / 2, size, size);
        return rect.Contains(pt);
    }

    public static HandleType HitTestHandlesEnds(Point pt, Point start, Point end)
    {
        if (HitTestHandle(pt, start)) return HandleType.Start;
        if (HitTestHandle(pt, end)) return HandleType.End;
        return HandleType.None;
    }

    public static HandleType HitTestHandlesCorners(Point pt, Rect boundingBox)
    {
        if (HitTestHandle(pt, boundingBox.TopLeft)) return HandleType.TopLeft;
        if (HitTestHandle(pt, boundingBox.TopRight)) return HandleType.TopRight;
        if (HitTestHandle(pt, boundingBox.BottomLeft)) return HandleType.BottomLeft;
        if (HitTestHandle(pt, boundingBox.BottomRight)) return HandleType.BottomRight;
        return HandleType.None;
    }

    public static HandleType HitTestHandlesCenters(Point pt, Rect boundingBox)
    {
        if (HitTestHandle(pt, new Point(boundingBox.Center.X, boundingBox.Top))) return HandleType.TopCenter;
        if (HitTestHandle(pt, new Point(boundingBox.Center.X, boundingBox.Bottom))) return HandleType.BottomCenter;
        if (HitTestHandle(pt, new Point(boundingBox.Left, boundingBox.Center.Y))) return HandleType.LeftCenter;
        if (HitTestHandle(pt, new Point(boundingBox.Right, boundingBox.Center.Y))) return HandleType.RightCenter;
        return HandleType.None;
    }

    public static HandleType HitTestHandlesSides(Point pt, Rect boundingBox)
    {
        if (HitTestHandle(pt, new Point(boundingBox.Left, boundingBox.Center.Y))) return HandleType.LeftCenter;
        if (HitTestHandle(pt, new Point(boundingBox.Right, boundingBox.Center.Y))) return HandleType.RightCenter;
        return HandleType.None;
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
