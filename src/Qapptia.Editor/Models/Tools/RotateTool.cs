using System;
using System.Collections.Generic;
using Avalonia;
using Qapptia.Editor.Models.Geometry;

namespace Qapptia.Editor.Tools;

/// <summary>
/// Herramienta de rotación del lienzo (back). Expone el cálculo puro para rotar la
/// escena (conjunto de geometrías) y, como acción, delega la orquestación completa
/// (imagen + figuras) al front, que la inyecta al construirse.
/// </summary>
public sealed class RotateTool : ActionTool
{
    public RotateTool(Action action)
        : base("Rotate", "Rotar", null, action)
    {
    }

    /// <summary>
    /// Rota todas las geometrías visibles 90° en sentido horario sobre el esquema del
    /// lienzo (cálculo puro), preservando su posición relativa respecto a la imagen.
    /// </summary>
    /// <param name="geometries">Geometrías a rotar.</param>
    /// <param name="imageHeight">Altura original de la imagen antes de rotar.</param>
    public static void RotateScene90Clockwise(IEnumerable<VectorGeometry> geometries, double imageHeight)
    {
        ArgumentNullException.ThrowIfNull(geometries);

        foreach (var geometry in geometries)
        {
            var start = geometry.Start;
            var end = geometry.End;
            geometry.Start = new Point(imageHeight - start.Y, start.X);
            geometry.End = new Point(imageHeight - end.Y, end.X);
        }
    }

    /// <summary>
    /// Rota todas las geometrías visibles en torno a un pivote común (cálculo puro y genérico).
    /// Ángulo matemático (positivo = antihorario).
    /// </summary>
    public static void RotateGeometries(IEnumerable<VectorGeometry> geometries, double angleDegrees, Point pivot)
    {
        ArgumentNullException.ThrowIfNull(geometries);

        foreach (var geometry in geometries)
        {
            geometry.RotateAroundPoint(pivot, angleDegrees);
        }
    }
}
