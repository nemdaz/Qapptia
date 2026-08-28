using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Input;
using Qapptia.Editor.Core;
using Qapptia.Editor.Models;
using Qapptia.Editor.Models.Geometry;

namespace Qapptia.Editor.Tools;

/// <summary>
/// Herramienta interactiva de recorte dinámico directo sobre el lienzo. Encapsula el
/// cálculo geométrico del rect de recorte (redimensionado por manetas) y de las figuras
/// afectadas por el corte, que reside en el dominio y no en la capa de vista.
/// </summary>
public sealed class CropTool : InteractiveTool
{
    public override string Id => "Crop";
    public override string DisplayName => "Recortar";
    public override string IconData => IconCatalog.Crop;
    public override StandardCursorType DefaultCursor => StandardCursorType.Cross;

    /// <summary>
    /// Ajusta el rect de recorte arrastrando una maneta determinada. Devuelve el nuevo rect
    /// restrigido a los límites de la imagen y respetando el tamaño mínimo de corte.
    /// </summary>
    public static Rect ResizeRect(HandleType handle, Rect current, double dx, double dy, double imageWidth, double imageHeight)
    {
        double min = Constants.CropMinSize;

        switch (handle)
        {
            case HandleType.LeftCenter:
                double newLeft = Math.Clamp(current.Left + dx, 0, current.Right - min);
                return new Rect(newLeft, current.Top, current.Right - newLeft, current.Height);
            case HandleType.RightCenter:
                double newRight = Math.Clamp(current.Right + dx, current.Left + min, imageWidth);
                return new Rect(current.Left, current.Top, newRight - current.Left, current.Height);
            case HandleType.TopCenter:
                double newTop = Math.Clamp(current.Top + dy, 0, current.Bottom - min);
                return new Rect(current.Left, newTop, current.Width, current.Bottom - newTop);
            case HandleType.BottomCenter:
                double newBottom = Math.Clamp(current.Bottom + dy, current.Top + min, imageHeight);
                return new Rect(current.Left, current.Top, current.Width, newBottom - current.Top);
            case HandleType.TopLeft:
                double tlLeft = Math.Clamp(current.Left + dx, 0, current.Right - min);
                double tlTop = Math.Clamp(current.Top + dy, 0, current.Bottom - min);
                return new Rect(tlLeft, tlTop, current.Right - tlLeft, current.Bottom - tlTop);
            case HandleType.TopRight:
                double trRight = Math.Clamp(current.Right + dx, current.Left + min, imageWidth);
                double trTop = Math.Clamp(current.Top + dy, 0, current.Bottom - min);
                return new Rect(current.Left, trTop, trRight - current.Left, current.Bottom - trTop);
            case HandleType.BottomLeft:
                double blLeft = Math.Clamp(current.Left + dx, 0, current.Right - min);
                double blBottom = Math.Clamp(current.Bottom + dy, current.Top + min, imageHeight);
                return new Rect(blLeft, current.Top, current.Right - blLeft, blBottom - current.Top);
            case HandleType.BottomRight:
                double brRight = Math.Clamp(current.Right + dx, current.Left + min, imageWidth);
                double brBottom = Math.Clamp(current.Bottom + dy, current.Top + min, imageHeight);
                return new Rect(current.Left, current.Top, brRight - current.Left, brBottom - current.Top);
            case HandleType.Body:
                double newX = Math.Clamp(current.X + dx, 0, imageWidth - current.Width);
                double newY = Math.Clamp(current.Y + dy, 0, imageHeight - current.Height);
                return new Rect(newX, newY, current.Width, current.Height);
            default:
                return current;
        }
    }

    /// <summary>
    /// Determina si un rect de recorte es válido para ser aplicado (supera el mínimo y
    /// representa un recorte real respecto a la imagen).
    /// </summary>
    public static bool ShouldApplyCrop(Rect cropRect, double imageWidth, double imageHeight)
    {
        double min = Constants.CropMinSize;
        return cropRect.Width >= min && cropRect.Height >= min &&
               (cropRect.Width < imageWidth || cropRect.Height < imageHeight || cropRect.X > 0 || cropRect.Y > 0);
    }

    /// <summary>
    /// Traslada las geometrías tras un recorte para preservar su posición visual relativa
    /// sobre la imagen. Devuelve solo aquellas que siguen intersectando el área recortada.
    /// </summary>
    public static List<VectorGeometry> ShiftGeometries(
        IEnumerable<VectorGeometry> geometries,
        double deltaX, double deltaY, Rect newBounds)
    {
        var kept = new List<VectorGeometry>();
        foreach (var geometry in geometries)
        {
            geometry.Move(deltaX, deltaY);
            if (geometry.BoundingBox.Intersects(newBounds))
            {
                kept.Add(geometry);
            }
        }
        return kept;
    }
}
