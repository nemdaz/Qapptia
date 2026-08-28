using Avalonia;
using Qapptia.Editor.Core;
using Qapptia.Editor.Services;

namespace Qapptia.Editor.Models.Geometry;

/// <summary>
/// Geometría de rectángulo (back, sin renderizado).
/// </summary>
public class RectangleGeometry : VectorGeometry
{
    public override HandleType HitTest(Point point)
    {
        var rect = GetBoundingBox();
        if (IsSelected)
        {
            var handle = HitTestEngine.HitTestHandlesCorners(point, rect);
            if (handle != HandleType.None) return handle;
        }

        double tolerance = StrokeWidth + 8.0;

        var outerRect = rect.Inflate(tolerance);
        var innerRect = rect.Inflate(-tolerance);

        if (outerRect.Contains(point) && !innerRect.Contains(point))
        {
            return HandleType.Body;
        }
        return HandleType.None;
    }
}
