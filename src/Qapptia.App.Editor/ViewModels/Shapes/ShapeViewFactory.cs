using Qapptia.Editor.Models.Geometry;

namespace Qapptia.App.Editor.ViewModels.Shapes;

/// <summary>
/// Componente de presentación que envuelve una <see cref="VectorGeometry"/> (back)
/// en su <see cref="VectorShape"/> renderizable (front) correspondiente. Constituye el
/// único punto de la capa de vista donde se resuelve el mapeo geometría→render concretos.
/// </summary>
public static class ShapeViewFactory
{
    public static VectorShape Wrap(VectorGeometry geometry)
    {
        return geometry switch
        {
            LineGeometry g => new LineShape(g),
            ArrowGeometry g => new ArrowShape(g),
            RectangleGeometry g => new RectangleShape(g),
            EllipseGeometry g => new EllipseShape(g),
            HighlighterGeometry g => new HighlighterShape(g),
            TextGeometry g => new TextShape(g),
            _ => throw new System.NotSupportedException($"No hay vista de render para la geometría '{geometry.GetType().Name}'.")
        };
    }
}
