using Qapptia.Editor.Core;
using Qapptia.Editor.Models.Geometry;
using Qapptia.Editor.Services;
using SkiaSharp;

namespace Qapptia.App.Editor.ViewModels.Shapes;

/// <summary>
/// Renderizado de elipse (front).
/// </summary>
public class EllipseShape : VectorShape
{
    public EllipseShape() : base(new EllipseGeometry()) { }
    public EllipseShape(EllipseGeometry geometry) : base(geometry) { }

    public override void RenderSkia(SKCanvas canvas)
    {
        using var paint = new SKPaint
        {
            Color = Color.ToSKColor(),
            StrokeWidth = (float)StrokeWidth,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            ImageFilter = IsBurning ? Constants.CreateBurnedShadow() : Constants.CreateEditingShadow()
        };

        var rect = BoundingBox;

        float cx = (float)rect.Center.X;
        float cy = (float)rect.Center.Y;
        float rx = (float)(rect.Width / 2);
        float ry = (float)(rect.Height / 2);

        canvas.DrawOval(cx, cy, rx, ry, paint);

        if (IsSelected)
        {
            ShapeRenderHelper.DrawHandlesSkiaCenters(canvas, rect);
        }
    }
}
