using System;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using Qapptia.Editor.Models.Geometry;

namespace Qapptia.Editor.Tools;

/// <summary>
/// Clase base para herramientas que crean y manipulan figuras vectoriales persistentes.
/// </summary>
public abstract class VectorTool : Tool
{
    public override ToolType Type => ToolType.Vector;
    public override StandardCursorType DefaultCursor => StandardCursorType.Cross;

    /// <summary>
    /// Crea una nueva instancia de la geometría vectorial correspondiente.
    /// </summary>
    public abstract VectorGeometry CreateShape(Point startPoint, Color color);

    /// <summary>
    /// Actualiza la geometría de la figura durante el arrastre en el lienzo.
    /// </summary>
    public virtual void UpdateDrawing(VectorGeometry shape, Point currentPoint, KeyModifiers modifiers)
    {
        shape.End = currentPoint;
    }

    /// <summary>
    /// Determina si el trazo dibujado cumple con el umbral mínimo para ser confirmado.
    /// </summary>
    public virtual bool ShouldCommitOnRelease(VectorGeometry shape)
    {
        double dx = shape.End.X - shape.Start.X;
        double dy = shape.End.Y - shape.Start.Y;
        return (dx * dx + dy * dy) > 9; // Umbral mínimo de 3 píxeles
    }
}

/// <summary>
/// Clase base genérica y fuertemente tipada que asocia directamente la herramienta con su geometría vectorial correspondiente.
/// </summary>
/// <typeparam name="TGeometry">Tipo concreto de geometría derivado de VectorGeometry con constructor público sin parámetros.</typeparam>
public abstract class VectorTool<TGeometry> : VectorTool where TGeometry : VectorGeometry, new()
{
    public override Type TargetShapeType => typeof(TGeometry);

    public override VectorGeometry CreateShape(Point startPoint, Color color)
    {
        return new TGeometry
        {
            Start = startPoint,
            End = startPoint,
            Color = color
        };
    }
}
