using System;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using Qapptia.Editor.Models;

namespace Qapptia.Editor.Tools;

/// <summary>
/// Clase base para herramientas que crean y manipulan figuras vectoriales persistentes.
/// </summary>
public abstract class VectorTool : Tool
{
    public override ToolType Type => ToolType.Vector;
    public override StandardCursorType DefaultCursor => StandardCursorType.Cross;

    /// <summary>
    /// Crea una nueva instancia de la figura vectorial correspondiente.
    /// </summary>
    public abstract VectorShape CreateShape(Point startPoint, Color color);

    /// <summary>
    /// Actualiza la geometría de la figura durante el arrastre en el lienzo.
    /// </summary>
    public virtual void UpdateDrawing(VectorShape shape, Point currentPoint, KeyModifiers modifiers)
    {
        shape.End = currentPoint;
    }

    /// <summary>
    /// Determina si el trazo dibujado cumple con el umbral mínimo para ser confirmado.
    /// </summary>
    public virtual bool ShouldCommitOnRelease(VectorShape shape)
    {
        double dx = shape.End.X - shape.Start.X;
        double dy = shape.End.Y - shape.Start.Y;
        return (dx * dx + dy * dy) > 9; // Umbral mínimo de 3 píxeles
    }
}

/// <summary>
/// Clase base genérica y fuertemente tipada que asocia directamente la herramienta con su figura vectorial correspondiente.
/// </summary>
/// <typeparam name="TShape">Tipo concreto de figura derivado de VectorShape con constructor público sin parámetros.</typeparam>
public abstract class VectorTool<TShape> : VectorTool where TShape : VectorShape, new()
{
    public override Type TargetShapeType => typeof(TShape);

    public override VectorShape CreateShape(Point startPoint, Color color)
    {
        return new TShape
        {
            Start = startPoint,
            End = startPoint,
            Color = color
        };
    }
}
