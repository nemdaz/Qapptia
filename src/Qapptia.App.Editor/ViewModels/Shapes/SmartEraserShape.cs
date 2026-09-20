using System;
using Avalonia;
using Qapptia.Editor.Core;
using Qapptia.Editor.Models.Geometry;
using Qapptia.Editor.Services;
using SkiaSharp;

namespace Qapptia.App.Editor.ViewModels.Shapes;

/// <summary>
/// Presentación y renderizado en Skia del borrador inteligente (front).
/// Cubre la zona seleccionada con el color de fondo muestreado en modo plano sin sombras.
/// </summary>
public class SmartEraserShape : VectorShape
{
    public SmartEraserShape() : base(new SmartEraserGeometry()) { }
    public SmartEraserShape(SmartEraserGeometry geometry) : base(geometry) { }

    public override void RenderSkia(SKCanvas canvas, float zoom = 1.0f)
    {
        var rect = BoundingBox;
        if (rect.Width <= 0 || rect.Height <= 0) return;

        var skRect = new SKRect((float)rect.Left, (float)rect.Top, (float)rect.Right, (float)rect.Bottom);

        // 1. Relleno plano sólido del color muestreado (sin sombras de elevación, fundido perfecto con el fondo)
        using var fillPaint = new SKPaint
        {
            Color = Color.ToSKColor(),
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };
        canvas.DrawRect(skRect, fillPaint);

        // 2. Contorno punteado bicolor de alto contraste (blanco y negro alternado) si está seleccionado o en hover
        if (!IsBurning && (IsSelected || (ShowsHoverOutline && IsHovered)))
        {
            ShapeRenderHelper.DrawHighContrastDashedRect(canvas, rect, zoom);
        }

        // 3. Manetas de esquina solo cuando está seleccionado activamente
        if (IsSelected && !IsBurning)
        {
            ShapeRenderHelper.DrawHandlesSkiaCorners(canvas, rect, zoom);
        }
    }
}
