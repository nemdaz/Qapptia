using Avalonia;
using Qapptia.Editor.Core;
using Qapptia.Editor.Models.Geometry;
using Qapptia.Editor.Services;
using SkiaSharp;

namespace Qapptia.App.Editor.ViewModels.Shapes;

/// <summary>
/// Renderizado de línea a mano alzada (front).
/// </summary>
public class FreehandLineShape : VectorShape
{
    public FreehandLineGeometry FreehandGeometry => (FreehandLineGeometry)Geometry;

    public FreehandLineShape() : base(new FreehandLineGeometry()) { }
    public FreehandLineShape(FreehandLineGeometry geometry) : base(geometry) { }

    public override void RenderSkia(SKCanvas canvas, float zoom = 1.0f)
    {
        var points = FreehandGeometry.Points;
        if (points == null || points.Count < 2) return;

        using var path = new SKPath();
        if (points.Count == 2)
        {
            path.MoveTo((float)points[0].X, (float)points[0].Y);
            path.LineTo((float)points[1].X, (float)points[1].Y);
        }
        else
        {
            path.MoveTo((float)points[0].X, (float)points[0].Y);

            for (int i = 1; i < points.Count - 1; i++)
            {
                var p0 = points[i];
                var p1 = points[i + 1];
                float midX = (float)((p0.X + p1.X) / 2.0);
                float midY = (float)((p0.Y + p1.Y) / 2.0);
                path.QuadTo((float)p0.X, (float)p0.Y, midX, midY);
            }

            var last = points[^1];
            path.QuadTo((float)points[^2].X, (float)points[^2].Y, (float)last.X, (float)last.Y);
        }

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

        canvas.DrawPath(path, paint);

        if (IsSelected)
        {
            var bbox = BoundingBox;
            float safeZoom = Math.Max(0.01f, zoom);
            using var dashPaint = new SKPaint
            {
                Color = SKColors.Black.WithAlpha(90),
                StrokeWidth = 1f / safeZoom,
                Style = SKPaintStyle.Stroke,
                IsAntialias = true,
                PathEffect = SKPathEffect.CreateDash(new float[] { 3f / safeZoom, 3f / safeZoom }, 0)
            };
            var skBbox = new SKRect((float)bbox.Left, (float)bbox.Top, (float)bbox.Right, (float)bbox.Bottom);
            canvas.DrawRect(skBbox, dashPaint);

            ShapeRenderHelper.DrawHandlesSkiaCorners(canvas, bbox, zoom);
        }
    }
}
