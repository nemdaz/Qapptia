using Qapptia.Editor.Core;
using Qapptia.Editor.Models.Geometry;
using Qapptia.Editor.Services;
using SkiaSharp;

namespace Qapptia.App.Editor.ViewModels.Shapes;

/// <summary>
/// Renderizado de rectángulo de resaltado translúcido (front).
/// </summary>
public class HighlighterShape : VectorShape
{
    public HighlighterShape() : base(new HighlighterGeometry()) { }
    public HighlighterShape(HighlighterGeometry geometry) : base(geometry) { }

    public override void RenderSkia(SKCanvas canvas)
    {
        using var paint = new SKPaint
        {
            Color = Color.ToSKColor().WithAlpha(Constants.HighlighterAlpha),
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        };

        var rect = BoundingBox;
        var skRect = new SKRect((float)rect.Left, (float)rect.Top, (float)rect.Right, (float)rect.Bottom);

        canvas.DrawRect(skRect, paint);

        if (IsSelected)
        {
            ShapeRenderHelper.DrawHandlesSkiaCorners(canvas, rect);
        }
    }
}
