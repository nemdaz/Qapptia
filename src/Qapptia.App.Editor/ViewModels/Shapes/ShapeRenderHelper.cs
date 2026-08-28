using Avalonia;
using Qapptia.Editor.Core;
using SkiaSharp;

namespace Qapptia.App.Editor.ViewModels.Shapes;

/// <summary>
/// Utilidades puramente visuales (front) para dibujar elementos interactivos (handles, overlays).
/// NO contiene cálculos matemáticos, solo instrucciones de SkiaSharp.
/// </summary>
public static class ShapeRenderHelper
{
    public static void DrawHandle(SKCanvas canvas, Point center)
    {
        float size = (float)Constants.GripSize * 1.5f * 1.3f; // 30% larger
        float radius = size / 2.0f;

        using var shadowPaint = new SKPaint
        {
            Color = SKColors.Black.WithAlpha(80),
            Style = SKPaintStyle.Fill,
            IsAntialias = true,
            MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 2f)
        };

        using var paint = new SKPaint
        {
            Color = SKColors.White,
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };

        // Draw shadow slightly offset
        canvas.DrawCircle((float)center.X, (float)center.Y + 1f, radius, shadowPaint);
        // Draw white circle
        canvas.DrawCircle((float)center.X, (float)center.Y, radius, paint);
    }

    public static void DrawHandlesSkiaEnds(SKCanvas canvas, Point start, Point end)
    {
        DrawHandle(canvas, start);
        DrawHandle(canvas, end);
    }

    public static void DrawHandlesSkiaCorners(SKCanvas canvas, Rect boundingBox)
    {
        DrawHandle(canvas, boundingBox.TopLeft);
        DrawHandle(canvas, boundingBox.TopRight);
        DrawHandle(canvas, boundingBox.BottomLeft);
        DrawHandle(canvas, boundingBox.BottomRight);
    }

    public static void DrawHandlesSkiaCenters(SKCanvas canvas, Rect boundingBox)
    {
        DrawHandle(canvas, new Point(boundingBox.Center.X, boundingBox.Top));
        DrawHandle(canvas, new Point(boundingBox.Center.X, boundingBox.Bottom));
        DrawHandle(canvas, new Point(boundingBox.Left, boundingBox.Center.Y));
        DrawHandle(canvas, new Point(boundingBox.Right, boundingBox.Center.Y));
    }

    public static void DrawHandlesSkiaSides(SKCanvas canvas, Rect boundingBox)
    {
        DrawHandle(canvas, new Point(boundingBox.Left, boundingBox.Center.Y));
        DrawHandle(canvas, new Point(boundingBox.Right, boundingBox.Center.Y));
    }

    public static void DrawCropSquareHandle(SKCanvas canvas, Point center, float size = 10f)
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

        // Sombra
        var shadowRect = new SKRect(skRect.Left, skRect.Top + 1f, skRect.Right, skRect.Bottom + 1f);
        canvas.DrawRect(shadowRect, shadowPaint);

        // Cuadrado blanco y borde
        canvas.DrawRect(skRect, fillPaint);
        canvas.DrawRect(skRect, borderPaint);
    }

    private static readonly float[] s_cropDashIntervals = { 6f, 4f };

    public static void DrawCropOverlay(SKCanvas canvas, Rect cropRect, Rect imageBounds)
    {
        // 1. Oscurecimiento exterior (Scrim)
        using var scrimPaint = new SKPaint
        {
            Color = new SKColor(0, 0, 0, 160),
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };

        // Región superior
        if (cropRect.Top > 0)
        {
            canvas.DrawRect(0, 0, (float)imageBounds.Width, (float)cropRect.Top, scrimPaint);
        }
        // Región inferior
        if (cropRect.Bottom < imageBounds.Height)
        {
            canvas.DrawRect(0, (float)cropRect.Bottom, (float)imageBounds.Width, (float)(imageBounds.Height - cropRect.Bottom), scrimPaint);
        }
        // Región izquierda
        if (cropRect.Left > 0)
        {
            canvas.DrawRect(0, (float)cropRect.Top, (float)cropRect.Left, (float)cropRect.Height, scrimPaint);
        }
        // Región derecha
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
            PathEffect = SKPathEffect.CreateDash(s_cropDashIntervals, 0),
            IsAntialias = true
        };

        var cropSkRect = new SKRect((float)cropRect.Left, (float)cropRect.Top, (float)cropRect.Right, (float)cropRect.Bottom);
        canvas.DrawRect(cropSkRect, dashOutlinePaint);
        canvas.DrawRect(cropSkRect, dashPaint);

        // 3. Tiradores cuadrados (4 esquinas + 4 centros de arista)
        DrawCropSquareHandle(canvas, cropRect.TopLeft);
        DrawCropSquareHandle(canvas, cropRect.TopRight);
        DrawCropSquareHandle(canvas, cropRect.BottomLeft);
        DrawCropSquareHandle(canvas, cropRect.BottomRight);

        DrawCropSquareHandle(canvas, new Point(cropRect.Center.X, cropRect.Top));
        DrawCropSquareHandle(canvas, new Point(cropRect.Center.X, cropRect.Bottom));
        DrawCropSquareHandle(canvas, new Point(cropRect.Left, cropRect.Center.Y));
        DrawCropSquareHandle(canvas, new Point(cropRect.Right, cropRect.Center.Y));
    }
}
