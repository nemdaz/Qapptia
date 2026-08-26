using Avalonia;
using Avalonia.Media;
using Qapptia.Editor.Models;

namespace Qapptia.Editor.Tools;

/// <summary>
/// Herramienta vectorial para trazar flechas direccionales de 2 nodos con punta.
/// </summary>
public sealed class ArrowTool : VectorTool
{
    public override string Id => "Arrow";
    public override string DisplayName => "Flecha";
    public override string IconData => IconCatalog.Arrow;
    public override string? Shortcut => "A";

    public override VectorShape CreateShape(Point startPoint, Color color)
    {
        return new ArrowShape
        {
            Start = startPoint,
            End = startPoint,
            Color = color
        };
    }
}
