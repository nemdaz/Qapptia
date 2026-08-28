using System;
using Avalonia;
using Qapptia.Editor.Core;
using Qapptia.Editor.Models;

namespace Qapptia.Editor.Services;

/// <summary>
/// Motor de cálculo puro para la detección de impacto (hit-testing) de manetas de control
/// y cuerpos geométricos. No realiza renderizado; el dibujado de manetas reside en la capa
/// de presentación.
/// </summary>
public static class HitTestEngine
{
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

    public static bool HitTestRectPerimeter(Point pt, Rect rect, double tolerance)
    {
        if (PointToLineDistance(pt, rect.TopLeft, rect.TopRight, tolerance)) return true;
        if (PointToLineDistance(pt, rect.TopRight, rect.BottomRight, tolerance)) return true;
        if (PointToLineDistance(pt, rect.BottomRight, rect.BottomLeft, tolerance)) return true;
        if (PointToLineDistance(pt, rect.BottomLeft, rect.TopLeft, tolerance)) return true;
        return false;
    }

    public static HandleType HitTestCrop(Point pt, Rect cropRect)
    {
        if (HitTestHandle(pt, cropRect.TopLeft)) return HandleType.TopLeft;
        if (HitTestHandle(pt, cropRect.TopRight)) return HandleType.TopRight;
        if (HitTestHandle(pt, cropRect.BottomLeft)) return HandleType.BottomLeft;
        if (HitTestHandle(pt, cropRect.BottomRight)) return HandleType.BottomRight;

        if (HitTestHandle(pt, new Point(cropRect.Center.X, cropRect.Top))) return HandleType.TopCenter;
        if (HitTestHandle(pt, new Point(cropRect.Center.X, cropRect.Bottom))) return HandleType.BottomCenter;
        if (HitTestHandle(pt, new Point(cropRect.Left, cropRect.Center.Y))) return HandleType.LeftCenter;
        if (HitTestHandle(pt, new Point(cropRect.Right, cropRect.Center.Y))) return HandleType.RightCenter;

        if (HitTestRectPerimeter(pt, cropRect, Constants.GripSize)) return HandleType.Body;

        return HandleType.None;
    }

    public static Avalonia.Input.StandardCursorType GetCursorForCropHandle(HandleType handle)
    {
        return handle switch
        {
            HandleType.TopCenter => Avalonia.Input.StandardCursorType.TopSide,
            HandleType.BottomCenter => Avalonia.Input.StandardCursorType.BottomSide,
            HandleType.LeftCenter => Avalonia.Input.StandardCursorType.LeftSide,
            HandleType.RightCenter => Avalonia.Input.StandardCursorType.RightSide,
            HandleType.TopLeft => Avalonia.Input.StandardCursorType.TopLeftCorner,
            HandleType.TopRight => Avalonia.Input.StandardCursorType.TopRightCorner,
            HandleType.BottomLeft => Avalonia.Input.StandardCursorType.BottomLeftCorner,
            HandleType.BottomRight => Avalonia.Input.StandardCursorType.BottomRightCorner,
            HandleType.Body => Avalonia.Input.StandardCursorType.SizeAll,
            _ => Avalonia.Input.StandardCursorType.Arrow
        };
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
