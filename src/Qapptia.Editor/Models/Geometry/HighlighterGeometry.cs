using Avalonia;
using Qapptia.Editor.Services;

namespace Qapptia.Editor.Models.Geometry;

/// <summary>
/// Geometría de rectángulo de resaltado translúcido (back, sin renderizado).
/// </summary>
public class HighlighterGeometry : VectorGeometry
{
    public override HandleType HitTest(Point point)
    {
        var rect = GetBoundingBox();
        if (IsSelected)
        {
            var handle = HitTestEngine.HitTestHandlesCorners(point, rect);
            if (handle != HandleType.None) return handle;
        }

        if (rect.Inflate(4).Contains(point))
        {
            return HandleType.Body;
        }
        return HandleType.None;
    }
}
