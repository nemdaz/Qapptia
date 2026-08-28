using Qapptia.Editor.Models.Geometry;

namespace Qapptia.Editor.Tools;

/// <summary>
/// Herramienta vectorial para trazar flechas direccionales de 2 nodos con punta.
/// </summary>
public sealed class ArrowTool : VectorTool<ArrowGeometry>
{
    public override string Id => "Arrow";
    public override string DisplayName => "Flecha";
    public override string IconData => IconCatalog.Arrow;
    public override string? Shortcut => "A";
}
