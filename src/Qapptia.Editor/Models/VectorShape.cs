using System;
using Avalonia;
using Avalonia.Media;

namespace Qapptia.Editor.Models;

public abstract class VectorShape
{
    public Guid Id { get; } = Guid.NewGuid();
    
    public Point Start { get; set; }
    public Point End { get; set; }
    
    public Color Color { get; set; } = Colors.Red;
    public double StrokeWidth { get; set; } = 3.0;
    
    public bool IsSelected { get; set; }

    public abstract void Render(DrawingContext context);
    public abstract bool HitTest(Point point);

    protected Rect GetBoundingBox()
    {
        double left = Math.Min(Start.X, End.X);
        double right = Math.Max(Start.X, End.X);
        double top = Math.Min(Start.Y, End.Y);
        double bottom = Math.Max(Start.Y, End.Y);
        return new Rect(left, top, right - left, bottom - top);
    }
}
