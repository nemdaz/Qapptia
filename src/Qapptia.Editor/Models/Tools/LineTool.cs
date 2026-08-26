using Avalonia;
using Avalonia.Media;
using Qapptia.Editor.Models;

namespace Qapptia.Editor.Tools;

/// <summary>
/// Herramienta vectorial para trazar líneas rectas simples de 2 nodos.
/// </summary>
public sealed class LineTool : VectorTool
{
    public override string Id => "Line";
    public override string DisplayName => "Línea";
    public override string IconData => IconCatalog.Line;
    public override string? Shortcut => "L";

    public override VectorShape CreateShape(Point startPoint, Color color)
    {
        return new LineShape
        {
            Start = startPoint,
            End = startPoint,
            Color = color
        };
    }
}
