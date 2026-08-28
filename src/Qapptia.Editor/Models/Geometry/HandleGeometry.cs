using System.Collections.Generic;
using Avalonia;
using Qapptia.Editor.Core;

namespace Qapptia.Editor.Models.Geometry;

/// <summary>
/// Cálculo puro de las posiciones de las manetas de control (back). Devuelve puntos
/// geométricos; el dibujado de dichas manetas reside en la capa de presentación.
/// </summary>
public static class HandleGeometry
{
    /// <summary>
    /// Manetas de los extremos de una línea (inicio y fin).
    /// </summary>
    public static IEnumerable<Point> Ends(Point start, Point end)
    {
        yield return start;
        yield return end;
    }

    /// <summary>
    /// Manetas de las cuatro esquinas de un cuadro delimitador.
    /// </summary>
    public static IEnumerable<Point> Corners(Rect boundingBox)
    {
        yield return boundingBox.TopLeft;
        yield return boundingBox.TopRight;
        yield return boundingBox.BottomLeft;
        yield return boundingBox.BottomRight;
    }

    /// <summary>
    /// Manetas de los cuatro centros de arista de un cuadro delimitador.
    /// </summary>
    public static IEnumerable<Point> Centers(Rect boundingBox)
    {
        yield return new Point(boundingBox.Center.X, boundingBox.Top);
        yield return new Point(boundingBox.Center.X, boundingBox.Bottom);
        yield return new Point(boundingBox.Left, boundingBox.Center.Y);
        yield return new Point(boundingBox.Right, boundingBox.Center.Y);
    }

    /// <summary>
    /// Manetas laterales (centro izquierdo y derecho) de un cuadro delimitador.
    /// </summary>
    public static IEnumerable<Point> Sides(Rect boundingBox)
    {
        yield return new Point(boundingBox.Left, boundingBox.Center.Y);
        yield return new Point(boundingBox.Right, boundingBox.Center.Y);
    }
}
