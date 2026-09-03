using Qapptia.Editor.Core;
using Qapptia.Editor.Models.Geometry;
using Qapptia.Editor.Services;
using SkiaSharp;

namespace Qapptia.App.Editor.ViewModels.Shapes;

/// <summary>
/// Renderizado de línea recta simple (front).
/// </summary>
public class LineShape : VectorShape
{
    public LineShape() : base(new LineGeometry()) { }
    public LineShape(LineGeometry geometry) : base(geometry) { }

    public override void RenderSkia(SKCanvas canvas, float zoom = 1.0f)
    {
        using var paint = new SKPaint
        {
            Color = Color.ToSKColor(),
            StrokeWidth = (float)StrokeWidth,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeCap = SKStrokeCap.Round,
            ImageFilter = IsBurning ? Constants.CreateBurnedShadow() : Constants.CreateEditingShadow()
        };

        canvas.DrawLine((float)Start.X, (float)Start.Y, (float)End.X, (float)End.Y, paint);

        if (IsSelected)
        {
            ShapeRenderHelper.DrawHandlesSkiaEnds(canvas, Start, End, zoom);
        }
    }
}
