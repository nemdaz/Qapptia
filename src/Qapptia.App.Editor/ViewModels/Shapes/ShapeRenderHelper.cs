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
    public static void DrawHandle(SKCanvas canvas, Point center, float zoom = 1.0f)
    {
        float safeZoom = Math.Max(0.01f, zoom);
        float size = (float)Constants.GripSize * 1.5f * 1.3f / safeZoom; // 30% larger, zoom-compensated
        float radius = size / 2.0f;
        float shadowOffsetY = 1f / safeZoom;
        float blur = 2f / safeZoom;

        using var shadowPaint = new SKPaint
        {
            Color = SKColors.Black.WithAlpha(80),
            Style = SKPaintStyle.Fill,
            IsAntialias = true,
            MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, blur)
        };

        using var paint = new SKPaint
        {
            Color = SKColors.White,
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };

        // Draw shadow slightly offset
        canvas.DrawCircle((float)center.X, (float)center.Y + shadowOffsetY, radius, shadowPaint);
        // Draw white circle
        canvas.DrawCircle((float)center.X, (float)center.Y, radius, paint);
    }

    public static void DrawHandlesSkiaEnds(SKCanvas canvas, Point start, Point end, float zoom = 1.0f)
    {
        DrawHandle(canvas, start, zoom);
        DrawHandle(canvas, end, zoom);
    }

    public static void DrawHandlesSkiaCorners(SKCanvas canvas, Rect boundingBox, float zoom = 1.0f)
    {
        DrawHandle(canvas, boundingBox.TopLeft, zoom);
        DrawHandle(canvas, boundingBox.TopRight, zoom);
        DrawHandle(canvas, boundingBox.BottomLeft, zoom);
        DrawHandle(canvas, boundingBox.BottomRight, zoom);
    }

    public static void DrawHandlesSkiaCenters(SKCanvas canvas, Rect boundingBox, float zoom = 1.0f)
    {
        DrawHandle(canvas, new Point(boundingBox.Center.X, boundingBox.Top), zoom);
        DrawHandle(canvas, new Point(boundingBox.Center.X, boundingBox.Bottom), zoom);
        DrawHandle(canvas, new Point(boundingBox.Left, boundingBox.Center.Y), zoom);
        DrawHandle(canvas, new Point(boundingBox.Right, boundingBox.Center.Y), zoom);
    }

    public static void DrawHandlesSkiaSides(SKCanvas canvas, Rect boundingBox, float zoom = 1.0f)
    {
        DrawHandle(canvas, new Point(boundingBox.Left, boundingBox.Center.Y), zoom);
        DrawHandle(canvas, new Point(boundingBox.Right, boundingBox.Center.Y), zoom);
    }

    public static void DrawCropSquareHandle(SKCanvas canvas, Point center, float zoom = 1.0f, float baseSize = 10f)
    {
        float safeZoom = Math.Max(0.01f, zoom);
        float size = baseSize / safeZoom;
        float half = size / 2.0f;
        var skRect = new SKRect((float)center.X - half, (float)center.Y - half, (float)center.X + half, (float)center.Y + half);

        using var shadowPaint = new SKPaint
        {
            Color = SKColors.Black.WithAlpha(90),
            Style = SKPaintStyle.Fill,
            IsAntialias = true,
            MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 2f / safeZoom)
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
            StrokeWidth = 1f / safeZoom,
            IsAntialias = true
        };

        // Sombra
        var shadowRect = new SKRect(skRect.Left, skRect.Top + (1f / safeZoom), skRect.Right, skRect.Bottom + (1f / safeZoom));
        canvas.DrawRect(shadowRect, shadowPaint);

        // Cuadrado blanco y borde
        canvas.DrawRect(skRect, fillPaint);
        canvas.DrawRect(skRect, borderPaint);
    }

    private static readonly float[] s_cropActiveDashIntervals = { 6f, 4f };
    private static readonly float[] s_cropInactiveDashIntervals = { 3f, 3f };

    public static void DrawCropOverlay(SKCanvas canvas, Rect cropRect, Rect imageBounds, bool drawHandles, float zoom = 1.0f)
    {
        float safeZoom = Math.Max(0.01f, zoom);

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

        var cropSkRect = new SKRect((float)cropRect.Left, (float)cropRect.Top, (float)cropRect.Right, (float)cropRect.Bottom);

        if (drawHandles)
        {
            // 2. Línea delimitadora interlineada para modo Editable
            using var dashOutlinePaint = new SKPaint
            {
                Color = SKColors.Black.WithAlpha(180),
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 2f / safeZoom,
                IsAntialias = true
            };

            float[] activeDashes = { s_cropActiveDashIntervals[0] / safeZoom, s_cropActiveDashIntervals[1] / safeZoom };
            using var dashPaint = new SKPaint
            {
                Color = SKColors.White,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 1.5f / safeZoom,
                PathEffect = SKPathEffect.CreateDash(activeDashes, 0),
                IsAntialias = true
            };

            canvas.DrawRect(cropSkRect, dashOutlinePaint);
            canvas.DrawRect(cropSkRect, dashPaint);

            // 3. Tiradores cuadrados (4 esquinas + 4 centros de arista)
            DrawCropSquareHandle(canvas, cropRect.TopLeft, safeZoom);
            DrawCropSquareHandle(canvas, cropRect.TopRight, safeZoom);
            DrawCropSquareHandle(canvas, cropRect.BottomLeft, safeZoom);
            DrawCropSquareHandle(canvas, cropRect.BottomRight, safeZoom);

            DrawCropSquareHandle(canvas, new Point(cropRect.Center.X, cropRect.Top), safeZoom);
            DrawCropSquareHandle(canvas, new Point(cropRect.Center.X, cropRect.Bottom), safeZoom);
            DrawCropSquareHandle(canvas, new Point(cropRect.Left, cropRect.Center.Y), safeZoom);
            DrawCropSquareHandle(canvas, new Point(cropRect.Right, cropRect.Center.Y), safeZoom);
        }
        else
        {
            // 2. Línea delimitadora interlineada para modo No Editable (solo en aristas con recorte efectivo)
            using var inactiveOutlinePaint = new SKPaint
            {
                Color = SKColors.Black.WithAlpha(140),
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 1.5f / safeZoom,
                IsAntialias = true
            };

            float[] inactiveDashes = { s_cropInactiveDashIntervals[0] / safeZoom, s_cropInactiveDashIntervals[1] / safeZoom };
            using var inactiveDashPaint = new SKPaint
            {
                Color = SKColors.White.WithAlpha(200),
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 1f / safeZoom,
                PathEffect = SKPathEffect.CreateDash(inactiveDashes, 0),
                IsAntialias = true
            };

            float left = (float)cropRect.Left;
            float top = (float)cropRect.Top;
            float right = (float)cropRect.Right;
            float bottom = (float)cropRect.Bottom;

            // Borde superior
            if (cropRect.Top > 0)
            {
                canvas.DrawLine(left, top, right, top, inactiveOutlinePaint);
                canvas.DrawLine(left, top, right, top, inactiveDashPaint);
            }
            // Borde inferior
            if (cropRect.Bottom < imageBounds.Height)
            {
                canvas.DrawLine(left, bottom, right, bottom, inactiveOutlinePaint);
                canvas.DrawLine(left, bottom, right, bottom, inactiveDashPaint);
            }
            // Borde izquierdo
            if (cropRect.Left > 0)
            {
                canvas.DrawLine(left, top, left, bottom, inactiveOutlinePaint);
                canvas.DrawLine(left, top, left, bottom, inactiveDashPaint);
            }
            // Borde derecho
            if (cropRect.Right < imageBounds.Width)
            {
                canvas.DrawLine(right, top, right, bottom, inactiveOutlinePaint);
                canvas.DrawLine(right, top, right, bottom, inactiveDashPaint);
            }
        }
    }
}
