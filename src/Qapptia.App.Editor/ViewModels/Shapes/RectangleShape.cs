using Qapptia.Editor.Core;
using Qapptia.Editor.Models.Geometry;
using Qapptia.Editor.Services;
using SkiaSharp;

namespace Qapptia.App.Editor.ViewModels.Shapes;

/// <summary>
/// Renderizado de rectángulo (front).
/// </summary>
public class RectangleShape : VectorShape
{
    public RectangleShape() : base(new RectangleGeometry()) { }
    public RectangleShape(RectangleGeometry geometry) : base(geometry) { }

    public override void RenderSkia(SKCanvas canvas, float zoom = 1.0f)
    {
        using var paint = new SKPaint
        {
            Color = Color.ToSKColor(),
            StrokeWidth = (float)StrokeWidth,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeJoin = SKStrokeJoin.Round,
            ImageFilter = IsBurning ? Constants.CreateBurnedShadow() : Constants.CreateEditingShadow()
        };

        var rect = BoundingBox;
        var skRect = new SKRect((float)rect.Left, (float)rect.Top, (float)rect.Right, (float)rect.Bottom);

        canvas.DrawRect(skRect, paint);

        if (IsSelected)
        {
            ShapeRenderHelper.DrawHandlesSkiaCorners(canvas, rect, zoom);
        }
    }
}
