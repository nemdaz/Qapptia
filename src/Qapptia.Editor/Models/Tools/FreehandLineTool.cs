using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using Qapptia.Editor.Models.Geometry;

namespace Qapptia.Editor.Tools;

/// <summary>
/// Herramienta vectorial para trazar líneas a mano alzada.
/// </summary>
public sealed class FreehandLineTool : VectorTool<FreehandLineGeometry>
{
    public override string Id => "FreehandLine";
    public override string DisplayName => "Línea a mano alzada";
    public override string? Shortcut => null;

    public override VectorGeometry CreateShape(Point startPoint, Color color)
    {
        var shape = new FreehandLineGeometry
        {
            Start = startPoint,
            End = startPoint,
            Color = color
        };
        shape.AddPoint(startPoint);
        return shape;
    }

    public override void UpdateDrawing(VectorGeometry shape, Point currentPoint, KeyModifiers modifiers)
    {
        if (shape is FreehandLineGeometry freehand)
        {
            freehand.AddPoint(currentPoint);
        }
        else
        {
            shape.End = currentPoint;
        }
    }

    public override bool ShouldCommitOnRelease(VectorGeometry shape)
    {
        if (shape is FreehandLineGeometry freehand)
        {
            return freehand.Points.Count >= 2;
        }
        return base.ShouldCommitOnRelease(shape);
    }
}
