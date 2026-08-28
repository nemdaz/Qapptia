using System;
using Qapptia.Editor.Core;
using Qapptia.Editor.Models.Geometry;
using Qapptia.Editor.Services;
using SkiaSharp;

namespace Qapptia.App.Editor.ViewModels.Shapes;

/// <summary>
/// Renderizado de flecha direccional de 2 nodos con punta (front).
/// </summary>
public class ArrowShape : VectorShape
{
    public ArrowShape() : base(new ArrowGeometry()) { }
    public ArrowShape(ArrowGeometry geometry) : base(geometry) { }

    public override void RenderSkia(SKCanvas canvas)
    {
        using var paint = new SKPaint
        {
            Color = Color.ToSKColor(),
            StrokeWidth = (float)StrokeWidth,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeCap = SKStrokeCap.Round,
            StrokeJoin = SKStrokeJoin.Round,
            ImageFilter = IsBurning ? Constants.CreateBurnedShadow() : Constants.CreateEditingShadow()
        };

        canvas.DrawLine((float)Start.X, (float)Start.Y, (float)End.X, (float)End.Y, paint);

        DrawArrowHead(canvas, paint);

        if (IsSelected)
        {
            ShapeRenderHelper.DrawHandlesSkiaEnds(canvas, Start, End);
        }
    }

    private void DrawArrowHead(SKCanvas canvas, SKPaint paint)
    {
        var head = ((ArrowGeometry)Geometry).GetArrowHeadPoints();
        if (head == null) return;

        var w1 = new SKPoint((float)head.Value.w1.X, (float)head.Value.w1.Y);
        var w2 = new SKPoint((float)head.Value.w2.X, (float)head.Value.w2.Y);

        using var path = new SKPath();
        path.MoveTo(w1);
        path.LineTo((float)End.X, (float)End.Y);
        path.LineTo(w2);

        canvas.DrawPath(path, paint);
    }
}
