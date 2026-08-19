using Avalonia;
using Avalonia.Media;
using Qapptia.Editor.Models;

namespace Qapptia.Editor.Core;

/// <summary>
/// Fábrica centralizada para la instanciación de figuras vectoriales según la herramienta activa.
/// </summary>
public static class ShapeFactory
{
    public static VectorShape? Create(ToolType tool, Point startPoint, Color color)
    {
        return tool switch
        {
            ToolType.Line => new LineShape { Start = startPoint, End = startPoint, Color = color },
            ToolType.Arrow => new ArrowShape { Start = startPoint, End = startPoint, Color = color },
            ToolType.Rectangle => new RectangleShape { Start = startPoint, End = startPoint, Color = color },
            ToolType.Ellipse => new EllipseShape { Start = startPoint, End = startPoint, Color = color },
            ToolType.Highlighter => new HighlighterShape { Start = startPoint, End = startPoint, Color = color },
            ToolType.Text => new TextShape { Start = startPoint, End = startPoint, Color = color },
            _ => null
        };
    }
}
