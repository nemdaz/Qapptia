using Avalonia;
using SkiaSharp;

namespace Qapptia.App.Editor.ViewModels.Shapes;

/// <summary>
/// Renderizado del overlay de recorte (front). Dibuja el scrim oscurecido, la línea
/// delimitadora interlineada y los tiradores cuadrados. No realiza cálculo geométrico;
/// solo traduce los rectángulos recibidos a llamadas de dibujo.
/// </summary>
public static class CropOverlayRenderer
{
    private static readonly float[] s_dashIntervals = { 6f, 4f };

    public static void DrawSquareHandle(SKCanvas canvas, Point center, float size = 10f)
    {
        float half = size / 2.0f;
        var skRect = new SKRect((float)center.X - half, (float)center.Y - half, (float)center.X + half, (float)center.Y + half);

        using var shadowPaint = new SKPaint
        {
            Color = SKColors.Black.WithAlpha(90),
            Style = SKPaintStyle.Fill,
            IsAntialias = true,
            MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 2f)
        };

        using var fillPaint = new SKPaint
        {
            Color = SKColors.White,
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };

        using var borderPaint = new SKPaint
        {
            Color = SKColors.Black.WithAlpha(160),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1f,
            IsAntialias = true
        };

        var shadowRect = new SKRect(skRect.Left, skRect.Top + 1f, skRect.Right, skRect.Bottom + 1f);
        canvas.DrawRect(shadowRect, shadowPaint);

        canvas.DrawRect(skRect, fillPaint);
        canvas.DrawRect(skRect, borderPaint);
    }

    public static void DrawCropOverlay(SKCanvas canvas, Rect cropRect, Rect imageBounds)
    {
        // 1. Oscurecimiento exterior (Scrim)
        using var scrimPaint = new SKPaint
        {
            Color = new SKColor(0, 0, 0, 160),
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };

        if (cropRect.Top > 0)
        {
            canvas.DrawRect(0, 0, (float)imageBounds.Width, (float)cropRect.Top, scrimPaint);
        }

        if (cropRect.Bottom < imageBounds.Height)
        {
            canvas.DrawRect(0, (float)cropRect.Bottom, (float)imageBounds.Width, (float)(imageBounds.Height - cropRect.Bottom), scrimPaint);
        }

        if (cropRect.Left > 0)
        {
            canvas.DrawRect(0, (float)cropRect.Top, (float)cropRect.Left, (float)cropRect.Height, scrimPaint);
        }

        if (cropRect.Right < imageBounds.Width)
        {
            canvas.DrawRect((float)cropRect.Right, (float)cropRect.Top, (float)(imageBounds.Width - cropRect.Right), (float)cropRect.Height, scrimPaint);
        }

        // 2. Línea delimitadora interlineada (Dashed Line)
        using var dashOutlinePaint = new SKPaint
        {
            Color = SKColors.Black.WithAlpha(180),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2f,
            IsAntialias = true
        };

        using var dashPaint = new SKPaint
        {
            Color = SKColors.White,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1.5f,
            PathEffect = SKPathEffect.CreateDash(s_dashIntervals, 0),
            IsAntialias = true
        };

        var cropSkRect = new SKRect((float)cropRect.Left, (float)cropRect.Top, (float)cropRect.Right, (float)cropRect.Bottom);
        canvas.DrawRect(cropSkRect, dashOutlinePaint);
        canvas.DrawRect(cropSkRect, dashPaint);

        // 3. Tiradores cuadrados (4 esquinas + 4 centros de arista)
        DrawSquareHandle(canvas, cropRect.TopLeft);
        DrawSquareHandle(canvas, cropRect.TopRight);
        DrawSquareHandle(canvas, cropRect.BottomLeft);
        DrawSquareHandle(canvas, cropRect.BottomRight);

        DrawSquareHandle(canvas, new Point(cropRect.Center.X, cropRect.Top));
        DrawSquareHandle(canvas, new Point(cropRect.Center.X, cropRect.Bottom));
        DrawSquareHandle(canvas, new Point(cropRect.Left, cropRect.Center.Y));
        DrawSquareHandle(canvas, new Point(cropRect.Right, cropRect.Center.Y));
    }
}
