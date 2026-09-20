using System;
using Avalonia;
using Avalonia.Input;
using Qapptia.Editor.Core;
using Qapptia.Editor.Services;

namespace Qapptia.Editor.Models.Geometry;

/// <summary>
/// Geometría de borrador inteligente (back, sin renderizado).
/// Representa un área rectangular que disimula o cubre contenido con el color de fondo muestreado.
/// </summary>
public class SmartEraserGeometry : VectorGeometry
{
    public SmartEraserGeometry()
    {
        StrokeWidth = 0; // Sin trazo/borde visible por defecto
    }

    /// <summary>
    /// El borrador inteligente siempre revela su contorno punteado al pasar el puntero por encima.
    /// </summary>
    public override bool ShowsHoverOutline => true;

    /// <summary>
    /// El borrador inteligente es una cobertura sólida y opaca del fondo muestreado.
    /// </summary>
    public override bool HasSolidBackground => true;

    public override HandleType HitTest(Point point, float zoom = 1.0f)
    {
        var rect = GetBoundingBox();
        if (IsSelected)
        {
            var handle = HitTestEngine.HitTestHandlesCorners(point, rect, zoom);
            if (handle != HandleType.None) return handle;

            if (rect.Contains(point))
            {
                return HandleType.Body;
            }
        }
        else
        {
            // Al pasar el cursor y mostrar el contorno punteado, un clic en cualquier parte
            // de su interior selecciona la figura activando de inmediato sus manetas de esquina.
            if (rect.Contains(point))
            {
                return HandleType.Body;
            }
        }
        return HandleType.None;
    }

    public override StandardCursorType? GetCursorType(Point point, float zoom = 1.0f)
    {
        if (!IsSelected)
        {
            return BoundingBox.Contains(point) ? StandardCursorType.Hand : null;
        }

        return base.GetCursorType(point, zoom);
    }
}
