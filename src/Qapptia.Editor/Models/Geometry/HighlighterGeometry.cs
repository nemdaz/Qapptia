using Avalonia;
using Qapptia.Editor.Services;

namespace Qapptia.Editor.Models.Geometry;

/// <summary>
/// Geometría de rectángulo de resaltado translúcido (back, sin renderizado).
/// </summary>
public class HighlighterGeometry : VectorGeometry
{
    public override HandleType HitTest(Point point, float zoom = 1.0f)
    {
        var rect = GetBoundingBox();
        if (IsSelected)
        {
            var handle = HitTestEngine.HitTestHandlesCorners(point, rect, zoom);
            if (handle != HandleType.None) return handle;
        }

        if (rect.Inflate(4).Contains(point))
        {
            return HandleType.Body;
        }
        return HandleType.None;
    }
}
