using Avalonia;
using Qapptia.Editor.Core;
using SkiaSharp;

namespace Qapptia.App.Editor.ViewModels.Shapes;

/// <summary>
/// Renderizado de manetas de control (front). Recibe posiciones ya calculadas por
/// <see cref="Qapptia.Editor.Models.Geometry.HandleGeometry"/> y solo dibuja; no realiza
/// ningún cálculo geométrico.
/// </summary>
public static class HandleRenderer
{
    public static void Draw(SKCanvas canvas, Point center)
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

        canvas.DrawCircle((float)center.X, (float)center.Y + 1f, radius, shadowPaint);
        canvas.DrawCircle((float)center.X, (float)center.Y, radius, paint);
    }

    public static void DrawAll(SKCanvas canvas, System.Collections.Generic.IEnumerable<Point> handles)
    {
        foreach (var handle in handles)
        {
            Draw(canvas, handle);
        }
    }
}
