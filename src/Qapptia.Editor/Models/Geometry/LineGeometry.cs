using Avalonia;
using Qapptia.Editor.Core;
using Qapptia.Editor.Services;

namespace Qapptia.Editor.Models.Geometry;

/// <summary>
/// Geometría de línea recta simple de 2 nodos (back, sin renderizado).
/// </summary>
public class LineGeometry : VectorGeometry
{
    public override HandleType HitTest(Point point)
    {
        if (IsSelected)
        {
            var handle = HitTestEngine.HitTestHandlesEnds(point, Start, End);
            if (handle != HandleType.None) return handle;
        }

        if (HitTestEngine.PointToLineDistance(point, Start, End, StrokeWidth + 8.0))
        {
            return HandleType.Body;
        }
        return HandleType.None;
    }
}
