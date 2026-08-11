using System;
using Avalonia;
using Avalonia.Media;

namespace Qapptia.Editor.Models;

public abstract class VectorShape
{
    public Guid Id { get; } = Guid.NewGuid();
    
    public Point Start { get; set; }
    public Point End { get; set; }
    
    public Color Color { get; set; } = Qapptia.Editor.Core.Constants.FavoriteColors[1];
    public double StrokeWidth { get; set; } = Qapptia.Editor.Core.Constants.DefaultStrokeWidth;
    
    // Indica si el vector está seleccionado
    public bool IsSelected { get; set; }

    // Indica si el vector se está quemando en la imagen (para ajustar sombras)
    public bool IsBurning { get; set; }

    public abstract void RenderSkia(SkiaSharp.SKCanvas canvas);
    public abstract HandleType HitTest(Point point);

    protected Rect GetBoundingBox()
    {
        double left = Math.Min(Start.X, End.X);
        double right = Math.Max(Start.X, End.X);
        double top = Math.Min(Start.Y, End.Y);
        double bottom = Math.Max(Start.Y, End.Y);
        return new Rect(left, top, right - left, bottom - top);
    }
}
