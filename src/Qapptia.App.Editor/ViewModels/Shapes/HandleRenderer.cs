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
    public static void Draw(SKCanvas canvas, Point center, float zoom = 1.0f)
    {
        ShapeRenderHelper.DrawHandle(canvas, center, zoom);
    }

    public static void DrawAll(SKCanvas canvas, System.Collections.Generic.IEnumerable<Point> handles, float zoom = 1.0f)
    {
        foreach (var handle in handles)
        {
            Draw(canvas, handle, zoom);
        }
    }
}
